# Sprocket Shell Selector v0.10.0 — guided ATGMs

Adds two calibre-scaled missile profiles: sight-guided SACLOS ATGM and manually guided MCLOS ATGM. Both use HEAT impact/spaced-armour mechanics, nominally 600 mm penetration at 135 mm calibre. MCLOS uses WASD and blocks tank command input during the controllable missile flight, restoring control when it ends.

Custom profiles can choose fixed launch speed or native cannon muzzle speed with a multiplier, plus motor delay, acceleration, cruise speed, turn rate and guidance mode. Existing custom profiles retain their values and older constant-speed behavior; migrations create backups. Fresh examples launch at 50 m/s and accelerate to 200 m/s. These are gameplay proxies with no seeker/target lock, top attack or new launcher model.

Install one SprocketShellSelector.dll in BepInEx/plugins with the game closed. Defaults are embedded; example JSON is optional. See README.md, CUSTOM-SHELLS.md and ATGM.md. Back up saves and configuration. Rolling back requires the matching old DLL AND old shell JSON/CFG.

Validation: both guidance modes and the launch/acceleration build passed user gameplay testing. 301 standalone managed checks pass, covering flight, motor settings, configuration migration and existing shell behavior.
