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

- **The Eorzea Collection endpoint is undocumented.** GlamourLink reads it
  because it is the only source of a glamour's gear list, not because it is a
  published API. It can start returning errors or a different shape without
  notice; if imports stop working, that is the likely cause. The base URL and
  the User-Agent are both editable in settings if the site's own defenses
  change.
- **Items are matched by name**, since the site gives names, not item ids.
  Most names match exactly. A hand-typed name that does not match any item
  is reported as unresolved, not silently dropped, and a close-but-not-exact
  name is applied as a labelled guess rather than skipped, since a wrong
  guess is a one-click Glamourer revert. An occasional item will still fail
  to resolve.
- **Fashion accessories are never applied.** Glamourer has no slot for that
  category.
- **Facewear dye is never applied.** Glamourer's facewear IPC call takes no
  stain, so there is nothing to send a dye through even when the source
  glamour has one.
- **A weapon or off-hand from a different job's weapon type only shows in
  GPose.** This is how Glamourer itself handles a mismatched weapon type, not
  a GlamourLink limitation; the plan table warns about it inline.
- Character appearance (race, hair, face) is never touched, only equipment
  and bonus items.

## Needs in-game verification

The build is clean, but the following can only be confirmed by playing:

- `SetItem` applies without opening the glamour dresser.
- Both stains land correctly on a two-dye item.
- `itemId 0` clears a slot as expected.
- `SetBonusItem` applies glasses.
- Facewear dye is genuinely unreachable through the IPC as read from source.
- A weapon or off-hand of a different job's type shows only in GPose, as
  Glamourer's own rule predicts.
- `GetStateBase64`, called two ticks after apply, captures the applied look,
  and `AddDesign` creates the named design from it.
- `ApplyFlag.Once` survives a zone change or a redraw as expected.
- Building the item index off the framework thread neither hitches the game
  nor trips a Lumina thread-safety assertion.
- The first import's index build time, as logged at `Log.Information`, is
  short enough not to be noticeable.
- The library file appears at the documented path after the first save and
  parses as JSON with slot names written as strings.
- Applying a saved outfit works with the base URL in settings pointed at an
  unroutable host, proving the apply path makes no network call.
- Refetching an outfit updates its gear while keeping its name, tags,
  favourite and note.
- Deleting an outfit while its refetch is in flight does not bring the
  outfit back once the refetch completes.
- A delete survives a game restart.
- With all three GlamourLink windows closed, `/xlstats` shows no per-frame
  draw cost for the plugin.
- The save button is disabled once the library holds 500 outfits.
- A crash or kill between the temp-file write and the rename during a
  library save leaves the previous `library.json` intact rather than a
  truncated one.
- A hand-corrupted `library.json` loads as an empty library, is renamed to
  `library.corrupt-<unix>.json`, and logs one error in `/xllog`.

## Licence

MIT. See `LICENSE`.
