# Sprocket Shell Selector

Experimental shell profiles for Sprocket: APFSDS and APHE, selectable in the cannon inspector and armour penetration simulator. Vibe coded with AI assistance.

## Installation

Requires Sprocket 0.2.55.5 and a working Sprocket Mod Loader / BepInEx 6 IL2CPP installation with its .NET 6 runtime. Start the loader once to generate interop assemblies.

1. Close Sprocket.
2. Copy `SprocketShellSelector.dll` into `BepInEx/plugins`.
3. Start the game and select a shell profile in the cannon inspector or armour simulator.

Keep a backup of your previous DLL and configuration before installing an experimental update. Avoid installing multiple plugins that override the same shell behaviour.

## Shell behaviour

**APFSDS** uses a subcalibre penetrator calculated from the full gun calibre, rod length, density and ballistic constants. Spall increases as remaining native penetration decreases. The cone widens by up to 15% relative to its configured base width as remaining kinetic energy increases.

**APHE** uses reduced-penetration AP ballistics and the native AP spall simulation. By default it multiplies spall volume and fragment-count input by four. Fragment count remains subject to the native 32-fragment cap per burst. Direction, origin, speed, material handling and projectile continuation remain native AP. A visual-only native explosion effect is requested on live impacts that produce spall; it does not add blast damage. The armour simulator uses its normal trajectory visualization.

These are configurable gameplay approximations. The enhanced APHE behaviour and explosion visual need in-game testing. Shell selection applies to cannons sharing a blueprint and overrides loaded AP/APHE rounds; ammunition storage, loading and costs remain vanilla.

## Armour simulator

Choose a shell profile in the simulator. Calibre remains the full gun calibre, including for APFSDS. The penetration slider extends to 2000 mm and represents manually selected penetration, rather than a prediction of cannon performance. The detailed simulation uses the selected profile's geometry and mass; the native colour overlay has a less detailed calibre approximation.

## Configuration

Edit these files under `BepInEx/config` with the game closed:

- `sprocket.shellselector.shells.json`: shell geometry, density, velocity and penetration constants.
- `sprocket.shellselector.spall.json`: spall balance and APHE amplification settings.

Existing configuration is imported automatically when the new filenames are first created. Previous files are preserved; the new files then become authoritative. Custom burst settings are preserved.

APHE settings: `apheSpallMultiplier` (1–12, default 4) and `apheExplosionEffect` (default true). APFSDS settings: `coneMultiplier`, `coneEnergyWidening`, `thinRatio`, `thickRatio`, and `remainingPenetrationExponent`. Older fuse, spherical burst and thickness settings are accepted for configuration compatibility but do not control current behaviour.

## Build and testing

Install .NET SDK 8. Build with `dotnet build SprocketShellSelector.csproj -c Release -p:GameDir="YOUR_GAME_PATH"`. Run managed checks with `dotnet run --project tests/ShellSelector.Tests.csproj -c Release`. Game and interop DLLs are local references and are not redistributed. Managed checks do not replace gameplay testing.

## Rollback

Close the game and restore the previous shell DLL and its configuration from your backup.

