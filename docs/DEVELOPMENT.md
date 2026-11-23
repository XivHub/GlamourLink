# Development

## Build

Requires the .NET SDK and an extracted Dalamud dev bundle. Shared helpers come from
[XivHubPluginKit](https://github.com/XivHub/XivHubPluginKit), linked by source: clone it next to this
repo, so `../XivHubPluginKit` exists.

```bash
DALAMUD_HOME=~/.cache/dalamud-dev DOTNET_ROOT=~/.dotnet \
  dotnet build GlamourLink/GlamourLink.csproj -c Release -p:Platform=x64
```

Bump `<Version>` in `GlamourLink/GlamourLink.csproj` before publishing with `publish-plugin`.

## Icon

The icon is `images/icon.svg`; `images/icon.png` is rendered from it at 512 px, which
is the copy the plugin installer fetches.

## Dev logging

Settings has a `Developer` section that posts a live log to a devlog server on your own
network, for working on GlamourLink itself. It is off by default and points nowhere; the
telemetry object is not constructed until both the toggle and a URL are set, so a normal
install runs no timer and opens no connection for it.

Run the server from the plugin kit and point the plugin at it:

    python3 ~/dev/XivHubPluginKit/devlog_server.py
    tail -f ~/.cache/zhyra-devlog/live.log

It listens on `0.0.0.0:9999` with no authentication, so keep it to a trusted network.

The log covers the things that can only be checked in game: fetch status, the resolved
counts per import, every guessed or unresolved slot with both names, each slot's
Glamourer return code, the character state length read back for a design save, the item
index build time, and library saves.

## Why the HTTP client is unusual

Eorzea Collection sits behind a Cloudflare managed challenge that scores the TLS ClientHello
and the HTTP/2 SETTINGS frame. Both are sent before any header exists, so no User-Agent or
header set can satisfy it, and the HTML page is guarded more tightly than the JSON endpoint;
scraping it is not an alternative.

Measured against `/api/glamour/354779` from Windows: `SocketsHttpHandler`, .NET's default, is
refused on HTTP/1.1 and on HTTP/2. `WinHttpHandler` is refused on HTTP/1.1 and served on
HTTP/2. GlamourLink therefore uses `WinHttpHandler` and pins requests to HTTP/2.

This is a property of someone else's bot protection, so it can stop working at any time.
