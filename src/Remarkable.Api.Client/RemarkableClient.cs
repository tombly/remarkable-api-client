using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Remarkable.Api.Client;

/// <summary>
/// Client for the reMarkable cloud. Exposes the three operations needed to pair a device
/// and upload a PDF:
/// <list type="number">
///   <item><see cref="PairDeviceAsync"/> — exchange a one-time code for a long-lived device token.</item>
///   <item><see cref="RefreshSessionAsync"/> — exchange a device token for a short-lived session token.</item>
///   <item><see cref="UploadPdfAsync"/> — upload a PDF to the user's library root with a chosen display name.</item>
/// </list>
/// The client is stateless with respect to tokens: each method takes the token it needs as an
/// argument, leaving persistence and refresh policy to the caller.
/// </summary>
public sealed class RemarkableClient
{
    private const string RmSourceHeader = "RoR-Browser";

    private readonly HttpClient _httpClient;
    private readonly RemarkableHosts _hosts;

    public RemarkableClient(HttpClient httpClient)
        : this(httpClient, RemarkableHosts.Default)
    {
    }

    public RemarkableClient(HttpClient httpClient, RemarkableHosts hosts)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _hosts = hosts ?? throw new ArgumentNullException(nameof(hosts));
    }

    /// <summary>
    /// Exchange a one-time pairing code for a device token. The user obtains the 8-character
    /// code from <c>https://my.remarkable.com/device/&lt;family&gt;/connect</c>; the value of
    /// <paramref name="deviceDescription"/> must match the <c>&lt;family&gt;</c> in that URL.
    /// </summary>
    /// <param name="code">The 8-character one-time code.</param>
    /// <param name="deviceDescription">One of the values in <see cref="DeviceDescriptions"/>.</param>
    /// <param name="deviceId">A stable client-generated UUID; a fresh one is generated when omitted.</param>
    /// <returns>A device token. Persist it; it does not expire.</returns>
    public async Task<string> PairDeviceAsync(
        string code,
        string deviceDescription,
        Guid? deviceId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(deviceDescription);
        if (code.Length != 8)
        {
            throw new ArgumentException("Pairing code must be exactly 8 characters.", nameof(code));
        }

        var payload = new PairDeviceRequest(
            Code: code,
            DeviceDesc: deviceDescription,
            DeviceId: (deviceId ?? Guid.NewGuid()).ToString("D"));

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(_hosts.AuthHost, "/token/json/2/device/new"))
        {
            Content = JsonContent.Create(payload),
        };
        // The pairing endpoint requires the literal header value "Bearer" with no token.
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer");

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, "Device pairing failed.", cancellationToken).ConfigureAwait(false);
        var token = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return token.Trim();
    }

    /// <summary>
    /// Exchange a device token for a short-lived session token (a JWT).
    /// Session tokens are observed to expire after roughly one hour; refresh whenever
    /// uploads start returning <c>401</c>.
    /// </summary>
    /// <param name="deviceToken">A device token obtained from <see cref="PairDeviceAsync"/>.</param>
    /// <returns>A session token suitable for passing to <see cref="UploadPdfAsync"/>.</returns>
    public async Task<string> RefreshSessionAsync(
        string deviceToken,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(deviceToken);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(_hosts.AuthHost, "/token/json/2/user/new"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", deviceToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, "Session refresh failed.", cancellationToken).ConfigureAwait(false);
        var token = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return token.Trim();
    }

    /// <summary>
    /// Upload a PDF to the user's library root. The server assigns the document UUID and
    /// generates the internal metadata. The visible display name is set from
    /// <paramref name="visibleName"/>.
    /// </summary>
    /// <param name="sessionToken">A session token from <see cref="RefreshSessionAsync"/>.</param>
    /// <param name="visibleName">The name shown on the device.</param>
    /// <param name="pdfContent">A readable stream containing the PDF bytes.</param>
    /// <returns>The server-assigned document id and content hash.</returns>
    public async Task<UploadResult> UploadPdfAsync(
        string sessionToken,
        string visibleName,
        Stream pdfContent,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionToken);
        ArgumentException.ThrowIfNullOrEmpty(visibleName);
        ArgumentNullException.ThrowIfNull(pdfContent);

        var metaJson = JsonSerializer.Serialize(new FileMeta(visibleName));
        var rmMeta = Convert.ToBase64String(Encoding.UTF8.GetBytes(metaJson));

        var content = new StreamContent(pdfContent);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(_hosts.UploadHost, "/doc/v2/files"))
        {
            Content = content,
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sessionToken);
        request.Headers.TryAddWithoutValidation("rm-meta", rmMeta);
        request.Headers.TryAddWithoutValidation("rm-source", RmSourceHeader);

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, "PDF upload failed.", cancellationToken).ConfigureAwait(false);

        var result = await response.Content
            .ReadFromJsonAsync<UploadResult>(cancellationToken)
            .ConfigureAwait(false);
        return result ?? throw new RemarkableApiException(
            (int)response.StatusCode,
            "Upload succeeded but the response body did not parse as an UploadResult.");
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        string failureMessage,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        throw new RemarkableApiException(
            (int)response.StatusCode,
            $"{failureMessage} HTTP {(int)response.StatusCode} {response.ReasonPhrase}: {body}");
    }

    private sealed record PairDeviceRequest(
        [property: JsonPropertyName("code")] string Code,
        [property: JsonPropertyName("deviceDesc")] string DeviceDesc,
        [property: JsonPropertyName("deviceID")] string DeviceId);

    private sealed record FileMeta(
        [property: JsonPropertyName("file_name")] string FileName);
}
