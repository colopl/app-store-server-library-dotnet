using Mimo.AppStoreServerLibraryDotnet;
using Xunit;

namespace Mimo.AppStoreServerLibraryDotnetTests;

public class ReceiptUtilityTest
{
    [Fact]
    public void ExtractTransactionIdFromAppReceipt_WhenCalledWithValidAppReceipt_ReturnsTransactionId_0()
    {
        string appReceipt =
            File.ReadAllText(
                "./MockReceipts/InputFor_ExtractTransactionIdFromAppReceipt_WhenCalledWithValidAppReceipt_ReturnsTransactionId_0.txt");

        ReceiptUtility utility = new();

        string transactionIdFromAppReceipt = utility.ExtractTransactionIdFromAppReceipt(appReceipt);

        Assert.Equal("0", transactionIdFromAppReceipt);
    }

    [Fact]
    public void ExtractTransactionIdFromAppReceipt_WhitNoTransactions_ThrowsError()
    {
        //A receipt with an empty In-App Purchase array
        string appReceipt =
            File.ReadAllText(
                "./MockReceipts/InputFor_ExtractTransactionIdFromAppReceipt_WhitNoTransactions_ReturnsEmptyString.txt");

        ReceiptUtility utility = new();

        Assert.Throws<FormatException>(() => utility.ExtractTransactionIdFromAppReceipt(appReceipt));
    }
}