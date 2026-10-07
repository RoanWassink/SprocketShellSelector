# Sprocket Shell Selector v0.12.5

Adds a dedicated square-tube **ATGM launcher** and **finite ATGM ammunition box**. Each launcher starts a new combat instance with one ready missile; subsequent shots require compatible reserve ammunition. Optional automatic box loading supports matching nearby launchers, native finite capacity and an assigned gunner.

Shell availability now follows native Technology dates and enabled records for each behavior. The nine included files provide AP, HE, APHE, HEAT, HESH, APFSDS and both ATGM launch types. Unchanged stock HEAT advances from 1.2 to 4 penetration per calibre through its dated technology examples; customized chemical profiles retain their own settings.

The plugin ID, CFG filename and Harmony owners now use **sprocket.shellselector**. An existing legacy CFG is copied intact only if the neutral CFG is absent, with the original retained for rollback. Saved shell/profile/part identifiers and custom JSON remain compatible. Dependent plugins must update their Shell Selector dependency lookup to the new ID.

## Installation

Close Sprocket and merge the installation ZIP into its game folder. Include the supplied Parts, Localization, icons and Technology files; preserve customized configuration and Technology files. See [README](README.md) for the optional automatic-feed setting, crew requirements and complete setup.

The existing missile flight, guidance, AI, audio, exhaust and armour-response mechanics are retained.

<!-- sp-compat {"hamish.sprocket": "0.2.55.5", "bepinex.bepinex": "6.0.0-be.788"} -->

[Support development](https://www.paypal.com/donate/?hosted_button_id=7PE3SDBETXFQ6) to help with the ChatGPT budget and reverse engineering.
