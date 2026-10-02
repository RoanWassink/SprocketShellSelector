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

**APHE** uses reduced-penetration AP ballistics. Normal AP penetration and spall continue until traversed armour reaches the fuse threshold, including accumulated thin plates within one penetration simulation. Defaults: 25 mm RHA threshold and 0.5 ms delay. The delay becomes a travel distance behind the plate and is shortened when an intervening surface is detected. The internal burst generates 96 fragments with spherical directions, 2.4 kg total fragment mass and 650 m/s speed. The intact shell stops; fragments continue through the damage simulation.

These are configurable gameplay approximations. New fuse behaviour and balance need in-game testing. Shell selection applies to cannons sharing a blueprint and overrides loaded AP/APHE rounds; ammunition storage, loading and costs remain vanilla.

## Armour simulator

Choose a shell profile in the simulator. Calibre remains the full gun calibre, including for APFSDS. The penetration slider extends to 2000 mm and represents manually selected penetration, rather than a prediction of cannon performance. The detailed simulation uses the selected profile's geometry and mass; the native colour overlay has a less detailed calibre approximation.

## Configuration

Edit these files under `BepInEx/config` with the game closed:

- `sprocket.shellselector.shells.json`: shell geometry, density, velocity and penetration constants.
- `sprocket.shellselector.spall.json`: spall balance, APHE fuse and burst settings.

Existing configuration is imported automatically when the new filenames are first created. Previous files are preserved; the new files then become authoritative. Custom burst settings are preserved. The previous untouched APHE burst defaults are upgraded to the stronger burst described above.

APHE settings: `apheFuseRhaMm`, `apheFuseDelayMilliseconds`, `apheFragmentCount` (1–256), `apheFragmentMass`, `apheFragmentSpeed`, and `apheFragmentK`. Native fragment generation runs in batches of up to 32. APFSDS settings: `coneMultiplier`, `coneEnergyWidening`, `thinRatio`, `thickRatio`, and `remainingPenetrationExponent`. Legacy thickness anchors are accepted but do not control the current balance.

## Build and testing

Install .NET SDK 8. Build with `dotnet build SprocketShellSelector.csproj -c Release -p:GameDir="YOUR_GAME_PATH"`. Run managed checks with `dotnet run --project tests/ShellSelector.Tests.csproj -c Release`. Game and interop DLLs are local references and are not redistributed. Managed checks do not replace gameplay testing.

## Rollback

Close the game and restore the previous shell DLL and its configuration from your backup.
