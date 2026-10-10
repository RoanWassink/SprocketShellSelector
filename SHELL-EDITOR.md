# Design shells in game

Select a cannon, open its Shell profile panel and use the shell editor button. Expand an entry to edit it, or add/duplicate a starting example. Your label is editable; keep the saved ID stable. Changes apply to all cannons using that profile.

| Group | Choices | What changes |
|---|---|---|
| Tail | Spin, fins, guided, none | Guidance and visible command-link settings. Spin/fins do not add a separate stability bonus. |
| Propulsion | Ballistic, rocket | Launch, motor delay, acceleration, burn and coast settings. |
| Body | Full bore, long rod, shaped charge | Penetrator dimensions and density. A shaped-charge body alone does not add a HEAT effect. |
| Effect | Kinetic, spall, blast, HEAT, HESH | The impact adapter. APHE/spall burst settings remain shared. |
| Carrier | Full bore, sabot, launcher, gun launch | Launch context and native technology requirements. |

Fields display their supported units and bounds. Availability also follows your vehicle's native Technology records. A rocket motor and guidance do not silently change the impact effect. Some combinations cannot be applied; Save retains unsupported designs as drafts and preserves the live catalogue rather than guessing an impact behaviour.

## Save, import and recovery

The compiled runtime catalogue is `BepInEx/config/sprocket.shellselector.shells.json`. The editor keeps authoring data in `BepInEx/config/sprocket.shellselector.modules.json`. Preserve **both** on updates. Save validates, creates transaction backups next to changed files and refreshes the compatible catalogue in game. If refresh reports failure, retain the backup, close the game and restore the affected files before testing. For outside edits, close the game first; reopening the editor rebuilds its draft.

To use Import, place valid modular or supported current legacy shell JSON in `BepInEx/config/sprocket.shellselector.modules.import.json`. Import opens a draft; files do not change until Save. Keep unique IDs and do not overwrite a custom catalogue with an example. Existing runtime profile IDs remain unchanged. Check CUSTOM-SHELLS.md for a complete legacy profile and accepted field values.

The optional `examples/tow-basic-wire.profile-fragment.json` is **one profile**, not a complete catalogue. Add it to your catalogue's profile array with a unique ID, or import a complete supported catalogue containing it. It is a gameplay example, not an exact TOW specification. Use guidance and rocket modules when designing the equivalent in the editor.

## Damage feed

Enable the damage feed from the cannon panel or `[Damage feed] Enabled = true` in `BepInEx/config/sprocket.shellselector.cfg`. `Corner` accepts `right` (default) or `left`. `Console debug` is false by default. It reports detected penetration, remaining penetration, ERA activation and direct crew/component damage from projectiles and spall in Play. Repeated component messages within one shot are combined; ongoing fire damage is excluded. It may not account for every secondary fragment or damage event.

[Support my ChatGPT budget and help me reverse engineer Sprocket to make more mods.](https://www.paypal.com/donate/?hosted_button_id=7PE3SDBETXFQ6)
