# Guided ATGMs

SACLOS uses the controlled vehicle's sight or reticle. MCLOS uses W/S for vertical and A/D for horizontal steering, temporarily blocking vehicle commands while guidance is active. Choose your camera view before firing. AI retains native target selection and uses missile lead. Only the newest missile per vehicle is guided, including vehicles with multiple launchers.

Use `behavior: atgm` for launcher missiles and `behavior: atgm_gun` for gun-launched missiles. Guidance mode (`sight` or `keyboard`) is independent of launch type. Both use the HEAT impact and air-gap model, with calibre-scaled chemical penetration. No seeker, target lock, tandem warhead, physical wire, top attack or independent HE blast is modeled.

Motor settings are per-profile. Fresh examples use 50 m/s fixed launch speed, 150 m/s² acceleration after a 0.12 s delay, 200 m/s maximum flight speed, 4 s motor burn and 1 m/s² coast loss. The gun-launched example uses cannon speed × 0.2 and a 0.08 s motor delay. They are editable gameplay values. Native gravity and drag apply before ignition; powered guidance runs before native movement/collision. Flame, light and smoke are linked to motor ignition.

[Custom ATGM field ranges and formulas](CUSTOM-SHELLS.md#custom-atgms) explain all motor/guidance settings. [Launcher and finite ammunition setup](README.md#dedicated-launcher-and-ammunition-box) covers the native gunner, one ready initial missile and optional automatic reserve box.

## Optional settings

In `BepInEx/config/sprocket.shellselector.cfg`, `[ATGM Experimental] Enabled` controls powered guidance. If disabled or unavailable, missiles retain native ballistic launch with their chemical impact. `DiagnosticLogging` defaults to false and enables detailed commanded/native movement reports.

`[ATGM Launcher Tests] InitialReadyMissile` defaults to true; `AutomaticAmmoBox` and `OpticalSightDirection` default to false. These section/key names are retained for existing configuration compatibility. Restart after edits.

Audio is separate for launcher and gun-launched missiles. `[ATGM Audio] Enabled` defaults to true, `VolumeMultiplier` is 0–2 (default 1). Optional PCM16 WAV overrides belong in `BepInEx/config/sprocket.shellselector.audio/atgm.wav` and `atgm_gun.wav`. Native positional attenuation/mixer processing remains in use. See [audio provenance](audio/README.md).

## Updating

Existing tuned profiles and saved IDs remain intact. Missing legacy motor fields receive constant-speed equivalents rather than new soft-launch values. Configure motor fields explicitly to adopt the examples. The neutral CFG migrates from the old plugin ID only when absent; the original file remains for rollback. Keep a matching DLL/configuration backup when reverting.
