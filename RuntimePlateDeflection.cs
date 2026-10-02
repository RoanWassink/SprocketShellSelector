using HarmonyLib;
using Sprocket.DamageModelling;
namespace SprocketShellSelector;
[HarmonyPatch]
internal static class RuntimePlateDeflection
{
    [HarmonyPrefix,HarmonyPatch(typeof(CompoundStructure),nameof(CompoundStructure.SimulateFragment))]
    private static void Bend(CompoundStructure __instance,PenetrationSimulation sim,short index)
    {
        var s=RuntimeSpall.Settings;
        if(RuntimeSpall.Impact?.ProfileId!="apfsds" || !s.ApfsdsPlateDeflection || index<0 || index>=sim.FragmentCount)return;
        try
        {
            var f=sim.fragments[index];
            if((f.flags & FragmentFlag.OriginalPenetrator)==0 || (f.flags & (FragmentFlag.Killed|FragmentFlag.Embedded|FragmentFlag.Deflected))!=0 ||
                f.materialIndex<0 || f.hitStructureIndex<0 || f.hitStructureIndex>255 || f.hitTriangleIndex<0)return;
            var surface=(sim.Pointer,f.hitStructureIndex,f.hitTriangleIndex);
            if(RuntimeSpall.Impact.DeflectedSurfaces.Contains(surface))return;
            var hit=new StructureIntersection{structureIndex=(byte)f.hitStructureIndex,triangleIndex=f.hitTriangleIndex};
            var n=__instance.GetIntersectionNormal(ref hit);
            var d=f.direction;
            var bent=PlateDeflection.Bend(new(d.x,d.y,d.z),new(n.x,n.y,n.z),s.ApfsdsDeflectionMaximumDegrees,s.ApfsdsDeflectionMinimumObliquityDegrees);
            f.direction=new(bent.X,bent.Y,bent.Z);
            sim.fragments[index]=f;
            RuntimeSpall.Impact.DeflectedSurfaces.Add(surface);
            Plugin.ModLog.LogInfo($"[APFSDS Deflection] segment={index} native plate normal; direction={d} -> {f.direction}; speed/mass unchanged");
        }
        catch(Exception ex){Plugin.ModLog.LogError("[APFSDS Deflection] Native path retained: "+ex);}
    }
}
