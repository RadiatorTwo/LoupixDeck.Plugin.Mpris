# Plugin icon generator

`make_icon.py` draws the plugin icon: a progress bar with a matte thumb above a matte transport
bar with previous, play and next, on a deep violet tile. It shares the shading and accent of the
other plugin icons; the violet tile keeps it apart from them. The icon is original artwork, no
third-party source; no player's logo is used, since the plugin controls any MPRIS player.

```bash
pip install pillow numpy
python tools/icon/make_icon.py tools/icon/out
cp tools/icon/out/icon_256.png icon.png
```

`BG_HUE` at the top of the script sets the background tint; `PROGRESS` sets the playback position.

The script writes `icon_{256,128,64,32,16}.png` into the given folder; only the 256 px file is
used, as `icon.png` in the repo root. The `out/` folder is not committed.
