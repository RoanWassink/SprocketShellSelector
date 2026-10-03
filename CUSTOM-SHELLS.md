# Making your own shell profiles

This guide describes **v0.10.0 release candidate 1**. Profiles do not change the cannon's propellant setting or visible ammunition model.

## Add a shell

1. Close Sprocket and back up `BepInEx/config/sprocket.shellselector.shells.json`.
2. Copy an object inside its `profiles` array.
3. Give it a unique `id` and `label`. Set `behavior` to the shell mechanics you want.
4. Save valid JSON, restart, and select it on the cannon. The simulator has its own separate selection.

Use `{"schemaVersion": 1, "profiles": [ ... ]}`. Separate objects with commas, with no trailing commas or comments. Up to 16 profiles; IDs use lowercase letters, digits, `_`, `-` (1–40 characters). Labels are unique ignoring case (1–80 characters). `vanilla` / `Vanilla ammunition` are reserved. Unknown/duplicate keys or invalid values reject the file.

## Copyable APFSDS example

Add this object to the existing array:

```json
{
  "id": "my_long_rod",
  "label": "My long rod",
  "behavior": "apfsds",
  "penetratorDiameterFactor": 0.22,
  "penetratorLengthInCalibres": 6,
  "penetratorDensity": 17500,
  "velocityEfficiency": 0.85,
  "velocityMultiplier": 1,
  "maximumVelocityFactor": 2.2,
  "maximumVelocity": 2200,
  "penetrationQuality": 0.45,
  "fragmentDamageMultiplier": 0.5
}
```

**`behavior`, not `id` or `label`, activates APFSDS mechanics.** This copy gets the narrow rod cone, remaining-penetration spall, length modifiers and optional suppression of classic AP normalization. There is no need to use the old `apfsds` ID. Copy short rod with length 3 for a lighter starting point. A renamed copy with identical settings has identical calculated performance.

## Choose mechanics

| `behavior` | Mechanics | Payload settings |
|---|---|---|
| `ap` | Custom AP ballistics and native spall | No extra fields required |
| `apfsds` | Rod ballistics, narrow energy-dependent cone, more spall as penetration is spent | No extra fields required |
| `aphe` | AP ballistics with a heavy broad payload burst after perforation | Global APHE spall/visual settings |
| `he` | Native impact blast | Positive `nativeExplosivePower`; `explosionScale` |
| `heat` | High chemical first-plate budget, concentrated spall, spaced-armour gap loss | Positive `chemicalPenetrationMm`, `secondPlatePenetrationFactor`, `spallMultiplier`, `coneHalfAngleDegrees`, `explosionScale` |
| `hesh` | Heavy broad spall, lower chemical budget, spaced-armour gap loss | Same fields as HEAT |

All ten numeric ballistic fields in the example remain required for every behavior. Start from the corresponding built-in example in [default-shells.json](default-shells.json), rather than changing only a label. If `behavior` is absent, legacy IDs `apfsds` and `aphe` infer those behaviors; all other IDs infer `ap`. New custom shells should always specify it.

## Ballistic variables

Calibre is full gun calibre. Ranges are inclusive.

| Setting | Range | Interaction |
|---|---|---|
| `penetratorDiameterFactor` | 0.05–0.9 | Projectile diameter / gun calibre; mass scales with diameter squared |
| `penetratorLengthInCalibres` | 0.5–10 | Length / full gun calibre; mass scales with length |
| `penetratorDensity` | 1000–25000 | kg/m³; mass scales with density |
| `velocityEfficiency` | 0.1–2 | Multiplies mass-based speed factor |
| `velocityMultiplier` | 0.1–4 | Further speed tuning before limits |
| `maximumVelocityFactor` | 1–4 | Maximum multiple of vanilla muzzle speed; values below 1 are invalid for every behavior |
| `maximumVelocity` | 100–5000 | Final absolute speed cap in m/s |
| `penetrationQuality` | 0.1–4 | Higher improves penetration via native constant; no direct speed change |
| `fragmentDamageMultiplier` | 0.01–1 | Health-damage scaling for `ap`; APFSDS/APHE/HEAT/HESH use dedicated damage handling instead |

```text
diameter = calibreMetres × diameterFactor
length   = calibreMetres × lengthInCalibres
mass     = π/4 × diameter² × length × density
raw speed factor = sqrt((1.59e-5 × calibreMm³) / mass)
                   × velocityEfficiency × velocityMultiplier
speed = min(vanillaSpeed × clamp(raw factor, 1, maximumVelocityFactor),
            maximumVelocity)
```

Doubling rod length doubles mass. Doubling diameter quadruples mass. A heavier rod can slow down; a lighter one can speed up. The speed-factor floor is 1, so lowering efficiency eventually stops reducing speed; the absolute cap can still put speed below vanilla. Increasing speed settings has no effect once an upper limit is reached.

