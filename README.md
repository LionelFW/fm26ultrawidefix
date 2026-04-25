# FM26 Ultrawide Fix

A BepInEx plugin that adds proper ultrawide support to Football Manager 2026. The game ships with no ultrawide support and locks itself to a 16:9 aspect ratio and leaves empty space on 21:9 and 32:9 displays.

This mod patches the game's UI scaling pipeline at runtime to fill the full width of your monitor without stretching or distorting anything.

There are still a few quirks with the way the mod scales UI elements, and is by no means perfect. Some main menu elements are a bit cropped, some elements feel offset... This mod has been tested on a UWQHD monitor, so feel free to mention any and all issues with other resolutions. 
## Requirements

- Football Manager 2026
- BepInEx 6 #738 at a minimum. This is mandatory, BepInEx LTS versions such as BepInEx 5 are not supported.

## Installation

### Via Thunderstore (recommended)

Install using the [Thunderstore Mod Manager](https://www.overwolf.com/app/Overwolf-Thunderstore_Mod_Manager) or [r2modman](https://thunderstore.io/package/ebkr/r2modman/). Search for **FM26 Ultrawide Fix** and install.

### Manual installation

1. Install BepInEx 6 Bleeding Edge for Football Manager 26. Follow the instructions on the [BepInEx Thunderstore page](https://thunderstore.io/c/football-manager-2026/p/BepInEx/BepInExPack_FootballManager26/).
2. Launch FM26 once with BepInEx installed and then close it.
3. Download `FM26UltrawideFix.dll` from the [Releases](../../releases) page.
4. Copy the DLL into `Football Manager 26/BepInEx/plugins/`.
5. Launch FM26. The mod will activate immediately.

The default install path for the game on Steam is:

Windows :  
```
C:\Program Files (x86)\Steam\steamapps\common\Football Manager 26\
```
Linux :
```
~/.local/share/Steam/steamapps/common/Football Manager 26/
```

## Building from source

You need the .NET 6 SDK and a copy of FM26 with BepInEx already set up (so the interop assemblies exist).

```bash
git clone https://github.com/your-username/fm26ultrawidefix
cd fm26ultrawidefix
dotnet build -c Release
```

The build will look for FM26 in the default Steam library path. If yours is elsewhere, override it:

```bash
dotnet build -c Release -p:GameDir="/path/to/Football Manager 26"
```

The output DLL will be at `bin/Release/net6.0/FM26UltrawideFix.dll`.

## Bugs and issues
Feel free to report bugs and issues in the Issues section here on github. You can also contact me through Discord (Wobs OR with userId :  103905189341167616).

## Contributing

Bug reports and pull requests are welcome. A few things worth knowing before diving in:

The UI system is not standard Unity. FM26 uses Unity UI Toolkit (`PanelSettings`, `VisualElement`, `UIDocument`), not uGUI/Canvas. Fixes that work for other Unity games via `CanvasScaler` don't apply here. If you're researching a layout issue, the `DiagnosticDump` config option will log the full `VisualElement` hierarchy on each scene load, which is the most useful starting point.

The game compiles to native code, so many things that work in Mono builds behave differently here. `resolvedStyle` properties (interface dispatch) can throw silently in IL2CPP; `ve.layout` (a plain Rect struct) is reliable. `RuntimeHelpers.GetHashCode` does not give stable object identity for IL2CPP proxy objects, use `ve.Pointer.ToInt64()` as a dictionary key instead.

The fix runs on a polling loop. FM26 resets various style properties on scene transitions. The mod re-applies its changes every 30 frames rather than relying on one-shot hooks that the game can undo. I have not profiled the performance impact, but it feels ok for now.

If you find a screen that still has dead space or incorrect layout, enabling `LogExpansions` and `LogSkipped` in the config and attaching the output to a bug report is the fastest way to help narrow it down.

This project is licensed under the MIT License.
