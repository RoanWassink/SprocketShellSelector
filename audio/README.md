# ATGM launch audio

Both original recordings were supplied by RoanWassink, who explicitly confirmed ownership before this release. They are included as this project's original audio assets under its MIT license.

- atgm.wav: launcher-style sound from the supplied TOW-style recording. Mono conversion with reduced gain and short fades.
- atgm_gun.wav: separate gun-launched recording. Mono processing, average loudness matched to the launcher clip, shorter fade-in and silent tail removed.

Offline matching does not establish perceived equality with stock cannon fire. Native muzzle position, distance attenuation, playback delay and mixer settings still affect the result. The latest gun-launched audio is the version installed for the creator after the normalization request; final audible balancing has not been exhaustively validated.

In BepInEx/config/nl.roan.sprocket.shellselector.cfg:

```ini
[ATGM Audio]
Enabled = true
VolumeMultiplier = 1
```

Enabled=false uses native sounds. VolumeMultiplier accepts 0–2 (default 1), scaling custom samples; restart after edits. The module has its own patch owner so an audio setup failure does not intentionally disable missile flight/impact.

Optional PCM16 mono/stereo WAV overrides:

```text
BepInEx/config/sprocket.shellselector.audio/atgm.wav
BepInEx/config/sprocket.shellselector.audio/atgm_gun.wav
```

Overrides take precedence over embedded sounds and are preserved on update. Back up/move a previous override to hear the release default. Do not redistribute another creator's replacement recording without their permission. No external WAV installation is required for bundled audio.
