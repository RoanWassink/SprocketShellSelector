# Sprocket Shell Selector

A vibe-coded BepInEx IL2CPP plugin that adds **APFSDS and APHE shell profiles** to Sprocket's cannon inspector and armour penetration simulator.

**Built with AI assistance.** Version **0.8.1** has been tested in-game, with 137 managed regression checks passing.

## Requirements

- Sprocket **0.2.55.5** (Unity **6000.3.21f1**).
- A working **Sprocket Mod Loader / BepInEx 6 IL2CPP** setup with its .NET 6 runtime—the same environment used by Hans21223's *Sprocket Quality of Life*.
- Quality of Life and Material Selector are optional.

## Installation

1. Run the game once with the mod loader installed, then close it.
2. Download **SprocketShellSelector.dll** from this repository's [Releases](https://github.com/RoanWassink/SprocketShellSelector/releases) section.
3. Drop the DLL into:

   ```text
   Sprocket\BepInEx\plugins\
   ```

4. Launch the game and choose a shell profile in the cannon inspector or armour simulator.

No compiling needed. Back up your vehicle saves and existing configuration before updating. Install only one copy of the shell plugin.

## Shell profiles

**APFSDS** calculates a subcalibre rod from the full gun calibre, rod length, density and ballistic constants. Extra penetration comes with a narrower spall cone. Spall increases as remaining penetration decreases, so a shot with abundant penetration left produces less spall. Cone width can increase by up to 15% with remaining kinetic energy. Classical AP normalization can be disabled for APFSDS; projectile trajectory and ricochet handling otherwise remain native.

**APHE** combines reduced-penetration AP ballistics with much stronger native spall. The default is a **180-degree forward cone**, with four times the spall volume and fragment-count input. The native limit of 32 fragments per burst still applies. Successful live impacts that produce spall request a configurable native explosion visual. Damage comes from spall; the visual adds no blast damage and does not use the native HE tooltip's kg value.

These are gameplay approximations. Shell selection applies to cannons sharing a blueprint and overrides loaded AP/APHE rounds. Ammunition storage, loading and costs remain vanilla.

## Armour simulator

Choose a profile under **Simulator shell profile**. Calibre always means the full gun calibre, including for APFSDS. The penetration slider extends to **2000 mm** and represents the penetration you select manually, rather than a prediction of your cannon's performance.

The detailed simulation uses the selected profile's geometry and mass. The native colour overlay uses a simpler calibre approximation. Live explosion effects are not shown in the simulator.

## Custom shells

See [Making your own shell profiles](CUSTOM-SHELLS.md) for a valid JSON example, supported ranges, how geometry affects mass and speed, and the v0.8.1 limits on custom APFSDS/APHE behaviour.

## Configuration

The plugin creates these files in `Sprocket\BepInEx\config`. Edit them with the game closed, then restart:

- `sprocket.shellselector.shells.json`: shell geometry, density, velocity and penetration constants.
- `sprocket.shellselector.spall.json`: spall balance, cone width and APHE explosion visuals.

| Setting | Default | Effect |
|---|---|---|
| `apheSpallMultiplier` | 4 | Multiplies native APHE spall volume/count inputs |
| `apheConeHalfAngleDegrees` | 90 | Half-angle; 90 produces a 180-degree forward cone |
| `apheExplosionEffect` | true | Enables the live explosion visual |
| `apheExplosionScale` | 0.65 | Scales native explosion asset size limits |
| `coneMultiplier` | 0.3 | Base APFSDS spall spread multiplier |
| `coneEnergyWidening` | 0.15 | Maximum relative cone widening with remaining energy |
| `thinRatio` | 0.08 | APFSDS spall ratio with abundant penetration remaining |
| `thickRatio` | 1.25 | APFSDS spall ratio as penetration is exhausted |
| `remainingPenetrationExponent` | 1 | Shapes the transition between those ratios |
| `apfsdsDisableClassicNormalization` | true | Disables classical AP normalization for APFSDS |

Existing shell configuration is imported when the current filenames are first created. Older fuse, spherical-burst, thickness-anchor and experimental deflection keys remain accepted for compatibility but do not control current behaviour.

## Troubleshooting

Check `Sprocket\BepInEx\LogOutput.log` for messages containing `Sprocket Shell Selector`, `[Armour Simulator]` or `[APHE Effect]`.

When reporting an issue, include your game/mod-loader version, selected shell profile, what you did and relevant log lines. For simulator issues, include calibre, penetration and a screenshot of the trajectory.

To roll back, close the game and restore your previous shell DLL and configuration backup.

## Building from source

For contributors: install **.NET SDK 8** and start the game once with a working mod loader so `BepInEx\interop` exists.

```powershell
dotnet build -c Release -p:GameDir="C:\Program Files (x86)\Steam\steamapps\common\Sprocket"
```

The output is `bin\Release\net6.0\SprocketShellSelector.dll`. Game and loader assemblies are referenced locally and are not included here.

Run the independent regression checks with:

```powershell
dotnet run --project tests/ShellSelector.Tests.csproj -c Release
```

## Credits

Created by RoanWassink with AI assistance. The native inspector integration follows the pattern used by Hans21223's *Sprocket Quality of Life*.

## License

[MIT](LICENSE). The license applies to this plugin's code; game and loader assemblies are not distributed with it.
