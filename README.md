# GlamourLink

A Dalamud plugin for Final Fantasy XIV. Paste an Eorzea Collection glamour URL
or id and GlamourLink reads its gear list, matches each piece against the
game's own item data, and applies the result to your character through
Glamourer.

**Requires [Glamourer](https://github.com/Ottermandias/Glamourer).**
GlamourLink does not touch equipment itself; every change goes through
Glamourer's IPC. If Glamourer is not installed or not loaded, GlamourLink
tells you and the apply button stays disabled.

## Use

- `/glink` opens the import window.
- `/glink <url or id>` fetches a glamour directly, for example
  `/glink https://ffxiv.eorzeacollection.com/glamour/354779/archaic-artificer`
  or `/glink 354779`.
- `/glink config` opens settings.

Fetching a glamour shows a table of every slot: what it resolved to, whether
that was an exact match or a guess, and what could not be applied. Nothing
touches your character until you press "Apply to me".

## Your outfit library

`/glink library` opens a browsable library of the outfits you have saved.
From the main window, after fetching a glamour, press "Save to library" (or
"Update in library" if that Eorzea Collection id is already saved).

Saving stores the resolved gear itself, not just the Eorzea Collection id, so
applying a saved outfit later makes no network call. It keeps working even
if the Eorzea Collection endpoint changes or goes away.

You can tag outfits with your own words; Eorzea Collection supplies no tags,
so all of them are yours. The library window filters by a search box (name
and tags), and by character, server, favourite and "has a weapon".

"Refetch" re-resolves an outfit's gear against the current item and dye
sheets and updates its character and server, but keeps the outfit's name,
tags, favourite and note as you set them.

The library holds up to 500 outfits. Nothing is evicted automatically;
outfits are things you chose to save, so deleting the oldest one for you
would be data loss. At the cap, the save button is disabled until you
delete something.

The library file lives at
`%AppData%\XIVLauncher\pluginConfigs\GlamourLink\library.json`; the config
window shows the exact path and how many outfits are saved.

## Install

Add this URL under Dalamud Settings → Experimental → Custom Plugin
Repositories:

```
https://plugins.xivhub.net/pluginmaster.json
```

## What it can't do

- Eorzea Collection has no official API. If imports suddenly stop working, the site probably
  changed, and an update will be needed.
- Items are matched by name. A near match is applied and marked as a guess; anything that can't be
  matched is listed so you can see it.
- Fashion accessories and facewear dye can't be applied, because Glamourer doesn't support them.
- A weapon from a different job only shows in GPose. That's how Glamourer works.
- Only gear changes. Your race, hair and face are never touched.

## Licence

GlamourLink is licensed under the GNU Affero General Public License, version 3 or
later; see [LICENSE](LICENSE). It links against Glamourer.Api, which is MIT.

Building from source and developer logging: see [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md).
