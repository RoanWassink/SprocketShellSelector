# v0.8.0 experiment

RC1 (v0.6.0) remains preserved. This build is prepared, not installed: Sprocket was running. Build and 137 managed checks pass. Native hooks and visible effect size require gameplay validation.

APFSDS: plate-relative bend is applied to the original penetrator inside an entered material segment, using its native entry triangle normal. It bends toward the plate tangent, keeps an inward normal component, and preserves speed and mass. It is bounded and only applied once per simulation/entry surface. No world-downward force, erosion, rod flexure or guaranteed penetration; native ricochet/armour resistance remain. Test both mirrored slopes and normal incidence. Select APFSDS in the simulator.

APHE: accepted 180-degree forward cone and 4x native spall inputs retained. Explosion asset minimum/maximum scale bounds are temporarily reduced, then restored even on errors. Calibre scaling alone could be clamped at the asset minimum; this build scales the bounds themselves. Other effects retain their native bounds. Exact visible outcome needs a live shot test.

Edit BepInEx/config/sprocket.shellselector.spall.json with Sprocket closed and restart:

| Setting | Default | Meaning |
|---|---|---|
| apheConeHalfAngleDegrees | 90 | Half-angle; 90 means 180-degree full cone |
| apheSpallMultiplier | 4 | Spall volume/count input multiplier, native cap still applies |
| apheExplosionEffect | true | Live explosion visual toggle |
| apheExplosionScale | 0.65 | Multiply visual asset min/max size bounds |
| apfsdsPlateDeflection | true | Enable experimental plate-relative bend |
| apfsdsDeflectionMaximumDegrees | 8 | Maximum extra bend per entry surface, increases with obliquity |
| apfsdsDeflectionMinimumObliquityDegrees | 30 | Below this angle from plate normal, no extra bend |
| apfsdsDisableClassicNormalization | true | Disable native classical AP normalization on APFSDS |

Native colour overlay is not a model of this new bend. Compare detailed original-shell trajectories and live firing. An effect log [APHE Effect] Actual asset scale bounds... confirms that the size hook ran. [APFSDS Deflection] logs before/after directions.

Install-Experimental.ps1 checks Sprocket is closed, backs up the installed shell DLL/configs/log, adds missing settings without overwriting custom values and replaces the shell DLL. Restore that backup with the game closed to roll back.
