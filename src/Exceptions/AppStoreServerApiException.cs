using AppStoreServerLibraryDotnet.Models;

namespace AppStoreServerLibraryDotnet.Exceptions;

public class AppStoreServerApiException(string message, ErrorResponse errorResponse) : Exception(message)
{
    public int ApiErrorCode { get; set; } = errorResponse.ErrorCode;
    public string ApiErrorMessage { get; set; } = errorResponse.ErrorMessage;

    public override string ToString()
    {
        return base.ToString() + $" ApiErrorCode: {ApiErrorCode}, ApiErrorMessage: {ApiErrorMessage}";
    }
}