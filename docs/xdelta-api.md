# Gladius xDelta API — Client Integration Guide

How the ModdingGUI app discovers and downloads community `.xdelta` patches from
`gladiuscommunity.com`, and how another tool can do the same.

All endpoints below were verified live against the production server on **2026-10-25**.
There are **two** endpoints you need for read-only access. No API key, token, or login
is required to list or download patches.

---

## 1. How ModdingGUI detects available xdeltas

Source: [`ModdingGUI/frmMain_partials/xDeltaServer.cs`](../ModdingGUI/frmMain_partials/xDeltaServer.cs)

| Step | Location | Behaviour |
|---|---|---|
| Client setup | `xDeltaServer.cs:12-21` | Static `HttpClient` with a `CookieContainer`, `BaseAddress = https://gladiuscommunity.com/` |
| List on startup | `Form1.cs:273` → `xDeltaServer.cs:124` | `LoadFileListAsync()` runs on form load, **before any authentication** |
| Fetch manifest | `xDeltaServer.cs:130` | `GET xdelta/list` |
| Parse | `xDeltaServer.cs:137` | `System.Text.Json` → `List<XDeltaFile>` with `PropertyNameCaseInsensitive = true` |
| Model | `xDeltaServer.cs:376-382` | `mod`, `fileName`, `fileSize`, `uploadedDate` |
| Group/sort | `xDeltaServer.cs:146-153` | Group by `mod` (alphabetical); files per mod newest-first by `uploadedDate` |
| Display | `xDeltaServer.cs:155-172` | TreeView: mod nodes with file child nodes; each child's `Tag` is the record |
| Download | `xDeltaServer.cs:337-373` | `GET xdelta/download/{Uri.EscapeDataString(fileName)}`, then write the response bytes to disk |

Key takeaway: **discovery is just a public JSON GET**. Authentication (Discord OAuth →
`connect.sid` session cookie) is only needed to **upload**, or as a fallback retry if a
download ever returns `401`/`403`. The GUI treats those codes as "session expired" and
re-launches its WebView2 login.

---

## 2. Endpoints

Base URL: `https://gladiuscommunity.com`

### `GET /xdelta/list` — manifest (public)

Returns every published patch. No auth, no cookies, no query parameters (filters such
as `?mod=Ragnarok` are ignored — filter client-side).

```
200 OK
content-type: application/json; charset=utf-8
access-control-allow-origin: https://gladiuscommunity.com
```

```json
[
  {
    "mod": "Ragnarok",
    "fileName": "Gladius_Ragnarok_v2.2.2_patch.xdelta",
    "fileSize": 10502801,
    "uploadedDate": "2026-05-11T13:15:34.230Z"
  },
  {
    "mod": "LeoMod",
    "fileName": "Gladius Leonarth Public Beta v1.2.xdelta",
    "fileSize": 3237351,
    "uploadedDate": "2025-03-09T03:30:16.066Z"
  }
]
```

| Field | Type | Notes |
|---|---|---|
| `mod` | string | Display/grouping label. **Not** a filename and not guaranteed unique per file |
| `fileName` | string | Exact stored filename, always ends in `.xdelta`. **May contain spaces** — use verbatim |
| `fileSize` | number | Size in bytes; matches the download's `Content-Length` |
| `uploadedDate` | string | ISO-8601 UTC (`...Z`). Use this for version ordering |
| Response order | — | Not contractual. Sort yourself (the GUI sorts per-mod newest-first) |

CORS note: the server returns a **fixed** `Access-Control-Allow-Origin:
https://gladiuscommunity.com`, so a browser app hosted on any other origin cannot read
the manifest. Use a desktop, CLI, or server-side client (or proxy through your own
backend).

### `GET /xdelta/download/{fileName}` — patch bytes (public)

`{fileName}` is the `fileName` value from the manifest, percent-encoded as a **single
path segment** (encode the space in `Gladius Leonarth Public Beta v1.2.xdelta` as
`%20`). Do not build this from `mod`; always use the manifest's exact `fileName`.

```
200 OK
content-type: application/octet-stream
content-length: 3237351
content-disposition: attachment; filename="Gladius Leonarth Public Beta v1.2.xdelta"
accept-ranges: bytes
```

- Bytes are a standard xdelta3 patch (`VCDIFF`), not a zip.
- `Accept-Ranges: bytes` is advertised, so resumed/partial downloads are possible, but
  a plain full GET is the safe baseline.
- Missing file: `404` with `{"message":"File not found"}`.

### `POST /xdelta/upload` — publishing (authenticated)

Only needed if your tool **uploads** patches. Confirmed behaviour:

- Unauthenticated request → `401` `{"message":"Unauthorized: Please log in."}`
- Authenticated but not an approved modder → `403`
- Multipart field name is `file`; the filename should end in `.xdelta`
- The GUI enforces a 15 MB client-side cap (`xDeltaServer.cs:237`)

Login is Discord OAuth at `https://gladiuscommunity.com/auth/discord`, returning via
`/auth/discord/callback` and setting the Express session cookie `connect.sid`. The GUI
hosts that flow in a WebView2 window, then copies cookies into its `HttpClient`
(`xDeltaServer.cs:66-120`). A headless client must perform the same OAuth flow itself
(or be handed a `connect.sid` cookie by its user). Treat that cookie as a password:
it is a session credential and must not be logged, committed, or shared.