### Configuration errors

Every numeric setting must be finite and within its inclusive range above or in the payload table below. Ballistic limits apply to **all behaviors**, including HEAT and HESH. `maximumVelocityFactor: 0.75` is invalid even for an `early_heat` profile: the speed-factor calculation also has a lower bound of 1. Use a factor of at least 1; a lower absolute `maximumVelocity` can cap muzzle speed below vanilla if desired. This does not create velocity-independent HEAT mechanics by itself.

v0.9.8 reports the offending profile, JSON field, received value and limits, for example:

```text
Profile 'early_heat': maximumVelocityFactor is 0.75; expected 1–4 (finite, inclusive).
```

Earlier builds, including v0.9.4, instead report `Invalid APFSDS balance setting` for any behavior using the shared validator. This can come from a HEAT profile even when APFSDS profiles are valid. Invalid custom configurations are rejected and left intact; the mod does not silently replace their settings. Correct the reported field in `BepInEx/config/sprocket.shellselector.shells.json`, then restart the game. These messages do not relax the ranges or change the speed formula.


APFSDS also applies:

```text
effective quality = clamp(penetrationQuality × sqrt(lengthInCalibres/5), 0.1, 4)
rod cone modifier = clamp(1 + (5-lengthInCalibres) × 0.06, 0.7, 1.25)
K = clamp(round(vanillaK / effectiveQuality^(1/1.43)), 1, 65535)
```

Other behaviors use the configured quality directly. More quality means lower K and better native penetration. Longer rods gain efficiency and a narrower cone, but their mass/speed change still matters. Check resulting mass, velocity and base penetration on the cannon. These formulas are gameplay approximations, not a long-rod erosion model.

## Explosive and chemical variables

| Setting | Range | Meaning |
|---|---|---|
| `chemicalPenetrationMm` | 0–2000; positive for HEAT/HESH/ATGM | Initial RHA capacity at a **100mm gun**; actual capacity is value × calibre/100, bounded to 1–2000mm |
| `secondPlatePenetrationFactor` | 0.01–1 | Lower bound of each air-gap retention curve, applied to CURRENT remaining penetration; defaults HEAT .15, HESH .10 |
| `airGapLossPerCalibre` | 0–100 | Additional gap-loss sensitivity; HEAT defaults .35, HESH 12; independently configurable per profile. Zero disables extra gap loss |
| `nativeExplosivePower` | 0–500; positive for HE | Reference native blast power at 100mm; scales with calibre³ and is capped at 500. Not calibrated kg TNT |
| `spallMultiplier` | 1–12 | HEAT/HESH fragment volume/count tuning; release reference values HEAT 1.5, HESH 8 |
| `coneHalfAngleDegrees` | 1–90 | HEAT/HESH half-angle: default HEAT 8 gives 16° total; HESH 70 gives 140° |
| `explosionScale` | 0.1–3 | HE/HEAT/HESH visual asset scale, also scaled with calibre; no direct damage change |

Example HEAT extras, keeping all required ballistic fields:

```json
"behavior": "heat",
"chemicalPenetrationMm": 400,
"secondPlatePenetrationFactor": 0.15,
"spallMultiplier": 1.5,
"coneHalfAngleDegrees": 8,
"explosionScale": 0.7
```

At 100mm calibre, HEAT has 400mm initial capacity. The previous fixed 60mm second-plate cap is removed. Gap loss now multiplies remaining native penetration using spacing and preceding LOS RHA thickness. For a 10mm front plate, a 100mm gap retains about 87.7% of what remains; 500mm retains about 51.2%. A 5mm gap adds no extra HEAT loss at that calibre. HESH uses its own much steeper curve. See [equations](docs/SPACED-ARMOUR.md).

`secondPlatePenetrationFactor` changed meaning in v0.9.7: .15 means the HEAT curve approaches 15% retention for a very large gap, not an immediate cap of 15% of initial penetration. Multiple gaps compound. `airGapLossPerCalibre` can differ on every custom shell. Fragment energy does not reset the parent's budget; secondary fragments retain their own native penetration.

For HESH start from 300, .10, 8, 70, .65 respectively. It is a broad damage proxy that still requires perforation, not real nonperforating backface scabbing. For HE use reference power 40 and visual scale 1.5. For APHE use the built-in APHE body and tune the global `apheSpallMultiplier`, `apheConeHalfAngleDegrees` and `apheExplosionScale` in the spall config. APHE does not use these per-profile chemical fields.

Payload fragment mass scales with calibre³, while counts scale with calibre and stop at native 32 per burst. APHE/HESH/HEAT retain payload fragment speed even after a barely successful penetration. No payload burst is forced through an unperforated plate.

## Tuning tips

