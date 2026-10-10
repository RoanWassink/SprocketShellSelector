using BepInEx.Configuration;
using HarmonyLib;
using Sprocket;
using Sprocket.DamageModelling;
using Sprocket.PlayerControl;
using Sprocket.UI;
using Sprocket.Vehicles;
using Sprocket.Vehicles.CrewSystems;
using Sprocket.Vehicles.PlateStructures;
using PlayerController=Sprocket.Gameplay.VehicleControl.VehicleController;
using UnityEngine;
namespace SprocketShellSelector;
[HarmonyPatch]
internal static class RuntimeDamageFeed
{
    internal sealed class Shot
    {
        internal readonly int Number;internal readonly int NativeId;internal readonly float SpawnTime;internal readonly List<string> Lines=new();
        internal readonly Dictionary<(IntPtr,int),ComponentDamage> Damage=new();
        internal readonly DamageFeedActivations Activations=new();
        internal readonly Dictionary<IntPtr,(short Index,string Label)> Pending=new();
        internal readonly HashSet<string> Seen=new();
        internal Shot(int number,int nativeId,float spawnTime){Number=number;NativeId=nativeId;SpawnTime=spawnTime;}
        internal void Add(string key,string text){if(Lines.Count<12&&Seen.Add(key))Lines.Add(text);}
    }
    private sealed class Scope
    {
        internal readonly PenetrationSimulation Sim;internal readonly short Index;internal readonly int Count;
        internal string? Label;internal string? Key;internal bool Solid;
        internal Scope(PenetrationSimulation sim,short index){Sim=sim;Index=index;Count=sim.FragmentCount;}
    }
    private sealed record Pool(IntPtr Register,VehicleComponent Owner,bool Alive,float Value);
    [ThreadStatic] private static Shot? current;
    [ThreadStatic] private static Scope? active;
    private static readonly Dictionary<IntPtr,Pool> pools=new();
    internal static readonly DamageFeedBuffer Buffer=new();
    private static ConfigEntry<bool> enabled=null!,console=null!;
    private static ConfigEntry<string> corner=null!;
    private static int number,mainThread;
    internal static bool Ready;
    private static readonly Dictionary<(IntPtr,int), (float Time,ComponentDamage Damage)> uncorrelated=new();
    private static float playingUntil;
    private static bool editor=true;
    private static int warnings;private static int probes;private static int poolProbes,healthProbes;
    private static void HealthProbe(bool registration,string text){if(registration?poolProbes++<24:healthProbes++<48)Plugin.ModLog.LogInfo("[Damage feed health] "+text);}
    internal static void Probe(string text){if(probes++<24)Plugin.ModLog.LogInfo("[Damage feed probe] "+text);}
    internal static bool Enabled=>Ready&&enabled?.Value==true;
    internal static bool Left=>corner?.Value=="left";
    internal static bool Show=>Enabled&&!editor&&Time.unscaledTime<playingUntil&&!Cursor.visible;
    internal static void Configure(ConfigFile config)
    {
        mainThread=Environment.CurrentManagedThreadId;
        enabled=config.Bind("Damage feed","Enabled",false,"Show a compact live damage feed in Play. Also available in the cannon Shell profile panel.");
        corner=config.Bind("Damage feed","Corner","right",new ConfigDescription("Upper screen corner, below native HUD.",new AcceptableValueList<string>("right","left")));
        console=config.Bind("Damage feed","Console debug",false,"Also log emitted player damage-feed rows.");
    }
    private static void Safe(Action action)
    {try{action();}catch(Exception ex){if(warnings++<4)Plugin.ModLog.LogWarning("[Damage feed] Optional report unavailable: "+ex.Message);}}
    internal static void DrawOptions(IGUIElementDrawer ui,Action refresh)
    {
        if(enabled==null)return;
        ui.ToggleField("Damage feed in Play",Enabled,Ui.BoolCallback(v=>{enabled.Value=v;if(!v)Buffer.Clear();refresh();}),"Show actual penetrations, ERA reactions and native health changes. No input capture.");
        if(Enabled)
        {
            var labels=new Il2CppSystem.Collections.Generic.List<string>();labels.Add("Upper right");labels.Add("Upper left");
            ui.Dropdown("Damage feed corner",labels.Cast<Il2CppSystem.Collections.Generic.IReadOnlyList<string>>(),Left?1:0,Ui.IntCallback(v=>{corner.Value=v==1?"left":"right";refresh();}),"Below the native HUD; recent rows expire after eight seconds.");
        }
    }
    [HarmonyPostfix,HarmonyPatch(typeof(PlayerController),nameof(PlayerController.UpdateControl))]
    private static void Playing(PlayerController __instance)
    {Safe(()=>{if(__instance.controlTarget!=null){if(editor)Probe("native controlled-vehicle update; enabling Play observation");editor=false;playingUntil=Time.unscaledTime+1;}});}
    [HarmonyPostfix,HarmonyPatch(typeof(VehicleEditorScenarioGameState),nameof(VehicleEditorScenarioGameState.OnPlayerStateChanged))]
    private static void State(VehicleEditorScenarioGameState __instance,PlayerStateChangeEventArgs __1)
    {Safe(()=>{if(__1?.NewState==null)return;editor=__instance.editorPlayerState?.Pointer==__1.NewState.Pointer;Probe("committed state editor="+editor);if(editor){Buffer.Clear();playingUntil=0;}});}
    [HarmonyPrefix,HarmonyPatch(typeof(ArmourPiercingProjectileFunction),nameof(ArmourPiercingProjectileFunction.HitDamageModel))]
    private static void BeginImpact(ref ProjectileInstance __0,out Shot? __state)
    {__state=current;current=null;try{Probe($"impact enabled={Enabled} editor={editor} freshControl={Time.unscaledTime<playingUntil} cursor={Cursor.visible}");if(Enabled&&!editor&&Time.unscaledTime<playingUntil&&Environment.CurrentManagedThreadId==mainThread)current=new Shot(++number,__0.ID,__0.spawnTime);}catch(Exception ex){if(warnings++<4)Plugin.ModLog.LogWarning("[Damage feed] Impact report unavailable: "+ex.Message);}}
    [HarmonyFinalizer,HarmonyPatch(typeof(ArmourPiercingProjectileFunction),nameof(ArmourPiercingProjectileFunction.HitDamageModel))]
    private static void EndImpact(Shot? __state)
    {Safe(()=>{if(current is {} shot){var lines=DamageFeedActivations.Compose(shot.Activations.Rows,shot.Damage.Values.Select(d=>d.Text),shot.Lines).Select(t=>$"#{shot.Number} {t}").ToArray();Buffer.Add(Time.unscaledTime,lines);Probe("native="+shot.NativeId+" rows="+lines.Length);if(console.Value)foreach(var line in lines)Plugin.ModLog.LogInfo("[Damage feed] native="+shot.NativeId+" spawn="+shot.SpawnTime+" "+line);}});current=__state;}
    [HarmonyPrefix,HarmonyPatch(typeof(ExplosiveProjectileFunction),nameof(ExplosiveProjectileFunction.HitDamageModel))]
    private static void BeginExplosive(ref ProjectileInstance __0,out Shot? __state)=>BeginImpact(ref __0,out __state);
    [HarmonyFinalizer,HarmonyPatch(typeof(ExplosiveProjectileFunction),nameof(ExplosiveProjectileFunction.HitDamageModel))]
    private static void EndExplosive(Shot? __state)=>EndImpact(__state);
    [HarmonyPrefix,HarmonyPatch(typeof(CompoundStructure),nameof(CompoundStructure.SimulateFragment))]
    [HarmonyPriority(Priority.First)]
    private static void Begin(PenetrationSimulation sim,short index,out Scope? __state)
    {__state=active;active=null;Safe(()=>{if(current!=null&&index>=0&&index<sim.FragmentCount&&(sim.fragments[index].flags&FragmentFlag.OriginalPenetrator)!=0)active=new(sim,index);});}
    [HarmonyPostfix,HarmonyPatch(typeof(StructureIntersection),nameof(StructureIntersection.GetNextMaterialBlock))]
    [HarmonyAfter("sprocket.shellselector.armourresponses")]
    private static void Block(MaterialBlock __result,Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<StructureIntersection> __0,int __1)
    {Safe(()=>{
        if(active is not {} scope||current==null||!__result.Valid||__result.MaterialIndex<0)return;
        var e=__result.EnterIntersectionIndex;var x=__result.ExitIntersectionIndex;
        if(e<0||x<0||e>=__1||x>=__1||e>=__0.Length||x>=__0.Length)return;
        var first=__0[e];var last=__0[x];if(first.objectIndex<0||first.objectIndex>=scope.Sim.objects.Length)return;
        var id=scope.Sim.objects[first.objectIndex].ID;
        bool combined=first.objectIndex!=last.objectIndex||(__result.IntersectedObjectIndices?.Length??0)>1;
        for(int i=0;i<__1&&i<__0.Length;i++){var candidate=__0[i];if(candidate.distance>first.distance&&candidate.distance<last.distance&&candidate.objectIndex!=first.objectIndex)combined=true;}
        scope.Label=combined?"Combined armour block":ResolveLabel(scope.Sim,id);
        scope.Key=$"{scope.Sim.Pointer}:{id}";
        scope.Solid=true;
    });}
    [HarmonyFinalizer,HarmonyPatch(typeof(CompoundStructure),nameof(CompoundStructure.SimulateFragment))]
    [HarmonyPriority(Priority.Last),HarmonyAfter("sprocket.shellselector.armourresponses")]
    private static void End(Scope? __state,Exception? __exception)
    {Safe(()=>{
        if(active is not {} scope||current is not {} shot||scope.Index>=scope.Sim.FragmentCount)return;
        var f=scope.Sim.fragments[scope.Index];var budgets=new List<double>();
        for(int i=scope.Count;i<scope.Sim.FragmentCount;i++)
        {var c=scope.Sim.fragments[i];if(c.parentIndex==scope.Index&&(c.flags&FragmentFlag.OriginalPenetrator)!=0&&(c.flags&FragmentFlag.Killed)==0)budgets.Add(c.GetBasePenetration(scope.Sim.projectileProfiles[c.projectileProfileIndex].penetratorConstant)*1000);}
        var outcome=DamageFeedTruth.Outcome((f.flags&FragmentFlag.Penetrated)!=0,(f.flags&FragmentFlag.Embedded)!=0,(f.flags&FragmentFlag.Deflected)!=0,budgets.Count,__exception!=null);
        var label=scope.Label;
        if(label==null&&outcome==PlateOutcome.Stopped&&shot.Pending.TryGetValue(scope.Sim.Pointer,out var pending)&&f.parentIndex==pending.Index)label=pending.Label;
        if(label==null)return;
        if(scope.Solid&&outcome==PlateOutcome.Unknown&&(f.flags&FragmentFlag.Embedded)!=0&&budgets.Count==1&&(shot.Pending.Count<32||shot.Pending.ContainsKey(scope.Sim.Pointer)))shot.Pending[scope.Sim.Pointer]=(scope.Index,label);
        if(outcome==PlateOutcome.Penetrated&&scope.Solid)
        {
            var remaining=DamageFeedTruth.Remaining(budgets);
            shot.Add(scope.Key!,"Penetrated "+label+(remaining is {} mm?$" - {mm:0} mm left":""));shot.Pending.Remove(scope.Sim.Pointer);
        }
        else if(outcome==PlateOutcome.Stopped)
        {shot.Add("stop:"+scope.Sim.Pointer,"Stopped in "+label);shot.Pending.Remove(scope.Sim.Pointer);}
        else if(outcome==PlateOutcome.Deflected&&scope.Solid)shot.Add(scope.Key!,"Deflected at "+label);
    });active=__state;}
    private static string ResolveLabel(PenetrationSimulation sim,int id)
    {
        var reader=sim.damageApplier?.TryCast<VehicleHealthRegister>()?.objectReader;
        if(reader!=null&&reader.TryGet<VehicleComponent>(new VUID(id),out var owner)&&owner!=null&&owner.VUID.Value==id)return Label(owner);
        return "Armour #"+id;
    }
    private static string Label(VehicleComponent owner)
    {
        var plate=owner.TryCast<PlateStructure>();if(plate!=null)return DamageFeedTruth.Label(plate.Blueprint?.Name,"Plate #"+owner.VUID.Value);
        var crew=owner.TryCast<CrewSeat>();if(crew!=null)return DamageFeedTruth.Label(crew.SeatBlueprint?.Name,"Crew member #"+owner.VUID.Value);
        return DamageFeedTruth.Label(owner.ComponentID switch {"reliktCassette"=>"Relikt ERA brick", "reliktTurretCassette"=>"Relikt turret ERA brick", "kontakt1Cassette"=>"Kontakt-1 ERA brick", "kontakt5Cassette"=>"Kontakt-5 ERA brick", "turretDrive"=>"Turret drive",_=>owner.ComponentID},"Component #"+owner.VUID.Value);
    }
    internal static void EraActivated(PenetrationSimulation sim,int id,EraCell cell,string kind,bool live,bool first)
    {Safe(()=>{current?.Activations.Record($"{sim.Pointer}:{cell}",live,first,kind,ResolveLabel(sim,id));});}
    internal static void EraBudget(PenetrationSimulation sim,short index,EraCell cell,double before)
    {Safe(()=>{if(current==null)return;var f=sim.fragments[index];var after=f.GetBasePenetration(sim.projectileProfiles[f.projectileProfileIndex].penetratorConstant)*1000;current.Activations.Budget($"{sim.Pointer}:{cell}",before,after);});}

