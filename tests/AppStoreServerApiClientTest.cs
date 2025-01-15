using Mimo.AppStoreServerLibrary;
using Xunit;
using RichardSzalay.MockHttp;
using Mimo.AppStoreServerLibrary.Models;

namespace Mimo.AppStoreServerLibraryTests;

public class AppStoreServerApiClientTest
{
    private const string TestSigningKey =
    """
    -----BEGIN PRIVATE KEY-----
    MIGHAgEAMBMGByqGSM49AgEGCCqGSM49AwEHBG0wawIBAQQgSpP55ELdXswj9JRZ
    APRwtTfS4CNRqpKIs+28rNHiPAqhRANCAASs8nLES7b+goKslppNVOurf0MonZdw
    3pb6TxS8Z/5j+UNY1sWK1ChxpuwNS9I3R50cfdQo/lA9PPhw6XIg8ytd
    -----END PRIVATE KEY-----
    """;
    private const string KeyId = "TEST123456";
    private const string IssuerId = "99b16628-15e4-4668-972c-d7934e8838b6";
    private const string BundleId = "com.example.app";

    private static AppStoreServerApiClient GetAppStoreServerApiClient(MockHttpMessageHandler mockHttp)
    {
        return new AppStoreServerApiClient(TestSigningKey, KeyId, IssuerId, BundleId, AppStoreEnvironment.LocalTesting, mockHttp.ToHttpClient());
    }

    [Fact]
    public async Task GetAllSubscriptionStatuses_Success()
    {
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When($"{AppStoreEnvironment.LocalTesting.BaseUrl}v1/subscriptions/123456")
               .Respond("application/json", "{\"data\":[]}");

        AppStoreServerApiClient client = GetAppStoreServerApiClient(mockHttp);
        SubscriptionStatusResponse response = await client.GetAllSubscriptionStatuses("123456");

        Assert.NotNull(response);
    }

    [Fact]
    public async Task GetNotificationHistory_Success()
    {
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When($"{AppStoreEnvironment.LocalTesting.BaseUrl}v1/notifications/history")
               .Respond("application/json", "{\"notificationHistory\":[]}");

        AppStoreServerApiClient client = GetAppStoreServerApiClient(mockHttp);
        NotificationHistoryResponse? response = await client.GetNotificationHistory(new NotificationHistoryRequest());

        Assert.NotNull(response);
    }

    [Fact]
    public async Task GetTransactionHistory_Success()
    {
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When($"{AppStoreEnvironment.LocalTesting.BaseUrl}v2/history/123456")
               .Respond("application/json", "{\"signedTransactions\":[]}");

        AppStoreServerApiClient client = GetAppStoreServerApiClient(mockHttp);
        TransactionHistoryResponse? response = await client.GetTransactionHistory("123456");

        Assert.NotNull(response);
    }

    [Fact]
    public async Task SendConsumptionData_Success()
    {
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When($"{AppStoreEnvironment.LocalTesting.BaseUrl}v1/transactions/consumption/123456")
               .Respond(System.Net.HttpStatusCode.OK);

        AppStoreServerApiClient client = GetAppStoreServerApiClient(mockHttp);
        await client.SendConsumptionData("123456", new ConsumptionRequest
        {
            AccountTenure = 1,
            AppAccountToken = "test-token",
            ConsumptionStatus = 1,
            CustomerConsented = true,
            DeliveryStatus = 0,
            LifetimeDollarsPurchased = 1,
            LifetimeDollarsRefunded = 1,
            Platform = 1,
            PlayTime = 1,
            RefundPreference = 1,
            SampleContentProvided = false,
            UserStatus = 1
        });
    }
}
