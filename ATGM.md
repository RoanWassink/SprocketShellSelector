# ATGM guidance and motor settings

v0.11.0 adds AI support and three guided missile examples into Shell Selector. They are game approximations; the HEAT warhead budget is independent of flight speed.

## Controls

Select **SACLOS ATGM** for sight-ray guidance, or **MCLOS ATGM** for manual steering with W/S up/down and A/D left/right. Both are available at all calibres and nominally have 600 mm chemical penetration at 135 mm. Only the latest missile per vehicle is guided. The keyboard profile blocks native tank command dispatch during flight, clears drive/cruise and firing inputs, and releases the lock after impact, expiry, launcher loss or vehicle switch. The tank is not physically frozen. Enter your scope view before firing; scope toggling is a vehicle command and is blocked during manual guidance.

See [the JSON field table, formulas and examples](CUSTOM-SHELLS.md#custom-atgms). Fresh profiles use 50 m/s launch, 150 m/s² acceleration after 0.12 seconds, 200 m/s cruise, 20 degrees/second turn rate, 0.25 seconds guidance delay and 25 seconds lifetime. Fresh examples use a 4-second burn and 1 m/s² coast loss. Gun-launched SACLOS uses behavior `atgm_gun`, cannon launch speed × 0.2 capped at cruise, and 0.08-second motor delay. Before motor ignition, native gravity/drag affect ejection. These motor numbers are gameplay choices. Existing configured ATGMs are preserved; missing motor fields are materialized with constant-speed equivalents.

The optional module is controlled by `ATGM Experimental / Enabled` in `nl.roan.sprocket.shellselector.cfg`. If disabled or unavailable, missiles are native ballistic shots at launch speed with chemical impact, without powered flight or steering. `DiagnosticLogging = true` enables once-per-second COMMAND and NATIVE movement reports; default false keeps normal logs quieter. Launch and input-lock events remain logged.

## Guidance background

| System | Operator task | Mod status |
|---|---|---|
| MCLOS | Manually steer the missile while observing its flight and the target | Keyboard proxy, live tested |
| SACLOS | Keep the sight on the target; the guidance system supplies commands | Sight-ray gameplay proxy, live tested |
| Fire-and-forget | Lock before launch; an onboard seeker subsequently guides | Not implemented |

Original Malyutka is described as MCLOS by the [US Army equipment guide](https://odin.t2com.army.mil/WEG/Asset/b85f4d8139f4ba6f0ca235f1ab5690e4). [BDL's annual report](https://www.bdl-india.in/sites/default/files/AnnualReport2022-23.pdf) describes Konkurs-M as semi-automatic, optically tracked and wire-guided. Wire guidance describes the command link, not necessarily manual steering. [US Army Javelin history](https://history.redstone.army.mil/miss-javelin.html) describes an imaging-infrared seeker, target lock before launch and fire-and-forget. The mod does not model a physical wire, tracker or seeker.

## AI guidance and audio

AI retains native acquisition and target estimates. Its scoped intercept calculation uses missile lead without shell drop compensation; fresh aim rays steer the missile. User testing accepted AI with one missile at a time. A second launch from the same vehicle supersedes the previous missile even with separate launchers. Do not interpret `reason=superseded` as a tracking failure.

Launcher and gun-launched behaviors use separate embedded recordings supplied by RoanWassink, who confirmed ownership of both. The gun-launched clip has matched average loudness, a shorter fade-in and no silent tail. See [audio settings and provenance](audio/README.md). Exact audible balance against stock fire, ordinary-shell source reuse and all scene transitions have not been comprehensively validated.

## Validation

Both guidance modes were accepted by the user in live gameplay. Test 3 logs contained 3 keyboard-profile launches, 3 input locks and 3 unlocks, with keyboard commands and no ATGM warnings/errors. All 317 standalone managed checks pass, including safe migration, contextual validation, motor delay/acceleration/caps, legacy constant speed, turn limits and independent impact budgets. The native plugin compiles; the user also accepted the launch/acceleration build in live gameplay.

When tuning custom profiles, compare fixed soft launch and cannon mode with different propellant settings, steering while accelerating, impact, missed-shot lifetime and input restoration. Check COMMAND/NATIVE logs with diagnostics enabled. Retest a non-ATGM shell. The simulator tests missile impact only and cannot validate motor or guidance.

## Updating and rollback

Install only with Sprocket closed and keep one plugin DLL. Back up DLL, shells.json and plugin CFG. Existing profiles and tuned values are preserved; original experimental IDs remain for saved vehicle compatibility, while recognized stock experimental labels are renamed. Config migrations make an exact `.pre-v094-backup[-n]` before changes.

To roll back to v0.9.8 or an earlier ATGM test, close the game and restore **both DLL and matching shell JSON/CFG backups**. Old versions cannot parse newer ATGM fields. There is no new missile/launcher model, smoke trail, missile camera, target lock, top attack, tandem warhead or independent missile HE blast damage.
