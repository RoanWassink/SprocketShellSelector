using BepInEx;
using HarmonyLib;
using Sprocket.DamageModelling;
using Sprocket.Vehicles;
using Sprocket.Vehicles.PlateStructures;
using Sprocket.TechTrees;
using UnityEngine;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
namespace SprocketShellSelector;

[HarmonyPatch]
internal static class RuntimeArmourResponses
{
    private sealed record Plate(PlateStructure Component,long Spawn,IntPtr Root);
    private sealed class Shot
    {
        internal readonly ArmourShotState State=new();
        internal readonly EraCells Preview=new();
        internal (string Material,double Thickness,double Angle,Vector3 Exit,string Key)? Pending;
        internal Vector3? SteelExit;
    }
    private sealed record Scope(PenetrationSimulation Sim,short Index,CompoundStructure Structure,Shot Shot,int CountBefore,float SpeedBefore)
    {internal float ResidualSpeedRatio=1;}
    private static ArmourResponseCatalogue? catalogue;
    private static readonly Dictionary<IntPtr,long> spawns=new();
    private static readonly EraCells combat=new();
    private static long generation;
    private static readonly HashSet<string> warnings=new();
    private static int diagnostics;
    private static int cellDiagnostics;
    private static int mainThread;
    [ThreadStatic] private static Scope? active;
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ShellImpactContext,Dictionary<IntPtr,Shot>> shots=new();
    internal static bool Configure()
    {
        mainThread=Environment.CurrentManagedThreadId;
        var path=Path.Combine(Paths.ConfigPath,"sprocket.armour.responses.json");
        if(!File.Exists(path)){Plugin.ModLog.LogInfo("[Armour response] Catalogue absent; native behaviour retained.");return false;}
        catalogue=ArmourResponses.Parse(File.ReadAllText(path));
        Plugin.ModLog.LogInfo($"[Armour response] enabled={catalogue.Enabled}; exact component VUIDs, scalar response only.");
        return catalogue.Enabled;
    }
    [HarmonyPrefix,HarmonyPatch(typeof(VehicleObject),nameof(VehicleObject.Release))]
    private static void Remove(VehicleObject __instance)
    {
        if(Environment.CurrentManagedThreadId!=mainThread)return;
        if(!spawns.Remove(__instance.Pointer,out var spawn))return;
        combat.RemoveSpawn(spawn);
    }
    [HarmonyPrefix,HarmonyPatch(typeof(PlateStructure),nameof(PlateStructure.Release))]
    private static void RemovePlate(PlateStructure __instance)
    {
        if(Environment.CurrentManagedThreadId!=mainThread)return;
        if(__instance.VehicleRoot is {} cellRoot&&spawns.TryGetValue(cellRoot.Pointer,out var cellSpawn))combat.RemoveElement(cellSpawn,__instance.VUID.Value);
    }
    [HarmonyPrefix,HarmonyPatch(typeof(CompoundStructure),nameof(CompoundStructure.SimulateFragment))]
    private static void Begin(CompoundStructure __instance,PenetrationSimulation sim,short index,out Scope? __state)
    {
        __state=active;active=null;
        if(Environment.CurrentManagedThreadId!=mainThread){return;}
        var context=RuntimeSpall.Impact;
        if(context==null||catalogue is not {Enabled:true}||index<0||index>=sim.FragmentCount){return;}
        if((sim.fragments[index].flags&FragmentFlag.OriginalPenetrator)==0){return;}
        var map=shots.GetOrCreateValue(context);
        if(!map.TryGetValue(sim.Pointer,out var shot))map[sim.Pointer]=shot=new();
        active=new(sim,index,__instance,shot,sim.FragmentCount,sim.fragments[index].speed);
        
    }
    [HarmonyFinalizer,HarmonyPatch(typeof(CompoundStructure),nameof(CompoundStructure.SimulateFragment))]
    private static void End(Scope? __state)
    {
        try
        {
            var current=active;
            if(current is not {ResidualSpeedRatio:<1})return;
            // Native caches parent KE before GetNextMaterialBlock. Its new original
            // continuation speed must carry the same energy reduction as the array.
            for(var i=current.CountBefore;i<current.Sim.FragmentCount;i++)
            {
                var child=current.Sim.fragments[i];
                if(child.parentIndex!=current.Index||(child.flags&FragmentFlag.OriginalPenetrator)==0)continue;
                child.speed=ArmourResponseModel.ResidualSpeed(child.speed,current.ResidualSpeedRatio);
                current.Sim.fragments[i]=child;
            }
        }
        catch(Exception ex){if(warnings.Count<32&&warnings.Add("energy:"+ex.Message))Plugin.ModLog.LogWarning("[Armour response] Residual correction unavailable: "+ex.Message);}
        finally{active=__state;}
    }
    [HarmonyPostfix,HarmonyPatch(typeof(StructureIntersection),nameof(StructureIntersection.GetNextMaterialBlock))]
    [HarmonyPriority(Priority.Last)]
    private static void Layer(MaterialBlock __result,Il2CppStructArray<StructureIntersection> __0,int __1)
    {
        try{Apply(__result,__0,__1);}catch(Exception ex){if(warnings.Count<32&&warnings.Add(ex.Message))Plugin.ModLog.LogWarning("[Armour response] Hit skipped: "+ex.Message);}
    }
    private static void Apply(MaterialBlock block,Il2CppStructArray<StructureIntersection> hits,int count)
    {
        var scope=active;var context=RuntimeSpall.Impact;var config=catalogue;
        if(scope==null||context==null||config==null||!block.Valid||block.MaterialIndex<0)return;
        var entry=block.EnterIntersectionIndex;var exit=block.ExitIntersectionIndex;
        if(entry<0||exit<0||entry>=count||exit>=count||entry>=hits.Length||exit>=hits.Length)return;
        var first=hits[entry];var last=hits[exit];var sim=scope.Sim;
        if(first.objectIndex<0||first.objectIndex>=sim.objects.Length)return;
        // Require one resolved object, not a blended material block spanning multiple components.
        var objectCount=block.IntersectedObjectIndices?.Length??0;
        if(objectCount>1){return;}
        // Preview can omit the supplementary object list. Require the entire native
        // intersection span to belong to one object, including the exit boundary.
        if(objectCount==0)
        {
            // Native scans this unsorted buffer by distance (GetNextMaterialBlock
            // RVA1d7e290..1d7e418). Entry/exit array indices need not be ordered.
            if(!float.IsFinite(first.distance)||!float.IsFinite(last.distance)||last.distance<=first.distance)
            {return;}
            if(last.objectIndex!=first.objectIndex){return;}
            for(var i=0;i<count;i++)
            {
                var hit=hits[i];
                if(hit.distance>=first.distance&&hit.distance<=last.distance&&hit.objectIndex!=first.objectIndex)
                {return;}
            }
        }
        var entity=sim.objects[first.objectIndex].ID;
        Plate? plate=null;
        if(!context.LiveImpact)
        {
            if(plate==null)
            {
            // Resolve against the currently bound editor owner, not a global ID cache.
            var owner=RuntimeArmourSimulator.CurrentOwner;
            var items=owner?.ObjectReader?.Items;
            PlateStructure? exact=null;
            if(items!=null)for(var i=0;i<owner!.ObjectReader.Count;i++)
            {
                var item=items[i];if(item?.behaviours is not {} components)continue;
                for(var b=0;b<components.Length;b++)
                {
                    var candidate=components[b]?.TryCast<PlateStructure>();
                    // ObjectInfo.ID is StructureContext.ObjectID, passed from the
                    // component VUID, not the container EntityID (native1e7991d,
                    // 1ff7eff,1d77cf9). Search exact bound-owner components only.
                    if(candidate==null||candidate.VUID.Value!=entity)continue;
                    if(exact!=null&&exact.Pointer!=candidate.Pointer){return;}
                    exact=candidate;
                }
            }
            if(exact!=null&&exact.VehicleRoot is {} root)
            {
                if(!spawns.TryGetValue(root.Pointer,out var spawn))spawns[root.Pointer]=spawn=++generation;
                plate=new(exact,spawn,root.Pointer);
                
            }
            }
        }
        else
        {
            // Native combat damage is applied through VehicleHealthRegister's
            // target-local VUID reader; never search a global EntityID registry.
            var health=sim.damageApplier?.TryCast<VehicleHealthRegister>();
            if(health?.objectReader is {} reader&&reader.TryGet<PlateStructure>(new VUID(entity),out var target)&&target!=null&&target.VUID.Value==entity&&target.VehicleRoot is {} root)
            {
                if(!spawns.TryGetValue(root.Pointer,out var spawn))spawns[root.Pointer]=spawn=++generation;
                plate=new(target,spawn,root.Pointer);
                
            }
        }
        if(plate==null){return;}
        var component=plate.Component;var material=component.armourTechID;
        var tech=component.Vehicle?.Tech;var design=component.Vehicle?.DesignInfo;
        if(tech==null||design==null||!RuntimeShellEra.HasNativeContext(component.Vehicle)||!tech.TryGetTech(material,out var armourTech)||armourTech==null)return;
        var fragment=sim.fragments[scope.Index];var direction=fragment.direction.normalized;
        var normal=scope.Structure.GetIntersectionNormal(ref first).normalized;
        var cosine=Mathf.Abs(Vector3.Dot(direction,normal));
        if(cosine<.001f)return;
        var angle=Math.Acos(Math.Clamp(cosine,0,1))*180/Math.PI;
        var thickness=(last.distance-first.distance)*1000*cosine;
        var position=sim.SimToWorldSpacePoint(fragment.spawnPoint+direction*first.distance);
        var exitPosition=sim.SimToWorldSpacePoint(fragment.spawnPoint+direction*last.distance);
        var worldDirection=sim.SimToWorldSpaceDirection(direction).normalized;
        var shot=scope.Shot;
        // Native next block is reached only by a continuing original fragment. Prove the
        // previous exit lies behind this entry; stopped projectiles never precondition.
        if(shot.Pending is {} pending&&Vector3.Dot(position-pending.Exit,worldDirection)>=-.0001f)
        {
            if(ArmourResponseModel.PerforatedSteel(shot.State,config.Preconditioning,pending.Material,pending.Thickness,pending.Angle,0,pending.Key))shot.SteelExit=pending.Exit;
            shot.Pending=null;
        }
        var key=$"{plate.Spawn}:{entity}:{first.triangleIndex}:{first.distance:R}:{last.distance:R}";
        if(context.Behavior=="apfsds"&&config.Preconditioning.SteelIds.Contains(material))
            shot.Pending=(material,thickness,angle,exitPosition,key);
        var response=config.Responses.FirstOrDefault(r=>r.CompatibleMaterialIds.Contains(material));
        if(response==null){return;}
        if(block.MaterialIndex>=sim.materials.Length)return;
        var passive=sim.materials[block.MaterialIndex];
        
        if(!ArmourResponses.PassiveMatches(response,passive.density,passive.rhaFactor,passive.spallFactor))
        {if(warnings.Count<32&&warnings.Add("recipe:"+material))Plugin.ModLog.LogWarning("[Armour response] Custom passive recipe differs for "+material+"; additional response skipped.");return;}
        var intact=false;
        if(ArmourResponseModel.ReactiveThreat(response,context.Behavior)&&thickness>=response.Geometry.MinNormalThicknessMm&&thickness<=response.Geometry.MaxNormalThicknessMm&&!shot.State.Applied.Contains(key)&&
            (response.Kind!="heavyEra"||ArmourResponseModel.AngularWeight(response,context.Behavior,angle)>0))
        {
            // Rotation-only local basis preserves metres even on a scaled plate.
            var rotation=Quaternion.Inverse(component.VehicleTransform.Rotation);
            var local=rotation*(position-component.VehicleTransform.Position);
            var localNormal=rotation*sim.SimToWorldSpaceDirection(normal);
            var axis=Math.Abs(localNormal.x)>Math.Abs(localNormal.y)?(Math.Abs(localNormal.x)>Math.Abs(localNormal.z)?0:2):(Math.Abs(localNormal.y)>Math.Abs(localNormal.z)?1:2);
            var u=axis==0?local.y:local.x;var v=axis==2?local.y:local.z;
            var sign=(axis==0?localNormal.x:axis==1?localNormal.y:localNormal.z)>=0?1:-1;
            var cell=new EraCell(plate.Spawn,entity,(axis+1)*sign,(int)Math.Floor(u/response.CellPitchM),(int)Math.Floor(v/response.CellPitchM));
            intact=(context.LiveImpact?combat:shot.Preview).Consume(cell);
            if(cellDiagnostics++<40)Plugin.ModLog.LogInfo($"[Armour cell] cell={cell} intact={intact} live={context.LiveImpact}");
            if(intact&&context.LiveImpact)context.ArmourExplosions.Add(position);
        }
        var gap=shot.SteelExit is {} steelExit?Vector3.Dot(position-steelExit,worldDirection)*1000:double.NaN;
        var retention=ArmourResponseModel.Apply(config,response,shot.State,key,context.Behavior,true,true,thickness,angle,gap,intact);
        
        if(retention>=1)return;
        var constant=sim.projectileProfiles[fragment.projectileProfileIndex].penetratorConstant;
        var remaining=fragment.GetBasePenetration(constant)*1000;
        var speed=PenetrationUtils.ComputeRequiredPenetrationSpeed(fragment.diameter*1000,fragment.mass,(float)(remaining*retention),constant);
        if(!float.IsFinite(speed)||speed<=0)return;
        fragment.speed=Math.Min(fragment.speed,speed);sim.fragments[scope.Index]=fragment;
        if(scope.SpeedBefore>0)scope.ResidualSpeedRatio=Math.Clamp(fragment.speed/scope.SpeedBefore,0,1);
        if(diagnostics++<40)Plugin.ModLog.LogInfo($"[Armour response] {response.ResponseId} VUID={entity} live={context.LiveImpact} angle={angle:0.0} normal={thickness:0.0} gap={gap:0.0} disturbance={shot.State.Disturbance:0.000} retention={retention:0.000} ERAintact={intact}");
    }
}
