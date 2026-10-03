# Making your own shell profiles

This guide describes **v0.8.1**. Profiles change calculated projectile geometry and ballistics; they do not change the cannon's propellant setting or create a new visible ammunition model.

## Add a profile

1. Close Sprocket and back up `BepInEx/config/sprocket.shellselector.shells.json`.
2. Duplicate an object **inside its existing `profiles` array**.
3. Give the copy a unique `id` and `label`, keeping every setting below.
4. Separate objects with commas, save, and restart. Select your new profile in the cannon inspector or armour simulator.

Example object to add (not a replacement for the whole file):

```json
{
  "id": "custom_dart",
  "label": "Custom dart",
  "penetratorDiameterFactor": 0.22,
  "penetratorLengthInCalibres": 5,
  "penetratorDensity": 17500,
  "velocityEfficiency": 0.85,
  "velocityMultiplier": 1,
  "maximumVelocityFactor": 2.2,
  "maximumVelocity": 2200,
  "penetrationQuality": 0.6,
  "fragmentDamageMultiplier": 0.5
}
```

The file keeps this structure: `{"schemaVersion": 1, "profiles": [ ... ]}`. Use `}, {` between entries; no trailing comma or JSON comments. Up to 16 profiles are supported. IDs use lowercase letters, digits, `_` and `-` (1–40 characters). Labels must be unique too, ignoring capitalization (1–80 characters). `vanilla` and `Vanilla ammunition` are reserved. Unknown fields, missing fields, duplicate IDs/labels and invalid numbers cause configuration rejection.

**Important:** In v0.8.1, the special APFSDS spall/normalization behaviour is tied to `id: "apfsds"`, and APHE spall/explosion behaviour to `id: "aphe"`. A new ID gets custom AP-based ballistics, not those special behaviours. To strengthen the existing APFSDS while retaining its behaviour, edit that profile instead. Two independently selectable APFSDS variants with the same special behaviour need a plugin update. Do not duplicate the same ID.

## What the settings do

All numeric ranges below are inclusive. Calibre means the **full gun calibre**, not the dart diameter.

| Setting | Allowed range | Meaning and interaction |
|---|---|---|
| `penetratorDiameterFactor` | 0.05–0.9 | Projectile diameter / gun calibre. Diameter affects penetration and cylinder mass; mass increases with diameter squared. |
| `penetratorLengthInCalibres` | 0.5–10 | Length / gun calibre. A 125 mm gun with 5 gives a 625 mm rod. Longer rods weigh more and can reduce calculated speed. |
| `penetratorDensity` | 1000–25000 | kg/m³. Higher density increases mass without changing dimensions. |
| `velocityEfficiency` | 0.1–2 | Multiplies the mass-based velocity factor, before limits. |
| `velocityMultiplier` | 0.1–4 | Additional speed tuning factor; multiplies efficiency before limits. |
| `maximumVelocityFactor` | 1–4 | Maximum speed factor relative to the cannon's vanilla muzzle velocity. |
| `maximumVelocity` | 100–5000 | Absolute muzzle-speed cap in m/s, applied after the factor limit. |
| `penetrationQuality` | 0.1–4 | Higher values improve penetration at equal diameter, mass and speed, by adjusting the native penetrator constant. Does not change mass or speed. |
| `fragmentDamageMultiplier` | 0.01–1 | Damage scaling for new/custom IDs. Built-in `apfsds` and `aphe` bypass this scaling and use their dedicated spall handling. This does not control fragment count or cone width. |

## How the variables interact

The plugin first calculates a cylindrical projectile:

```text
diameter = gun calibre in metres × penetratorDiameterFactor
length   = gun calibre in metres × penetratorLengthInCalibres
mass     = π × diameter² / 4 × length × penetratorDensity
```

Doubling length or density doubles mass. Doubling diameter multiplies mass by four. Length is passed into the native projectile definition and affects mass; this is still an AP-based approximation, not a dedicated long-rod erosion model.

Speed then follows:

```text
raw factor = sqrt(vanilla reference mass / custom mass)
             × velocityEfficiency × velocityMultiplier
factor     = clamp(raw factor, 1, maximumVelocityFactor)
speed      = min(vanilla muzzle velocity × factor, maximumVelocity)
```

The reference mass is `1.59e-5 × calibreMm³`. Cannon/propellant changes still affect the baseline vanilla muzzle velocity.

A heavier rod can slow down, while a lighter one can speed up. The factor cannot fall below 1, so lowering efficiency or multiplier may stop having an effect at that floor. The absolute speed cap can still put speed below vanilla. Raising speed settings does nothing once either upper limit is reached.

The native penetration calculation uses diameter, mass, speed and a modified penetrator constant:

```text
K = clamp(round(vanilla K / penetrationQuality^(1 / 1.43)), 1, 65535)
```

Higher quality lowers K and improves penetration. Rounding and limits mean the change is not perfectly continuous. Changing dimensions also changes mass and potentially speed, so a longer/heavier rod does **not** guarantee higher penetration. Check the resulting mass, speed and base RHA penetration in the cannon inspector.

## Practical tuning

- **More penetration, same geometry:** increase `penetrationQuality` gradually, for example from 0.5 to 0.6. Keep the `apfsds` ID if you want its special behaviour.
- **More speed:** increase `velocityMultiplier` or `velocityEfficiency`, then check whether the speed caps are already active. These two settings multiply together.
- **Longer/heavier projectile:** increase length or density, then inspect the new velocity and penetration rather than assuming an improvement.
- **Different spall or APHE visual size:** edit `sprocket.shellselector.spall.json`; those settings are separate from the shell-profile geometry. See the [README configuration table](README.md#configuration).

Change one variable at a time and compare the same gun, target and impact angle. These settings are gameplay approximations rather than a way to predict real ammunition performance.

## Simulator and troubleshooting

The simulator's penetration slider is **manual**. Choosing 500 mm sets the simulated penetration to 500 mm using a calculated equivalent speed; it does not display your cannon's newly calculated penetration. Use the cannon inspector to compare ballistic output, then use the simulator to explore penetration/spall at chosen values and live shots to verify gameplay.

If the dropdown fails after editing, restore the backup and check `BepInEx/LogOutput.log` for `[Shell Profiles]` or `Shell selector disabled`. Include the relevant error and your JSON when reporting the issue. Do not change a profile ID after saving vehicles unless you intend those saved selections to become unavailable and fall back to vanilla.

