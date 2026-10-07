using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Sprocket.PartImporting;
using Sprocket.Vehicles;
using Sprocket.Vehicles.AssetManagement;
using Sprocket.Vehicles.Cannons;
using Sprocket.Vehicles.Colliders;
using Sprocket.Vehicles.CrewSystems;
using Sprocket.Vehicles.Weapons.Cannons;
using Sprocket.WeaponFramework;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
namespace SprocketShellSelector;

[HarmonyPatch]
internal static class RuntimeAtgmLauncher
{
    internal static bool Ready;
    internal static bool IsLauncher(Cannon? cannon)=>cannon?.VehicleObject?.GUID==AtgmLauncherModel.Guid;
    private static readonly Dictionary<IntPtr,string> buildSignatures=new();
    private static readonly Dictionary<IntPtr,Mesh> meshes=new();
    private static readonly Dictionary<IntPtr,HashSet<IntPtr>> meshOwners=new();
    private static readonly Dictionary<IntPtr,(float Next,int Count)> diagnostics=new();
    private static readonly HashSet<string> warnings=new();
    private static VehicleMaterial? paint;
    private static void Warn(string area,Exception ex)
    {if(warnings.Add(area))Plugin.ModLog.LogWarning("[ATGM launcher] "+area+": "+ex.Message);}
    internal static bool CanLaunch(Cannon cannon)=>Ready&&RuntimeAtgm.Ready&&RuntimeAtgm.Enabled&&RuntimeShellSelection.Enabled&&AtgmLauncherModel.Allows(RuntimeShellSelection.CannonProfile(cannon));

