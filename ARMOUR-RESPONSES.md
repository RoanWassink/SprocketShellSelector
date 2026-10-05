# Armour materials and reactive protection

## What to install

For active armour interactions use **Material Selector 0.4.5 + Shell Selector 0.12.4 + Cold War core 0.1.3**, with the response catalogue enabled. The full Cold War pack configures this combination. Installing a material DLL alone gives passive material properties, not an active ERA effect. Do not load the new heavyEra catalogue with an older Shell Selector: its parser can reject the whole catalogue.

Edit `BepInEx/config/sprocket.armour.responses.json` with the game closed and restart. The standalone example is disabled (`enabled: false`); enable it only after installing the matching consumers. The pack explicitly enables its clean starter catalogue. Updating DLLs does not overwrite an existing catalogue or automatically append a new recipe. Back up and merge the new entry, retaining your root settings and customized old recipes.

## Choosing an armour recipe

| Recipe | Purpose and limits |
|---|---|
| Glass/textolite | Additional reduction for a rod already disturbed by supported upstream steel; not a universal bonus against an intact APFSDS rod. |
| NERA sandwich | Reusable, angle-dependent HEAT interaction; it does not explode. Full added response at 30–60 degrees from the plate normal; none head-on. |
| Light ERA | One-use cells against eligible HEAT, including single-charge ATGM proxies. 15–60 mm normal cassette thickness; nominal additional penetration retention 0.62 at 0–60 degrees. |
| Heavy ERA | Kontakt-5-inspired HEAT/APFSDS gameplay proxy. Use 70 mm for an initial test; only 60–80 mm normal thickness is eligible. HEAT retention 0.50 at 0–60 degrees; APFSDS retention 0.85 at 30–60 degrees and no reactive effect head-on. |
| Passive composite | Threat-dependent additional protection with bulk/weight/cost tradeoffs, not real layer-by-layer ceramic fracture. |

Angles are measured **from the plate normal**: 0 degrees is a straight-on hit. Values are retention of remaining penetration capacity, not a fixed amount of RHA protection, damage reduction, or proof that a shell will be stopped. Angular weighting and global caps apply. Cassette thickness is measured normal to the surface, not line-of-sight depth. Outside eligibility limits the material keeps its passive properties but loses the extra response.

Both ERA kinds divide the surface into 25 cm cells. Heavy ERA shares the same cell between HEAT and APFSDS: the first eligible reaction consumes it, and later hits in that cell receive passive protection only. Nearby cells are separate. A head-on APFSDS hit on heavy ERA does not consume a cell because its kinetic response weight is zero. Preview state is separate from live combat. Permanent spent-cell blueprint persistence is not promised.

Reactive hits request a small native explosion visual. Flying tiles, explosion area damage, chain reactions, real rod rotation/fracture and tandem-charge protection are not implemented. AP/APHE/HE/HESH receive the heavy material's passive properties only. These coefficients are **gameplay calibrations**, not exact Kontakt-5 specifications or universal historical performance. The whole-pack policy exposes its Cold War repertoire from the era start; this is not a claim that every technology existed in 1945.

At 70 mm, heavy ERA has density 4000 kg/m³ and passive RHA factor 0.55: a 1 m² cassette weighs 280 kg and its passive normal resistance is 38.5 mm RHA-equivalent. An equal 785 kg comparison adds 505 kg of RHA backing. That complete array has approximately 2.52 times the reference RHA material cost; assembly costs are separate. Do not compare equal thickness and call it equal weight. A cassette usually needs backing armour.

## Adding your own material: complete examples

`examples/myHeavyEra.json` is a complete native Technology definition. Copy it to `Sprocket_Data/StreamingAssets/Technology/myHeavyEra.json`. `examples/myHeavyEra-response.json` is a complete response entry: append it to the `responses` array in your backed-up catalogue. It is **not** a complete root catalogue. Use `examples/sprocket.armour.responses.json` only for a clean installation/reference.

The native `type` must equal the response's `compatibleMaterialIds` entry. Each responseId and bound material ID must be unique. Never bind the protected `rha` or `sheetMetal` IDs. Keep a saved vehicle's IDs stable; changing a label is safer than changing its identity. Edit values in the native Technology file and matching `passiveMaterial` together: mismatched density/RHA/spall causes the extra response to be skipped. For an unchanged clone, keep all curves and geometry from the example.

| Field | Units / supported values | Use |
|---|---|---|
| Technology `type` | Unique material ID | Save identity and recipe binding. |
| Technology `date` | yyyy.MM.dd | Native availability; Cold War core is needed for the new era. |
| `density` | kg/m³; response parser 100–25000 | Physical weight; 4000 for the heavy example. |
| `rhaFactor` | Dimensionless; 0.001–5 | Passive resistance per thickness; 0.55 for heavy. |
| `spallFactor` | 0–1 | Native spall scaling; 0.25 for heavy. |
| `costMultiplier` / `requestedCostMultiplier` | Nonnegative, response upper bound 1e12 | Requested price; balance floors may raise it. Example 10.5 is not 10.5 times RHA, whose effective multiplier is 2. |
| `heatRetention`, rod retentions | 0–1, constrained by global loss caps | Lower retention increases the extra response; full heavy defaults 0.50/0.85. |
| Root `maximumAdditionalHeatLoss` | 0–0.60 | Cumulative extra HEAT loss cap. |
| Root `maximumAdditionalKineticLoss` | 0–0.35 | Cumulative extra kinetic loss cap. |
| Thickness limits | mm; 0.1–1000, max >= min | Reactive eligibility, heavy defaults 60–80. |
| Curve angle / weight | Degrees / 0–1 | Sorted response weights. HEAT uses angularCurve; heavyEra additionally requires kineticAngularCurve. Old kinds must not include that field. |
| `cellPitchM` | Metres; strict ERA bounds in validator | Finite cell size. Keep 0.25 for these ERA recipes; changing pitch can change cell identity/spent behaviour. |

Only the existing kinds glassTextolite, nera, lightEra, heavyEra and passiveComposite are supported. Do not invent kinds, keys or hidden shell physics. All response recipes are Cold War-only (`minimumEra: coldwar`). The complete example carries the required remaining fields and defaults. Unknown fields, duplicate IDs, invalid curves, trailing commas in the response JSON, or a recipe outside the root loss caps can disable the optional response catalogue. Native Technology files permit trailing commas; use strict valid JSON for all examples and config edits.

## Update, recovery and uninstall

Back up the catalogue and native Technology files before changing them. Preserve root enabled/caps/preconditioning and existing entries when adding heavy ERA. Restart both consumers after edits; a material-side reload does not reload Shell's startup catalogue. Check LogOutput.log for skipped recipes, unknown IDs and parser errors. Restore the paired DLLs and pre-update catalogue together for rollback. Before uninstalling, replace custom materials on affected vehicles with stock materials and save; keep originals/backups if you want to return later.
## Native verification in 0.12.4

Heavy ERA against HEAT is verified in both Sample and Play, including same-cell consumption and new-Play reset. The lookup repair also serves the other recipes; their individual native behavior is not yet equally verified. Additional protection remains conditional on valid thickness, angle, native technology and matching passive recipe. Repeated Sample tests use separate preview cells and do not spend combat cells.
