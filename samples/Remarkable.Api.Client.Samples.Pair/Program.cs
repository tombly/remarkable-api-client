using Remarkable.Api.Client;

if (args.Length == 0 || args[0] is "-h" or "--help")
{
    Console.Error.WriteLine("Usage: rmpair <8-character-code> [device-description]");
    Console.Error.WriteLine();
    Console.Error.WriteLine("  <8-character-code>    The one-time code shown at");
    Console.Error.WriteLine("                        https://my.remarkable.com/device/desktop/connect");
    Console.Error.WriteLine();
    Console.Error.WriteLine("  [device-description]  Defaults to '" + DeviceDescriptions.DesktopMacOs + "'.");
    Console.Error.WriteLine("                        Must match the connect-URL family:");
    Console.Error.WriteLine("                          desktop-windows | desktop-macos | desktop-linux");
    Console.Error.WriteLine("                          mobile-android  | mobile-ios");
    Console.Error.WriteLine("                          browser-chrome");
    return 1;
}

var code = args[0];
var deviceDescription = args.Length > 1 ? args[1] : DeviceDescriptions.DesktopMacOs;

using var http = new HttpClient();
var client = new RemarkableClient(http);

try
{
    var deviceToken = await client.PairDeviceAsync(code, deviceDescription);
    Console.WriteLine(deviceToken);
    Console.Error.WriteLine();
    Console.Error.WriteLine("Device token printed to stdout. Save it — it does not expire and is");
    Console.Error.WriteLine("needed to obtain a session token for every subsequent upload.");
    return 0;
}
catch (RemarkableApiException ex)
{
    Console.Error.WriteLine($"Pairing failed (HTTP {ex.StatusCode}):");
    Console.Error.WriteLine(ex.Message);
    return 2;
}
