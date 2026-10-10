using BepInEx;
using SprocketEraBindings;
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
    private sealed record Plate(VehicleComponent Component,long Spawn,IntPtr Root,bool Placed=false,EraPartBinding? Binding=null);
    private sealed class Shot
    {
        internal readonly ArmourShotState State=new();
        internal readonly EraCells Preview=new();
        internal (string Material,double Thickness,double Angle,Vector3 Exit,string Key)? Pending;
        internal Vector3? SteelExit;
        internal bool PlacedHit;
    }
    private sealed record Scope(PenetrationSimulation Sim,short Index,CompoundStructure Structure,Shot Shot,int CountBefore,float SpeedBefore)
    {internal float ResidualSpeedRatio=1;}
    private static ArmourResponseCatalogue? catalogue;
    private static EraPartBindingCatalogue bindings=EraPartBindings.Parse("{\"schemaVersion\":1,\"matchMode\":\"componentId\",\"bindings\":[]}");
    private static bool bindingsStarted;
    private static readonly Dictionary<IntPtr,long> spawns=new();
    private static readonly EraCells combat=new();
    private static readonly PlacedEraState placedState=new();
    private static long generation;
    private static readonly HashSet<string> warnings=new();
    private static int diagnostics;
    private static int cellDiagnostics;
    private static int placedDiagnostics;
    private static int placedLiveTraceBudget=240,placedPreviewTraceBudget=80;
    internal static void PlacedTrace(string detail)
    {var live=RuntimeSpall.Impact?.LiveImpact==true||detail.Contains("live=True",StringComparison.Ordinal);if(live){if(placedLiveTraceBudget<=0)return;placedLiveTraceBudget--;}else{if(placedPreviewTraceBudget<=0)return;placedPreviewTraceBudget--;}try{Plugin.ModLog.LogInfo("[Placed ERA trace] "+detail);}catch{ /* Optional logging cannot alter impact simulation. */ }}
    private static int mainThread;
    [ThreadStatic] private static Scope? active;
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ShellImpactContext,Dictionary<IntPtr,Shot>> shots=new();
    internal static bool Configure()
    {
        mainThread=Environment.CurrentManagedThreadId;
        if(!bindingsStarted)
        {
            bindingsStarted=true;
            try{bindings=EraPartBindings.Parse(File.ReadAllText(Path.Combine(Paths.ConfigPath,"sprocket.era.bindings.json")));Plugin.ModLog.LogInfo($"[Placed ERA] Immutable exact binding snapshot: {bindings.Bindings.Count} cassettes.");}
            catch(Exception ex){Plugin.ModLog.LogWarning("[Placed ERA] Bindings unavailable; placed active classification disabled, oldplate adapter retained: "+ex.Message);}
        }
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
        combat.RemoveSpawn(spawn);placedState.RemoveSpawn(spawn);
    }
    [HarmonyPrefix,HarmonyPatch(typeof(PlateStructure),nameof(PlateStructure.Release))]
    private static void RemovePlate(PlateStructure __instance)
    {
        if(Environment.CurrentManagedThreadId!=mainThread)return;
        if(__instance.VehicleRoot is {} cellRoot&&spawns.TryGetValue(cellRoot.Pointer,out var cellSpawn))combat.RemoveElement(cellSpawn,__instance.VUID.Value);
    }
    internal static bool PlacedSpent(IntPtr root,int vuid)=>Environment.CurrentManagedThreadId==mainThread&&spawns.TryGetValue(root,out var spawn)&&placedState.Spent(combat,spawn,vuid);
    internal static bool CurrentPlaced(PlacedEraActivation e)=>spawns.TryGetValue(e.VehicleRootPointer,out var spawn)&&spawn==e.Spawn&&placedState.Spent(combat,spawn,e.ComponentVuid);
    internal static void ResetPlacedForEdit()
    {if(Environment.CurrentManagedThreadId==mainThread)placedState.Reset(combat);}
    [HarmonyPrefix,HarmonyPatch(typeof(VehicleObjectModel),nameof(VehicleObjectModel.Release))]
    private static void RemoveModel(VehicleObjectModel __instance)
    {
        if(Environment.CurrentManagedThreadId!=mainThread||!bindings.TryGetCassette(__instance.ComponentID,out _))return;
        if(__instance.VehicleRoot is {} root&&spawns.TryGetValue(root.Pointer,out var spawn))
        {combat.RemoveElement(spawn,__instance.VUID.Value);placedState.RemoveElement(spawn,__instance.VUID.Value);}
    }
    private static Plate? PlacedModel(PenetrationSimulation sim,int entity,bool live)
    {
        VehicleObjectModel? model=null;
        if(live)
        {
            var health=sim.damageApplier?.TryCast<VehicleHealthRegister>();
            if(health?.objectReader is {} reader&&reader.TryGet<VehicleObjectModel>(new VUID(entity),out var target))model=target;
        }
        else
        {
            var reader=RuntimeArmourSimulator.CurrentOwner?.ObjectReader;
            var items=reader?.Items;
            if(items!=null)for(var i=0;i<reader!.Count;i++)
            {
                var item=items[i];if(item?.behaviours is not {} components)continue;
                for(var j=0;j<components.Length;j++)
                {
                    var candidate=components[j]?.TryCast<VehicleObjectModel>();
                    if(candidate==null||candidate.VUID.Value!=entity||!bindings.TryGetCassette(candidate.ComponentID,out _))continue;
                    if(model!=null&&model.Pointer!=candidate.Pointer)return null;
                    model=candidate;
                }
            }
        }
        if(model==null||model.VUID.Value!=entity||!bindings.TryGetCassette(model.ComponentID,out var binding)||model.VehicleRoot is not {} root)return null;
        if(!spawns.TryGetValue(root.Pointer,out var spawn))spawns[root.Pointer]=spawn=++generation;
        if(placedDiagnostics++<24)Plugin.ModLog.LogInfo($"[Placed ERA identity] live={live} objectInfoID={entity} modelVUID={model.VUID.Value} component={model.ComponentID} root={root.Pointer} spawn={spawn}; exact target-local model resolved.");
        return new(model,spawn,root.Pointer,true,binding);
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
            if(current is {} observed&&observed.Shot.PlacedHit&&observed.Index>=0&&observed.Index<observed.Sim.FragmentCount)
            {
                try
                {
                var final=observed.Sim.fragments[observed.Index];
                var count=0;
                for(var j=observed.CountBefore;j<observed.Sim.FragmentCount;j++)
                {var child=observed.Sim.fragments[j];if(child.parentIndex==observed.Index&&(child.flags&FragmentFlag.OriginalPenetrator)!=0&&(child.flags&FragmentFlag.Killed)==0)count++;}
                PlacedTrace($"stage=native-fragment-end flags={final.flags} nativeKilled={(final.flags&FragmentFlag.Killed)!=0} originalContinuations={count} residualBudget={final.GetBasePenetration(observed.Sim.projectileProfiles[final.projectileProfileIndex].penetratorConstant)*1000:0.000}mm; status of this fragment only, not a proven full vehicle/ERA stop");
                }
                catch(Exception ex){PlacedTrace("stage=native-fragment-trace-unavailable "+ex.Message);}
            }
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
        var diagnosticEntity=sim.objects[first.objectIndex].ID;
        Plate? diagnosticModel=null;
        try{diagnosticModel=PlacedModel(sim,diagnosticEntity,context.LiveImpact);}catch{ /* Probe failures must not suppress native/old-plate processing. */ }
        void Trace(string reason){if(diagnosticModel!=null)PlacedTrace($"id={diagnosticEntity} live={context.LiveImpact} {reason}");}
        if(diagnosticModel!=null)
        {
            scope.Shot.PlacedHit=true;
            Trace($"stage=native-block targetModelResolved=True entry={entry} exit={exit} entryObject={first.objectIndex} exitObject={last.objectIndex} entryID={diagnosticEntity} exitID={(last.objectIndex>=0&&last.objectIndex<sim.objects.Length?sim.objects[last.objectIndex].ID:-1)} material={block.MaterialIndex}");
        }
        // Require one resolved object, not a blended material block spanning multiple components.
        double placedPhysicalDepth=0;
        if(diagnosticModel?.Component.TryCast<VehicleObjectModel>() is {} ownModel&&ownModel.Model?.CollisionMesh is {} ownMesh)
            {var scale=ownModel.GetModelScale();placedPhysicalDepth=PlacedPartDepth.Measure(ownMesh.vertices.Select(v=>new System.Numerics.Vector3(v.x,v.y,v.z)).ToArray(),ownMesh.triangles.ToArray(),new(scale.x,scale.y,scale.z));}
        bool ownBrick=PlacedBrickTrigger.Reached(diagnosticModel!=null,(sim.fragments[scope.Index].flags&FragmentFlag.OriginalPenetrator)!=0,
            (first.flags&IntersectionFlags.Enter)!=0,(first.flags&IntersectionFlags.Exit)!=0,first.distance,placedPhysicalDepth);
        if(diagnosticModel!=null&&!ownBrick){Trace($"reject=not-reached-native-cassette-entry flags={first.flags} distance={first.distance:0.000} physicalDepth={placedPhysicalDepth:0.000}");return;}
        if(ownBrick)Trace($"stage=reached-native-cassette physicalDepth={placedPhysicalDepth:0.000}mm entryFlags={first.flags}; no own-exit requirement, native passive block unchanged");
        var objectCount=block.IntersectedObjectIndices?.Length??0;
        if(!ownBrick&&objectCount>1){Trace($"reject=multi-object-block count={objectCount}");return;}
        // Preview can omit the supplementary object list. Require the entire native
        // intersection span to belong to one object, including the exit boundary.
        if(!ownBrick&&objectCount==0)
        {
            // Native scans this unsorted buffer by distance (GetNextMaterialBlock
            // RVA1d7e290..1d7e418). Entry/exit array indices need not be ordered.
            if(!float.IsFinite(first.distance)||!float.IsFinite(last.distance)||last.distance<=first.distance)
            {Trace("reject=invalid-span-distance");return;}
            if(last.objectIndex!=first.objectIndex){Trace("reject=exit-object-mismatch");return;}
            for(var i=0;i<count;i++)
            {
                var hit=hits[i];
                if(hit.distance>=first.distance&&hit.distance<=last.distance&&hit.objectIndex!=first.objectIndex)
                {Trace($"reject=mixed-span object={hit.objectIndex}");return;}
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
        if(plate==null)plate=diagnosticModel??PlacedModel(sim,entity,context.LiveImpact);
        if(plate==null){return;}
        var component=plate.Component;var material=plate.Placed?plate.Binding!.MaterialId:component.TryCast<PlateStructure>()!.armourTechID;
        var tech=component.Vehicle?.Tech;var design=component.Vehicle?.DesignInfo;
        if(tech==null||design==null||!RuntimeShellEra.HasNativeContext(component.Vehicle)||!tech.TryGetTech(material,out var armourTech)||armourTech==null){Trace($"reject=native-tech-context material={material} tech={tech!=null} design={design!=null}");return;}
        Trace($"stage=native-tech-pass material={material} date={design.Date}");
        var fragment=sim.fragments[scope.Index];var direction=fragment.direction.normalized;
        var normal=scope.Structure.GetIntersectionNormal(ref first).normalized;
        var cosine=Mathf.Abs(Vector3.Dot(direction,normal));
        if(cosine<.001f){Trace("reject=invalid-grazing-normal");return;}
        var angle=Math.Acos(Math.Clamp(cosine,0,1))*180/Math.PI;
        var thickness=(last.distance-first.distance)*1000*cosine;
        Trace($"stage=geometry thicknessNormal={thickness:0.000}mm path={((last.distance-first.distance)*1000):0.000}mm angle={angle:0.00}deg normal=({normal.x:0.000},{normal.y:0.000},{normal.z:0.000})");
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
        var response=plate.Placed?config.Responses.FirstOrDefault(r=>r.ResponseId==plate.Binding!.ResponseId):config.Responses.FirstOrDefault(r=>r.CompatibleMaterialIds.Contains(material));
        if(response==null||plate.Placed&&!plate.Binding!.MatchesRecipe(response.ResponseId,response.Kind,response.CompatibleMaterialIds)){Trace($"reject=missing-or-wrong-recipe material={material} response={response?.ResponseId??"absent"}");return;}
        var passiveIndex=ownBrick?sim.objects[first.objectIndex].materialIndex:block.MaterialIndex;
        if(passiveIndex<0||passiveIndex>=sim.materials.Length){Trace("reject=material-index");return;}
        var passive=sim.materials[passiveIndex];
        
        if(!ArmourResponses.PassiveMatches(response,passive.density,passive.rhaFactor,passive.spallFactor))
        {Trace($"reject=passive-mismatch density={passive.density:0.000} rha={passive.rhaFactor:0.000} spall={passive.spallFactor:0.000}");if(warnings.Count<32&&warnings.Add("recipe:"+material))Plugin.ModLog.LogWarning("[Armour response] Custom passive recipe differs for "+material+"; additional response skipped.");return;}
        Trace($"stage=recipe-pass response={response.ResponseId} density={passive.density:0.000} rha={passive.rhaFactor:0.000} spall={passive.spallFactor:0.000}");
        var eligibleThreat=ArmourResponseModel.ReactiveThreat(response,context.Behavior);
        var eligibilityThickness=ownBrick?placedPhysicalDepth:thickness;
        var eligibleGeometry=eligibilityThickness>=response.Geometry.MinNormalThicknessMm&&eligibilityThickness<=response.Geometry.MaxNormalThicknessMm;
        var angularWeight=ArmourResponseModel.AngularWeight(response,context.Behavior,angle);
        Trace($"stage=eligibility behavior={context.Behavior} threat={eligibleThreat} geometry={eligibleGeometry} calibrationDepth={eligibilityThickness:0.000}mm nativeChordNormal={thickness:0.000}mm allowed={response.Geometry.MinNormalThicknessMm:0.000}..{response.Geometry.MaxNormalThicknessMm:0.000}mm angleWeight={angularWeight:0.000} alreadyApplied={shot.State.Applied.Contains(key)}");
        if(!eligibleThreat)Trace("reject=unsupported-threat");
        else if(!eligibleGeometry)Trace("reject=normal-thickness-outside-gate");
        else if(response.Kind=="heavyEra"&&angularWeight<=0)Trace("reject=zero-angular-weight");
        else if(shot.State.Applied.Contains(key))Trace("reject=duplicate-block-in-shot");
        var intact=false;EraCell? activatedCell=null;
        if(plate.Placed?PlacedBrickTrigger.CanConsume(eligibleThreat,eligibleGeometry,angularWeight,shot.State.Applied.Contains(key)):
            eligibleThreat&&eligibleGeometry&&!shot.State.Applied.Contains(key)&&(response.Kind!="heavyEra"||angularWeight>0))
        {
            // Rotation-only local basis preserves metres even on a scaled plate.
            var rotation=Quaternion.Inverse(component.VehicleTransform.Rotation);
            var local=rotation*(position-component.VehicleTransform.Position);
            var localNormal=rotation*sim.SimToWorldSpaceDirection(normal);
            var axis=Math.Abs(localNormal.x)>Math.Abs(localNormal.y)?(Math.Abs(localNormal.x)>Math.Abs(localNormal.z)?0:2):(Math.Abs(localNormal.y)>Math.Abs(localNormal.z)?1:2);
            var u=axis==0?local.y:local.x;var v=axis==2?local.y:local.z;
            var sign=(axis==0?localNormal.x:axis==1?localNormal.y:localNormal.z)>=0?1:-1;
            var cell=plate.Placed?PlacedEraState.Key(plate.Spawn,entity):new EraCell(plate.Spawn,entity,(axis+1)*sign,(int)Math.Floor(u/response.CellPitchM),(int)Math.Floor(v/response.CellPitchM));
            intact=(context.LiveImpact?combat:shot.Preview).Consume(cell);
            Trace($"stage=consume spawn={plate.Spawn} vuid={entity} first={intact} spentBefore={!intact} preview={!context.LiveImpact}");
            if(cellDiagnostics++<40)Plugin.ModLog.LogInfo($"[Armour cell] cell={cell} intact={intact} live={context.LiveImpact}");
            if(intact&&context.LiveImpact)
            {
                activatedCell=cell;
                RuntimeDamageFeed.EraActivated(sim,entity,cell,response.Kind,context.LiveImpact,intact);
                context.ArmourExplosions.Add(position);
                if(plate.Placed)
                {
                    placedState.Track(cell);
                    var worldNormal=sim.SimToWorldSpaceDirection(normal).normalized;
                    PlacedEra.Queue(context,new(component.Vehicle!.Pointer,plate.Root,plate.Spawn,entity,
                        new(position.x,position.y,position.z),new(worldNormal.x,worldNormal.y,worldNormal.z),response.ResponseId));
                }
            }
        }
        var gap=shot.SteelExit is {} steelExit?Vector3.Dot(position-steelExit,worldDirection)*1000:double.NaN;
        var retention=ArmourResponseModel.Apply(config,response,shot.State,key,context.Behavior,true,true,eligibilityThickness,angle,gap,intact);
        
        if(retention>=1){Trace($"stage=response retention={retention:0.000000} additionalLoss=none; no claim of armour stop");return;}
        var constant=sim.projectileProfiles[fragment.projectileProfileIndex].penetratorConstant;
        var remaining=fragment.GetBasePenetration(constant)*1000;
        var speed=PenetrationUtils.ComputeRequiredPenetrationSpeed(fragment.diameter*1000,fragment.mass,(float)(remaining*retention),constant);
        if(!float.IsFinite(speed)||speed<=0){Trace("reject=invalid-inverse-budget-speed");return;}
        fragment.speed=Math.Min(fragment.speed,speed);sim.fragments[scope.Index]=fragment;
        if(activatedCell is {} activated)RuntimeDamageFeed.EraBudget(sim,scope.Index,activated,remaining);
        Trace($"stage=response incomingBudget={remaining:0.000}mm retention={retention:0.000000} outgoingBudget={fragment.GetBasePenetration(constant)*1000:0.000}mm (before native plate consumption; reduction does not mean full stop)");
        if(scope.SpeedBefore>0)scope.ResidualSpeedRatio=Math.Clamp(fragment.speed/scope.SpeedBefore,0,1);
        if(diagnostics++<40)Plugin.ModLog.LogInfo($"[Armour response] {response.ResponseId} VUID={entity} live={context.LiveImpact} angle={angle:0.0} normal={thickness:0.0} gap={gap:0.0} disturbance={shot.State.Disturbance:0.000} retention={retention:0.000} ERAintact={intact}");
    }
}
