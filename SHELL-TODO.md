# v0.3.0 simulator/penetration experiment

- [x] User's simulator route traced in current native code and interop.
- [x] Simulator shell dropdown, full gun calibre and penetration slider maximum2000 implemented.
- [x] Detailed synthetic projectile and spall hooks share existing profile settings.
- [x] APFSDS quality .75 -> .45 calibration; geometry/speed preserved, no calibre-specific exceptions.
- [x] Build and99 managed checks.
- [ ] New startup hooks and simulator UI/gameplay verified.
- [ ] Actual125 mm cannon check: speed unchanged, penetration around60% of old value.
- [ ] Follow-up: validated long-rod physics model including strength, rather than native AP approximation.

# v0.2.0 experiment

- [x] APFSDS native spread scaled to 0.3, native fragment-count inputs retained.
- [x] APFSDS spall volume relative to gun-calibre native AP reference: thin .08, middle 1, thick 1.25; separate JSON tuning.
- [x] APFSDS ballistic settings retained; fixed 50% health scaling bypassed for this experiment.
- [x] APHE profile: reduced native AP penetration, fixed 24-fragment/.6 kg/450 m/s sphere, K=8000, intact continuation stopped after breakup.
- [x] Build and 96 managed checks passed. Installed v0.2.0 with v0.1.0 rollback backup on 2026-10-02.
- [ ] New Harmony hooks confirmed in startup log.
- [ ] APFSDS measured against AP on equal calibre, velocity/material/angle and thickness grid.
- [ ] APHE logs confirm one 24-fragment burst across different penetrable thicknesses; check whole sphere and low fragment penetration.
- [ ] User gameplay: crew/module damage, outer plate/back plate effects and balance feedback.

# Standalone v0.1.0 split checks

- [x] Own namespace, assembly, GUID, Harmony owner and CFG/JSON paths.
- [x] Preserve both existing vehicle savekeys and combined v0.5.1 ballistics.
- [x] One-time read-only import from legacy JSON or numeric CFG; 74 checks pass.
- [x] Build against installed current interop; known MSB3246 broad-reference warning.
- [x] Refuse patches if combined shell Harmony owner already exists; material dependency establishes load order.
- [x] Confirm stable v0.4.0 backup SHA256 matches handoff.
- [x] Installed on 2026-10-02 after Sprocket exited; exact stable v0.4.0 material DLL restored, shell v0.1.0 installed, backups retained.
- [x] Startup log confirms Material Selector v0.4.0 and Shell Selector v0.1.0 both loaded.
- [x] User reported all v0.1.0 gameplay tests passed on 2026-10-02.

v0.1.0 split remains archived. v0.2.0 is the separate spall/APHE experiment; gameplay validation pending.

# Shell beta work list

Keep changes in separately archived test versions. Only confirmed members from
the installed Sprocket 0.2.55.5 interop and native call chains may be used.

## v0.5.1 — profile framework (first test step)

- [x] Archive the user-tested v0.5.0 DLL, source and configuration.
- [x] Load validated shell profiles from JSON; stable IDs rather than dropdown indices.
- [x] Preserve v0.5.0 APFSDS settings on first migration.
- [x] Use the selected profile for inspector, aiming, projectile registration and impacts.
- [x] Persist profile ID, support blueprint cloning and migrate the old boolean save field.
- [x] Preserve legacy APFSDS save field so v0.5.0 can reopen APFSDS saves.
- [x] Reject invalid, duplicate, missing and unknown profile settings.
- [x] Build and automated validation (175 checks).
- [x] Install and startup check: v0.5.1, JSON profile and Harmony patches loaded.
- [x] User tests passed with the standalone v0.1.0 split.
- [x] User reported the user-test checklist passed.

## Next separate experiments

- [x] Traced native SimulateFragment -> CreateSpallBurst -> GetFragmentDirection/NewFragment against matching GameAssembly SHA256.
- [ ] Narrower APFSDS cone while preserving native fragment quantity; do not use reduced health damage as a substitute for cone width.
- [ ] Thickness response: thin plate low spall, middle crossover with AP, thick plate comparable to AP. Define comparison at equal cannon/plate and document angle effects.
- [ ] Add cone/thickness/spall fields to JSON only when their actual native hooks work.
- [ ] Trace rack -> loading -> selected round -> firing. Keep calibre/cartridge compatibility separate from projectile profile.
- [ ] Assign profile per rack/round; retain mixed AP/APFSDS counts and implement next-round selection. Remove cannon-wide override only after this route is verified.
- [ ] Trace native AmmoRack.Build cost and vehicle total updates; price stored rounds without compounding rebuilds or charging all ammunition as APFSDS.
- [ ] Add validated costMultiplier to profiles: initial AP 1x, APCBC 1.4x, APDS 2.5x, APFSDS 5x. These prices are targets, not active settings yet.
- [ ] Optional APHE: test spherical fragments with lower penetration and thickness-independent fragment budget. Do not describe this as a true delayed internal explosion.
- [ ] Separate test versions and source/config snapshots for each experiment; no GitHub beta publication unless requested.

## Confirmed starting points, not yet implemented routes

`AmmoRackBlueprint.ShellTypeGuid` and `ShellSlotBlueprintID`,
`AmmoRack.LoadInfo`, `AmmoRack.Capacity`, `AmmoRack.Build`,
`AmmoRack.SyncWithShellSlotBlueprint`, `AmmoRackProperties.Cost`, and
`CannonBehaviour.FireInternal(ProjectileTypeID)` exist in the current interop.
The complete loading and cost call chains still need inspection before patching.

`PenetrationSimulation` exposes `Penetrator`, `fragments`, `FragmentCount`,
`NewFragment` and `ApplyFragmentHits`. These members alone do not establish where
the spall cone is generated. The current tested hook scales health damage during
custom impacts; it does not change the spall cone or fragment count.






## v0.4.0 follow-up
- [x] User confirmed v0.3.1 loads and simulator shell selection works.
- [x] APHE native fragment writeback and immediate parent stop implemented.
- [x] APFSDS plate-thickness curve replaced by native remaining/initial penetration ratio, broad default linear response; full-calibre mass/count reference and narrow cone.
- [x] Release build and 109 managed checks pass.
- [ ] User verify APHE intact shell stops at explosion in simulator and real firing.
- [ ] User compare APFSDS spall on the same plate at multiple penetration slider values, then real firing.

## v0.5.0
- [x] Neutral shell and spall JSON filenames with existing settings migration; README rewritten around current behaviour.
- [x] APHE AP continuation below cumulative RHA fuse threshold, delayed exit-side burst, spherical direction writeback, stronger fragment count/mass/speed, isolated fragment profile.
- [x] APFSDS cone widening 0–15% of base width using remaining kinetic energy.
- [x] 122 managed checks and release build pass.
- [ ] Verify APHE thin plates, cumulative spaced plates, stopped rounds, fuse delay and internal damage in simulator and live fire.
- [ ] Verify APFSDS cone width at multiple remaining energy values.

## v0.6.0
- [x] User screenshots/logs show APHE sphere is not producing reliable evaluated damage paths; fuse/sphere custom route removed.
- [x] APHE native AP origin/directions/material/speed/profile/continuation preserved; 4x spall volume/count input with native cap.
- [x] Optional visual-only native explosion hook for actual shots; lower APHE penetration retained.
- [x] APFSDS accepted behaviour preserved; existing native angle-based normalization inspected, no custom plate-following bend.
- [x] Release build and 120 managed checks pass.
- [ ] Repeat APHE simulator test from above/side at unchanged settings and sliders moving both directions.
- [ ] Live APHE damage and explosion effect validation.
