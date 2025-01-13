# app-store-server-library-dotnet

An unofficial .NET SDK for App Store Server Notifications and API

This library started as helpers to work with the App Store Server Notifications. Using it you can verify and decode notifications, transactions and renewal infos. It implements the required security protocols to verify the payload.

It also provides a way to interact with the App Store API and to decode the Store kit 1 receipt data to retrieve the Transaction Id.

The library design is copied from the official Apple libraries and it is intended to be used in the same way.
More resources:

- [Meet the App Store Server Library](https://developer.apple.com/videos/play/wwdc2023/10143/)
- [App Store Server Notifications](https://developer.apple.com/documentation/appstoreservernotifications)

## Features

This is a list of the features that are currently implemented in the library, compared to the official Apple libraries.

| Feature                                                     | Apple | This library |
| ----------------------------------------------------------- | :---: | :----------: |
| Verify and decode notifications                             |  ✅   |      ✅      |
| Verify and decode transactions                              |  ✅   |      ✅      |
| Verify and decode renewal infos                             |  ✅   |      ✅      |
| Generate API Token                                          |  ✅   |      ✅      |
| Get All Subscription Statuses                               |  ✅   |      ✅      |
| Get Notification History                                    |  ✅   |      ✅      |
| Get Transaction History v2                                  |  ✅   |      ✅      |
| Extract Transaction Id from receipt                         |  ✅   |      ✅      |
| Get Transaction History v1                                  |  ✅   |              |
| Get Transaction Info                                        |  ✅   |              |
| Send Consumption Information                                |  ✅   |              |
| Look Up Order ID                                            |  ✅   |              |
| Get Refund History                                          |  ✅   |              |
| Extending the renewal date for auto-renewable subscriptions |  ✅   |              |
| Request a Test Notification                                 |  ✅   |              |

To summarize, it's missing a full coverage on the API endpoints.

## Installation

The library is available on NuGet. You can install it using the following command:

```bash
TBD
```

## Usage

The library is designed to be used in the same way as the official Apple libraries.

### Obtaining Apple Root Certificates

Download and store the root certificates found in the Apple Root Certificates section of the Apple PKI site. Provide these certificates as an array to a SignedDataVerifier to allow verifying the signed data comes from Apple.

### Notification Verification

Here is an example of how to verify a notification:

```csharp
//This is the endpoint you could use to receive the notifications from Apple
app.MapPost("/verify-decode-notification", async (ISignedDataVerifier signedDataVerifier, [FromBody] ResponseBodyV2 request) =>
    {
        //First decode the notification
        var decodedNotificaiton =  await signedDataVerifier.VerifyAndDecodeNotification(request.SignedPayload);

        //Then you can decode the transaction
        JwsTransactionDecodedPayload decodedTransaction =
            await signedDataVerifier.VerifyAndDecodeTransaction(decodedNotificaiton.Data.SignedTransactionInfo!);

        //And the renewal info
        JWSRenewalInfoDecodedPayload decodedRenewalInfo =
            await signedDataVerifier.VerifyAndDecodeRenewalInfo(decodedNotificaiton.Data.SignedRenewalInfo!);

        return decodedNotificaiton;
    })
    .WithName("VerifyAndDecodeNotification")
    .WithOpenApi();
```

If the verification fails it will raise a `VerificationException` exception with failure details.
Once you get the decoded transaction, you can use `VerifyAndDecodeTransaction` and `VerifyAndDecodeRenewalInfo` to decode the transaction and renewal info.

> [!IMPORTANT]  
> The `SignedDataVerifier` will not verify the payload if the `environment` parameter is `LocalTesting`. This was done to allow testing fake payloads locally without the need to verify them.

### API

Here is an example of how to get all notification history for a specific transaction using the `Pagination token` :

```csharp
GetAppStoreNotificationHistoryResponse results = new([]);

NotificationHistoryRequest appStoreRequest = new()
{
    //At which date the history should start
    StartDate = request.StartDate.ToUnixTimeMilliseconds(),
    //At which date the history should end
    EndDate = request.EndDate.ToUnixTimeMilliseconds(),
    //The transaction id to get the history for, if not set it will get all the history
    TransactionId = request.TransactionId
};

NotificationHistoryResponse? notifications =
    await appStoreServerClient.GetNotificationHistory(appStoreRequest);

if (notifications != null)
{
    //For clarity of this example, ExtractNotificationInfo is a helper method that will
    //call ISignedDataVerifier.VerifyAndDecodeNotification for each notification and
    //extract any required info
    results.Transactions.AddRange(await this.ExtractNotificationInfo(notifications));

    //While there are more notifications to get, get them
    while (notifications!.HasMore)
    {
        notifications = await appStoreHelper.GetNotificationHistory(appStoreRequest,
            notifications.PaginationToken);

        results.Transactions.AddRange(await this.ExtractNotificationInfo(notifications!));
    }
}

//Return the list of retrieved and decoded notifications
return results;
```

And another example on how to get the subscription status for a specific subscription using the `Transaction Id` :

```csharp
string transactionId = "your-transaction-id";

//Inject IAppStoreServerAPIClient as appStoreServerClient to use the API
SubscriptionStatusResponse response = await appStoreServerClient.GetAllSubscriptionStatuses(transactionId);

//As we retrieved the subscription status based on the original transaction id, the response already contains only
//the TransactionItem related to the subscription we are looking for
//Also the LastTransactions property, if we believe the documentation, only contains the most recent transaction.
//It means that even if it's a list it should contain only one item.
SubscriptionStatusLastTransactionsItem lastRemoteTransaction = response.Data
    .Last()
    .LastTransactions.Last();

//And inject ISignedDataVerifier as signedDataVerifier to decode the transaction and renewal info
JwsTransactionDecodedPayload decodedTransaction =
    await signedDataVerifier.VerifyAndDecodeTransaction(lastRemoteTransaction.SignedTransactionInfo!);

JWSRenewalInfoDecodedPayload decodedRenewalInfo =
    await signedDataVerifier.VerifyAndDecodeRenewalInfo(lastRemoteTransaction.SignedRenewalInfo!);

Console.WriteLine($"The subscription status is {lastRemoteTransaction.Status} for transaction Id {decodedTransaction.TransactionId}");
```

### Receipt Utility

Here is an example of how to extract the transaction id from a receipt:

```csharp
string receiptData = "your-receipt-data";
ReceiptUtility utility = new ReceiptUtility();

string transactionId = utility.ExtractTransactionIdFromAppReceipt(receiptData);
```

Note that the Receipt Utility is not injectable and should be instantiated when needed.
