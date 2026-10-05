using HarmonyLib;
using Sprocket.Vehicles;
using Sprocket.VehicleDesigner;
using Sprocket.VehicleDesigner.ArmourEditing;
using Sprocket.VehicleDesigner.Access;
namespace SprocketShellSelector;
[HarmonyPatch]
internal static class RuntimeShellEra
{
    private static readonly HashSet<string> warnings=new();
    internal static void WarnCached(IntPtr source)
    {
        if(warnings.Add("cached:"+source))Plugin.ModLog.LogWarning("[Shell era] Cached custom projectile has no native input for this source; shot withheld.");
    }
    private sealed record PreviewBinding(IVehicleEditor Editor,IVehicleOverlayApplier Applier);
    private static readonly Dictionary<IntPtr,PreviewBinding> previewOwners=new();
    internal static string? Era(IVehicleGateway? owner)
    {
        try{return owner?.DesignInfo is {} info?VehicleClassifications.GetEra(info.Date)?.Name:null;}
        catch{return null;}
    }
    internal static bool Allowed(ShellProfile profile,IVehicleGateway? owner)=>ShellEraPolicy.Allowed(profile,Era(owner));
    internal static ShellProfile? Resolve(ShellProfile? stored,IVehicleGateway? owner)
    {
        var era=Era(owner);var effective=ShellEraPolicy.Effective(stored,era);
        if(stored!=null&&effective==null&&warnings.Add(stored.Id+":"+era))
            Plugin.ModLog.LogWarning($"[Shell era] Stored '{stored.Id}' unavailable in '{era??"unknown"}'; native ammunition used, stored ID retained.");
        return effective;
    }
    internal static IReadOnlyList<ShellProfile> Available(IReadOnlyList<ShellProfile> all,IVehicleGateway? owner)=>all.Where(p=>Allowed(p,owner)).ToArray();
    internal static Il2CppSystem.Collections.Generic.List<string> Labels(IReadOnlyList<ShellProfile> profiles)
    {
        var labels=new Il2CppSystem.Collections.Generic.List<string>();labels.Add("Vanilla ammunition");
        foreach(var p in profiles)labels.Add(p.Label);return labels;
    }
    internal static IVehicleGateway? PreviewOwner(ArmourOverlay? overlay)
    {
        try{return overlay!=null&&previewOwners.TryGetValue(overlay.Pointer,out var binding)&&binding.Applier.Current?.Pointer==overlay.Pointer?binding.Editor.Target:null;}
        catch{return null;}
    }
    [HarmonyPrefix,HarmonyPatch(typeof(VehicleEditor),nameof(VehicleEditor.Detach))]
    private static void Detach()=>previewOwners.Clear();
    [HarmonyPostfix,HarmonyPatch(typeof(VehicleEditorOverlayControlPointerOperator),nameof(VehicleEditorOverlayControlPointerOperator.Update))]
    private static void OverlayOwner(VehicleEditorOverlayControlPointerOperator __instance)
    {
        var editor=__instance.editor;var applier=__instance.overlayApplier;
        var overlay=applier?.Current?.TryCast<ArmourOverlay>();
        if(overlay==null)return;
        if(editor?.Target==null||applier==null)previewOwners.Remove(overlay.Pointer);else previewOwners[overlay.Pointer]=new(editor,applier);
    }
}
