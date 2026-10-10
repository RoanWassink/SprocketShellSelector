using Sprocket.DamageModelling;
using Sprocket.Vehicles.Cannons;
using Sprocket.Vehicles.Weapons.Cannons;
using UnityEngine;
using NVector=System.Numerics.Vector3;
using Object=UnityEngine.Object;
namespace SprocketShellSelector;

// Independent of smoke/trail visibility. Link state survives optional renderer failure.
internal static class RuntimeWire
{
    private sealed class Entry
    {
        internal readonly float Spawn,Born;
        internal readonly string Definition;
        internal readonly CannonBehaviour Weapon;
        internal readonly ShellProfile Profile;
        internal readonly WireCable Cable;
        internal WireCableRenderer? Renderer;
        internal bool Finished;
        internal WireSettlingCable? Settling;
        internal WireGroundSampler? Ground;
        internal Entry(ProjectileInstance p,CannonBehaviour weapon,ShellProfile profile,WireCable cable)
        {Spawn=p.spawnTime;Born=Time.time;Definition=p.Definition.Guid.ToString();Weapon=weapon;Profile=profile;Cable=cable;}
    }
    private const int VisualBudget=32;
    private static readonly Dictionary<(IntPtr Register,int Id),Entry> Cables=new();
    private static readonly HashSet<string> Warnings=new();
    private static Material? material;
    private static NVector Position(Vector3 p)=>new(p.x,p.y,p.z);
    internal static bool Connected(IntPtr register,int id)=>Cables.TryGetValue((register,id),out var e)&&e.Cable.Connected;
    private static void Warn(string key,Exception? ex=null)
    {if(Warnings.Add(key))Plugin.ModLog.LogWarning("[ATGM wire] "+key+(ex==null?"":": "+ex.Message));}
    private static Material PrepareMaterial()
    {
        if(material!=null)return material;
        var shader=Shader.Find("HDRP/Unlit")??Shader.Find("Unlit/Transparent")??Shader.Find("Unlit/Color");
        if(shader==null)throw new InvalidOperationException("No cable shader available.");
        material=new Material(shader){hideFlags=HideFlags.HideAndDontSave};
        foreach(var name in new[]{"_UnlitColor","_BaseColor","_Color"})if(material.HasProperty(name))material.SetColor(name,new Color(.12f,.09f,.05f,1));
        return material;
    }
    internal static void Attach(ProjectileRegister register,ProjectileInstance p,CannonBehaviour weapon,ShellProfile profile)
    {
        if(profile.Wire is not {Transport:"wire"} settings)return;
        try
        {
            var key=(register.Pointer,p.ID);
            if(Cables.Remove(key,out var prior))prior.Renderer?.Dispose();
            // The native spawn position is the fixed launch-site anchor, not an invented offset.
            var cable=new WireCable(Position(p.position),Position(p.position),settings.MaximumLength,settings.SampleDistance,256,settings.RetentionSeconds,Time.time);
            var entry=new Entry(p,weapon,profile,cable);Cables[key]=entry;
            try
            {
                if(Cables.Values.Count(e=>e.Renderer!=null)>=VisualBudget)
                {
                    var oldest=Cables.Values.Where(e=>e.Renderer!=null).OrderByDescending(e=>e.Finished).ThenBy(e=>e.Born).First();
                    oldest.Renderer!.Dispose();oldest.Renderer=null;
                    Warn("Cable visual budget reached; command links remain active.");
                }
                entry.Renderer=new WireCableRenderer(PrepareMaterial(),p.transform?.gameObject.layer??0,(float)settings.DisplayWidth);
                entry.Renderer.Update(cable);
            }
            catch(Exception ex){entry.Renderer?.Dispose();entry.Renderer=null;Warn("Optional cable rendering unavailable; wire link remains active",ex);}
            Plugin.ModLog.LogInfo($"[ATGM wire] ATTACH id={p.ID} profile={profile.Id} budget={settings.MaximumLength:0}m width={settings.DisplayWidth:0.000}m fixed launch-site anchor={p.position}");
        }
        catch(Exception ex){Warn("Cable initialization failed",ex);}
    }
    private static void Finish(Entry e,Vector3? position=null)
    {
        if(e.Finished)return;
        if(position is {} point)e.Cable.Observe(Position(point),Time.time,true,true);
        e.Cable.Finish(Time.time);e.Finished=true;
        try
        {
            e.Settling=new WireSettlingCable(e.Cable.RenderPoints(.15f),e.Cable.PaidOut,e.Cable.RetentionSeconds);
            e.Settling.WholeCableDetach(Time.time);
            e.Ground=new WireGroundSampler(e.Settling.OriginalDetachPoints);
            e.Renderer?.UpdatePoints(e.Settling.Points);
        }catch(Exception ex){e.Renderer?.Dispose();e.Renderer=null;Warn("Cable update failed; physics unchanged",ex);}
    }
    internal static void Hit(ProjectileInstance p)
    {
        foreach(var e in Cables.Where(c=>c.Key.Id==p.ID&&c.Value.Spawn==p.spawnTime&&c.Value.Definition==p.Definition.Guid.ToString()).Select(c=>c.Value))Finish(e,p.position);
    }
    internal static void Release(IntPtr register,int id)
    {if(Cables.TryGetValue((register,id),out var e))Finish(e);}
    internal static void Tick(ProjectileRegister register)
    {
        WireGroundSampler.BeginFrame(Time.fixedTime);
        foreach(var pair in Cables.ToArray())
        {
            var key=pair.Key;var e=pair.Value;
            if(e.Cable.Expired(Time.time)){e.Renderer?.Dispose();Cables.Remove(key);continue;}
            if(key.Register!=register.Pointer)continue;
            if(e.Finished)
            {
                try
                {
                    if(e.Settling is {} settled)
                    {
                        settled.Step(Time.fixedDeltaTime,Time.time,probe=>e.Ground?.Surface(probe.Vertex,Time.fixedTime),
                            gravity:Math.Clamp(-Physics.gravity.y,0,100));
                        e.Renderer?.UpdatePoints(settled.Points);
                    }
                }
                catch(Exception ex){e.Renderer?.Dispose();e.Renderer=null;Warn("Detached cable rendering unavailable",ex);}
                continue;
            }
            try
            {
                if(!register.activeIdMap.TryGetValue(key.Id,out var index)){Finish(e);continue;}
                var p=register.pool[index];
                if(p.spawnTime!=e.Spawn||p.Definition.Guid.ToString()!=e.Definition||((int)p.flags&1)==0||((int)p.flags&14)!=0){Finish(e);continue;}
                var healthy=e.Weapon.mount?.TryCast<Cannon>() is {} cannon&&cannon.HealthFraction>0;
                var owns=e.Profile.Atgm?.GuidanceMode is null or "none"||RuntimeAtgm.WireOwner(register.Pointer,key.Id);
                var connected=e.Cable.Connected;
                e.Cable.Observe(Position(p.position),Time.time,healthy,owns);
                if(connected&&!e.Cable.Connected)Plugin.ModLog.LogInfo($"[ATGM wire] BREAK id={key.Id} reason={e.Cable.BreakReason} payout={e.Cable.PaidOut:0.0}m; propulsion and impact unchanged.");
                if(!e.Cable.Connected){Finish(e);continue;}
                try{e.Renderer?.Update(e.Cable);}catch(Exception ex){e.Renderer?.Dispose();e.Renderer=null;Warn("Cable renderer update unavailable; link retained",ex);}
            }
            catch(Exception ex){Finish(e);Warn("Cable observation failed",ex);}
        }
        // Active states are bounded by native active pool identities; retained models by32.
        foreach(var pair in Cables.Where(p=>p.Value.Finished).OrderByDescending(p=>p.Value.Born).Skip(VisualBudget).ToArray())
        {pair.Value.Renderer?.Dispose();Cables.Remove(pair.Key);}
    }
    internal static void Clear(IntPtr? register=null)
    {
        foreach(var pair in Cables.Where(p=>register==null||p.Key.Register==register).ToArray())
        {pair.Value.Renderer?.Dispose();Cables.Remove(pair.Key);}
        if(Cables.Count==0){if(material!=null)Object.Destroy(material);material=null;Warnings.Clear();}
    }
}

