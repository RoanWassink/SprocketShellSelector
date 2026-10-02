# Standalone v0.1.0 split checks

- [x] Own namespace, assembly, GUID, Harmony owner and CFG/JSON paths.
- [x] Preserve both existing vehicle savekeys and combined v0.5.1 ballistics.
- [x] One-time read-only import from legacy JSON or numeric CFG; 74 checks pass.
- [x] Build against installed current interop; known MSB3246 broad-reference warning.
- [x] Refuse patches if combined shell Harmony owner already exists; material dependency establishes load order.
- [x] Confirm stable v0.4.0 backup SHA256 matches handoff.
- [ ] Installation: two Sprocket processes were running; no game files changed.
- [ ] Startup log: Material Selector v0.4.0 and Shell Selector v0.1.0 both loaded.
- [ ] Existing APFSDS vehicle, vanilla shot, save/load, clone and JSON restart test.

Follow-up experiments below retain their original status; no new spall/cost behavior in the split.

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
- [ ] User test: existing vehicle, APFSDS shot, vanilla shot, save/load and cloned cannon.
- [ ] User test: edit a profile value with game closed and check preview and actual shot after restart.

## Next separate experiments

- [ ] Trace native spall creation: direction/cone, fragment quantity, plate thickness and residual energy.
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

