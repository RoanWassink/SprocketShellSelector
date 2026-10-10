# Sprocket Shell Selector

Custom ammunition and guided missiles for Sprocket: **APFSDS long/short rod, APHE, HE, HEAT, HESH, SACLOS and MCLOS ATGM**, plus a dedicated launcher and finite ammunition box.

**[Download v0.13.2](https://github.com/RoanWassink/SprocketShellSelector/releases/tag/v0.13.2)**. Download the installation ZIP from the release; GitHub's **Code > Download ZIP** contains source code.

## New in v0.13.2

- **Stronger full-calibre recoil:** HE, APHE and other non-rocket full-bore profiles now retain at least the native recoil of the same cannon and propellant charge. Penetration and damage balance are unchanged. APFSDS and ATGM recoil remain unchanged.
- **Cleaner damage feed:** crew and component messages report direct projectile and spall damage. Repeated messages from ongoing fires are excluded, and repeated hits on the same component within one shot are combined. Penetration and ERA activation remain visible.

## Editors, missiles and armour integration

- Design shell modules in the shared in-game JSON editor; Save refreshes valid compiled profiles. Existing custom profiles are preserved.
- Optional TOW wire-guidance example with cable gravity and ground settling.
- Optional damage feed for penetration, direct shot damage and ERA activation.
- Placed ERA compatibility with matching Material Selector parts and response catalogues.

See [the in-game editor and damage-feed guide](SHELL-EDITOR.md) for module choices, Save/Import, files and recovery.

## Requirements

- Included **Sprocket JSON Editor 0.1.0** is required.
- Sprocket **0.2.55.5**, Unity **6000.3.21f1**.
- A working Sprocket Mod Loader / **BepInEx 6 IL2CPP** setup with its .NET 6 runtime, as used by Hans21223's Sprocket Quality of Life.
- Quality of Life and Material Selector are optional. Armour response recipes require the matching Material Selector definitions and `sprocket.armour.responses.json` catalogue.

The release contains this mod's files, not the mod loader or game assemblies.

## Installation and updates

1. Start the game once with the mod loader, then close it.
2. Extract the installation ZIP into the folder containing `Sprocket.exe`, merging its folders. Keep one `BepInEx/plugins/SprocketShellSelector.dll`.
3. Keep the included icons, Parts, Localization and nine Technology files in their supplied locations. The DLL alone does not install the launcher or shell technology.
4. Start the game, select a cannon and open **Shell profile > Shell type**.

Back up vehicle saves before updating. Preserve your existing configuration and customized Technology files; skip replacing a Technology file you have edited. The ZIP contains no installed player configuration. Default shell examples are separate under `examples/`; they are optional and should not replace custom profiles.

The plugin now uses `sprocket.shellselector.cfg`. When that file is absent, an existing `nl.roan.sprocket.shellselector.cfg` is copied intact before settings are read; the original remains for rollback. If both exist, the new file wins. Shell profile IDs, part GUIDs and saved selections are unchanged. Existing shell/spall JSON migrations remain supported and preserve customized values.

## Shell profiles

| Type | Use |
|---|---|
| APFSDS long rod | Higher rod penetration efficiency and narrower spall. |
| APFSDS short rod | Lighter, less penetration efficiency and wider spall. |
| APHE | Less penetration than AP; a broad, heavy fragment burst after perforation. |
| HE | Native blast damage, with a larger visual explosion than APHE. |
| HEAT | High calibre-scaled penetration and concentrated forward fragments. |
| HESH | Broad, heavy spall against simpler armour; more vulnerable to separated layers. |
| SACLOS ATGM | Steer with the sight or third-person reticle; HEAT impact. |
| Gun-launched SACLOS ATGM | Cannon-dependent launch speed with the same guidance and warhead. |
| MCLOS ATGM | Keyboard steering; vehicle controls blocked while guiding. |

Profiles scale with full cannon calibre. APFSDS produces less spall when substantial penetration remains and more as its penetration budget is used. Rod length also affects mass, speed, penetration quality and cone width. Non-ATGM shells retain native projectile paths.

APHE/HESH produce an amplified payload burst after perforation. HEAT can generate concentrated spall when its original penetrator perforates further plates. HEAT and HESH use separate air-gap loss curves; spacing reduces current remaining penetration without restoring previously lost capacity. HESH still requires perforation: nonperforating backface scabbing is not implemented.

These are gameplay approximations. Explosion visual size and native damage are separate settings. Native HE power is not a calibrated kg TNT value. Ordinary cannon ammunition storage and loading remain native.

## Dedicated launcher and ammunition box

Place **ATGM launcher** from the native part picker, assign a gunner and select a launcher ATGM profile. Put its muzzle outside armour. Its square tube uses native mounting, selection, sight association and projectile ejection. It accepts launcher missiles (`behavior: atgm`), not ordinary shells or gun-launched missiles (`atgm_gun`).

Each dedicated launcher starts with **one finite ready missile per new combat instance**, enabled by `InitialReadyMissile`. Firing consumes it. It is not an unlimited reserve, and rebuilding the same combat instance does not grant another round. Native gunner, functioning and firing-readiness checks still apply.

After that shot, reload from compatible native ammunition with a crew loader, or use the optional **ATGM ammo box** automatic feed:

1. With the game closed, set `AutomaticAmmoBox = true` under `[ATGM Launcher Tests]` in `sprocket.shellselector.cfg`, then restart. It is disabled by default.
2. Place the dedicated ammo box within **2 metres of the native loading points** of a matching dedicated launcher on the same vehicle.
3. In the box editor, enable **Automatic feed** and choose its **Ammunition reference**, or use the nearest-launcher option. Match calibre, propellant and profile.
4. Set native box size and fill. Capacity, ammunition mass, damage and consumption remain finite. Nearby matching launchers share that stock; ordinary racks do not refill it automatically.

Automatic loading needs a gunner but no crew loader and adds mechanism mass/cost. Ordinary cannons can use compatible box ammunition through native manual loading, but are not automatic-box references.

For dedicated launcher optical initialization, `OpticalSightDirection = true` enables the optional native sight-direction adjustment; restart after changing it. Existing values are preserved. The legacy section name `[ATGM Launcher Tests]` remains for configuration compatibility.

## Guidance, audio and exhaust

- **SACLOS:** keep controlling the launching vehicle and aim the sight or reticle at the target.
- **MCLOS:** select your view before firing. Use **W/S** for up/down and **A/D** for left/right. Vehicle driving, aiming, firing and scope-toggle commands are blocked during manual guidance and restored when guidance ends. The vehicle can coast.
- AI uses native target acquisition and estimated target motion with missile lead. **Only the newest missile per vehicle receives guidance**, even with multiple launchers.
- Motor delay, launch speed, acceleration, burn time, coast loss and turn rate are configurable per profile. The examples are gameplay presets, not measured missile flight models.
- Launcher and gun-launched missiles have separate embedded launch recordings and optional WAV overrides. Non-ATGM shots retain native cannon audio.
- ATGM profiles have motor-linked exhaust flame, light and smoke, starting at motor ignition. Other shell types retain their native visuals.

An optional wire guidance example includes a visible cable with gravity and terrain settling. No onboard seeker, target lock, fire-and-forget, top attack, missile camera or tandem warhead is implemented. Missile impact uses the HEAT model; its explosion effect adds no separate HE blast damage.

See [ATGM controls and settings](ATGM.md).

## Native technology and dates

Availability uses the vehicle's native technology frame and `shellSelector_<behavior>` records. Dates are editable data, rather than fixed era-name requirements in the DLL. Missing, unavailable or disabled technology leaves the saved profile selection intact and uses native ammunition.

| Behavior | Included first availability |
|---|---|
| AP / HE | 1914.07.28 |
| APHE / HEAT | 1939.09.02 |
| APFSDS / HESH / ATGM / gun-launched ATGM | 1945.09.03 |

Change the corresponding Technology JSON date, add a later record of the same type, or set `properties.enabled` to false; restart to reload data. Changing an era boundary does not automatically change invention dates. Dedicated parts also retain their native availability dates.

Unchanged stock HEAT follows its active technology's `penetrationPerCalibre`: **1.2** in the initial example and **4** in the later example. Custom or edited chemical profiles keep their own configured budget. Values must be finite, greater than zero and at most 20; final penetration remains capped at 2000 mm.

## Armour simulator

Choose **Simulator shell profile** independently of your live cannon selection. Calibre is full gun calibre, including for APFSDS. The penetration slider reaches **2000 mm** and is manually selected. HEAT/HESH/ATGM use the lower of that slider and their calibre-scaled chemical budget.

The native colour overlay is a simpler approximation. HE blast and explosion visuals require live firing. The simulator tests ATGM impact, not powered flight or guidance. The shell list supports scrolling.

## Custom shells and configuration

See [Making your own shells](CUSTOM-SHELLS.md). Give each copy a unique ID and label, and retain its `behavior` to inherit that shell's mechanics. **Any unique ID with `"behavior": "apfsds"` receives the full rod logic.** Long/short rods are examples you can copy or edit.

Edit with the game closed and restart:

| File under `BepInEx/config` | Purpose |
|---|---|
| `sprocket.shellselector.shells.json` | Profiles, ballistics, payload and missile motor settings. |
| `sprocket.shellselector.spall.json` | Global APFSDS/APHE spall and APHE visual tuning. |
| `sprocket.shellselector.cfg` | Optional modules, launcher settings, audio gain and diagnostic logging. |
| `sprocket.shellselector.audio/atgm.wav` / `atgm_gun.wav` | Optional PCM16 mono/stereo audio overrides. |
| `sprocket.armour.responses.json` | Optional shared material response catalogue. |

Detailed ATGM flight logging defaults to false (`[ATGM Experimental] DiagnosticLogging`). Audio replacement defaults to true; `[ATGM Audio] VolumeMultiplier` ranges from 0 to 2. Section names are retained to preserve existing settings.

## FAQ

### Can I copy APFSDS for different rods?

Yes. Use a unique ID/label and `"behavior": "apfsds"`. Changing only the name does not change performance. Geometry, ballistic quality and behavior settings do.

### What does penetratorLengthInCalibres change?

Calculated penetrator length relative to full calibre: 120 mm with 5 gives 600 mm. It affects mass and speed; APFSDS also uses length-dependent quality and cone modifiers. It does not change the cannon's propellant setting or visible shell mesh. Longer does not guarantee better penetration after speed/mass limits.

### Do custom gun technology files affect ammunition?

Yes: calibre, baseline muzzle speed and native penetrator constants feed ballistic calculations. Chemical penetration uses the profile's calibre-scaled budget, except unchanged stock HEAT's technology progression. The simulator slider is independent of live cannon output.

### Why is a shell unavailable or missing?

Check the installed Technology files and the vehicle's design date. Select the shell on the live cannon as well as in the simulator. A copied profile needs a unique valid ID/label and an available behavior technology.

## Troubleshooting and rollback

Check `BepInEx/LogOutput.log` for Shell Selector messages. Include game/loader version, profile, log lines and reproduction steps. For simulator issues include calibre, penetration and a screenshot. Validation errors identify the profile, field, received value and permitted range; invalid custom configurations are preserved.

To roll back, close the game and restore the matching DLL, owned parts/technology/assets and configuration backup. Keep unrelated plugins and saves intact. Plugins that declare a dependency on Shell Selector must use its new ID, **`sprocket.shellselector`**.

## Build, credits and licence

Build with .NET SDK 8 after the working loader has generated `BepInEx/interop`:

```powershell
dotnet build -c Release -p:GameDir="C:\Program Files (x86)\Steam\steamapps\common\Sprocket"
dotnet run --project tests/ShellSelector.Tests.csproj -c Release
```

Created with AI assistance. Native inspector integration follows the pattern used by Hans21223's Sprocket Quality of Life. [MIT](LICENSE) covers the plugin code; supplied audio is documented separately in [audio provenance](audio/README.md).

[Support development](https://www.paypal.com/donate/?hosted_button_id=7PE3SDBETXFQ6) to help with the ChatGPT budget and reverse engineering.
