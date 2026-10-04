using HarmonyLib;
using Sprocket.DamageModelling;
using Sprocket.UI;
using Sprocket.VehicleDesigner.ArmourEditing;
using Sprocket.VehicleDesigner.UI;
using Sprocket.Vehicles;
namespace SprocketShellSelector;
[HarmonyPatch]
internal static class RuntimeArmourSimulator
{
    private sealed record Choice(ArmourOverlay Overlay,string Id);
    private static readonly Dictionary<IntPtr,Choice> Choices=new();
    private static readonly Il2CppSystem.Collections.Generic.List<string> Labels=new();
    [ThreadStatic] private static ArmourOverlay? activeOverlay;
    [ThreadStatic] internal static bool DrawingShellChoice;
    private static IReadOnlyList<ShellProfile> SimulatorProfiles => RuntimeShellSelection.Profiles.Where(p=>p.Behavior!="he").ToArray();
    internal static void Configure()
    {
        Labels.Clear();Labels.Add("Vanilla ammunition");
        foreach(var p in SimulatorProfiles) Labels.Add(p.Label);
    }
    private static ShellProfile? Selected(ArmourOverlay? overlay) => RuntimeShellSelection.Enabled && overlay!=null && Choices.TryGetValue(overlay.Pointer,out var choice)
        ? RuntimeShellSelection.Profiles.FirstOrDefault(p=>p.Id==choice.Id) : null;
    private static float EffectivePenetration(ShellProfile profile,ArmourOverlay overlay) => profile.Behavior is "heat" or "hesh" or "atgm" or "atgm_gun"
        ? Math.Min(overlay.Penetration,(float)ShellBalance.ChemicalPenetration(profile,overlay.Calibre)) : overlay.Penetration;
    [HarmonyPrefix,HarmonyPatch(typeof(ArmourOverlayConfig),nameof(ArmourOverlayConfig.Draw))]
    private static void Limits(ArmourOverlayConfig __instance)
    {
        __instance.Penetration.SetLimits(__instance.Penetration.Min,2000f);
        __instance.Penetration.softMax=2000f;
    }
    [HarmonyPostfix,HarmonyPatch(typeof(ArmourOverlayConfig),nameof(ArmourOverlayConfig.Draw))]
    private static void Draw(ArmourOverlayConfig __instance,IGUILayout __0)
    {
        if(!RuntimeShellSelection.Enabled) return;
        try
        {
            var layout=__0;
            var ui=layout.TryCast<IGUIElementDrawer>();if(ui==null)return;
            layout.EndAllDropdowns();
            layout.BeginDropdown("Simulator shell profile",true,Ui.BoolCallback(_=>{}));
            try
            {
                var selected=Selected(__instance.overlay);
                var previousDrawing=DrawingShellChoice;
                DrawingShellChoice=true;
                try {ui.Dropdown("Shell type",Labels.Cast<Il2CppSystem.Collections.Generic.IReadOnlyList<string>>(),
                    selected==null?0:SimulatorProfiles.ToList().FindIndex(p=>p.Id==selected.Id)+1,
                    Ui.IntCallback(index=>{
                        if(index<0||index>SimulatorProfiles.Count)return;
                        Choices[__instance.overlay.Pointer]=new(__instance.overlay,index==0?ShellProfiles.Vanilla:SimulatorProfiles[index-1].Id);
                        // Redraw overlay and invalidate the pointer simulation when profile changes.
                        __instance.overlay.RedrawRequired=true;
                    }),"Calibre is the full gun calibre. Penetration is the manually chosen RHA target, not a cannon performance prediction.");}
                finally{DrawingShellChoice=previousDrawing;}
                if(selected!=null)
                    ui.InfoField("Penetration is chosen by slider; chemical profiles also obey a calibre-scaled budget. Select live shells separately on the cannon.",2);
            }
            finally{layout.EndAllDropdowns();}
        }
        catch(Exception ex){Plugin.ModLog.LogError("[Armour Simulator] UI: "+ex);}
    }
    private sealed record UpdateState(ArmourOverlay? PreviousOverlay,ShellImpactContext? PreviousImpact);
    private static readonly Dictionary<IntPtr,string> LastChoices=new();
    [HarmonyPrefix,HarmonyPatch(typeof(ArmourOverlayPointerOperator),nameof(ArmourOverlayPointerOperator.Update))]
    private static void Begin(ArmourOverlayPointerOperator __instance,out UpdateState __state)
    {
        __state=new(activeOverlay,RuntimeSpall.Impact);
        activeOverlay=__instance.overlay.TryCast<ArmourOverlay>();
        var selected=Selected(activeOverlay);
        var id=selected?.Id??ShellProfiles.Vanilla;
        if(!LastChoices.TryGetValue(__instance.Pointer,out var last)||last!=id){__instance.Clear();LastChoices[__instance.Pointer]=id;}
        RuntimeSpall.Impact=selected==null?null:new(){ProfileId=selected.Id,Profile=selected,GunDiameter=activeOverlay!.Calibre*.001f};
    }
    [HarmonyFinalizer,HarmonyPatch(typeof(ArmourOverlayPointerOperator),nameof(ArmourOverlayPointerOperator.Update))]
    private static void End(UpdateState __state){activeOverlay=__state.PreviousOverlay;RuntimeSpall.Impact=__state.PreviousImpact;}
    [HarmonyPostfix,HarmonyPatch(typeof(ArmourOverlay),"get_SampleSettings")]
    private static void Sample(ArmourOverlay __instance,ref ArmourOverlaySampleSettings __result)
    {
        var profile=Selected(__instance);if(profile==null)return;
        // The native colour sampler only supports integer calibre/AP mass. Keep its chosen RHA penetration equal,
        // then replace the exact geometry/mass at the detailed Penetrate call below.
        var mm=(ushort)Math.Clamp(Math.Round(__instance.Calibre*profile.Settings.DiameterRatio),1,ushort.MaxValue);
        __result=new ArmourOverlaySampleSettings(mm,PenetrationUtils.ComputeRequiredPenetrationSpeed(mm,(float)(1.59e-5*mm*mm*mm),EffectivePenetration(profile,__instance),__instance.PenetratorConstant));
    }
    [HarmonyPrefix,HarmonyPatch(typeof(CompoundStructure),nameof(CompoundStructure.Penetrate))]
    private static void Projectile(ref PenetratorInfo penetrator)
    {
        var overlay=activeOverlay;var profile=Selected(overlay);if(profile==null)return;
        try
        {
            var dart=ShellBallistics.Calculate(overlay!.Calibre,800,overlay.PenetratorConstant,ShellBalance.BallisticSettings(profile));
            if(ShellBalance.ImpactBehavior(profile)=="heat") dart=dart with {Diameter=overlay.Calibre*.001f*.05f,Mass=dart.Mass*.05f};
            var speed=PenetrationUtils.ComputeRequiredPenetrationSpeed(dart.Diameter*1000,dart.Mass,EffectivePenetration(profile,overlay),dart.PenetratorConstant);
            var direction=penetrator.Velocity.normalized;
            if(!float.IsFinite(speed)||speed<=0||direction.sqrMagnitude<.5f)throw new InvalidOperationException("Invalid synthetic simulator shot.");
            // Preserve position and normalization; the manual slider fixes penetration, so speed is inverted from it.
            penetrator=new PenetratorInfo(dart.Mass,dart.Diameter,penetrator.Density,penetrator.MaxNormalizationAngle,dart.PenetratorConstant,penetrator.Position,direction*speed);
            Plugin.ModLog.LogInfo($"[Armour Simulator] {profile.Id} gun={overlay.Calibre}mm dart={dart.Diameter*1000:0.0}mm requested={overlay.Penetration:0}mm equivalentSpeed={speed:0}m/s");
        }
        catch(Exception ex){Plugin.ModLog.LogError("[Armour Simulator] Shot: "+ex);}
    }
}