    internal static void PoolCreated(VehicleHealthRegister __instance,VehicleComponent __0,IDurable __result)
    {Safe(()=>{if(Environment.CurrentManagedThreadId!=mainThread||__result==null||__0==null)return;if(pools.Count<8192||pools.ContainsKey(__result.Pointer)){var info=__result.HealthInfo;pools[__result.Pointer]=new(__instance.Pointer,__0,info.Alive,info.Current);HealthProbe(true,$"created register={__instance.Pointer} durable={__result.Pointer} component={__0.ComponentID} vuid={__0.VUID.Value} value={info.Current}");}});}
    
    internal static void HealthChanged(VehicleHealthRegister __instance,IDurable __0,HealthChangeInfo __1)
    {Safe(()=>{
        if(Environment.CurrentManagedThreadId!=mainThread||__0==null)return;
        if(!pools.TryGetValue(__0.Pointer,out var previous)){HealthProbe(false,$"unregistered callback register={__instance.Pointer} durable={__0.Pointer} delta={__1.Delta}");return;}
        var info=__0.HealthInfo;bool alive=info.Alive;pools[__0.Pointer]=previous with {Alive=alive,Value=info.Current};
        HealthProbe(false,$"callback durable={__0.Pointer} component={previous.Owner.ComponentID} delta={__1.Delta} alive={alive} enabled={Enabled} editor={editor}");
        Emit(previous,alive,__1.Delta);
    });}
    private static void Emit(Pool previous,bool alive,float delta)
    {
        if(!Enabled||editor||Time.unscaledTime>=playingUntil||!float.IsFinite(delta)||delta>=0)return;
        var owner=previous.Owner;if(owner==null||owner.VehicleRoot is not {} root)return;
        var label=Label(owner);bool dead=DamageFeedTruth.Died(previous.Alive,alive,delta);
        string line=owner.TryCast<CrewSeat>()!=null?(dead?$"Crew member {label} killed":$"Crew member {label} wounded"):(dead?label+" destroyed":label+" damaged");
        var key=(root.Pointer,owner.VUID.Value);var damage=new ComponentDamage(line,dead);
        if(current is {} shot)
        {if(shot.Damage.TryGetValue(key,out var prior))shot.Damage[key]=prior.Merge(damage);else if(shot.Damage.Count<24)shot.Damage[key]=damage;}
        else
        {var now=Time.unscaledTime;foreach(var stale in uncorrelated.Where(p=>now-p.Value.Time>=.5f||now<p.Value.Time).ToArray())uncorrelated.Remove(stale.Key);if(uncorrelated.TryGetValue(key,out var prior)){if(prior.Damage.Fatal||!dead)return;}if(uncorrelated.Count<64||uncorrelated.ContainsKey(key)){uncorrelated[key]=(now,damage);Buffer.Add(now,new[]{"Damage event: "+line});}}
    }
    internal static void Linked(VehicleHealthRegister register,IDurable durable,int id)
    {Safe(()=>{if(Environment.CurrentManagedThreadId!=mainThread||durable==null)return;if(register.objectReader?.TryGet<VehicleComponent>(new VUID(id),out var owner)==true&&owner!=null&&owner.VUID.Value==id)PoolCreated(register,owner,durable);else HealthProbe(true,$"link unresolved register={register.Pointer} durable={durable.Pointer} id={id}");});}
    internal static void ObserveRegister(VehicleHealthRegister register)
    {Safe(()=>{if(Environment.CurrentManagedThreadId!=mainThread)return;var lookup=register.register?.lookup;if(lookup==null)return;foreach(var pair in lookup){var durable=pair.Value;if(durable==null)continue;if(!pools.TryGetValue(durable.Pointer,out var previous)){Linked(register,durable.Cast<IDurable>(),pair.Key);continue;}var info=durable.HealthInfo;var delta=info.Current-previous.Value;pools[durable.Pointer]=previous with {Alive=info.Alive,Value=info.Current};if(float.IsFinite(delta)&&delta<0){HealthProbe(false,$"observed native pool durable={durable.Pointer} component={previous.Owner.ComponentID} delta={delta} alive={info.Alive}");Emit(previous,info.Alive,delta);}}});}

