# ATGM launch audio

The release retains the user-supplied launcher and gun-launched recordings embedded in the DLL. Launcher audio originated from `BGM-71 Tow launch sound.wav`; gun-launched audio from `ATGM_launch_02_long_impact.wav`. The gun-launched clip was trimmed/level-adjusted during preparation. This release does not change either audio file.

The code's MIT licence does not assign a licence to the original recordings. Optional user PCM16 WAV overrides take priority over embedded clips. Configure `[ATGM Audio] Enabled` and `VolumeMultiplier` in `sprocket.shellselector.cfg`; restart after changing settings. Native positional attenuation and mixer handling are retained.
