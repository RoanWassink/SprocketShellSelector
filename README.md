# Sprocket ShellSelector

APFSDS, APHE, HE, HEAT, HESH and guided missiles, with configurable profiles and armour interactions.

**v0.12.3-heavyera.1 — beta.** Profiles now respect era availability. Unmodified stock WWII HEAT is recalibrated; renamed or customized profiles retain their values. Adds threat-specific composite/NERA/ERA responses, including shared one-use heavy ERA cells against HEAT and APFSDS.

## Requirements

- Sprocket **0.2.55.5**, Windows x64, Unity 6000.3.21f1.
- A working **Sprocket Mod Loader / BepInEx 6 IL2CPP (6.0.0-be.788)** setup with its runtime and generated interop. Loader installation is separate. Stock BepInEx alone is not claimed equivalent to the tested Sprocket-specific setup.
- Quality of Life is not required or included. Other game versions have not been verified.

## Install and update

1. Install a working Sprocket Mod Loader / BepInEx 6 IL2CPP setup, run Sprocket once, then close it. The loader is a separate prerequisite and is not included.
2. Download **SprocketShellSelector-v0.12.3-heavyera.1.zip** from [this release](https://github.com/RoanWassink/SprocketShellSelector/releases/tag/v0.12.3-heavyera.1).
3. In Steam, use Sprocket > Manage > Browse local files. Copy the ZIP's folders into the folder containing Sprocket.exe. Merge folders; keep the internal structure intact.
4. Keep one copy of each plugin. Back up matching mod files and vehicle saves before updating. Never replace the whole BepInEx folder.
5. Preserve existing BepInEx/config files, customized thermal-models.json and sound overrides. Install required dependencies separately. Restart the game.

## Usage, controls and settings

On a cannon, use Shell profile > Shell type. APFSDS and guided ATGM profiles are Cold War-only; earlier eras retain their supported types. Unmodified stock WWII HEAT uses 1.20 times calibre as its nominal gameplay chemical budget; Cold War uses 4 times calibre. Custom/renamed records retain their own settings. These budgets are not guaranteed penetration against every target. [Custom shells](CUSTOM-SHELLS.md), [guided missiles](ATGM.md), and [armour responses](ARMOUR-RESPONSES.md) explain usage and limitations. Existing shell/spall JSON and WAV overrides must be preserved. No Keybinds API is required for this module.

## Troubleshooting, saves and rollback

If the mod is absent, check BepInEx/LogOutput.log for the mod name, missing dependencies, duplicate plugin versions or invalid configuration. When this mod requires Keybinds, missing/incompatible Keybinds causes the mod to be skipped; old direct-key CFG entries do not replace that requirement. Preserve a malformed file for inspection instead of overwriting all your settings. Restart after repairs.

Restore your backed-up mod files and settings together for rollback. Do not delete an entire shared folder. Custom parts/materials may be referenced by vehicle saves: return affected vehicles to stock parts/materials and save before uninstalling. Keep save backups; installed mods and release archives do not back up every vehicle automatically.

## Credits and support

Made with AI assistance. Mod code is MIT licensed; native Sprocket meshes/icons are resolved from your installed game and are not bundled. Donation: [Support my ChatGPT budget and help me reverse engineer Sprocket to make more mods](https://www.paypal.com/donate/?hosted_button_id=7PE3SDBETXFQ6).

## Where to get the separate loader

Use [Hans21223's Sprocket Mod Loader](https://github.com/Hans21223/Sprocket-Mod-Loader) and follow its [manual installation guide](https://github.com/Hans21223/Sprocket-Mod-Loader/blob/main/package/MANUAL-INSTALL.md) or its documented manager installation. That upstream project targets the tested Sprocket version and supplies the Sprocket-specific patch. These mod downloads do not install the loader. Follow one upstream loader method and its update/backup instructions; the creator's supplied ModManager archive is not redistributed here.
