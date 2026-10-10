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
    private static readonly string[] behaviors={"ap","he","aphe","heat","hesh","apfsds","atgm","atgm_gun"};
    internal static ShellEraContext? Context(IVehicleGateway? owner,IEnumerable<string>? requirements=null)
    {
        try
        {
            if(owner?.DesignInfo is not {} info||owner.Tech is not {} tech||VehicleClassifications.eras is not {} eras)return null;
            var names=new string[eras.Length];var starts=new ShellNativeDate[eras.Length];
            for(var i=0;i<eras.Length;i++)
            {
                if(eras[i]==null)return null;
                names[i]=eras[i].Name;var start=eras[i].StartDate;starts[i]=new(start.Year,start.Month,start.Day);
            }
            // Native classification, including the core's last-era boundary fix,
            // is authoritative. Do not substitute any date into the tech frame.
            var index=VehicleClassifications.GetEraIndex(info.Date);
            var raw=info.Date;var date=new ShellNativeDate(raw.Year,raw.Month,raw.Day);
            var available=new HashSet<string>(StringComparer.Ordinal);double? heatFactor=null;
            foreach(var behavior in behaviors)
                if(tech.TryGetTech("shellSelector_"+behavior,out var shellTech)&&shellTech!=null&&shellTech.GetBool("enabled",false))
                {
                    available.Add(behavior);
                    if(behavior=="heat")
                    {
                        var factor=shellTech.GetFloat("penetrationPerCalibre",float.NaN);
                        if(float.IsFinite(factor)&&factor>0&&factor<=20)heatFactor=factor;
                    }
                }
            var enabledTechnologies=available.Select(b=>"shellSelector_"+b).ToHashSet(StringComparer.Ordinal);
            foreach(var id in requirements??Array.Empty<string>())
                if(tech.TryGetTech(id,out var required)&&required!=null&&required.GetBool("enabled",false))enabledTechnologies.Add(id);
            var context=new ShellEraContext(date,names,starts,index,available,heatFactor,enabledTechnologies);
            return ShellEraPolicy.Valid(context)?context:null;
        }
        catch{return null;}
    }
    internal static string? Era(IVehicleGateway? owner)=>Context(owner) is {} c?c.Names[c.Index]:null;
    internal static bool HasNativeContext(IVehicleGateway? owner)=>Context(owner)!=null;
    internal static bool Allowed(ShellProfile profile,IVehicleGateway? owner)
    {
        var c=Context(owner,ShellBalance.RequiredTechnologyIds(profile));
        return ShellEraPolicy.Allowed(profile,c)&&(!ShellChemicalBudget.IsStock(profile)||c?.HeatFactor!=null);
    }
    internal static double ChemicalBudget(ShellProfile p,double calibre,IVehicleGateway? owner)
    {
        var c=Context(owner,ShellBalance.RequiredTechnologyIds(p));
        return ShellChemicalBudget.Resolve(p,calibre,c?.HeatFactor,ShellEraPolicy.Allowed(p,c));
    }
    internal static string ChemicalDescription(ShellProfile p,IVehicleGateway? owner)=>ShellChemicalBudget.Description(p,Context(owner)?.HeatFactor);
    internal static ShellProfile? Resolve(ShellProfile? stored,IVehicleGateway? owner)
    {
        var era=Era(owner);var effective=stored!=null&&Allowed(stored,owner)?stored:null;
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
