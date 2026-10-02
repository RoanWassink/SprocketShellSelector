# v0.2.0 experimental spall and APHE

Vibe coded with AI assistance. This separate test keeps the accepted v0.1.0 archive intact. No mixed loadout or ammunition cost changes.

## APFSDS

The current gun calibre and existing penetration/flight settings are preserved. The native spread parameter is multiplied by 0.3. The native count inputs (cross-sectional area and material spall factor) are unchanged by cone narrowing.

The spall mass/volume curve now uses the native full gun-calibre AP volume as its reference, at the SAME material path length. The ratio is .08 below .15 gun calibres, 1 at .75 calibres, and 1.25 at/above 1.5 calibres. Cubic interpolation makes the transition more pronounced. For a 130 mm gun these anchors are 19.5, 97.5 and 195 mm of material path. Angled plates have a longer path; these are NOT normal plate thickness thresholds.

The old global .50 health multiplier is bypassed for APFSDS/experimental APHE. Lower thin-plate damage comes from reduced fragment mass/energy, while thick plates can generate substantial mass. Equal spall mass does not promise equal crew damage: speed, fragmentation geometry, hit locations and fragment penetration still differ from AP. Native minimum fragment diameter/speed and rejection/caps still apply. Very thin plates may yield no fragments; preserving native count inputs is not a guarantee that native filtering never removes a fragment.

## APHE approximation

Select `APHE (spherical experiment)` in the cannon inspector. Projectile diameter .9 gun calibre, length 2 calibres, density 7800 kg/m3; velocity remains vanilla and penetration quality .65 reduces native AP penetration relative to full-calibre AP.

At the first eligible native spall burst of a penetrating original projectile, replace the burst with 24 fragments, .6 kg total, 450 m/s, and native fragment K=8000. These budgets do not depend on plate thickness. Uniform sphere sampling replaces native direction sampling only inside this burst, and the native surface hemisphere rejection is neutralized. Later intact projectile continuation is stopped after breakup. There is no native HE function, blast damage, fuze timer or confirmed delayed interior explosion. The native exit position of the penetrated material remains the burst origin. Spaced armour or an outer plate can therefore trigger breakup earlier than desired; test this explicitly.

The budget applies to one impact scope. Separate later damage-model impacts are not promised to share one lifetime-wide detonation state. Native fragment filters/buffer limits remain in effect. Non-penetrated plates do not guarantee a burst.

## Config

With game closed, tune `nl.roan.sprocket.shellselector.spall.json` and restart. Required fields are validated; unknown, duplicate, missing, nonfinite and out-of-range values disable the plugin. Existing profile JSON remains schemaVersion 1; the test config adds the APHE ID and preserves existing APFSDS settings.

## Tests

Build succeeded against current installed interop; same GameAssembly hash as the handoff. Managed checks cover old ballistics, migration, strict settings, thickness anchors/monotonicity, sphere normalization/hemisphere symmetry and lower APHE penetration. Native runtime hooks and gameplay balance are pending.

Compare AP and APFSDS with the same cannon against RHA plates 10/20/50/100/200 mm, initially at 0 degrees. Capture `[Spall]` path, ratio and spawned count alongside crew/module damage. Repeat at one fixed angle. For APHE compare multiple penetrable plate thicknesses and look for `APHE spawned=24`; also test thin outer armour plus a back plate. Check vanilla remains vanilla and save/load/clone still work.

## Install and rollback

Close Sprocket. Install this DLL and the supplied shell/spall JSON after backing up your current shell DLL/configs. Material Selector stays stable v0.4.0. Keep only one Shell Selector DLL. Rollback restores v0.1.0 DLL and the backed-up shell JSON/CFG; v0.1.0 ignores the new spall sidecar. No material config is changed.

## v0.4.0: remaining penetration and APHE stop repair

User tests confirmed v0.3.1 loads and simulator shell selection works. APHE burst/continuation and APFSDS spall balance require a new gameplay test.

APFSDS now measures current original-penetrator RHA penetration with native Fragment.GetBasePenetration and divides it by the native initial Penetrator penetration. This includes the native velocity, mass and K rather than a plate-thickness estimate. With default settings, spall ratio = 0.08 + 1.17 * (1 - remaining/original penetration). At 90%, 50%, and 10% remaining penetration the ratios are 0.197, 0.665, and 1.133. The reference volume is the saturated full-calibre native AP capacity. Native count inputs now use full gun cross-sectional area scaled by the same ratio; native rounding/cap (32) still applies. Spread alone is multiplied by coneMultiplier (0.3). Plate material still controls native spallFactor and density. Fragment speed remains native. These are game balance constants, not a physical energy-to-spall model.

The optional remainingPenetrationExponent defaults to 1 with existing configs. thinRatio and thickRatio remain configurable lower/upper bounds. Legacy thinCalibres/middleCalibres/thickCalibres/middleRatio/curveExponent remain accepted for rollback compatibility but no longer drive runtime APFSDS spall.

APHE now stops the original penetrator immediately after a successful burst by zeroing speed, setting Killed and writing the fragment back into the native simulation array. The later SimulateFragment guard also writes back. Il2CppReferenceArray boxes native value-type fragments on get_Item; changing only a retrieved wrapper did not change the native fragment. Its set_Item copies the unboxed value back. Native caller order was checked: CreateSpallBurst precedes the next GetBasePenetration/continuation calculation. Generated fragments retain their separate material profile K and spherical distribution.

Validation: release build and managed balance checks; array boxing/writeback and native call order inspected against installed game. Requires gameplay verification: APHE sphere should remain but the intact projectile must stop there; compare APFSDS on the same plate at several manually selected penetration values. Higher selected penetration should give lower spall ratio. Check real shots separately. No promise of exact count monotonicity because the native generator rounds/caps counts.

## v0.6.0 current APHE behaviour

The delayed sphere/fuse experiment is retired following user screenshots of stub-like unevaluated fragments and incorrect paths. APHE now only scales native AP spall volume and count inputs by apheSpallMultiplier (default 4). Native origin, direction/spread, speed, occupancy, profile and evaluation/continuation are preserved. No forced sphere, delayed emission, array/profile rewriting or killed original penetrator. Native count cap is 32 per burst. Ballistic settings continue to give less cannon penetration than AP; manually selecting equal penetration in the simulator still specifies equal penetration.

Optional live impact visuals reuse ProjectileEffectConfig.PlayEffect with ProjectileEffectType.Explosion after a successful APHE spall burst. Visual factory assets and enum were checked in current game. No extra blast damage, and no simulator effect spam. Separate optional Harmony owner isolates visual patch failure. Both enhanced spall and visual require gameplay validation. APFSDS is unchanged.
