<!-- sp-compat {"hamish.sprocket": "0.2.55.5", "bepinex.bepinex": "6.0.0-be.788"} -->

## What changes for you

Fixes additional armour responses in both the armour simulator and live combat. Heavy ERA now applies its HEAT reaction, consumes the hit cell once, and starts fresh when a new Play vehicle spawns. Modern shell and armour availability uses design dates from 3 September 1945, including valid custom eras with other names.

**Beta.** 553 automatic checks and the exact release DLL passed Heavy ERA/HEAT Sample and live first-hit/spent-cell/Play-reset tests. Individual other armour responses and live custom-era scenarios have not received the same validation.

The shared availability cutoff is **3 September 1945**, inclusive, without a finite future cutoff for valid registered eras. Earlier eras keep their supported features. Saved dates and customized settings are preserved. This does not change historical balance coefficients.

## Install or update

Requires Sprocket **0.2.55.5**, Windows x64 and a working **Sprocket Mod Loader / BepInEx 6 IL2CPP 6.0.0-be.788** setup. **Loader not included; Quality of Life not required.**


Close Sprocket and back up saves and matching mod files. Merge the ZIP's **BepInEx** and, where included, **Sprocket_Data** folders into the folder containing Sprocket.exe. Keep one DLL per plugin. **Preserve existing configs, custom Technology/material files, thermal-models.json and WAV overrides.**

For the supplied armour reactions, update **Material Selector 0.4.5 and Shell Selector 0.12.4 together**, retain the corresponding material definitions and ensure your response catalogue is enabled. Material Selector alone provides passive properties. No new preset values are needed.

See the [installation and customization guide](https://github.com/RoanWassink/SprocketShellSelector#readme) for requirements, examples and rollback.

[Support my ChatGPT budget and help me reverse engineer Sprocket to make more mods](https://www.paypal.com/donate/?hosted_button_id=7PE3SDBETXFQ6).
