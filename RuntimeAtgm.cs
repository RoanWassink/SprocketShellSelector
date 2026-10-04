using BepInEx.Configuration;
using HarmonyLib;
using Sprocket.DamageModelling;
using Sprocket.Vehicles;
using Sprocket.Vehicles.Cannons;
using Sprocket.Vehicles.Weapons.Cannons;
using UnityEngine;
using UnityEngine.InputSystem;
using NVector = System.Numerics.Vector3;
using PlayerVehicleController = Sprocket.Gameplay.VehicleControl.VehicleController;

namespace SprocketShellSelector;

// Separate optional Harmony owner: native collision/impact evaluation stays in the game.
[HarmonyPatch]
internal static class RuntimeAtgm
{
    private sealed record AimPoint(Vector3 Origin, Vector3 Direction, float Time, IntPtr Controller);
    private sealed class Flight
    {
        internal readonly CannonBehaviour Weapon;
        internal readonly ShellProfile Profile;
        internal readonly string Definition;
        internal readonly float SpawnTime;
        internal double InitialSpeed;
        internal bool MotorStarted;
        internal Vector3 Heading;
        internal float LastLog;
        internal bool Report;
        internal Vector3 BeforePosition;
        internal bool Guide = true;
        internal readonly IntPtr Vehicle;
        internal Flight(CannonBehaviour weapon,ShellProfile profile,ProjectileInstance p)
        {Weapon=weapon;Profile=profile;Definition=p.Definition.Guid.ToString();SpawnTime=p.spawnTime;Heading=p.velocity.normalized;InitialSpeed=Math.Clamp(p.velocity.magnitude,10,profile.Atgm!.FlightSpeed);Vehicle=weapon.mount?.TryCast<Cannon>()?.Vehicle?.Behaviour?.Pointer ?? IntPtr.Zero;}
    }
    private static readonly Dictionary<(IntPtr Register,int Id),Flight> Flights=new();
    private static readonly Dictionary<IntPtr,AimPoint> Aims=new();
    private static readonly Dictionary<IntPtr,AimPoint> PlayerAims=new();
    private static readonly HashSet<string> Warnings=new();
    private static IntPtr currentVehicle;
    private static readonly HashSet<IntPtr> LockedControllers=new();
    private static ConfigEntry<bool> enabled=null!;
    private static ConfigEntry<bool> diagnostics=null!;
    internal static bool Enabled => enabled?.Value ?? false;
    internal static bool Ready;
    internal static void Configure(ConfigFile config)
    {
        enabled=config.Bind("ATGM Experimental","Enabled",true,"Enable ATGM powered flight and guidance. Other shells are unaffected.");
        diagnostics=config.Bind("ATGM Experimental","DiagnosticLogging",false,"Log commanded and native movement once per second for ATGM testing.");
    }
    private static bool Finite(Vector3 v)=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z);
    private static void Warn(string area,Exception ex)
    {if(Warnings.Add(area))Plugin.ModLog.LogWarning($"[ATGM] {area}: {ex.Message}");}

    internal static bool IsPlayerVehicle(IntPtr vehicle) => vehicle!=IntPtr.Zero && vehicle==currentVehicle;
    private static bool KeyboardOwned(Flight flight, float now) => Ready && Enabled && flight.Guide &&
        flight.Profile.Atgm?.GuidanceMode == "keyboard" && flight.Vehicle != IntPtr.Zero && flight.Vehicle == currentVehicle &&
        now >= flight.SpawnTime && now-flight.SpawnTime < flight.Profile.Atgm.MaximumFlightTime &&
        flight.Weapon.mount?.TryCast<Cannon>() is {} cannon && cannon.HealthFraction > 0;

    // Block only the native vehicle command dispatch. Camera and general UI input remain
    // in the player state; no InputAction maps are disabled, so no persistent restore is needed.
    [HarmonyPrefix,HarmonyPatch(typeof(PlayerVehicleController),nameof(PlayerVehicleController.UpdateControl))]
    private static bool VehicleInput(PlayerVehicleController __instance)
    {
        try
        {
            currentVehicle=__instance.ControlledVehicle?.Pointer ?? IntPtr.Zero;
            var locked=Flights.Values.Any(f=>KeyboardOwned(f,Time.time));
            if(!locked)
            {
                if(LockedControllers.Remove(__instance.Pointer))Plugin.ModLog.LogInfo("[ATGM] INPUT unlocked");
                return true;
            }
            __instance.ResetDriveInputs();
            __instance.cruiseControlLevel=0;
            __instance.firing=false;
            if(__instance.driver!=null)__instance.driver.Solution=Sprocket.VehicleControl.DriveSolution.Idle;
            if(__instance.gunners!=null)foreach(var gun in __instance.gunners)if(gun!=null)gun.Firing=false;
            if(LockedControllers.Add(__instance.Pointer))
            {
                if(__instance.gunLayers!=null)foreach(var layer in __instance.gunLayers)if(layer!=null)layer.ClearTarget();
                Plugin.ModLog.LogInfo("[ATGM] INPUT locked: keyboard missile; W/S up/down, A/D left/right");
            }
            return false;
        }
        catch(Exception ex){LockedControllers.Remove(__instance.Pointer);Warn("Vehicle input lock",ex);return true;}
    }

    // Capture the actual player control ray. This supports both scope and third-person aiming,
    // and binds commands to the launcher's vehicle rather than relying on sight wrapper identity.
    [HarmonyPostfix,HarmonyPatch(typeof(PlayerVehicleController),nameof(PlayerVehicleController.UpdateControl))]
    private static void PlayerAim(PlayerVehicleController __instance,Ray __1)
    {
        if(!Enabled)return;
        try
        {
            var vehicle=__instance.ControlledVehicle;
            if(vehicle!=null && Finite(__1.origin) && Finite(__1.direction) && __1.direction.sqrMagnitude>.5f)
                PlayerAims[vehicle.Pointer]=new(__1.origin,__1.direction.normalized,Time.time,__instance.Pointer);
        }
        catch(Exception ex){Warn("Player aim ray",ex);}
    }

    [HarmonyPostfix,HarmonyPatch(typeof(ScopeController),nameof(ScopeController.Update))]
    private static void Sight(ScopeController __instance)
    {
        if(!Enabled)return;
        try
        {
            var scope=__instance.ControlledScope;
            if(scope==null)return;
            if(!__instance.Scoped || !__instance.HasActive){Aims.Remove(scope.Pointer);return;}
            // Fallback for callers where the simple SetAimPoint method was inlined.
            // Convert the native convergence point back to a ray; never chase that fixed point.
            if(!Aims.TryGetValue(scope.Pointer,out var existing) || Time.time-existing.Time>.1f)
            {
                var parent=scope.SightParent;
                if(parent==null)return;
                var origin=parent.TransformPoint(scope.ObjectiveLensLocalPosition);
                var direction=__instance.ScopeAimPoint-origin;
                if(Finite(origin)&&Finite(direction)&&direction.sqrMagnitude>1)
                    Aims[scope.Pointer]=new(origin,direction.normalized,Time.time,__instance.Pointer);
            }
        }
        catch(Exception ex){Warn("Scope lookup",ex);}
    }
    [HarmonyPostfix,HarmonyPatch(typeof(ScopeController),nameof(ScopeController.SetAimPoint),new[]{typeof(Vector3),typeof(Vector3)})]
    private static void AimRay(ScopeController __instance,Vector3 __0,Vector3 __1)
    {
        if(!Enabled)return;
        try
        {
            var scope=__instance.ControlledScope;
            if(scope!=null && __instance.Scoped && __instance.HasActive && Finite(__0) && Finite(__1) && __1.sqrMagnitude>.5f)
                Aims[scope.Pointer]=new(__0,__1.normalized,Time.time,__instance.Pointer);
        }
        catch(Exception ex){Warn("Aim ray",ex);}
    }
    [HarmonyPrefix,HarmonyPatch(typeof(ScopeController),nameof(ScopeController.Dispose))]
    private static void DisposeSight(ScopeController __instance)
    {foreach(var key in Aims.Where(p=>p.Value.Controller==__instance.Pointer).Select(p=>p.Key).ToArray())Aims.Remove(key);}

    [HarmonyPostfix,HarmonyPatch(typeof(ProjectileRegister),nameof(ProjectileRegister.Launch))]
    private static void Launched(ProjectileRegister __instance,Il2CppSystem.Object source,ref ProjectileFireInfo fireInfo)
    {
        if(!Ready || !Enabled)return;
        try
        {
            var weapon=source?.TryCast<CannonBehaviour>();
            if(weapon==null || !__instance.activeIdMap.TryGetValue(fireInfo.ProjectileID,out var index))return;
            var p=__instance.pool[index];
            var profile=RuntimeShellSelection.ProjectileProfile(p);
            if(profile==null || !ShellBalance.IsAtgm(profile.Behavior) || profile.Atgm==null)return;
            var flight=new Flight(weapon,profile,p);
            foreach(var old in Flights.Values.Where(f=>f.Weapon.Pointer==weapon.Pointer || flight.Vehicle!=IntPtr.Zero && f.Vehicle==flight.Vehicle))old.Guide=false;
            Flights[(__instance.Pointer,p.ID)]=flight;
            Plugin.ModLog.LogInfo($"[ATGM] LAUNCH id={p.ID} profile={profile.Id} speed={p.velocity.magnitude:0.0}m/s cruise={profile.Atgm.FlightSpeed:0}m/s acceleration={profile.Atgm.Acceleration:0}m/s2 vehicle={flight.Vehicle}; latest missile per vehicle receives guidance commands.");
        }
        catch(Exception ex){Plugin.ModLog.LogError("[ATGM] Launch tracking: "+ex);}
    }

    [HarmonyPrefix,HarmonyPatch(typeof(ProjectileRegister),nameof(ProjectileRegister.FixedUpdate))]
    private static void BeforeTick(ProjectileRegister __instance)
    {
        if(!Ready || !Enabled || Flights.Count==0)return;
        var now=Time.time;var dt=Time.fixedDeltaTime;
        if(!float.IsFinite(dt)||dt<=0)return;
        foreach(var pair in Flights.Where(p=>p.Key.Register==__instance.Pointer).ToArray())
        {
            var key=pair.Key;var flight=pair.Value;
            try
            {
                if(!__instance.activeIdMap.TryGetValue(key.Id,out var index)){Flights.Remove(key);continue;}
                var p=__instance.pool[index];
                // Guard against pooled slots being reused or a projectile already terminated.
                if(p.ID!=key.Id || p.spawnTime!=flight.SpawnTime || p.Definition.Guid.ToString()!=flight.Definition ||
                    // Projectiles.dll Active=1, Release=2; another game assembly exposes a different enum with the same name.
                    ((int)p.flags & 1)==0 || ((int)p.flags & 14)!=0)
                {Flights.Remove(key);continue;}
                var settings=flight.Profile.Atgm!;var age=now-flight.SpawnTime;
                if(age>=settings.MaximumFlightTime){__instance.Release(key.Id);Flights.Remove(key);Plugin.ModLog.LogInfo($"[ATGM] EXPIRED id={key.Id}; no detonation.");continue;}
                if(age<settings.MotorDelay)
                {
                    // Let native gravity/drag integrate the actual ejection velocity.
                    flight.Heading=p.velocity.normalized;
                    continue;
                }
                if(!flight.MotorStarted)
                {
                    flight.MotorStarted=true;
                    flight.InitialSpeed=Math.Clamp(p.velocity.magnitude,10,settings.FlightSpeed);
                    flight.Heading=p.velocity.normalized;
                    Plugin.ModLog.LogInfo($"[ATGM] MOTOR id={key.Id} age={age:0.00}s speed={p.velocity.magnitude:0.0}m/s heading={flight.Heading}");
                }
                var speed=AtgmGuidance.SpeedAtAge(settings,flight.InitialSpeed,Math.Max(0,age));
                var steeringSettings=settings with {FlightSpeed=speed};
                Vector3? target=null;
                var cannon=flight.Weapon.mount?.TryCast<Cannon>();
                var sight=flight.Weapon.Sight;
                AimPoint? aim=null;
                var reason=!flight.Guide ? "superseded" : age<settings.GuidanceDelay ? "arming" : settings.GuidanceMode!="sight" ? "unguided" :
                    cannon==null || cannon.HealthFraction<=0 ? "launcher-unavailable" : "no-fresh-player-ray";
                if(flight.Guide && age>=settings.GuidanceDelay && settings.GuidanceMode=="sight" && cannon!=null && cannon.HealthFraction>0)
                {
                    if(flight.Vehicle!=IntPtr.Zero && PlayerAims.TryGetValue(flight.Vehicle,out var playerAim) && AtgmGuidance.FreshAim(now,playerAim.Time))
                    {aim=playerAim;reason="player-ray";}
                    else if(sight!=null && Aims.TryGetValue(sight.Pointer,out var scopeAim) && AtgmGuidance.FreshAim(now,scopeAim.Time))
                    {aim=scopeAim;reason="scope-ray";}
                }
                if(flight.Guide && age>=settings.GuidanceDelay && settings.GuidanceMode!="none" &&
                    !IsPlayerVehicle(flight.Vehicle) && cannon!=null && cannon.HealthFraction>0 &&
                    RuntimeAtgmAi.TryAim(flight.Weapon.Pointer,now,out var aiOrigin,out var aiDirection))
                {
                    aim=new(aiOrigin,aiDirection,now,IntPtr.Zero);reason="ai-ray";
                    steeringSettings=steeringSettings with {GuidanceMode="sight"};
                }
                if(aim!=null)
                {
                    var aimTarget=AtgmGuidance.AimAlongRay(new(aim.Origin.x,aim.Origin.y,aim.Origin.z),
                        new(aim.Direction.x,aim.Direction.y,aim.Direction.z),new(p.position.x,p.position.y,p.position.z),speed);
                    var direction=new Vector3(aimTarget.X,aimTarget.Y,aimTarget.Z)-p.position;
                    if(Finite(direction) && direction.sqrMagnitude>1)target=direction;
                }
                var heading=new NVector(flight.Heading.x,flight.Heading.y,flight.Heading.z);
                var desired=target is {} t ? new NVector(t.x,t.y,t.z) : (NVector?)null;
                var velocity=AtgmGuidance.Step(heading,desired,steeringSettings,dt);
                if(KeyboardOwned(flight,now) && age>=settings.GuidanceDelay &&
                    PlayerAims.TryGetValue(flight.Vehicle,out var owner) && AtgmGuidance.FreshAim(now,owner.Time))
                {
                    var keys=Keyboard.current;
                    var yaw=(keys?.dKey.isPressed==true?1:0)-(keys?.aKey.isPressed==true?1:0);
                    var pitch=(keys?.wKey.isPressed==true?1:0)-(keys?.sKey.isPressed==true?1:0);
                    velocity=AtgmGuidance.KeyboardStep(heading,yaw,pitch,steeringSettings,dt);
                    reason=$"keyboard yaw={yaw} pitch={pitch}";
                }
                var command=new Vector3(velocity.X,velocity.Y,velocity.Z);
                flight.Heading=command.normalized;
                // pool[index] is a boxed value copy: commit it back before native movement/collisions.
                // Native drag/gravity still act within this step; powered flight restores command next tick.
                p.velocity=command;
                __instance.pool[index]=p;
                if(diagnostics.Value && now-flight.LastLog>=1){flight.LastLog=now;flight.Report=true;flight.BeforePosition=p.position;Plugin.ModLog.LogInfo($"[ATGM] COMMAND id={key.Id} age={age:0.0}s speed={command.magnitude:0.0}m/s guidance={(reason.StartsWith("keyboard")?"keyboard":target.HasValue?reason:"straight")} reason={reason} playerCache={PlayerAims.Count} scopeCache={Aims.Count} vehicle={flight.Vehicle} heading={flight.Heading} position={p.position}");}
            }
            catch(Exception ex){Flights.Remove(key);Plugin.ModLog.LogError($"[ATGM] Flight id={key.Id} reverted to native ballistic movement: {ex}");}
        }
    }
    [HarmonyPostfix,HarmonyPatch(typeof(ProjectileRegister),nameof(ProjectileRegister.FixedUpdate))]
    private static void AfterTick(ProjectileRegister __instance)
    {
        foreach(var pair in Flights.Where(p=>p.Key.Register==__instance.Pointer && p.Value.Report).ToArray())
        {
            var flight=pair.Value;flight.Report=false;
            try
            {
                if(!__instance.activeIdMap.TryGetValue(pair.Key.Id,out var index))continue;
                var p=__instance.pool[index];
                if(p.spawnTime!=flight.SpawnTime || p.Definition.Guid.ToString()!=flight.Definition)continue;
                Plugin.ModLog.LogInfo($"[ATGM] NATIVE id={pair.Key.Id} speed={p.velocity.magnitude:0.0}m/s moved={Vector3.Distance(p.position,flight.BeforePosition):0.00}m per tick headingDot={Vector3.Dot(p.velocity.normalized,flight.Heading):0.000}");
            }
            catch(Exception ex){Warn("Native movement diagnostics",ex);}
        }
    }
    [HarmonyPostfix,HarmonyPatch(typeof(ProjectileRegister),nameof(ProjectileRegister.Release))]
    private static void Released(ProjectileRegister __instance,int __0)=>Flights.Remove((__instance.Pointer,__0));
    [HarmonyPrefix,HarmonyPatch(typeof(ProjectileRegister),nameof(ProjectileRegister.DestroyAll))]
    private static void Clear(ProjectileRegister __instance)
    {foreach(var key in Flights.Keys.Where(k=>k.Register==__instance.Pointer).ToArray())Flights.Remove(key);Aims.Clear();PlayerAims.Clear();Warnings.Clear();LockedControllers.Clear();currentVehicle=IntPtr.Zero;RuntimeAtgmAi.Clear();}
}
