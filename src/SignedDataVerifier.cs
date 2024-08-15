using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using AppStoreServerLibraryDotnet.Configuration;
using AppStoreServerLibraryDotnet.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace AppStoreServerLibraryDotnet;

public class SignedDataVerifier(
    ILogger<SignedDataVerifier> logger,
    IOptions<AppleOptions> options) : ISignedDataVerifier
{
    // It's recommended to reuse the JsonSerializerOptions instance.
    // https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/configure-options?pivots=dotnet-8-0#reuse-jsonserializeroptions-instances
    private readonly JsonSerializerOptions jsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<ResponseBodyV2DecodedPayload> VerifyAndDecodeNotification(string signedPayload)
    {
        return await VerifyAndDecodeNotification(options.Value.Environment, options.Value.BundleId, signedPayload);
    }

    public async Task<ResponseBodyV2DecodedPayload> VerifyAndDecodeNotification(string environment, string bundleId, string signedPayload)
    {
        VerifySignedDataResult result = await VerifySignedData(signedPayload);

        if (result is VerifySignedDataResult.Failure (var message))
        {
            throw new InvalidOperationException(message);
        }

        var payload = (VerifySignedDataResult.Success) result;

        ResponseBodyV2DecodedPayload decodedPayload;

        try
        {
            decodedPayload = JsonSerializer.Deserialize<ResponseBodyV2DecodedPayload>(payload.Payload, jsonSerializerOptions)!;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error deserializing notification payload. Payload : {payload.Payload}");
            throw;
        }

        if (decodedPayload.Data.Environment != environment)
        {
            throw new InvalidOperationException($"Environment in payload does not match expected environment. Expected : {environment}, Actual : {decodedPayload.Data.Environment}");
        }

        if (decodedPayload.Data.BundleId != bundleId)
        {
            throw new InvalidOperationException($"BundleId in payload does not match expected bundleId. Expected : {bundleId}, Actual : {decodedPayload.Data.BundleId}");
        }

        return decodedPayload;
    }

    public async Task<JwsTransactionDecodedPayload> VerifyAndDecodeTransaction(string signedPayload)
    {
        VerifySignedDataResult result = await this.VerifySignedData(signedPayload);

        if (result is VerifySignedDataResult.Failure(var message))
        {
            logger.LogError(message);
            throw new InvalidOperationException(message);
        }

        var payload = (VerifySignedDataResult.Success) result;

        try
        {
            return JsonSerializer.Deserialize<JwsTransactionDecodedPayload>(payload.Payload, jsonSerializerOptions)!;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error deserializing transaction payload. Payload : {payload.Payload}");
            throw;
        }
    }

    public async Task<JWSRenewalInfoDecodedPayload> VerifyAndDecodeRenewalInfo(string signedPayload)
    {
        VerifySignedDataResult result = await this.VerifySignedData(signedPayload);

        if (result is VerifySignedDataResult.Failure(var message))
        {
            logger.LogError(message);
            throw new InvalidOperationException(message);
        }

        var payload = (VerifySignedDataResult.Success) result;

        try
        {
            return JsonSerializer.Deserialize<JWSRenewalInfoDecodedPayload>(payload.Payload, jsonSerializerOptions)!;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error deserializing renewal info payload. Payload : {payload.Payload}");
            throw;
        }
    }

    private async Task<VerifySignedDataResult> VerifySignedData(string signedPayload)
    {
        //1. Verify the payload is composed of 3 parts separated by a dot => Indicates the JWS is well formed
        //2. Decode the payload to verify that there is a header, a payload and a signature => Indicates the JWS is well formed
        //3. Parse the header and verify it contains an x5c claim with 3 certificates AND that the alg claim == ES256
        //4. Verify signature chain and check for revocation
        //5. Get the public key from the leaf certificate (1st certificate in x5c claim) and verify the payloads signature with it.

        //Depends on :
        //Microsoft.IdentityModel.Tokens
        //Microsoft.IdentityModel.JsonWebTokens

        // Split the payload into 3 parts
        string[] parts = signedPayload.Split('.');
        if (parts.Length != 3)
        {
            return new VerifySignedDataResult.Failure("Payload does not have the correct format");
        }

        // Decode the header and the payload , which are the 1st and 2nd parts of the payload
        // We do not use a JsonWebToken to parse the token because it does not expose the x5c claim that we need to verify the signature
        string headerJson = Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(parts[0]));
        string payloadJson = Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(parts[1]));

        var header = JsonSerializer.Deserialize<JWSDecodedHeader>(headerJson, jsonSerializerOptions);

        //Check if Environment is development, in this case data may not be signed by the App Store, and verification should be skipped
        //This is useful for local development and unit tests
        //If not configured, the default value is Null for unit tests
        if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
        {
            return new VerifySignedDataResult.Success(payloadJson);
        }

        if (header?.x5c == null || header.x5c.Length != 3)
        {
            return new VerifySignedDataResult.Failure("x5c claim is null or has more or less than 3 certificates");
        }

        if (header.Alg != "ES256")
        {
            return new VerifySignedDataResult.Failure($"Unrecognized JWT algorithm attribute : {header.Alg}");
        }

        //We don't include the root cert in the path, we use the one downloaded from https://www.apple.com/certificateauthority/AppleRootCA-G3.cer
        //See Apple implementation for Java : https://github.com/apple/app-store-server-library-java/blob/main/src/main/java/com/apple/itunes/storekit/verification/ChainVerifier.java#L70C14-L71C90
        //or Node : https://github.com/apple/app-store-server-library-node/blob/main/jws_verification.ts#L185
        X509Certificate2Collection col = new();
        foreach (string c in header.x5c[..2])
        {
            byte[] bytes = Convert.FromBase64String(c);
            col.Add(new X509Certificate2(bytes));
        }

        //As the header contains a x5c parameter we need to check for the certificate chain
        bool certChainIsValid = this.CheckCertificateChain(col);

        if (!certChainIsValid)
        {
            return new VerifySignedDataResult.Failure("Certificate chain is invalid");
        }

        //We now need to verify the signature using the leaf (first) certificate of the x5c parameter.
        //This is done by retrieving it's public key and calling the JWT library to verify the signature
        var leafCert = new X509Certificate2(Convert.FromBase64String(header.x5c[0]));

        var securityTokenHandler = new JsonWebTokenHandler();
        var leafPublicKey = new ECDsaSecurityKey(leafCert.GetECDsaPublicKey());
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = leafPublicKey,
            ValidateIssuer = false,
            ValidateAudience = false,
            //Notifications don't have an expiration date
            ValidateLifetime = false
        };

        TokenValidationResult? result = await securityTokenHandler.ValidateTokenAsync(signedPayload, validationParameters);

        if (!result.IsValid)
        {
            return new VerifySignedDataResult.Failure(
                $"Payload signature could not be verified : {result.Exception.Message}");
        }

        return new VerifySignedDataResult.Success(payloadJson);
    }

    /// <summary>
    /// Checks the certificate chain of the notification
    /// Rfc for the x5c parameter : https://datatracker.ietf.org/doc/html/rfc7515#section-4.1.6
    /// Also see following discussion on Apple forum for better explanations : https://forums.developer.apple.com/forums/thread/693351
    /// </summary>
    /// <returns>If the certificate chain is valid</returns>
    private bool CheckCertificateChain(X509Certificate2Collection certificates)
    {
        var chain = new X509Chain();

        // Apple root intermediate certificate https://www.apple.com/certificateauthority/AppleRootCA-G3.cer
        var rootCertificate = new X509Certificate2(GetEmbeddedAppleRootCertificate());

        // Configure the chain to check for certificate revocation online.
        // Online check can result in a longer delay while the certificate authority is contacted.
        // It's still unclear if OCSP is supported by Apple, see following comment in the Java Library code base :
        // https://github.com/apple/app-store-server-library-java/blob/main/src/main/java/com/apple/itunes/storekit/verification/ChainVerifier.java#L70C14-L71C90
        // Also an explanation of the use of OCSP can be found here : https://forums.developer.apple.com/forums/thread/693351
        // The default value for DisableOnlineCertificateRevocationCheck is false
        chain.ChainPolicy.RevocationMode = options.Value.DisableOnlineCertificateRevocationCheck ?  X509RevocationMode.NoCheck : X509RevocationMode.Online;

        // By allowing unknown certificates we can avoid adding the Apple root certificate to the trust store
        // It's one less step to take when setting up the environment (docker file)
        // Not setting this flag will result in a "Chain validation failed. self-signed certificate - Status : UntrustedRoot" error when the app is running in a container
        // See following issue for more details about this flag behavior : https://github.com/dotnet/runtime/issues/26449#issue-558387269
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;

        foreach (X509Certificate2 cert in certificates)
        {
            //Add the intermediate certificates to the extra store so they can be used to build the chain
            chain.ChainPolicy.ExtraStore.Add(cert);
        }

        bool isValid = chain.Build(rootCertificate);

        if (!isValid)
        {
            string message = "Chain validation failed. ";
            foreach (X509ChainStatus chainStatus in chain.ChainStatus)
            {
                message += chainStatus.StatusInformation + " - Status : " + chainStatus.Status;
            }

            logger.LogError(message);
        }

        return isValid;
    }

    /// <summary>
    /// Retrieves the Apple root certificate from the embedded resources
    /// </summary>
    /// <returns>Apple root certificate in a byte array</returns>
    private byte[] GetEmbeddedAppleRootCertificate()
    {
        string resourceName = "AppStoreServerLibraryDotnet.AppleRootCertificate.AppleRootCA-G3.cer";
        using var stream = typeof(SignedDataVerifier).Assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            throw new InvalidOperationException($"AppleRoot Certificate not found. Should be embedded there : {resourceName}");
        }

        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }
}

/// <summary>
/// This record is used to return the result of the verification of a signed payload.
/// </summary>
internal abstract record VerifySignedDataResult
{
    private VerifySignedDataResult() { }

    public record Success(string Payload) : VerifySignedDataResult;

    public record Failure(string Message) : VerifySignedDataResult;
}