---

## 3. Minimal client

### curl

```bash
# 1. Discover
curl -s https://gladiuscommunity.com/xdelta/list

# 2. Download one patch (space → %20, keep the rest of the name verbatim)
curl -fL -o "Gladius Leonarth Public Beta v1.2.xdelta" \
  "https://gladiuscommunity.com/xdelta/download/Gladius%20Leonarth%20Public%20Beta%20v1.2.xdelta"
```

### Python

```python
import json, urllib.parse, urllib.request
from pathlib import Path

BASE = "https://gladiuscommunity.com"

def list_patches() -> list[dict]:
    with urllib.request.urlopen(f"{BASE}/xdelta/list", timeout=30) as r:
        return json.load(r)

def download(file_name: str, dest_dir: Path) -> Path:
    # quote(safe="") percent-encodes spaces and any reserved characters
    url = f"{BASE}/xdelta/download/{urllib.parse.quote(file_name, safe='')}"
    dest = dest_dir / file_name          # use the manifest name, not `mod`
    dest.parent.mkdir(parents=True, exist_ok=True)
    with urllib.request.urlopen(url, timeout=120) as r, open(dest, "wb") as f:
        while chunk := r.read(1 << 20):  # stream: one patch is ~640 MB
            f.write(chunk)
    return dest

patches = list_patches()
latest_per_mod = {}
for p in patches:                        # newest by uploadedDate, per mod
    cur = latest_per_mod.get(p["mod"])
    if cur is None or p["uploadedDate"] > cur["uploadedDate"]:
        latest_per_mod[p["mod"]] = p

for mod, p in sorted(latest_per_mod.items()):
    print(f"{mod}: {p['fileName']} ({p['fileSize']:,} bytes, {p['uploadedDate']})")
```

### C# (.NET) — mirrors the GUI

```csharp
using System.Net.Http;
using System.Text.Json;

var client = new HttpClient { BaseAddress = new Uri("https://gladiuscommunity.com/") };

record XDeltaFile(string mod, string fileName, long fileSize, DateTime uploadedDate);

var files = JsonSerializer.Deserialize<List<XDeltaFile>>(
    await client.GetStringAsync("xdelta/list"),
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

string name = "Gladius Leonarth Public Beta v1.2.xdelta";
using var res = await client.GetAsync(
    $"xdelta/download/{Uri.EscapeDataString(name)}",
    HttpCompletionOption.ResponseHeadersRead);
res.EnsureSuccessStatusCode();
await using var src = await res.Content.ReadAsStreamAsync();
await using var dst = File.Create(name);          // stream, don't buffer ~640 MB
await src.CopyToAsync(dst);
```

### Node.js (CLI/server only — not a browser)

```js
const BASE = "https://gladiuscommunity.com";
const list = await (await fetch(`${BASE}/xdelta/list`)).json();
const patch = list.find(p => p.mod === "Ragnarok" && p.fileName.includes("v2.2.2"));
const url = `${BASE}/xdelta/download/${encodeURIComponent(patch.fileName)}`;
const res = await fetch(url);
if (!res.ok) throw new Error(`${res.status}: ${await res.text()}`);

// Stream to disk instead of buffering ~640 MB
const { Readable } = await import("node:stream");
const { createWriteStream } = await import("node:fs");
const { pipeline } = await import("node:stream/promises");
await pipeline(Readable.fromWeb(res.body), createWriteStream(patch.fileName));
```

---

## 4. Practical advice

- **Treat `fileName` as an opaque key.** Spaces, mixed case, and version strings vary.
  Percent-encode it as one path segment; never path-join it blindly onto a directory
  without sanitising (it comes from a remote server).
- **Stream downloads to disk.** The largest published patch is ~643 MB
  (`Gladius Recruit Hack GC.xdelta`); buffering the whole response in memory is avoidable.
- **Verify size.** Compare the written byte count with `fileSize` / `Content-Length`
  before applying. Then apply with xdelta3:
  `xdelta3 -d -f -s vanilla.iso patch.xdelta out.iso` (the GUI does exactly this in
  `Patching.cs:201`).
- **Poll gently.** One GET of `/xdelta/list` at startup is what the GUI does. A few
  requests per hour is plenty; compare `(fileName, fileSize, uploadedDate)` to detect
  changes and don't re-download unchanged patches.
- **Don't hard-code the host.** Keep the base URL in config; the server may move.
- **Handle the error codes** the GUI handles: `404` = gone; `401`/`403` on download =
  retry after re-auth (rare today, since downloads are public); `401`/`403` on upload =
  not logged in / not an approved modder.
- **CORS blocks browser-only clients** from other origins — plan for a native or
  server-side component.

## 5. Source references

- List/download/upload implementation: [`xDeltaServer.cs`](../ModdingGUI/frmMain_partials/xDeltaServer.cs)
- Startup call to `LoadFileListAsync()`: [`Form1.cs:273`](../ModdingGUI/Form1.cs)
- Patch application with xdelta3: [`Patching.cs:188-230`](../ModdingGUI/frmMain_partials/Patching.cs)
