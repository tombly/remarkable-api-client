namespace Remarkable.Api.Client;

/// <summary>
/// Base URLs for the reMarkable cloud endpoints. Use <see cref="Default"/> for production,
/// or construct a custom instance to point at a self-hosted or mock backend.
/// </summary>
public sealed record RemarkableHosts(Uri AuthHost, Uri UploadHost)
{
    public static RemarkableHosts Default { get; } = new(
        AuthHost: new Uri("https://webapp-prod.cloud.remarkable.engineering"),
        UploadHost: new Uri("https://internal.cloud.remarkable.com"));
}
