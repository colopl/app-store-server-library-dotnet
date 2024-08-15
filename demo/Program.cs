using AppStoreServerLibraryDotnet;
using AppStoreServerLibraryDotnet.Configuration;
using AppStoreServerLibraryDotnet.Extensions;
using AppStoreServerLibraryDotnet.Models;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

//Add the Library to the services collection
builder.Services.AddAppStoreServerLibrary(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/auth-token", (IAppStoreServerAPIClient appStoreServerClient, IOptions<AppleOptions> appleOptions) =>
    {
        return appStoreServerClient.GetAppStoreServerApiToken(appleOptions.Value.AppStoreServerApiKeyId,
            appleOptions.Value.AppStoreServerApiIssuerId,
            appleOptions.Value.AppStoreServerApiSubscriptionKey,
            appleOptions.Value.BundleId);
    })
    .WithName("GetAuthenticationToken")
    .WithOpenApi();

app.MapGet("/get-subscriptions-status/{transactionId}", (IAppStoreServerAPIClient appStoreServerClient, string transactionId) =>
    {
        return appStoreServerClient.GetAllSubscriptionStatuses(transactionId);
    })
    .WithName("GetAllSubscriptionStatusesByTransactionId")
    .WithOpenApi();

app.MapGet("/get-notification-history-last-week/", (IAppStoreServerAPIClient appStoreServerClient) =>
    {
        var request = new NotificationHistoryRequest
        {
            StartDate = DateTimeOffset.UtcNow.AddDays(-7).ToUnixTimeMilliseconds(),
            EndDate =  DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        return appStoreServerClient.GetNotificationHistory(request);
    })
    .WithName("GetNotificationHistoryForLastWeek")
    .WithOpenApi();

app.MapGet("/get-transaction-history/{transactionId}", (IAppStoreServerAPIClient appStoreServerClient, string transactionId) =>
    {
        return appStoreServerClient.GetTransactionHistory(transactionId);
    })
    .WithName("GetTransactionHistory")
    .WithOpenApi();

app.MapPost("/verify-decode-notification", (ISignedDataVerifier signedDataVerifier, DecodePayloadRequest request) =>
    {
        return signedDataVerifier.VerifyAndDecodeNotification(request.Payload);
    })
    .WithName("VerifyAndDecodeNotification")
    .WithOpenApi();

app.MapPost("/verify-decode-transaction", (ISignedDataVerifier signedDataVerifier, DecodePayloadRequest request) =>
    {
        return signedDataVerifier.VerifyAndDecodeTransaction(request.Payload);
    })
    .WithName("VerifyAndDecodeTransaction")
    .WithOpenApi();

app.MapPost("/verify-decode-renewal", (ISignedDataVerifier signedDataVerifier, DecodePayloadRequest request) =>
    {
        return signedDataVerifier.VerifyAndDecodeRenewalInfo(request.Payload);
    })
    .WithName("VerifyAndDecodeRenewal")
    .WithOpenApi();

app.MapPost("/extract-receipt-transaction-id", (DecodeReceiptRequest request) =>
    {
        ReceiptUtility utility = new ReceiptUtility();
        return utility.ExtractTransactionIdFromAppReceipt(request.Receipt);
    })
    .WithName("GetTransactionIdFromReceipt")
    .WithOpenApi();

app.Run();


public record DecodePayloadRequest(string Payload);
public record DecodeReceiptRequest(string Receipt);