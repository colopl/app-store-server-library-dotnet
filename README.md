# App Store Server Library for .NET

An unofficial .NET SDK for App Store Server Notifications v2 and API

The overall library and API design is copied from the official Apple libraries and it is intended to be used in the same way.
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
| Get Transaction Info                                        |  ✅   |      ✅      |
| Send Consumption Information                                |  ✅   |      ✅      |
| Look Up Order ID                                            |  ✅   |      ✅      |
| Get Refund History                                          |  ✅   |              |
| Extending the renewal date for auto-renewable subscriptions |  ✅   |              |
| Request a Test Notification                                 |  ✅   |              |

## Installation

The library is available on NuGet. You can install it using the following command:

```bash
dotnet add package Mimo.AppStoreServerLibrary
```

## Usage

### Obtaining Apple Root Certificates

Download and store the root certificates found in the Apple Root Certificates section of the Apple PKI site. Provide these certificates as an array to a SignedDataVerifier to allow verifying the signed data comes from Apple.

### Notification Verification

Here is an example of how to verify a notification:

```csharp
var signedDataVerifier = new SignedDataVerifier(
    appleRootCertificates: rootCertificatesBytes,
    enableOnlineChecks: true,
    environment: AppStoreEnvironment.Sandbox,
    bundleId: "com.example.app"
);

try {
    var decodedPayload = await signedDataVerifier.VerifyAndDecodeNotification(signedPayload);

} catch (VerificationException ex) {
    Console.WriteLine($"Verification failed: {ex.Message}");
}

```

> [!IMPORTANT]
> The `SignedDataVerifier` will not verify the payload if the `environment` parameter is `LocalTesting`. This was done to allow testing fake payloads locally without the need to verify them.

### API

Here is an example of how to get all notification history for a specific transaction using the `Pagination token` :

```csharp
// Create a request for notification history
NotificationHistoryRequest request = new()
{
    StartDate = DateTimeOffset.UtcNow.AddDays(-7).ToUnixTimeMilliseconds(),
    EndDate = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
    // Optional: filter by transaction ID
    TransactionId = "your-transaction-id"
};

// Get initial page of notifications
NotificationHistoryResponse? notifications =
    await appStoreServerClient.GetNotificationHistory(request);

if (notifications != null)
{
    // Process notifications on current page
    foreach (var notification in notifications.NotificationHistory)
    {
        Console.WriteLine($"Notification ID: {notification.SignedPayload}");
    }

    // Get additional pages if they exist
    while (notifications.HasMore)
    {
        notifications = await appStoreServerClient.GetNotificationHistory(
            request,
            notifications.PaginationToken
        );

        foreach (var notification in notifications!.NotificationHistory)
        {
            Console.WriteLine($"Notification ID: {notification.SignedPayload}");
        }
    }
}
```

### Receipt Utility

Here is an example of how to extract the transaction id from a receipt:

```csharp
string receiptData = "your-receipt-data";
ReceiptUtility utility = new();

string transactionId = utility.ExtractTransactionIdFromAppReceipt(receiptData);
```
