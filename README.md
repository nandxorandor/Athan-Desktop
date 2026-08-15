<h1 align="center">Athan for Windows</h1>

<p align="center">
  <b>Offline prayer times and the athan, on your PC.</b><br>
  One file. No installer, no account, no ads, no tracking, no network calls.
</p>

<p align="center">
  <a href="../../releases/latest"><img src="https://img.shields.io/badge/download-Athan.exe-2FD968?style=flat-square" alt="Download"></a>
  <img src="https://img.shields.io/badge/Windows-10%2F11%20(64--bit)-4FC79A?style=flat-square" alt="Windows 10/11 64-bit">
  <img src="https://img.shields.io/badge/code-MIT-4FC79A?style=flat-square" alt="MIT licence for the code">
</p>

---

The desktop companion to [Athan for Android](https://github.com/nandxorandor/Athan).
Same prayer-time library, same 29 bundled recordings, same credits — so the two
agree to the minute.

## What it does

- **Prayer times calculated offline**, with the [adhan](https://github.com/batoulapps/adhan-java)
  library. Calculation method and madhab are selectable, plus a ±120-minute
  manual adjustment when your local timetable differs.
- **Calls the athan at the right moment**, with a window you cannot miss and one
  Stop button.
- **Three modes per prayer**, clicked on the row itself:
  **Sound** (window and recording) · **Popup** (window only) · **Silent** (nothing).
- **Runs in the notification area.** Closing the window does not quit it; it
  keeps waiting for the next prayer. Optionally starts when you sign in.
- **Qibla bearing** from your coordinates, with the distance to Mecca.
- **Hijri date**, from the Umm al-Qura calendar Windows already ships.

## Download

Grab `Athan.exe` from the [Releases](../../releases) page and run it. There is
nothing to install and nothing to unzip — the .NET runtime and all 29 recordings
are inside the one file.

Windows SmartScreen will warn you the first time, because the file is not signed
with a paid code-signing certificate. **More info → Run anyway.** That warning
means Windows has not seen this file before, not that it found anything in it.

Requires **Windows 10 (build 19041) or newer, 64-bit**. About 105 MB.

## Setting your location

Three ways, in the order most people will want them:

1. **Detect my location** — asks Windows where the PC is. Works anywhere in the
   world. Needs Location turned on in *Settings → Privacy & security → Location*,
   including for desktop apps.
2. **Pick a city** from the bundled list.
3. **Type coordinates** directly, for anywhere the list does not reach.

Whichever you use, the coordinates are stored only in your own Windows profile
and never leave the machine.

## Where your settings live

`%APPDATA%\Athan\settings.json` — deliberately not next to the exe, so you can
move `Athan.exe` anywhere without losing your location or sound choices.

If you enable "start when I sign in", that is an ordinary `Run` entry under
`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, visible and removable in
Task Manager's Startup tab. It stores the exe's path, so run Athan once from its
new home if you move it.

## About the recordings

29 athans are bundled. Three are the author's own; the rest were published by
**IslamWeb**, which permits its material to be used for non-commercial purposes
as long as the source is named ([fatwa no. 379009](https://www.islamweb.net/en/fatwa/379009/)).
This app is free, carries no ads and sells nothing. Every recording is credited
to its reciter and source under **Settings → Resources & credits**, generated
from the shipped audio index so it cannot drift out of step with what is in the
file.

The Mecca and Madina recordings are deliberately **not** included: those are
Saudi state broadcasts, which IslamWeb hosts but does not own, so its permission
does not extend to them.

## Privacy

No analytics, no crash reporter, no ad network, no network requests at all.
Prayer times and the qibla are astronomy, computed on your machine.

## Building from source

```powershell
cd AthanDesktop
dotnet publish -c Release -r win-x64 --self-contained true -o ..\dist
```

Needs the **.NET 9 SDK**. The result is a single `dist\Athan.exe`.

The recordings under `AthanDesktop\Assets\athan\` are copied from the Android
app's assets, index and all, so both platforms ship the same catalogue.

## Differences from the Android app

| | Android | Windows |
|---|---|---|
| Qibla | live compass | bearing in degrees (a PC has no magnetometer) |
| Vibrate mode | yes | replaced by **Popup** — window, no sound |
| Pre-prayer heads-up | yes | not yet |
| Downloaded athans | browse and import | pick any MP3 on the PC |

## Licence

**Source code:** [MIT](LICENSE).

**Audio** — not covered by the MIT licence. The three `Developer_Athan-*`
recordings are the author's, under CC BY 4.0. Everything else remains the
property of its reciters and of IslamWeb, is included under their
non-commercial-with-attribution terms, and is **not** relicensed here. If you
fork this repo, either honour those terms or delete the files.

## Contact

Ahmed Khalaf — ahmedkhalaf1@yahoo.com · [github.com/nandxorandor](https://github.com/nandxorandor)