    [HarmonyPostfix,HarmonyPatch(typeof(WeaponBehaviour),"get_CanFire")]
    private static void FireGate(WeaponBehaviour __instance,ref bool __result)
    {
        var cannon=__instance.TryCast<CannonBehaviour>()?.mount?.TryCast<Cannon>();
        if(!IsLauncher(cannon))return;
        var nativeReady=__result;__result=nativeReady&&CanLaunch(cannon!);
        try
        {
            diagnostics.TryGetValue(__instance.Pointer,out var sample);
            if(sample.Count>=12||Time.unscaledTime<sample.Next)return;
            diagnostics[__instance.Pointer]=(Time.unscaledTime+2,sample.Count+1);
            var op=cannon!.operable;var loader=cannon.breechModel?.TryCast<CannonBreech>()?.Operable;
            var gunner=op?.behaviour?.TryCast<Gunner>();
            Plugin.ModLog.LogInfo($"[ATGM launcher] READY VUID={cannon.VUID.Value} native={nativeReady} allowed={CanLaunch(cannon)} functioning={__instance.Functioning} needsReload={__instance.NeedsReload} loadState={__instance.LoadState} loadFraction={__instance.LoadFraction:0.00} loadedType={__instance.LoadedProjectileTypeID} gunnerAssigned={op?.OperatorAssigned} gunnerValid={op?.Valid} gunnerEfficiency={gunner?.currentEfficiency ?? -1:0.00} sightRequired={op?.SightRequired} loaderAssigned={loader?.OperatorAssigned} loaderValid={loader?.Valid} calibre={cannon.Blueprint.Caliber} propellant={cannon.Blueprint.PropellantLength} shellSlot={cannon.Blueprint.ShellSlotBlueprintID}; no native readiness bypass.");
        }
        catch(Exception ex){Warn("Readiness diagnostic",ex);}
    }
    private static void RelaxSight(Cannon cannon)
    {
        var op=cannon.operable;
        if(op!=null&&op.SightRequired){op.SightRequired=false;op.MarkModified();}
    }
    [HarmonyPostfix,HarmonyPatch(typeof(Cannon),nameof(Cannon.Build))]
    [HarmonyPriority(Priority.Last)]
    private static void Built(Cannon __instance)
    {
        if(!IsLauncher(__instance))return;
        try
        {
            RelaxSight(__instance);Apply(__instance);
            var ammo=__instance.shellBlueprintSlot?.Blueprint;
            var barrel=__instance.Barrel?.TryCast<CannonBarrelModel>();
            var rootInteractions=barrel?.segments.Count(s=>s?.barrelCollider?.TryCast<IVehicleCollider>()?.AssociatedComponent?.Pointer==__instance.Pointer) ?? 0;
            var signature=$"calibre={__instance.Blueprint.Caliber} propellant={__instance.Blueprint.PropellantLength} ammoCalibre={ammo?.Diameter} ammoPropellant={ammo?.PropellantLength} ammoLength={ammo?.Length} nativeMass={__instance.Blueprint.Mass:0.0} muzzle={barrel?.FireLocalPosition} sightRequired={__instance.operable?.SightRequired} segments={__instance.Blueprint.SegmentCount} rootInteractions={rootInteractions}";
            if(!buildSignatures.TryGetValue(__instance.Pointer,out var previous)||previous!=signature)
            {
                buildSignatures[__instance.Pointer]=signature;
                Plugin.ModLog.LogInfo($"[ATGM launcher] BUILD3 VUID={__instance.VUID.Value} {signature}; native blueprint/mass/cost/reload retained, tube interactions associated with root cannon.");
            }
        }
        catch(Exception ex){Warn("Native launcher build",ex);}
    }
    private static VehicleMaterial Paint()
    {
        if(paint!=null)return paint;
        var shader=Shader.Find("HDRP/Lit")??throw new InvalidOperationException("HDRP/Lit unavailable.");
        var m=new Material(shader){name="ATGM launcher olive paint",hideFlags=HideFlags.HideAndDontSave};
        m.SetColor("_BaseColor",new Color(.19f,.24f,.16f,1));m.SetFloat("_Smoothness",.25f);HDMaterial.ValidateMaterial(m);
        return paint=new VehicleMaterial(m){Flags=VehicleMaterialFlags.Unpainted|VehicleMaterialFlags.DisableGrime|VehicleMaterialFlags.CustomShader};
    }
    private static Mesh MeshFor(IVehicleModel target,CannonBarrelModel barrel,float length,float frontOffset,bool mount)
    {
        if(meshes.Remove(target.Pointer,out var previous))UnityEngine.Object.Destroy(previous);
        var vertices=new List<Vector3>();var triangles=new List<int>();
        var origin=barrel.transform.TransformPoint(barrel.FireLocalPosition);
        var boxes=AtgmLauncherModel.SegmentBoxes(length,frontOffset,mount);
        foreach(var box in boxes)
        {
            var start=vertices.Count;
            foreach(var corner in new[]{new Vector3(-1,-1,-1),new Vector3(1,-1,-1),new Vector3(1,1,-1),new Vector3(-1,1,-1),new Vector3(-1,-1,1),new Vector3(1,-1,1),new Vector3(1,1,1),new Vector3(-1,1,1)})
            {
                var point=new Vector3(box.X+corner.x*box.Width/2,box.Y+corner.y*box.Height/2,box.Z+corner.z*box.Length/2);
                vertices.Add(target.transform.InverseTransformPoint(origin+barrel.transform.rotation*point));
            }
            foreach(var index in new[]{0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,3,7,6,3,6,2,0,4,7,0,7,3,1,2,6,1,6,5})triangles.Add(start+index);
        }
        var mesh=new Mesh{name="ATGM native barrel box",hideFlags=HideFlags.HideAndDontSave};
        mesh.vertices=vertices.ToArray();mesh.triangles=triangles.ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();meshes[target.Pointer]=mesh;
        if(!meshOwners.TryGetValue(barrel.cannon.Pointer,out var owned))meshOwners[barrel.cannon.Pointer]=owned=new();
        owned.Add(target.Pointer);return mesh;
    }
    private static void SetModel(IVehicleModel target,Mesh mesh)
    {
        target.Flags &= ~VehicleRendererFlags.Static;
        target.SetMesh(mesh);target.SetShadowProxy(mesh);target.SetInteractionMesh(mesh);target.SetCollisionMesh(mesh);target.SetMaterial(Paint());
    }
    private static void Apply(Cannon cannon)
    {
        var barrel=cannon.Barrel?.TryCast<CannonBarrelModel>();if(barrel==null)return;
        float remaining=0;
        foreach(var segment in barrel.segments)if(segment?.BuiltBlueprint!=null)remaining+=segment.BuiltBlueprint.Length*.001f;
        var totalLength=remaining;
        for(var i=0;i<barrel.segments.Length;i++)
        {
            var segment=barrel.segments[i];var native=segment?.model;if(native==null||segment!.BuiltBlueprint==null)continue;
            var length=segment.BuiltBlueprint.Length*.001f;
            if(length<=.001f)continue;
            remaining-=length;
            var mesh=MeshFor(native,barrel,length,-remaining,i==0);SetModel(native,mesh);
            segment.barrelCollider?.SetMesh(mesh.vertices,mesh.triangles);
            // Owner remains the native segment for its lifecycle/intersections.
            // Picker association selects the native root cannon, not an undeletable
            // generated barrel segment. No global CanDelete/transform bypass.
            var collider=segment.barrelCollider?.TryCast<IVehicleCollider>();
            if(collider!=null)collider.AssociatedComponent=cannon;
        }
        if(barrel.boreModel!=null)barrel.boreModel.gameObject.SetActive(false);
        // The native breech box is required for root selection/manipulation.
        // Retain only Selection, with its positive-sized volume inside the rear
        // square tube. Keep the functional breech and normal registered lifecycle.
        var breech=cannon.breechModel?.TryCast<CannonBreech>();
        if(breech!=null)
        {
            var box=breech.breechCollider?.TryCast<IVehicleCollider>();
            if(box?.Owner?.Pointer==breech.Pointer)
            {
                box.AssociatedComponent=cannon;
                if(float.IsFinite(totalLength)&&totalLength>.001f&&box.Transform!=null)
                {
                    var length=Math.Min(.20f,totalLength);
                    var origin=barrel.transform.TransformPoint(barrel.FireLocalPosition);
                    var centre=-totalLength+length/2;
                    Bounds? bounds=null;
                    // Express the rendered tube's world-space box in the native
                    // collider's own frame, including mirroring/parent transforms.
                    foreach(var x in new[]{-.10f,.10f})foreach(var y in new[]{-.10f,.10f})foreach(var z in new[]{centre-length/2,centre+length/2})
                    {
                        var point=box.Transform.InverseTransformPoint(origin+barrel.transform.rotation*new Vector3(x,y,z));
                        if(bounds==null)bounds=new Bounds(point,Vector3.zero);
                        else{var b=bounds.Value;b.Encapsulate(point);bounds=b;}
                    }
                    var size=bounds!.Value.size;var position=bounds.Value.center;
                    if(float.IsFinite(size.x)&&float.IsFinite(size.y)&&float.IsFinite(size.z)&&size.x>0&&size.y>0&&size.z>0&&
                        float.IsFinite(position.x)&&float.IsFinite(position.y)&&float.IsFinite(position.z))breech.breechCollider!.Set(size,position);
                }
                box.Type=ColliderType.Selection;
            }
            breech.model?.SetInteractionColliderAssociation(cannon);
            breech.model?.SetVisible(false);
            // Separate load-area and rotated trunnion clearance remain native.
            // Tube segment pickers still select the root Cannon.
        }
    }
    [HarmonyPostfix,HarmonyPatch(typeof(Cannon),nameof(Cannon.OnVehicleBuilt))]
    private static void VehicleBuilt(Cannon __instance)
    {if(IsLauncher(__instance))try{RelaxSight(__instance);Apply(__instance);}catch(Exception ex){Warn("Vehicle build refresh",ex);}}
    [HarmonyPrefix,HarmonyPatch(typeof(Cannon),nameof(Cannon.Release))]
    private static void Release(Cannon __instance)
    {
        buildSignatures.Remove(__instance.Pointer);
        if(__instance.Behaviour!=null)diagnostics.Remove(__instance.Behaviour.Pointer);
        if(meshOwners.Remove(__instance.Pointer,out var owned))
            foreach(var key in owned)if(meshes.Remove(key,out var mesh))UnityEngine.Object.Destroy(mesh);
    }
    [HarmonyPrefix,HarmonyPatch(typeof(WeaponBehaviour),nameof(WeaponBehaviour.Release))]
    private static void WeaponReleased(WeaponBehaviour __instance)=>diagnostics.Remove(__instance.Pointer);

