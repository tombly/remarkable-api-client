namespace Remarkable.Api.Client;

/// <summary>
/// Known values for the <c>deviceDesc</c> field accepted by the reMarkable pairing endpoint.
/// The value must match the connect-URL family that produced the one-time code:
/// <list type="bullet">
///   <item>Desktop values pair with <c>my.remarkable.com/device/desktop/connect</c>.</item>
///   <item>Mobile values pair with <c>my.remarkable.com/device/mobile/connect</c>.</item>
///   <item><see cref="BrowserChrome"/> pairs with <c>my.remarkable.com/device/browser/connect</c>.</item>
/// </list>
/// </summary>
public static class DeviceDescriptions
{
    public const string DesktopWindows = "desktop-windows";
    public const string DesktopMacOs = "desktop-macos";
    public const string DesktopLinux = "desktop-linux";
    public const string MobileAndroid = "mobile-android";
    public const string MobileIos = "mobile-ios";
    public const string BrowserChrome = "browser-chrome";
    public const string Remarkable = "remarkable";
}
