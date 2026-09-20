# LoupixDeck.Plugin.Mpris

Media control plugin for [LoupixDeck](https://github.com/RadiatorTwo/LoupixDeck),
built against [LoupixDeck.PluginSdk](https://github.com/RadiatorTwo/LoupixDeck.PluginSdk).

Linux only. Every player that implements MPRIS — VLC, Spotify, Elisa, mpv with its
MPRIS script, Firefox and the Chromium-based browsers — is controlled through the
D-Bus session bus. The plugin talks to D-Bus directly and never runs `playerctl` or
any other command line tool.

## Choosing the player

Every command carries a player strategy, picked in the command menu under **Media**:

- **Automatic** — the most recently active player.
- **Currently Playing** — the player that is playing right now.
- **Preferred Player** — the player stored in the plugin settings, set from the
  player folder or by `Mpris.SetPreferredPlayer`.
- **Fixed Player** — the player stored in the button's own binding.

Resolution is deterministic: playing beats paused beats stopped, then the most
recently changed player wins, and the bus name breaks a remaining tie. A browser
appends a changing instance number to its bus name, so a fixed binding also matches
on the application part of the name and keeps working after a browser restart. When
the fixed or preferred player is not running, the command does nothing and writes a
log entry — unless *Fall back when the selected player is unavailable* is on.

## Commands

`Mpris.PlayPause` / `Mpris.Play` / `Mpris.Pause` / `Mpris.Stop` / `Mpris.Next` /
`Mpris.Previous` — the transport controls.

`Mpris.SeekForward` / `Mpris.SeekBackward` — jump by a step that is editable per
assignment; an empty or zero step falls back to the one from the settings. `Mpris.SeekToPosition` jumps to an absolute
position in seconds.

`Mpris.VolumeUp` / `Mpris.VolumeDown` / `Mpris.SetVolume` — only for players that
expose a volume; the others log the skip instead of failing.

`Mpris.ToggleShuffle` / `Mpris.CycleRepeat` — shuffle and the repeat mode, again
only where the player supports them.

`Mpris.SelectPlayer` — opens a touch-screen folder listing the running players with
their state and current track. Pressing an entry makes that player the preferred one.

`Mpris.SetPreferredPlayer` — stores the player a binding resolves to as the
preferred one.

## Displays

`Mpris.NowPlaying` shows status, artist, title and position on one button:

```text
▶  Metallica
One
03:42 / 07:27
```

`Mpris.Artwork` draws the cover with the track and a progress bar. Single values are
available as `Mpris.PlayerName`, `Mpris.Status`, `Mpris.Title`, `Mpris.Artist`,
`Mpris.Album`, `Mpris.Position`, `Mpris.Duration`, `Mpris.Progress`, `Mpris.Volume`,
`Mpris.Shuffle`, `Mpris.Repeat` and `Mpris.PlayerAvailable`. Pressing any of them
toggles playback of the player it shows.

The position is not polled. While a player is playing it is extrapolated locally and
resynchronized when the status or the track changes, on `Seeked`, and at most every
ten seconds while a position is actually on screen.

## Rotary encoders

Two dial presets are offered in the encoder's context menu: **Media Volume** (turn to
change the volume, press to play/pause) and **Media Seek** (turn to seek, press to
play/pause). The same pairs are available per strategy in the command menu as
*Volume Control* and *Seek Control*.

## Settings

Preferred player, automatic selection, fallback when the selected player is
unavailable, showing browser players, the default seek and volume steps, and whether
cover art may be downloaded over http — on by default, because Spotify and the
browsers publish their covers that way. Local cover files are always used; artwork is
cached with a size limit and an LRU eviction, and a track without a usable cover falls
back to a neutral media symbol.

## Build

```bash
dotnet build -c Release
```

The output lands in `bin/Release`. Copy `LoupixDeck.Plugin.Mpris.dll`, `plugin.json`
and the `strings.<code>.json` files into `<LoupixDeck>/plugins/mpris/`, or run
`release.ps1`, which publishes and assembles that folder under `dist/mpris`.
