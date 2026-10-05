<!-- sp-compat {"hamish.sprocket": "0.2.55.5", "bepinex.bepinex": "6.0.0-be.788"} -->

APFSDS, APHE, HE, HEAT, HESH and guided missiles, with configurable profiles and armour interactions.

## What changes for you

Profiles now respect era availability. Unmodified stock WWII HEAT is recalibrated; renamed or customized profiles retain their values. Adds threat-specific composite/NERA/ERA responses, including shared one-use heavy ERA cells against HEAT and APFSDS.

**Beta:** tested together in the Cold War pack. Armour-response values are bounded gameplay approximations, not exact historical protection or a guarantee against every shell.

## Requirements and update

Sprocket 0.2.55.5, Windows x64 and an already-working Sprocket Mod Loader / BepInEx 6 IL2CPP setup. **Loader not included. Quality of Life not required.**

Close the game, back up matching files and saves, then merge the ZIP's folders into the game directory. Keep one DLL per plugin and preserve customized configs/catalogues/WAV overrides. See [README](https://github.com/RoanWassink/SprocketShellSelector#readme) for exact use, controls, limitations and uninstall instructions.

[Support my ChatGPT budget and help me reverse engineer Sprocket to make more mods](https://www.paypal.com/donate/?hosted_button_id=7PE3SDBETXFQ6).

[Separate loader installation](https://github.com/Hans21223/Sprocket-Mod-Loader).


**Active armour dependency:** use Material Selector **0.4.4** and Shell Selector **0.12.3-heavyera.1** together, plus Cold War core **0.1.3** and the enabled armour-response catalogue. Older Shell consumers may reject the new heavy ERA catalogue. The standalone catalogue is an opt-in example; preserve and merge existing custom settings. The full pack supplies the matching pair.


**Era changes:** HESH, APFSDS and guided ATGM are Cold War-only; APHE/HEAT start in Earlywar. An imported earlier-era cannon retains its saved profile identity but does not receive an unavailable modern effect. Use the core-only download or full Cold War pack to access the new era.

