using BepInEx.Configuration;
using HarmonyLib;
using Sprocket.DamageModelling;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;
namespace SprocketShellSelector;

// Optional mesh/billboard visuals; observes native flight without editing physics.
internal static class RuntimeAtgmTrail
{
    private sealed record Hidden(Renderer Renderer,bool Enabled);
    private sealed record Effect(float Spawn,GameObject Root,GameObject Flame,Light Glow,List<Hidden> Native)
    {internal float LastPuff=-100;}
    private sealed record Puff(GameObject Root,Material Material)
    {internal float Born,Size;internal Vector3 Origin;internal bool Active;}
    private static readonly Dictionary<(IntPtr,int),Effect> Effects=new();
    private static readonly List<Puff> Puffs=new();
    private static ConfigEntry<bool> enabled=null!;
    private static Material? smokeMaterial,flameMaterial,bodyMaterial,finMaterial;
    private static Texture2D? texture;
    private static Mesh? noseMesh;
    private static bool failed;
    internal static void Configure(ConfigFile cfg)=>enabled=cfg.Bind("ATGM Visuals","Enabled",true,"ATGM-only procedural missile, motor flame and short smoke puffs. Exhaust starts at motor ignition; no flight changes.");
    private static void Tint(Material material,Color color)
    {foreach(var name in new[]{"_UnlitColor","_BaseColor","_Color"})if(material.HasProperty(name))material.SetColor(name,color);}
    private static Material MakeMaterial(Shader shader,Color color)
    {var result=new Material(shader){hideFlags=HideFlags.HideAndDontSave};Tint(result,color);return result;}
    private static void Prepare()
    {
        if(bodyMaterial!=null && noseMesh!=null)return;
        var shader=Shader.Find("HDRP/Unlit") ?? Shader.Find("Unlit/Transparent") ?? Shader.Find("Unlit/Color");
        if(shader==null)throw new InvalidOperationException("No available unlit visual shader.");
        flameMaterial=MakeMaterial(shader,new Color(18,.08f,.015f,1));
        bodyMaterial=MakeMaterial(shader,new Color(.18f,.22f,.13f,1));
        finMaterial=MakeMaterial(shader,new Color(.09f,.1f,.08f,1));
        smokeMaterial=MakeMaterial(shader,new Color(.65f,.65f,.63f,.28f));
        texture=new Texture2D(32,32){hideFlags=HideFlags.HideAndDontSave};
        var pixels=new Color[1024];
        for(var y=0;y<32;y++)for(var x=0;x<32;x++)
        {var radius=MathF.Sqrt(MathF.Pow((x-15.5f)/15.5f,2)+MathF.Pow((y-15.5f)/15.5f,2));pixels[y*32+x]=new Color(1,1,1,MathF.Pow(Math.Max(0,1-radius),2));}
        texture.SetPixels(pixels);texture.Apply();
        foreach(var name in new[]{"_UnlitColorMap","_BaseMap","_MainTex"})if(smokeMaterial.HasProperty(name))smokeMaterial.SetTexture(name,texture);
        smokeMaterial.EnableKeyword("_UNLIT_COLOR_MAP");
        foreach(var setting in new[]{("_SurfaceType",1),("_BlendMode",0),("_ZWrite",0),("_SrcBlend",5),("_DstBlend",10),("_AlphaSrcBlend",1),("_AlphaDstBlend",10)})
            if(smokeMaterial.HasProperty(setting.Item1))smokeMaterial.SetFloat(setting.Item1,setting.Item2);
        smokeMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");smokeMaterial.renderQueue=3000;
        var vertices=new Vector3[14];var triangles=new int[72];vertices[12]=new Vector3(0,0,1);vertices[13]=Vector3.zero;
        for(var i=0;i<12;i++)
        {var a=i*MathF.PI*2/12;vertices[i]=new Vector3(MathF.Cos(a)*.5f,MathF.Sin(a)*.5f,0);var j=(i+1)%12;
         triangles[i*6]=i;triangles[i*6+1]=j;triangles[i*6+2]=12;triangles[i*6+3]=j;triangles[i*6+4]=i;triangles[i*6+5]=13;}
        noseMesh=new Mesh(){hideFlags=HideFlags.HideAndDontSave};noseMesh.vertices=vertices;noseMesh.triangles=triangles;noseMesh.RecalculateNormals();noseMesh.RecalculateBounds();
        Plugin.ModLog.LogInfo($"[ATGM trail] Missile/smoke materials ready: {shader.name}");
    }
    private static void SetRenderer(Renderer renderer,Material? material)
    {renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;}
    private static GameObject Part(PrimitiveType type,Transform parent,Vector3 position,Vector3 scale,Material? material)
    {
        var part=GameObject.CreatePrimitive(type);
        var collider=part.GetComponent<Collider>();if(collider!=null){collider.enabled=false;Object.Destroy(collider);}
        part.transform.SetParent(parent,false);part.transform.localPosition=position;part.transform.localScale=scale;
        SetRenderer(part.GetComponent<Renderer>(),material);return part;
    }
    private static Effect Create(float spawn,Vector3 position,float calibre,Transform native)
    {
        Prepare();var root=new GameObject("Shell Selector missile");root.transform.position=position;
        var hidden=new List<Hidden>();
        try
        {
            var diameter=Math.Clamp(calibre*.9f,.03f,.35f);var length=diameter*8;
            var body=Part(PrimitiveType.Cylinder,root.transform,Vector3.zero,new Vector3(diameter,length*.5f,diameter),bodyMaterial);
            body.transform.localRotation=Quaternion.Euler(90,0,0);
            var nose=new GameObject("Missile nose");nose.transform.SetParent(root.transform,false);nose.transform.localPosition=new Vector3(0,0,length*.5f);
            nose.transform.localScale=new Vector3(diameter,diameter,diameter*1.7f);nose.AddComponent<MeshFilter>().sharedMesh=noseMesh;SetRenderer(nose.AddComponent<MeshRenderer>(),bodyMaterial);
            for(var i=0;i<4;i++)
            {var a=i*MathF.PI*.5f;var fin=Part(PrimitiveType.Cube,root.transform,new Vector3(MathF.Cos(a)*diameter*.65f,MathF.Sin(a)*diameter*.65f,-length*.33f),new Vector3(diameter*.9f,diameter*.07f,diameter*1.6f),finMaterial);fin.transform.localRotation=Quaternion.Euler(0,0,i*90);}
            var flame=Part(PrimitiveType.Sphere,root.transform,new Vector3(0,0,-length*.5f-diameter),new Vector3(diameter*.7f,diameter*.7f,diameter*1.5f),flameMaterial);flame.SetActive(false);
            var glow=flame.AddComponent<Light>();glow.type=LightType.Point;
            glow.color=new Color(1,.025f,.005f);glow.intensity=8;
            glow.range=Math.Clamp(diameter*20,1.5f,5);glow.shadows=LightShadows.None;
            if(native!=null)foreach(var renderer in native.GetComponentsInChildren<MeshRenderer>(true))
            {hidden.Add(new(renderer,renderer.enabled));renderer.enabled=false;}
            Plugin.ModLog.LogInfo("[ATGM trail] Missile model created; exhaust waits for motor ignition.");
            return new(spawn,root,flame,glow,hidden);
        }
        catch{foreach(var item in hidden)if(item.Renderer!=null)item.Renderer.enabled=item.Enabled;Object.Destroy(root);throw;}
    }
    private static void EmitPuff(Vector3 position,float calibre)
    {
        var puff=Puffs.FirstOrDefault(p=>!p.Active && p.Root!=null);
        if(puff==null)
        {
            if(Puffs.Count>=128)return;
            var quad=Part(PrimitiveType.Quad,new GameObject("ATGM smoke puff holder").transform,Vector3.zero,Vector3.one,smokeMaterial);
            var holder=quad.transform.parent.gameObject;var material=new Material(smokeMaterial!);quad.GetComponent<Renderer>().sharedMaterial=material;
            puff=new(holder,material);Puffs.Add(puff);
        }
        puff.Born=Time.time;puff.Origin=position;puff.Size=Math.Clamp(calibre*1.4f,.06f,.5f);puff.Active=true;puff.Root.SetActive(true);
    }
    private static void UpdatePuffs()
    {
        var camera=Camera.main;
        foreach(var puff in Puffs.Where(p=>p.Active))
        {
            if(puff.Root==null){puff.Active=false;continue;}
            var age=Time.time-puff.Born;var fraction=age/.65f;
            if(fraction>=1){puff.Active=false;puff.Root.SetActive(false);continue;}
            puff.Root.transform.position=puff.Origin+Vector3.up*age*.3f;
            puff.Root.transform.localScale=Vector3.one*puff.Size*(1+fraction*2);
            if(camera!=null)puff.Root.transform.rotation=Quaternion.LookRotation(puff.Root.transform.position-camera.transform.position);
            Tint(puff.Material,new Color(.65f,.65f,.63f,.28f*(1-fraction)));
        }
    }
    [HarmonyPostfix,HarmonyPatch(typeof(ProjectileRegister),nameof(ProjectileRegister.FixedUpdate))]
    private static void UpdateVisuals(ProjectileRegister __instance)
    {
        if(failed || !enabled.Value || !RuntimeAtgm.Ready || !RuntimeAtgm.Enabled){ClearRegister(__instance.Pointer);UpdatePuffs();return;}
        try
        {
            var live=new HashSet<(IntPtr,int)>();
            foreach(var flight in RuntimeAtgm.VisualFlights(__instance))
            {
                var key=(__instance.Pointer,flight.Id);live.Add(key);
                if(Effects.TryGetValue(key,out var old) && (old.Spawn!=flight.Spawn || old.Root==null))Remove(key);
                if(!Effects.TryGetValue(key,out var effect))Effects[key]=effect=Create(flight.Spawn,flight.Position,flight.Calibre,flight.Native);
                effect.Root.transform.position=flight.Position;
                if(flight.Direction.sqrMagnitude>.1f)effect.Root.transform.rotation=Quaternion.LookRotation(flight.Direction);
                effect.Flame.SetActive(flight.Burning);
                if(flight.Burning && Time.time-effect.LastPuff>=.015f)
                {effect.LastPuff=Time.time;EmitPuff(flight.Position-flight.Direction*flight.Calibre*4.5f,flight.Calibre);}
            }
            foreach(var key in Effects.Keys.Where(k=>k.Item1==__instance.Pointer && !live.Contains(k)).ToArray())Remove(key);
            UpdatePuffs();
        }
        catch(Exception ex){failed=true;ClearAll();Plugin.ModLog.LogWarning("[ATGM trail] Visuals disabled; flight unchanged: "+ex);}
    }
    private static void Remove((IntPtr,int) key)
    {
        if(!Effects.Remove(key,out var effect))return;
        foreach(var item in effect.Native)if(item.Renderer!=null)item.Renderer.enabled=item.Enabled;
        if(effect.Root!=null)Object.Destroy(effect.Root);
    }
    private static void ClearRegister(IntPtr register){foreach(var key in Effects.Keys.Where(k=>k.Item1==register).ToArray())Remove(key);}
    private static void ClearAll()
    {foreach(var key in Effects.Keys.ToArray())Remove(key);foreach(var puff in Puffs){if(puff.Root!=null)Object.Destroy(puff.Root);Object.Destroy(puff.Material);}Puffs.Clear();}
    [HarmonyPrefix,HarmonyPatch(typeof(ProjectileRegister),nameof(ProjectileRegister.Release))]
    private static void Release(ProjectileRegister __instance,int __0)=>Remove((__instance.Pointer,__0));
    [HarmonyPrefix,HarmonyPatch(typeof(ProjectileRegister),nameof(ProjectileRegister.DestroyAll))]
    private static void Reset(ProjectileRegister __instance)=>ClearAll();
}
