using AppStoreServerLibraryDotnet.Models;

namespace AppStoreServerLibraryDotnet.Configuration;

public class AppleOptions
{
    /// <summary>
    /// Name of the settings in the config file
    /// </summary>
    public const string AppleOptionsSectionName = "AppleOptions";

    public string BundleId { get; set; } = string.Empty;
    public string AppStoreServerApiKeyId { get; set; } = string.Empty;
    public string AppStoreServerApiIssuerId { get; set; } = string.Empty;
    public string AppStoreServerApiSubscriptionKey { get; set; } = string.Empty;
    public bool DisableOnlineCertificateRevocationCheck { get; set; }
    public string Environment { get; set; } = AppStoreServerEnvironment.Sandbox;
}