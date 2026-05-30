# reMarkable Cloud Protocol Reference

Pure protocol reference for authenticating with the reMarkable cloud and
uploading a PDF document with a chosen display name.

This document is derived from reading the source code of the MIT-licensed
[`rmapi-js`][rmapi-js] project, version **10.0.0** (the `main` branch as of
2026-05-29).

Per-section sources cite specific functions in `rmapi-js`:

- `src/index.ts` — top-level api: hosts, `register()`, `auth()`, the
  `Remarkable` class.
- `src/raw.ts` — lower-level wire calls, including `uploadFile()` on
  `RawRemarkable`.

[rmapi-js]: https://github.com/erikbrinkman/rmapi-js/tree/v10.0.0

Scope:

1. Pairing a device and obtaining a long-lived **device token**.
2. Exchanging a device token for a short-lived **session token**.
3. Uploading a PDF via the server-side `/doc/v2/files` endpoint and setting
   its visible name.

> The upload endpoint described here is server-side: it accepts the raw PDF
> bytes and assigns a UUID, generates the document's metadata/content blobs,
> and inserts the document into the user's library root on the server. It
> works regardless of whether the account is on the v3 or v4 sync schema, so
> no schema-version handling is required on the client.

Out of scope: notebooks, EPUBs (the flow is identical apart from the MIME
type), templates, the `.rm` page format, file download, modification, move,
delete, the trash, folder placement, tags, pinning, bibliographic metadata,
rendering settings, and direct access to the low-level content-addressed
sync store.

---

## 1. Hosts

| Purpose      | Default URL                                         |
| ------------ | --------------------------------------------------- |
| Auth host    | `https://webapp-prod.cloud.remarkable.engineering`  |
| Upload host  | `https://internal.cloud.remarkable.com`             |

These hosts are stable in practice and used by all observed clients. Service
discovery is not required for this flow — hosts are hardcoded.

All requests use HTTPS. All JSON bodies use UTF-8.

> *Source: `src/index.ts` constants `AUTH_HOST` and `UPLOAD_HOST` in
> [rmapi-js][rmapi-js].*

---

## 2. Authentication

Authentication is a two-step exchange:

1. **One-time pairing.** The user obtains an 8-character one-time code from
   `https://my.remarkable.com/device/desktop/connect` (or `.../browser/connect`
   for browser-class clients). The code is exchanged for a **device token**
   that is persisted by the client and never expires.
2. **Session refresh.** Before making upload calls, the device token is
   exchanged for a short-lived **session token** (JWT). The session token is
   sent as `Authorization: Bearer <session-token>` on every upload call.

### 2.1 Device pairing

```
POST {AUTH_HOST}/token/json/2/device/new
Authorization: Bearer
Content-Type: application/json

{
  "code":       "<8-character code from my.remarkable.com>",
  "deviceDesc": "<device class — see below>",
  "deviceID":   "<UUIDv4 chosen by the client>"
}
```

- `Authorization` is the literal value `Bearer` with no token. The header
  must be present.
- `deviceDesc` is one of the device classes accepted by the server. The class
  must match the connect URL the user used to generate the code:

  | `deviceDesc`        | Connect URL family                          |
  | ------------------- | ------------------------------------------- |
  | `desktop-windows`   | `my.remarkable.com/device/desktop/connect`  |
  | `desktop-macos`     | `my.remarkable.com/device/desktop/connect`  |
  | `desktop-linux`     | `my.remarkable.com/device/desktop/connect`  |
  | `mobile-android`    | `my.remarkable.com/device/mobile/connect`   |
  | `mobile-ios`        | `my.remarkable.com/device/mobile/connect`   |
  | `browser-chrome`    | `my.remarkable.com/device/browser/connect`  |
  | `remarkable`        | (the device itself)                         |

  Sending a `deviceDesc` whose family does not match the code's connect URL
  will cause the pairing call to fail.

- `deviceID` is a client-generated UUIDv4. It is sent to the server but its
  only requirement is uniqueness; clients commonly generate it fresh per
  installation and persist it alongside the device token.

**Response** (`200 OK`)

```
<device-token>
```

The response body is the device token as a **plain text** string (not JSON).
Persist it. It does not expire.

**Failure**: any non-2xx response means the code was invalid, the
`deviceDesc` mismatched the code family, or the code was already consumed.
Codes are single-use.

> *Source: the `register()` function in `src/index.ts` of
> [rmapi-js][rmapi-js], including the `RegisterOptions.deviceDesc` enum.*

### 2.2 Session token refresh

```
POST {AUTH_HOST}/token/json/2/user/new
Authorization: Bearer <device-token>
```

No request body.

**Response** (`200 OK`)

```
<session-token>
```

The response body is the session token as a **plain text** JWT. Session
tokens are short-lived (observed ~1 hour). Refresh whenever upload calls
begin returning `401`.

The upload request below uses `Authorization: Bearer <session-token>`.

> *Source: the `auth()` function in `src/index.ts` of [rmapi-js][rmapi-js].*

---

## 3. Upload a PDF

The client `POST`s the raw PDF bytes along with a small JSON metadata
header. The server assigns the document UUID, generates all internal
metadata, and inserts the document into the user's library root.

```
POST {UPLOAD_HOST}/doc/v2/files
Authorization: Bearer <session-token>
Content-Type:  application/pdf
rm-meta:       <base64( JSON({ "file_name": "<visible name>" }) )>
rm-source:     RoR-Browser

<raw PDF bytes as request body>
```

- `Content-Type` for a PDF: `application/pdf`. (`application/epub+zip` for
  an EPUB; `folder` with an empty body to create a folder. Only
  `application/pdf` is in scope here.)
- `rm-meta` is the base64-encoded UTF-8 bytes of the JSON object
  `{"file_name": "<visible name>"}`. Standard base64, no URL-safe variant,
  no line wrapping.
- `rm-source` identifies the client; any short string accepted by the
  server works. `RoR-Browser` is the observed value used by web clients.

**Response** (`200 OK`)

```json
{
  "docID": "<uuid4 of the new document>",
  "hash":  "<sha256 of the document collection, hex>"
}
```

The server assigns the document UUID and computes the collection hash. The
file is placed in the user's library root with `visibleName` set to the
`file_name` field.

> *Source: the `uploadFile()` method on the `RawRemarkable` class in
> `src/raw.ts` of [rmapi-js][rmapi-js], together with the `UploadMimeType`
> and `NativeSimpleEntry` types defined in the same file.*

---

## 4. Quick reference

| Step              | Method | URL                                  | Auth header                | Body / key headers                                                  |
| ----------------- | ------ | ------------------------------------ | -------------------------- | ------------------------------------------------------------------- |
| Pair device       | POST   | `{AUTH}/token/json/2/device/new`     | `Bearer`                   | JSON `{code, deviceDesc, deviceID}` → text device token             |
| Refresh session   | POST   | `{AUTH}/token/json/2/user/new`       | `Bearer <device-token>`    | empty → text session token (JWT)                                    |
| Upload PDF        | POST   | `{UPLOAD}/doc/v2/files`              | `Bearer <session-token>`   | PDF bytes; `Content-Type: application/pdf`, `rm-meta`, `rm-source`  |

`{AUTH}` and `{UPLOAD}` are the hosts listed in §1.
