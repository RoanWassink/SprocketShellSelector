# Sprocket Shell Selector 0.13.2 — beta

Changes since the public 0.13.0 release:

- **Stronger full-calibre recoil:** HE, APHE and other non-rocket full-bore profiles now retain at least the native recoil of the same cannon and propellant charge. Penetration and damage balance are unchanged. APFSDS and ATGM recoil remain unchanged.
- **Cleaner damage feed:** crew and component messages report direct projectile and spall damage. Repeated messages from ongoing fires are excluded, and repeated hits on the same component within one shot are combined. Penetration and ERA activation remain visible.

## Requirements and update

For Sprocket **0.2.55.5** and **BepInEx 6 IL2CPP 6.0.0-be.788**. **Sprocket JSON Editor 0.1.0 is required and included.** Armour responses with Material Selector require **Material Selector 0.5.0** and the matching response catalogue.

Close the game, back up the files you replace, and extract the installation ZIP into the folder containing Sprocket.exe. Keep one copy of each plugin. Preserve custom CFG, shell/module JSON, keybinds, audio overrides and edited Technology files. No configuration changes are required for these fixes. The source ZIP is for developers.

[Support my ChatGPT budget and help me reverse engineer Sprocket to make more mods.](https://www.paypal.com/donate/?hosted_button_id=7PE3SDBETXFQ6)

<!-- sp-compat {"hamish.sprocket": "0.2.55.5", "bepinex.bepinex": "6.0.0-be.788"} -->
