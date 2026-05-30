# Remarkable.Api.Client

A small .NET client for the [reMarkable](https://remarkable.com) cloud. Covers
the three operations needed to pair a device and upload a PDF:

1. Exchange a one-time pairing code for a long-lived **device token**.
2. Exchange that device token for a short-lived **session token**.
3. Upload a PDF to the user's library root with a chosen display name.

The client is stateless with respect to tokens — each method takes the token it
needs as an argument, leaving persistence and refresh policy to the caller.

## Install

```sh
dotnet add package Remarkable.Api.Client
```

Targets `net10.0`.

## Usage

```csharp
using Remarkable.Api.Client;

using var http = new HttpClient();
var client = new RemarkableClient(http);

// 1. Pair. The user gets the 8-character code from
//    https://my.remarkable.com/device/desktop/connect
var deviceToken = await client.PairDeviceAsync(
    code: "ABCD1234",
    deviceDescription: DeviceDescriptions.DesktopMacOs);

// Persist deviceToken — it does not expire.

// 2. Refresh. Session tokens expire after roughly an hour; refresh whenever
//    uploads start returning 401.
var sessionToken = await client.RefreshSessionAsync(deviceToken);

// 3. Upload.
await using var pdf = File.OpenRead("paper.pdf");
var result = await client.UploadPdfAsync(sessionToken, "Interesting paper", pdf);

Console.WriteLine($"Uploaded {result.DocumentId}");
```

Errors from the cloud surface as `RemarkableApiException` with the HTTP status
code and response body.

### Device descriptions

The `deviceDescription` passed to `PairDeviceAsync` must match the connect-URL
family that produced the one-time code:

| Description constant                       | Connect URL family                          |
| ------------------------------------------ | ------------------------------------------- |
| `DeviceDescriptions.DesktopWindows`        | `my.remarkable.com/device/desktop/connect`  |
| `DeviceDescriptions.DesktopMacOs`          | `my.remarkable.com/device/desktop/connect`  |
| `DeviceDescriptions.DesktopLinux`          | `my.remarkable.com/device/desktop/connect`  |
| `DeviceDescriptions.MobileAndroid`         | `my.remarkable.com/device/mobile/connect`   |
| `DeviceDescriptions.MobileIos`             | `my.remarkable.com/device/mobile/connect`   |
| `DeviceDescriptions.BrowserChrome`         | `my.remarkable.com/device/browser/connect`  |

### Custom hosts

For testing against a mock or self-hosted backend, pass a `RemarkableHosts`:

```csharp
var hosts = new RemarkableHosts(
    AuthHost:   new Uri("https://auth.local"),
    UploadHost: new Uri("https://upload.local"));
var client = new RemarkableClient(http, hosts);
```

## Samples

Two CLI samples live under `samples/`:

- `rmpair` — pair a device and print the device token.
- `rmupload` — upload a PDF, optionally refreshing the session token first.

## Protocol reference

This library is a clean-room implementation of the protocol described in
[`docs/protocol-reference.md`](https://github.com/tombly/remarkable-api-client/blob/main/docs/protocol-reference.md),
derived from reading the source of the MIT-licensed
[`rmapi-js`](https://github.com/erikbrinkman/rmapi-js) project.

This library is not affiliated with reMarkable AS.

## License

[MIT](https://github.com/tombly/remarkable-api-client/blob/main/LICENSE)