    private static Texture2D? texture;
    private static Sprite? icon;
    private static Sprite LoadIcon()
    {
        if(icon!=null)return icon;
        var path=Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location)!,"SprocketShellSelector.assets","atgm-launcher.png");
        texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
        if(!ImageConversion.LoadImage(texture,File.ReadAllBytes(path),false))throw new InvalidOperationException("Launcher icon decode failed.");
        icon=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f),100);
        texture.hideFlags=HideFlags.HideAndDontSave;icon.hideFlags=HideFlags.HideAndDontSave;
        return icon;
    }
    [HarmonyPrefix,HarmonyPatch(typeof(Sprocket.GameIcons),nameof(Sprocket.GameIcons.GetOrDefault))]
    private static bool GetIcon(string __0,ref Sprite __result)
    {
        if(__0!=AtgmLauncherModel.Guid&&__0!="atgmLauncher")return true;
        try{__result=LoadIcon();return false;}catch(Exception ex){Warn("Icon lookup",ex);return true;}
    }
    [HarmonyPostfix,HarmonyPatch(typeof(PartDefinitionCardFactory),nameof(PartDefinitionCardFactory.CreateCard))]
    private static void Card(PartDefinition __0,PartDisplayCard __result)
    {
        if(__0?.guid!=AtgmLauncherModel.Guid||__result==null)return;
        try
        {
            __result.Icon=LoadIcon();
        }
        catch(Exception ex){Warn("Icon",ex);}
    }
}

