# GlamourLink working notes

Things that cost real time to find, kept here so they are not rediscovered.

## Eorzea Collection is reachable only over WinHttpHandler on HTTP/2

The site is behind a Cloudflare managed challenge that scores the TLS ClientHello and the
HTTP/2 SETTINGS frame. Both are sent before any header exists, so no User-Agent, header
set or protocol tweak can satisfy it from `SocketsHttpHandler`.

Measured, same URL and same Chrome User-Agent:

| client | result |
| --- | --- |
| curl, HTTP/2 | 200 JSON |
| curl, forced HTTP/1.1 | 403 |
| .NET `SocketsHttpHandler`, HTTP/1.1 and HTTP/2 | 403 |
| .NET with curl-identical headers, and with the full Chrome header set | 403 |
| python urllib, python httpx over HTTP/2, wget | 403 |
| .NET `WinHttpHandler`, HTTP/1.1 | 403 |
| **.NET `WinHttpHandler`, HTTP/2** | **200 JSON** |

HTTP/2 is necessary but not sufficient. Neither curl nor WinHTTP resembles Chrome; curl
offers 30 cipher suites against Chrome's ~16, and their HTTP/2 SETTINGS frames differ.
The challenge is a reputation score on the handshake, not a browser check.

The User-Agent still matters on top of that: no User-Agent, or a bare `Mozilla/5.0`, is
refused even from curl. `ParseAdd` splits the string into product tokens internally but
reassembles it byte-for-byte on the wire, which was verified against a local listener.

The HTML page is guarded more tightly than the JSON endpoint; curl passes on `/api/` and
is refused on `/glamour/<id>/<slug>`. Scraping the page is not a fallback, and reading
their JavaScript does not help, because the decision happens during the handshake.

## Dalamud IPC turns a byte[] into base64, so never pass one

`CheckAndConvertArgs` in `Dalamud/Plugin/Ipc/Internal/CallGateChannel.cs` compares an
argument's type to the parameter type and, on a mismatch, walks only the **base-type**
chain. Interfaces are not considered. `byte[]` therefore does not match a parameter typed
`IReadOnlyList<byte>`, because its base type is `Array`, and Dalamud falls back to a
Newtonsoft round-trip. Newtonsoft writes a `byte[]` as a base64 **string**, which cannot
deserialize back into a list, and the call dies with `IpcTypeMismatchError`.

`List<byte>` serializes as `[102,0]` and survives. Glamourer's `SetItem` takes its stains
as `IReadOnlyList<byte>`, so it must be given a `List<byte>`.

The failure is silent about its cause: the useful text is in the *inner* exceptions, which
is why every catch in this plugin flattens the chain into the dev log.

## A design saved through AddDesign must have its customize flags cleared

`DesignsApi.AddDesign` hardcodes `customize: true, equip: true`, but those arguments only
*strip* application flags when false; they never add any. The flags come from the payload.

So a design built from a live character state applies that character's face, body and
colours unless the flags are turned off first. `GetStateBase64` cannot be edited, so read
the state with `GetState`, which returns a `JObject`, clear every `Apply` under
`Customize` and `Parameters`, and pass that JSON to `AddDesign`. Glamourer omits a false
`Apply` rather than writing it, so an absent flag is already off.

## Checking Glamourer's API

Check against `upstream/stable` in `~/dev/Glamourer`, not whatever branch the fork is on.
As of this writing `upstream/stable` and the fork point at the same `Glamourer.Api`
commit, and nuget's newest `Glamourer.Api` is 2.8.2, so the three agree.

## Still unverified in game

The full list is in the README. The one this session did not reach is the gear-only design
save: tick "Save as a Glamourer design", then apply the resulting design to a character
with a different face or body and confirm only the outfit changes.

Designs created before 0.2.3 carry live customization flags and will change appearance.