    internal static void ReleaseHealth(VehicleHealthRegister __instance)
    {Safe(()=>{foreach(var pair in pools.Where(p=>p.Value.Register==__instance.Pointer).ToArray())pools.Remove(pair.Key);});}
    internal static void Clear(){Buffer.Clear();pools.Clear();uncorrelated.Clear();active=null;current=null;DamageFeedHud.Destroy();}
}

[HarmonyPatch]
internal static class RuntimeDamageHealth
{
    [HarmonyPostfix,HarmonyPatch(typeof(VehicleHealthRegister),nameof(VehicleHealthRegister.LinkHealthPool))]
    private static void Linked(VehicleHealthRegister __instance,IDurable __0,int __1)=>RuntimeDamageFeed.Linked(__instance,__0,__1);
    [HarmonyPrefix,HarmonyPatch(typeof(VehicleHealthRegister),nameof(VehicleHealthRegister.Update))]
    private static void Updating(VehicleHealthRegister __instance,out bool __state){__state=false;try{__state=__instance.register?.HealthChanged==true;}catch{}}
    [HarmonyPostfix,HarmonyPatch(typeof(VehicleHealthRegister),nameof(VehicleHealthRegister.Update))]
    private static void Updated(VehicleHealthRegister __instance,bool __state){if(__state)RuntimeDamageFeed.ObserveRegister(__instance);}
    [HarmonyPostfix,HarmonyPatch(typeof(VehicleHealthRegister),nameof(VehicleHealthRegister.CreateHealthPoolInternal))]
    private static void Created(VehicleHealthRegister __instance,VehicleComponent __0,IDurable __result)=>RuntimeDamageFeed.PoolCreated(__instance,__0,__result);
    [HarmonyPostfix,HarmonyPatch(typeof(VehicleHealthRegister),nameof(VehicleHealthRegister.OnHealthChanged))]
    private static void Changed(VehicleHealthRegister __instance,IDurable __0,HealthChangeInfo __1)=>RuntimeDamageFeed.HealthChanged(__instance,__0,__1);
    [HarmonyPrefix,HarmonyPatch(typeof(VehicleHealthRegister),nameof(VehicleHealthRegister.Release))]
    private static void Released(VehicleHealthRegister __instance)=>RuntimeDamageFeed.ReleaseHealth(__instance);
}
