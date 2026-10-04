using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using BepInEx.Configuration;
using HarmonyLib;
using Sprocket.Vehicles.Fires;
using Sprocket.Vehicles.Weapons.Cannons;
using UnityEngine;
using UnityEngine.Bindings;
using Object = UnityEngine.Object;
namespace SprocketShellSelector;

// Own Harmony owner; native muzzle visuals and sound mixing remain intact.
internal static class RuntimeLaunchAudio
{
    private static ConfigEntry<bool> enabled=null!;
    private static ConfigEntry<float> volume=null!;
    [ThreadStatic] private static string? firingType;
    [ThreadStatic] private static HashSet<IntPtr>? muzzleSources;
    private static readonly HashSet<string> Errors=new();
    internal static bool Enabled => enabled?.Value ?? false;
    internal static void Configure(ConfigFile cfg)
    {
        enabled=cfg.Bind("ATGM Audio","Enabled",true,"Replace ATGM launch sounds; other ammunition keeps native cannon sounds.");
        volume=cfg.Bind("ATGM Audio","VolumeMultiplier",1f,new ConfigDescription("Gain for custom launch samples. Restart after changes. Native distance attenuation and mixer volume remain in use.",new AcceptableValueRange<float>(0f,2f)));
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeSpan { internal IntPtr Begin; internal int Length; }
    private static readonly Dictionary<string, AudioClip> Clips = new();
    internal static void PreloadClips()
    {
        if (Marshal.SizeOf<NativeSpan>() != Marshal.SizeOf<ManagedSpanWrapper>() ||
            Marshal.OffsetOf<ManagedSpanWrapper>(nameof(ManagedSpanWrapper.begin)).ToInt32() != 0 ||
            Marshal.OffsetOf<ManagedSpanWrapper>(nameof(ManagedSpanWrapper.length)).ToInt32() != IntPtr.Size)
            throw new InvalidOperationException("Unity audio span layout differs from the inspected bindings.");
        foreach (var name in new[] { "atgm", "atgm_gun" })
        {
            if (Clips.TryGetValue(name, out var existing) && existing != null) continue;
            var file=Path.Combine(BepInEx.Paths.ConfigPath,"sprocket.shellselector.audio",name+".wav");
            using var stream = File.Exists(file) ? File.OpenRead(file) : typeof(RuntimeLaunchAudio).Assembly.GetManifestResourceStream($"ShellSelector.Audio.{name}.wav")
                ?? throw new FileNotFoundException($"Embedded launch sound {name} missing.");
            var wave = PcmWave.Read(stream, preserveStereo: false);
            var gain=float.IsFinite(volume.Value)?Math.Clamp(volume.Value,0f,2f):1f;
            for(var i=0;i<wave.Samples.Length;i++)wave.Samples[i]=Math.Clamp(wave.Samples[i]*gain,-.98f,.98f);
            var clip = AudioClip.Construct_Internal();
            // Managed references do not keep Unity assets alive across scene cleanup.
            clip.hideFlags = HideFlags.HideAndDontSave;
            var title = $"Shell Selector {name}".ToCharArray();
            var titlePin = GCHandle.Alloc(title, GCHandleType.Pinned);
            var samplePin = GCHandle.Alloc(wave.Samples, GCHandleType.Pinned);
            try
            {
                // Inspected Unity 6000.3 wrappers take a blittable {pointer, length}
                // span and a native Unity object pointer. Avoid the broken span helpers.
                var nativeLabel = new NativeSpan { Begin = titlePin.AddrOfPinnedObject(), Length = title.Length };
                ref var label = ref Unsafe.As<NativeSpan, ManagedSpanWrapper>(ref nativeLabel);
                var frames = wave.Samples.Length / wave.Channels;
                AudioClip.CreateUserSound_Injected(clip.m_CachedPtr, ref label, frames, wave.Channels, wave.Frequency, false);
                var nativeData = new NativeSpan { Begin = samplePin.AddrOfPinnedObject(), Length = wave.Samples.Length };
                ref var data = ref Unsafe.As<NativeSpan, ManagedSpanWrapper>(ref nativeData);
                if (!AudioClip.SetData_Injected(clip.m_CachedPtr, ref data, 0) ||
                    clip.samples != frames || clip.channels != wave.Channels || clip.frequency != wave.Frequency)
                    throw new InvalidOperationException($"Native audio upload failed for {name}.");
                Clips[name] = clip;
                Plugin.ModLog.LogInfo($"[ATGM audio] Custom {name} audio uploaded: {clip.samples} samples, {clip.channels} channels, {clip.frequency} Hz, {clip.length:0.00} s.");
            }
            catch { Object.Destroy(clip); throw; }
            finally { samplePin.Free(); titlePin.Free(); }
        }
    }

    [HarmonyPrefix,HarmonyPatch(typeof(CannonBehaviour),nameof(CannonBehaviour.FireInternal))]
    private static void BeforeFire(CannonBehaviour __instance,out string? __state)
    {
        __state=firingType;firingType=null;
        if(!Enabled)return;
        try
        {
            var behavior=RuntimeShellSelection.SelectedProfile(__instance)?.Behavior;
            if(ShellBalance.IsAtgm(behavior))firingType=behavior;
        }
        catch(Exception ex){Warn("fire context",ex);}
    }
    [HarmonyFinalizer,HarmonyPatch(typeof(CannonBehaviour),nameof(CannonBehaviour.FireInternal))]
    private static void AfterFire(string? __state)=>firingType=__state;

    [HarmonyPrefix,HarmonyPatch(typeof(MuzzleFlashEffect),nameof(MuzzleFlashEffect.Setup))]
    private static void BeforeMuzzle(MuzzleFlashEffect __instance,out HashSet<IntPtr>? __state)
    {
        __state=muzzleSources;muzzleSources=null;
        if(!Enabled || firingType==null)return;
        try
        {
            if(!Clips.TryGetValue(firingType,out var clip) || clip==null)PreloadClips();
            if(!Clips.TryGetValue(firingType,out clip) || clip==null)return;
            muzzleSources=new();
            if(__instance.audioSource!=null)muzzleSources.Add(__instance.audioSource.Pointer);
            if(__instance.longRangeAudioSource!=null)muzzleSources.Add(__instance.longRangeAudioSource.Pointer);
        }
        catch(Exception ex){muzzleSources=null;Warn("muzzle context",ex);}
    }
    [HarmonyFinalizer,HarmonyPatch(typeof(MuzzleFlashEffect),nameof(MuzzleFlashEffect.Setup))]
    private static void AfterMuzzle(HashSet<IntPtr>? __state)=>muzzleSources=__state;

    [HarmonyPrefix,HarmonyPatch(typeof(AudioSource),nameof(AudioSource.Play),new Type[]{})]
    private static void Play(AudioSource __instance)=>Replace(__instance);
    [HarmonyPrefix,HarmonyPatch(typeof(AudioSource),nameof(AudioSource.Play),new[]{typeof(double)})]
    private static void PlayDelayed(AudioSource __instance)=>Replace(__instance);
    private static void Replace(AudioSource source)
    {
        if(!Enabled || firingType==null || muzzleSources?.Contains(source.Pointer)!=true)return;
        try
        {
            if(!Clips.TryGetValue(firingType,out var clip) || clip==null)return;
            source.clip=clip;
            source.pitch=1;
            // Native near/distant source positioning, volume, delay and mixer are preserved.
            Plugin.ModLog.LogInfo($"[ATGM audio] Replaced native muzzle sound: {firingType}, source={source.Pointer}");
        }
        catch(Exception ex){Warn("sound replacement",ex);}
    }
    private static void Warn(string area,Exception ex)
    {if(Errors.Add(area))Plugin.ModLog.LogWarning($"[ATGM audio] {area}: {ex.Message}; native sound remains available.");}
}
