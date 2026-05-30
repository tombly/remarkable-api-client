using Remarkable.Api.Client;

if (args.Length < 2 || args[0] is "-h" or "--help")
{
    Console.Error.WriteLine("Usage: rmupload <pdf-path> <visible-name> [--device-token <token> | --session-token <token>]");
    Console.Error.WriteLine();
    Console.Error.WriteLine("  <pdf-path>       Path to the PDF file to upload.");
    Console.Error.WriteLine("  <visible-name>   The name shown on the device.");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Provide exactly one of:");
    Console.Error.WriteLine("  --device-token <token>   A device token from `rmpair`. The sample will refresh");
    Console.Error.WriteLine("                           it to a session token before uploading. Falls back to");
    Console.Error.WriteLine("                           the REMARKABLE_DEVICE_TOKEN environment variable.");
    Console.Error.WriteLine("  --session-token <token>  Skip the refresh step and use this session token");
    Console.Error.WriteLine("                           directly. Falls back to REMARKABLE_SESSION_TOKEN.");
    return 1;
}

var pdfPath = args[0];
var visibleName = args[1];

string? deviceToken = null;
string? sessionToken = null;
for (var i = 2; i < args.Length - 1; i++)
{
    if (args[i] == "--device-token")
    {
        deviceToken = args[++i];
    }
    else if (args[i] == "--session-token")
    {
        sessionToken = args[++i];
    }
}

deviceToken ??= Environment.GetEnvironmentVariable("REMARKABLE_DEVICE_TOKEN");
sessionToken ??= Environment.GetEnvironmentVariable("REMARKABLE_SESSION_TOKEN");

if (sessionToken is null && deviceToken is null)
{
    Console.Error.WriteLine("Error: provide --device-token or --session-token (or the matching env var).");
    return 1;
}

if (!File.Exists(pdfPath))
{
    Console.Error.WriteLine($"Error: PDF not found at '{pdfPath}'.");
    return 1;
}

using var http = new HttpClient();
var client = new RemarkableClient(http);

try
{
    if (sessionToken is null)
    {
        Console.Error.WriteLine("Refreshing session token...");
        sessionToken = await client.RefreshSessionAsync(deviceToken!);
    }

    Console.Error.WriteLine($"Uploading '{pdfPath}' as '{visibleName}'...");
    await using var pdf = File.OpenRead(pdfPath);
    var result = await client.UploadPdfAsync(sessionToken, visibleName, pdf);

    Console.WriteLine($"Document ID: {result.DocumentId}");
    Console.WriteLine($"Hash:        {result.Hash}");
    return 0;
}
catch (RemarkableApiException ex)
{
    Console.Error.WriteLine($"Upload failed (HTTP {ex.StatusCode}):");
    Console.Error.WriteLine(ex.Message);
    return 2;
}
