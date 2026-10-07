using HarmonyLib;
using Sprocket.PartImporting;
using Sprocket.Vehicles;
using Sprocket.Vehicles.AmmunitionStorage;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using Object=UnityEngine.Object;
namespace SprocketShellSelector;

[HarmonyPatch]
internal static class AtgmAmmoBoxVisuals
{
    private sealed record Visual(GameObject Root,Material Olive,Material Dark,Material Band);
    private static readonly Dictionary<IntPtr,Visual> models=new();
    private static readonly HashSet<string> warnings=new();
    private static Sprite? icon;
    private static void Guard(string area,Action action)
    {try{action();}catch(Exception ex){if(warnings.Add(area))Plugin.ModLog.LogWarning("[ATGM ammo box] "+area+": "+ex.Message);}}
    private static Material Paint(string name,Color color)
    {
        var shader=Shader.Find("HDRP/Lit")??throw new InvalidOperationException("HDRP/Lit unavailable");
        var m=new Material(shader){name=name,hideFlags=HideFlags.HideAndDontSave};
        m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.2f);HDMaterial.ValidateMaterial(m);return m;
    }
    private static void Box(Transform parent,Material material,string name,Vector3 position,Vector3 size)
    {
        var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name=name;obj.layer=parent.gameObject.layer;
        var collider=obj.GetComponent<Collider>();collider.enabled=false;Object.Destroy(collider);
        obj.transform.SetParent(parent,false);obj.transform.localPosition=position;obj.transform.localScale=size;
        obj.GetComponent<MeshRenderer>().sharedMaterial=material;
    }
    internal static void Rebuild(AmmoRack rack)=>Guard("Container model",()=>
    {
        if(!RuntimeAtgmAmmoBox.Own(rack))return;
        var native=rack.model?.TryCast<AmmoRackModel>();if(native==null||native.material==null)return;
        Remove(rack.Pointer);
        var root=new GameObject("ATGM automatic reserve container");root.layer=rack.gameObject.layer;
        root.transform.SetParent(rack.transform,false);root.transform.localPosition=rack.boundsCollider?.Centre??Vector3.zero;
        var olive=Paint("ATGM container olive",new(.19f,.24f,.16f,1));
        var dark=Paint("ATGM container handles",new(.05f,.06f,.045f,1));
        var band=Paint("ATGM container markings",new(.65f,.55f,.18f,1));
        models[rack.Pointer]=new(root,olive,dark,band);
        var d=rack.boundsSize;float x=Mathf.Max(.05f,d.x),y=Mathf.Max(.05f,d.y),z=Mathf.Max(.05f,d.z),t=Mathf.Min(.025f,y*.1f);
        Box(root.transform,olive,"Missile container",Vector3.zero,new(x,y,z));
        Box(root.transform,dark,"Lid seam",new(0,y/2-t,0),new(x+.005f,t,z+.005f));
        Box(root.transform,olive,"Lid",new(0,y/2-t/3,0),new(x+.012f,t,z+.012f));
        foreach(float side in new[]{-1f,1f})
        {
            Box(root.transform,dark,"Carry handle",new(side*(x/2+.006f),0,0),new(.015f,y*.35f,z*.25f));
            Box(root.transform,band,"Missile band",new(0,0,side*z*.3f),new(x+.007f,y+.007f,Mathf.Min(.04f,z*.08f)));
        }
        // Decorative pieces have no colliders; the native rack retains selection,
        // physical obstruction, installation health, damage and finite ammunition.
        root.SetActive(native.Visible);
    });
    internal static void Remove(IntPtr pointer)
    {
        if(!models.Remove(pointer,out var v))return;
        v.Root.SetActive(false);Object.Destroy(v.Root);Object.Destroy(v.Olive);Object.Destroy(v.Dark);Object.Destroy(v.Band);
    }
    [HarmonyPostfix,HarmonyPatch(typeof(AmmoRack),nameof(AmmoRack.Build))]
    private static void Built(AmmoRack __instance)
    {if(RuntimeAtgmAmmoBox.Own(__instance))Rebuild(__instance);}
    [HarmonyPrefix,HarmonyPatch(typeof(VehicleComponent),nameof(VehicleComponent.ReleaseInternal))]
    private static void Released(VehicleComponent __instance)=>Remove(__instance.Pointer);
    [HarmonyPostfix,HarmonyPatch(typeof(AmmoRackModel),nameof(AmmoRackModel.ApplyRendererSettings))]
    [HarmonyPatch(typeof(AmmoRackModel),nameof(AmmoRackModel.ApplyRenderers))]
    private static void Visible(AmmoRackModel __instance)=>Guard("Container visibility",()=>
    {
        var parts=__instance.VehicleObject.Components;
        int count=parts.Cast<Il2CppSystem.Collections.Generic.IReadOnlyCollection<VehicleComponent>>().Count;
        for(int i=0;i<count;i++)if(parts[i].TryCast<AmmoRack>() is {} rack&&RuntimeAtgmAmmoBox.Own(rack))
        {if(!models.TryGetValue(rack.Pointer,out var v))Rebuild(rack);else v.Root.SetActive(__instance.Visible);}
    });
    private static Sprite Icon()
    {
        if(icon!=null)return icon;
        var path=Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location)!,"SprocketShellSelector.assets","atgm-ammo-box.png");
        var tex=new Texture2D(2,2,TextureFormat.RGBA32,false){hideFlags=HideFlags.HideAndDontSave};
        if(!ImageConversion.LoadImage(tex,File.ReadAllBytes(path),false))throw new InvalidOperationException("Container icon decode failed");
        icon=Sprite.Create(tex,new Rect(0,0,tex.width,tex.height),new(.5f,.5f),100);icon.hideFlags=HideFlags.HideAndDontSave;return icon;
    }
    [HarmonyPrefix,HarmonyPatch(typeof(Sprocket.GameIcons),nameof(Sprocket.GameIcons.GetOrDefault))]
    private static bool GetIcon(string __0,ref Sprite __result)
    {if(__0!=AtgmAmmoBoxRules.Guid&&__0!="atgmAmmoBox")return true;try{__result=Icon();return false;}catch(Exception ex){Guard("Icon",()=>throw ex);return true;}}
    [HarmonyPostfix,HarmonyPatch(typeof(PartDefinitionCardFactory),nameof(PartDefinitionCardFactory.CreateCard))]
    private static void Card(PartDefinition __0,PartDisplayCard __result)
    {if(__0?.guid==AtgmAmmoBoxRules.Guid&&__result!=null)Guard("Container card",()=>__result.Icon=Icon());}
}