- For more APFSDS penetration with unchanged geometry, increase quality gradually. Speed limits may make velocity tweaks ineffective.
- For long/short rod experiments, change length, then compare mass, speed, penetration and cone. Do not assume length alone guarantees better output.
- For stronger HEAT penetration, change the chemical budget. Quality/flight speed are not its impact penetration budget.
- For more HEAT/HESH damage, change spall multiplier and cone. Count can hit 32; volume still changes fragment mass.
- For stronger spaced-armour resistance, increase airGapLossPerCalibre or lower the secondPlatePenetrationFactor floor. Tune HEAT and HESH separately; this is a distance-aware gameplay proxy.
- Compare shells at equal full gun calibre. Simulator penetration is manual; chemical modes also obey their configured cap. Select the live shell separately on the cannon.

Back up config before tuning. Restart after edits and check `BepInEx/LogOutput.log` if the selector disappears.

## Custom ATGMs

Copy either ATGM example from [default-shells.json](default-shells.json), change ID/label, keep `"behavior": "atgm"`, and choose `"guidanceMode": "sight"`, `"keyboard"` or `"none"`. IDs do not determine guidance. Both guided modes share the same motor controls and HEAT impact logic; `none` is powered straight flight.

| Setting | Range / default when omitted | Interaction |
|---|---|---|
| `referenceCalibreMm` | 10�500 / 100 | Calibre at which chemicalPenetrationMm applies; examples use 135 |
| `flightSpeed` | 50�1000 / 200 | Maximum powered speed, m/s |
| `launchSpeedMode` | fixed or cannon / fixed | Choose configured launch speed or native cannon muzzle velocity |
| `launchSpeed` | 10�flightSpeed / flightSpeed | Initial speed in fixed mode; still validated if supplied in cannon mode |
| `launchSpeedMultiplier` | 0.01�4 / 1 | In cannon mode, multiply native muzzle velocity then clamp to 10�flightSpeed |
| `acceleration` | 0�2000 / 0 | Motor acceleration in m/s�; zero retains initial speed |
| `motorDelay` | 0�5 / 0 | Seconds after spawn before acceleration; less than flight lifetime if acceleration > 0 |
| `maxTurnRate` | 0�90 / 20 | Degrees/second shared by both guidance modes; diagonal keyboard commands share the limit |
| `maximumFlightTime` | 1�60 / 25 | Seconds before missile release without additional detonation |
| `guidanceDelay` | 0�5 / 0.25 | Delay before steering; must be less than maximumFlightTime |
| `guidanceMode` | sight, keyboard or none / sight | Reticle, WASD or straight flight |

For a soft launch, add these fields inside either existing ATGM profile:

```json
"launchSpeedMode": "fixed",
"launchSpeed": 50,
"launchSpeedMultiplier": 1,
"acceleration": 150,
"motorDelay": 0.15,
"flightSpeed": 200
```

For propellant-dependent launch speed, use `"launchSpeedMode": "cannon"` and e.g. `"launchSpeedMultiplier": 0.2`. A native 400 m/s cannon launches at 80 m/s; an 800 m/s cannon launches at 160 m/s; speeds above 200 m/s are capped when flightSpeed is 200. Cannon propellant/barrel/technology changes affect the native muzzle speed. The missile motor uses its own `acceleration`; it is not automatically strengthened by a longer cannon charge.

```text
fixed launch = launchSpeed (or flightSpeed if omitted)
cannon launch = clamp(native cannon muzzle velocity � launchSpeedMultiplier, 10, flightSpeed)
powered speed(t) = min(flightSpeed, initial speed + acceleration � max(0, t - motorDelay))
chemical penetration = clamp(chemicalPenetrationMm � gun calibre / referenceCalibreMm, 1, 2000)
```

Launch inherits vehicle velocity. The controller records the resulting initial world-speed magnitude, clamped to 10�flightSpeed, and applies the motor curve to that speed. Native gravity/drag can act within each physics tick; the next powered command restores the curve. This is a gameplay motor model, not thrust/mass/fuel simulation. Guidance starts independently of motor delay. Flight time with acceleration is not simply distance/cruise speed near the launcher.

Penetration does not increase with flight speed: ATGM uses its configured chemical budget and HEAT air-gap losses. Tune `chemicalPenetrationMm`, `spallMultiplier`, `coneHalfAngleDegrees` and `explosionScale` for the impact; velocityEfficiency/maximumVelocity remain validated body fields but do not control ATGM launch speed. Changing launch speed does not change the native ammunition mesh, propellant UI, storage or loading cost.

Old custom ATGMs without motor fields retain constant flightSpeed. Migration writes explicit equivalent settings and a backup. At the 16-profile limit, free a slot to add a missing example. Do not replace your whole config with default-shells.json unless you want to discard custom shells.

See [ATGM.md](ATGM.md) for controls, guidance references and rollback.
