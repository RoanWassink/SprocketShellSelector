# v0.7.0 experimental build

Accepted release candidate 1 is unchanged v0.6.0, still installed. This experimental DLL has not been installed or tested in-game. Build and 129 managed checks pass.

APHE: 90-degree half-angle produces a 180-degree full forward cone, sampled uniformly about the native inward plate normal. Native origin, material filter, speed, fragment count/mass and evaluation remain intact. No sphere or fragment-array direction rewriting. apheConeHalfAngleDegrees permits narrower tests. Native hemisphere rejection and finite fragment count still affect visible results.

Explosion: apheExplosionScale=0.65 scales the calibre supplied only to the explosion visual/audio factory. It does not change cannon calibre, spall or damage. Exact displayed size depends on native assets.

APFSDS: apfsdsDisableClassicNormalization=true sets maximum classical AP normalization angle to zero for APFSDS in live impacts and detailed simulator shots. Penetration mass/speed/K and native ricochet/obliquity remain unchanged. This is a partial gameplay experiment, not erosion, melting, rod flexure, burying or denormalization. Native colour sampling is not modified for this new setting; compare detailed trajectories.

Install only with Sprocket closed. Back up current shell DLL and neutral JSON configs. Copy this DLL into BepInEx/plugins and merge the three new settings into the current spall JSON, or use the supplied copied configuration. Do not install alongside another shell-selector DLL. Restore RC1 DLL/configs to roll back.

Test APHE from front, side and above at angled plates, keeping native paths evaluated; compare RC1. For APFSDS compare classical-normalization flag on/off at identical calibre, velocity/penetration and 0/30/60/75-degree plate obliquity. Expect native ricochet rules to remain. Check preview versus live firing separately.
