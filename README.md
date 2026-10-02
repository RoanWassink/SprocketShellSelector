# Sprocket Shell Selector

Standalone v0.1.0 beta, split from combined Material Selector v0.5.1. Vibe coded with AI assistance.

## Requirements and installation

Sprocket 0.2.55.5, Unity 6000.3.21f1, a working Sprocket Mod Loader / BepInEx 6 IL2CPP setup, and its .NET 6 runtime. Start the loader once to generate interop assemblies.

1. Download `SprocketShellSelector.dll` from the release/test package.
2. Close Sprocket before changing plugins.
3. If using combined Material Selector v0.5.x, restore Material Selector stable v0.4.0 first. Never load combined and standalone shell hooks together; this plugin refuses to patch when the known combined shell owner is present.
4. Copy only `SprocketShellSelector.dll` into `BepInEx/plugins`.
5. Start the game. Open the cannon inspector and select a shell profile.

On first start, existing `nl.roan.sprocket.materialselector.shells.json` is imported into `nl.roan.sprocket.shellselector.shells.json`. If absent, legacy numeric APFSDS CFG settings are read. Material files are never written. Existing standalone JSON is authoritative; invalid imports disable this plugin and preserve the source. Keep custom configs backed up and edit with the game closed.

## Behavior and limitations

Same APFSDS balance as combined v0.5.1; gun calibre remains adjustable. Existing `roanShellProfile` and `roanApfsdsBeta` vehicle fields are retained. This is a subcalibre native AP approximation, not long-rod physics. Selection applies to cannons sharing a blueprint and redirects loaded AP/APHE shots. Mixed ammunition, actual ammunition costs, new spall cones and thickness balancing are pending. Fragment damage multiplier scales health damage only.

74 managed checks pass and the plugin compiles against the installed interop. Standalone startup, existing vehicle firing, vanilla firing, save/load and clone gameplay have not yet been tested. See SHELL-TODO.md.

## Build

Install .NET SDK 8. Build with `dotnet build SprocketShellSelector.csproj -c Release -p:GameDir="YOUR_GAME_PATH"`. Run checks with `dotnet run --project tests/ShellSelector.Tests.csproj -c Release`. Game/interop DLLs are local references and are not redistributed.

## Rollback

Close the game, remove the standalone DLL, and restore your previous combined DLL and its backed-up configs. Keep standalone configs for later use. Never keep both shell implementations active.
