using AppStoreServerLibraryDotnet.Models;

namespace AppStoreServerLibraryDotnet;

public interface ISignedDataVerifier
{
    /// <summary>
    /// Verify and decode the notification payload.
    /// This signature is kept to be closer to the original implementation.
    /// </summary>
    /// <param name="environment">The server environment that the notification applies to, either <see cref='AppStoreServerEnvironment.Sandbox'/> or <see cref='AppStoreServerEnvironment.Production'/>.</param>
    /// <param name="bundleId">The expected bundle identifier of the app.</param>
    /// <param name="signedPayload">Encoded payload</param>
    /// <returns>A ResponseBodyV2DecodedPayload object</returns>
    Task<ResponseBodyV2DecodedPayload> VerifyAndDecodeNotification(string environment, string bundleId, string signedPayload);

    /// <summary>
    /// Verify and decode the notification payload, using the bundleId and environment from the config.
    /// </summary>
    /// <param name="signedPayload">Encoded payload</param>
    /// <returns>A ResponseBodyV2DecodedPayload object</returns>
    Task<ResponseBodyV2DecodedPayload> VerifyAndDecodeNotification(string signedPayload);

    /// <summary>
    /// Verify and decode the transaction payload
    /// </summary>
    /// <param name="signedPayload">Encoded transaction found in ResponseBodyV2DecodedPayload</param>
    /// <returns>A JWSTransactionDecodedPayload object</returns>
    Task<JwsTransactionDecodedPayload> VerifyAndDecodeTransaction(string signedPayload);

    /// <summary>
    /// Verify and decode a renewal info payload
    /// </summary>
    /// <param name="signedPayload">Encoded renewal info found in ResponseBodyV2DecodedPayload</param>
    /// <returns>A JWSRenewalInfoDecodedPayload object</returns>
    Task<JWSRenewalInfoDecodedPayload> VerifyAndDecodeRenewalInfo(string signedPayload);
}