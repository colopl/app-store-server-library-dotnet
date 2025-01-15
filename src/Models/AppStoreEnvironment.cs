namespace Mimo.AppStoreServerLibrary.Models;

public record AppStoreEnvironment
{
    public static readonly AppStoreEnvironment Sandbox = new("Sandbox", new Uri("https://api.storekit-sandbox.itunes.apple.com/inApps"));

    public static readonly AppStoreEnvironment Production = new("Production", new Uri("https://api.storekit.itunes.apple.com/inApps"));

    /// <summary>
    /// Environment used for local unit testing.
    /// </summary>
    public static readonly AppStoreEnvironment LocalTesting = new("LocalTesting", new Uri("https://local-testing-base-url"));

    private AppStoreEnvironment(string name, Uri baseUrl)
    {
        this.Name = name;
        this.BaseUrl = baseUrl;
    }

    public string Name { get; }

    public Uri BaseUrl { get; }
}