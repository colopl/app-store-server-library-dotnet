namespace Mimo.AppStoreServerLibraryDotnet.Models;

public record AppStoreEnvironment
{
    public static readonly AppStoreEnvironment Sandbox = new("Sandbox", "https://api.storekit-sandbox.itunes.apple.com/inApps");

    public static readonly AppStoreEnvironment Production = new("Production", "https://api.storekit.itunes.apple.com/inApps");

    /// <summary>
    /// Environment used for local unit testing.
    /// </summary>
    public static readonly AppStoreEnvironment LocalTesting = new("LocalTesting", "https://local-testing-base-url");

    private AppStoreEnvironment(string name, string baseUrl)
    {
        this.Name = name;
        this.BaseUrl = baseUrl;
    }

    public string Name { get; }

    public string BaseUrl { get; }
}