# Sprocket Shell Selector

A vibe-coded BepInEx IL2CPP plugin that adds **APFSDS long/short rod, APHE, HE, HEAT, HESH and guided ATGM** profiles to Sprocket.

Built with AI assistance. **v0.10.0** adds sight-guided and keyboard-guided ATGM profiles with JSON-configurable launch speed and motor acceleration. Both guidance modes were accepted in user gameplay testing; the new motor settings were also accepted in user gameplay testing. Existing shell mechanics remain unchanged.

## Requirements

- Sprocket **0.2.55.5** (Unity **6000.3.21f1**).
- A working **Sprocket Mod Loader / BepInEx 6 IL2CPP** setup with its .NET 6 runtimeâ€”the same environment used by Hans21223's *Sprocket Quality of Life*.
- Quality of Life and Material Selector are optional.

## Installation

1. Run the game once with the mod loader installed, then close it.
2. Download **SprocketShellSelector.dll** from [Releases](https://github.com/RoanWassink/SprocketShellSelector/releases/latest).
3. Put it in `Sprocket\BepInEx\plugins\`, replacing the previous shell DLL.
4. Launch the game and choose **Shell profile â†’ Shell type** on your cannon.

No compiling needed. Keep only one copy of this plugin. Back up your vehicle saves before experimenting. The DLL creates the default profiles itself; copying example JSON is optional.

Updating adds missing release presets while preserving existing profiles, with a `.pre-v094-backup` before configuration changes. The old stock `APFSDS (beta)` / `APFSDS standard` entry is removed; saved selections using that ID fall back to long rod. A renamed custom entry is retained. Existing profiles with a release preset ID keep their values; at the 16-profile limit, free slots to add missing examples. Invalid JSON is reported rather than overwritten.

## Shell profiles

| Type | Role |
|---|---|
| APFSDS long rod | Higher rod penetration efficiency, narrower spall cone. |
| APFSDS short rod | Lighter rod, lower penetration efficiency, wider cone. |
| APHE | Reduced AP penetration with a very heavy, broad internal fragment burst. |
| HE | Native explosion damage; larger visual explosion than APHE. |
| HEAT | High first-plate penetration and powerful fragments in a narrow cone. |
| HESH | Heavy, broad spall against simpler armour; less first-plate penetration than HEAT. |
| SACLOS ATGM | Sight-guided gameplay proxy, chemical HEAT impact; nominal 600 mm at 135 mm calibre. |
| MCLOS ATGM | Manually steer with WASD; tank controls blocked during the controllable missile flight. |

These are **gameplay approximations**, not physical shaped-charge, erosion or backface-scabbing simulations. All profiles scale with full gun calibre. APFSDS spall increases as remaining penetration decreases; its cone can widen by up to 15% with remaining energy. Long/short rod also differ in calculated mass/speed and a length-based efficiency/cone modifier. Native projectile paths remain in use for non-ATGM shells; ATGM flight is steered before native movement and collision processing.

APHE and HESH produce one amplified payload burst after native perforation. HEAT can produce concentrated spall at subsequent plates when its original jet perforates them; secondary fragments cannot trigger repeated amplified payload bursts. Fragment mass scales with calibre cubed; count scales with calibre and is bounded to 32 per burst. Payload fragment speed does not collapse when the shell barely penetrates. HESH still needs perforation to generate spall. APHE attempts to stop the parent after its burst.

**Spaced armour:** HEAT and HESH use independent, calibre-scaled loss curves over actual air gaps. Native plate consumption happens first; each measured gap multiplies CURRENT remaining penetration, never restoring lost capacity. HEAT loss increases gradually with spacing and preceding plate LOS RHA thickness; a tiny gap (up to .1 gun calibre) adds no loss. HESH has a much steeper decoupling curve across a separated layer. Secondary fragments retain native penetration. The older fixed second-plate cap has been replaced.

![HEAT air-gap curve](docs/heat-air-gap.png)

![HESH air-gap curve](docs/hesh-air-gap.png)

These are **calculated model curves**, not measured penetration data. See [curve equations and validation](docs/SPACED-ARMOUR.md). HESH is still a perforating proxy; true nonperforating backface scabbing is not implemented. Native blast and shaped-charge stand-off optimisation are not modeled by these curves.

HE native blast power scales with calibre cubed, with an upper limit. It has no validated armour-thickness gate. Explosion visuals scale separately with calibre; default HE is larger than APHE, and HESH matches APHE's visual scale. Native HE power is **not a calibrated kg TNT value**.

Shell selection applies to cannons sharing a blueprint and overrides loaded AP/APHE rounds. Ammunition storage, loading and costs remain vanilla.

## Guided ATGMs

Select the missile profile **on the cannon** and test in live play. Both examples are available at any calibre: chemical penetration scales from 600 mm at 135 mm; flight speed is configured separately. They reuse HEAT impact and spaced-armour mechanics.

- **SACLOS ATGM:** steer by moving the scope or third-person reticle. Keep controlling the launching vehicle. Reloading does not disable guidance.
- **MCLOS ATGM:** enter your preferred view before firing, then use **W/S** for up/down and **A/D** for left/right. Mouse aim does not steer it. Driving, aiming and firing commands are blocked during the controllable missile flight; controls return after impact, expiry or loss of the launcher. The tank can coast. General camera/UI processing remains available, but vehicle actions such as scope toggling are blocked.
- Only the newest missile per vehicle receives commands. Switching vehicles stops new commands; the missile continues on its last heading. No input-action maps are permanently disabled.
- Fresh examples launch at **50 m/s**, accelerate at **150 m/s²** after **0.15 s**, and cap at **200 m/s**. These are editable gameplay defaults, not measured Konkurs launch/motor data.

See [ATGM settings and testing](ATGM.md) and [custom shell profiles](CUSTOM-SHELLS.md). There is no target lock, fire-and-forget seeker, top attack, missile camera, new launcher mesh, smoke trail or tandem warhead. ATGM explosions are visual HEAT effects, not additional independent HE blast damage.

Existing ATGM configs keep their values. Migration exposes missing motor fields with the previous constant-speed behavior (`launchSpeed = flightSpeed`, `acceleration = 0`); it does not overwrite customized profiles. To use the new soft-launch example on an existing profile, set the motor fields yourself. Original test profile IDs remain stable for vehicle saves; stock experimental labels are updated to HE/HEAT/HESH and SACLOS/MCLOS ATGM.

## Armour simulator

Choose a profile under **Simulator shell profile**. This does not change the live shell selected on your cannon.

Calibre means full gun calibre, including for APFSDS. The penetration slider extends to **2000mm** and is manually selected, not a prediction of your cannon's performance. HEAT/HESH/ATGM use the lower of the slider and their calibre-scaled chemical penetration budget. The native colour overlay is a simpler approximation of the detailed simulation.

HE is live-firing only and omitted from the simulator list. Explosion visuals are live-only. The simulator tests ATGM impact, not powered flight or guidance.

## Custom shells

See [Making your own shell profiles](CUSTOM-SHELLS.md). **Any unique ID can now use `"behavior": "apfsds"`** and receive the full rod behavior. The same applies to APHE, HE, HEAT, HESH and ATGM. Copy the long/short rod examples to create your own rods.

## Configuration

Edit with the game closed, then restart:

- `Sprocket\BepInEx\config\sprocket.shellselector.shells.json`: profiles, ballistics and payload settings.
- `Sprocket\BepInEx\config\sprocket.shellselector.spall.json`: global APFSDS/APHE spall and APHE visuals.
- `Sprocket\BepInEx\config\nl.roan.sprocket.shellselector.cfg`: optional ATGM module (`ATGM Experimental / Enabled`) and detailed flight logs (`DiagnosticLogging`, default false).

| Global setting | Default | Effect |
|---|---|---|
| `apheSpallMultiplier` | 4 | APHE payload volume/count tuning relative to the release baseline |
| `apheConeHalfAngleDegrees` | 90 | 180-degree full forward cone |
| `apheExplosionEffect` | true | Live explosion visual enabled |
| `apheExplosionScale` | 0.65 | APHE visual asset scale |
| `coneMultiplier` | 0.3 | Base APFSDS native spread multiplier |
| `coneEnergyWidening` | 0.15 | Maximum extra APFSDS spread with remaining energy |
| `thinRatio` | 0.08 | APFSDS spall ratio with abundant penetration left |
| `thickRatio` | 1.25 | APFSDS ratio as penetration is exhausted |
| `remainingPenetrationExponent` | 1 | Shapes that transition |
| `apfsdsDisableClassicNormalization` | true | Disables classical AP normalization for rods |

Older spherical-burst, fuse and custom-deflection settings remain accepted for compatibility but do not control current behavior.

## FAQ

### Can I copy APFSDS to make more rods?

Yes. Copy either rod, use a unique ID/label, and keep `"behavior": "apfsds"`. Unlike v0.8.2, the ID no longer determines the special behavior. Changing only the name does not change performance. Geometry, ballistic and behavior settings do.

### What does penetratorLengthInCalibres do?

It sets calculated rod length relative to full gun calibre. A 120mm gun with 5 gives a 600mm rod. Length changes mass and speed; APFSDS also uses `sqrt(length/5)` as a penetration-quality modifier and a length-dependent cone modifier. It does not change the cannon's propellant setting or visible ammunition model. Longer does not guarantee better penetration after all speed/mass limits.

### Do custom gun technology files affect ammunition?

Yes, if they change calibre, baseline muzzle speed or native penetrator constant. These feed the profile calculation. Chemical penetration is instead defined by the profile's calibre-scaled budget. The simulator slider is independent of live cannon output.

### Why is HESH missing in live firing?

Select it on the cannon as well as in the simulator. Those selections are independent. Check `[Shell Selection]` and firing messages in the log.

## Troubleshooting

Check `Sprocket\BepInEx\LogOutput.log` for `Sprocket Shell Selector`, `[Shell Selection]`, `[Payload burst]`, `[Chemical layers]`, `[Armour Simulator]` or `[APHE Effect]`.

If an older build reports `Invalid APFSDS balance setting`, check every custom profile, including HEAT/HESH. All behaviors share the [ballistic validation limits](CUSTOM-SHELLS.md#ballistic-variables): `maximumVelocityFactor` must be 1â€“4. v0.9.8 identifies the profile, field, received value and range. Invalid configurations are preserved; correct the reported setting and restart rather than deleting your custom shells.

Include the game/loader version, profile, relevant log lines and reproduction steps. For simulator issues include calibre, penetration and a screenshot. To roll back, close the game and restore your previous DLL and configuration backup.

## Building from source

Install **.NET SDK 8** and start the working mod loader once so `BepInEx\interop` exists:

```powershell
dotnet build -c Release -p:GameDir="C:\Program Files (x86)\Steam\steamapps\common\Sprocket"
dotnet run --project tests/ShellSelector.Tests.csproj -c Release -- default-shells.json
```

Output: `bin\Release\net6.0\SprocketShellSelector.dll`. Game/loader assemblies are referenced locally and are not distributed.

## Credits

Created by RoanWassink with AI assistance. The native inspector integration follows the pattern used by Hans21223's *Sprocket Quality of Life*.

## License

[MIT](LICENSE), covering this plugin's code.

Donations
For ChatGPT budget. Helps me reverse engineer sprocket to add cool mods. https://www.paypal.com/donate/?hosted_button_id=7PE3SDBETXFQ6
