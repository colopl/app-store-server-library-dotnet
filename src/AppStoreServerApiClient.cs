using System.Security.Cryptography;
using System.Text.Json;
using Mimo.AppStoreServerLibraryDotnet.Exceptions;
using Mimo.AppStoreServerLibraryDotnet.Models;
using Flurl.Http;
using Flurl.Http.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Mimo.AppStoreServerLibraryDotnet;

/// <summary>
/// Create an App Store Server API client
/// </summary>
/// <param name="signingKey">Your private key downloaded from App Store Connect</param>
/// <param name="keyId">Your private key ID from App Store Connect</param>
/// <param name="issuerId">Your issuer ID from the Keys page in App Store Connect</param>
/// <param name="bundleId">Your app's bundle ID</param>
/// <param name="environment">The environment to target</param>
public class AppStoreServerApiClient(string signingKey, string keyId, string issuerId, string bundleId, AppStoreEnvironment environment)
{
    /// <summary>
    /// Get the statuses for all of a customer’s auto-renewable subscriptions in your app.
    /// </summary>
    /// <param name="transactionId"> The identifier of a transaction that belongs to the customer, and which may be an original transaction identifier</param>
    /// <returns>The status for all the customer’s subscriptions, organized by their subscription group identifier.</returns>
    public Task<SubscriptionStatusResponse> GetAllSubscriptionStatuses(string transactionId)
    {
        //Call to https://developer.apple.com/documentation/appstoreserverapi/get_all_subscription_statuses

        string path = $"v1/subscriptions/{transactionId}";

        return this.MakeRequest<SubscriptionStatusResponse>(path, HttpMethod.Get)!;
    }

    /// <summary>
    /// Get a list of notifications that the App Store server attempted to send to your server.
    /// </summary>
    /// <returns>A list of notifications and their attempts</returns>
    public Task<NotificationHistoryResponse?> GetNotificationHistory(NotificationHistoryRequest notificationHistoryRequest,
        string paginationToken = "")
    {
        //Call to https://developer.apple.com/documentation/appstoreserverapi/get_notification_history
        Dictionary<string, string> queryParameters = new();
        if (!string.IsNullOrEmpty(paginationToken))
        {
            queryParameters.Add("paginationToken", paginationToken);
        }

        string path = $"v1/notifications/history";

        return this.MakeRequest<NotificationHistoryResponse>(path, HttpMethod.Post, queryParameters, notificationHistoryRequest);
    }

    /// <summary>
    /// Get a customer’s in-app purchase transaction history for your app.
    /// </summary>
    /// <returns>A list of transactions associated with the provided Transaction Id</returns>
    public async Task<TransactionHistoryResponse?> GetTransactionHistory(string transactionId,
        string revisionToken = "")
    {
        //Call to https://developer.apple.com/documentation/appstoreserverapi/get_transaction_history
        Dictionary<string, string> queryParameters = new();
        if (!string.IsNullOrEmpty(revisionToken))
        {
            queryParameters.Add("revision", revisionToken);
        }

        string path = $"v2/history/{transactionId}";

        return await this.MakeRequest<TransactionHistoryResponse>(path, HttpMethod.Get, queryParameters);
    }

    /// <summary>
    /// Send consumption information about a consumable in-app purchase to the App Store after your server receives a consumption request notification.
    /// </summary>
    /// <param name="transactionId">The transaction identifier for which you're providing consumption information. You receive this identifier in the CONSUMPTION_REQUEST notification the App Store sends to your server.</param>
    /// <param name="consumptionRequest">The request body containing consumption information.</param>
    /// <exception cref="ApiException">Thrown when a response indicates the request could not be processed</exception>
    /// <remarks>
    /// See <see href="https://developer.apple.com/documentation/appstoreserverapi/send_consumption_information">Send Consumption Information</see>
    /// </remarks>
    public Task SendConsumptionData(string transactionId, ConsumptionRequest consumptionRequest)
    {
        string path = $"v1/transactions/consumption/{transactionId}";

        return this.MakeRequest<TransactionHistoryResponse>(path, HttpMethod.Put, null, consumptionRequest);
    }

    private static string CreateBearerToken(string keyId, string issuerId, string signingKey, string bundleId)
    {
        ReadOnlySpan<byte> keyAsSpan = Convert.FromBase64String(signingKey);
        var prvKey = ECDsa.Create();
        prvKey.ImportPkcs8PrivateKey(keyAsSpan, out int _);

        var securityDescriptor = new SecurityTokenDescriptor
        {
            IssuedAt = DateTime.UtcNow,
            Expires = DateTime.UtcNow.AddHours(1),
            Claims = new Dictionary<string, object>
            {
                { "iss", issuerId },
                { "aud", "appstoreconnect-v1" },
                { "bid", bundleId }
            },
            TokenType = "JWT"
        };

        var securityKey = new ECDsaSecurityKey(prvKey)
        {
            KeyId = keyId
        };

        securityDescriptor.SigningCredentials = new SigningCredentials(securityKey, "ES256");

        return new JsonWebTokenHandler().CreateToken(securityDescriptor);
    }

    /// <summary>
    /// Call the App Store Server API
    /// </summary>
    /// <param name="path">Endpoint you need to call</param>
    /// <param name="method">Http Method : Get, Post, etc.</param>
    /// <param name="queryParameters">Any query param you need to append</param>
    /// <param name="body">Query body if required</param>
    /// <typeparam name="TReturn">The type to deserialize the API response to</typeparam>
    /// <exception cref="NotSupportedException">Supports only Get and Post http methods</exception>
    private async Task<TReturn?> MakeRequest<TReturn>(string path, HttpMethod method, Dictionary<string, string>? queryParameters = null,
        object? body = null)
    {
        string token = CreateBearerToken(keyId, issuerId, signingKey, bundleId);

        Uri url = new (environment.BaseUrl, path);

        TReturn? response;
        try
        {
            IFlurlRequest request = url
                .WithOAuthBearerToken(token)
                .WithSettings(settings => settings.JsonSerializer = new DefaultJsonSerializer(new JsonSerializerOptions()
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }));

            if (queryParameters != null && queryParameters.Any())
            {
                foreach (KeyValuePair<string, string> queryParameter in queryParameters)
                {
                    request.SetQueryParam(queryParameter.Key, queryParameter.Value);
                }
            }

            if (method == HttpMethod.Get)
            {
                response = await request
                    .GetAsync()
                    .ReceiveJson<TReturn>();
            }
            else if (method == HttpMethod.Post)
            {
                response = await request
                    .PostJsonAsync(body)
                    .ReceiveJson<TReturn>();
            }
            else if (method == HttpMethod.Put)
            {
                response = await request
                    .PutJsonAsync(body)
                    .ReceiveJson<TReturn>();
            }
            else
            {
                throw new NotSupportedException($"Method {method} not supported");
            }
        }
        catch (FlurlHttpException ex)
        {
            var error = await ex.GetResponseJsonAsync<ErrorResponse>();

            if (error != null)
            {
                throw new ApiException($"Error when calling App Store Server API for endpoint {path}. Received error code: {error.ErrorCode}, Received error message: {error.ErrorMessage}",
                    error);
            }

            //If the error is not in the expected format, rethrow
            throw;
        }

        return response;
    }
}