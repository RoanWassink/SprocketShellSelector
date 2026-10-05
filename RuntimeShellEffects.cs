using HarmonyLib;
using Sprocket.DamageModelling;
using Sprocket.Vehicles.Weapons;
namespace SprocketShellSelector;
[HarmonyPatch]
internal static class RuntimeShellEffects
{
    private sealed record ScaleState(ProjectileImpactEffect Owner,ProjectileImpactEffect.ShellImpactEffectAssets Assets,float Minimum,float Maximum);
    [HarmonyPrefix,HarmonyPatch(typeof(ProjectileImpactEffect),nameof(ProjectileImpactEffect.PlayEffect))]
    private static void Scale(ProjectileImpactEffect __instance,ProjectileEffectInfo __0,out ScaleState? __state)
    {
        __state=null;
        var context=RuntimeSpall.Impact;
        if(context?.ArmourEffectPlaying==true)return;
        if(context is not {LiveImpact:true} || context.Behavior is not ("aphe" or "heat" or "hesh" or "he") || (__0.Type & ProjectileEffectType.Explosion)==0)return;
        var assets=__instance.explosionAssets;
        if(assets==null)return;
        __state=new(__instance,assets,assets.MinScale,assets.MaxScale);
        var scale=ShellBalance.Visual(context.Behavior=="aphe" ? RuntimeSpall.Settings.ApheExplosionScale : context.Profile!.ExplosionScale,context.GunDiameter*1000);
        assets.MinScale*=(float)scale;
        assets.MaxScale*=(float)scale;
        __instance.explosionAssets=assets;
        Plugin.ModLog.LogInfo($"[APHE Effect] {context.Behavior} asset scale bounds multiplied by {scale:0.00}");
    }
    [HarmonyFinalizer,HarmonyPatch(typeof(ProjectileImpactEffect),nameof(ProjectileImpactEffect.PlayEffect))]
    private static void RestoreScale(ScaleState? __state)
    {
        if(__state==null)return;
        __state.Assets.MinScale=__state.Minimum;__state.Assets.MaxScale=__state.Maximum;
        __state.Owner.explosionAssets=__state.Assets;
    }
    [HarmonyPostfix,HarmonyPatch(typeof(ProjectileEffectConfig),nameof(ProjectileEffectConfig.PlayEffect))]
    private static void Play(ProjectileEffectConfig __instance,ProjectileEffectInfo __0)
    {
        var context=RuntimeSpall.Impact;
        if(context?.ArmourEffectPlaying==true)return;
        if(context is {LiveImpact:true} && context.ArmourExplosions.Count>0)
        {
            var positions=context.ArmourExplosions.ToArray();context.ArmourExplosions.Clear();
            context.ArmourEffectPlaying=true;
            try
            {
                foreach(var position in positions)
                    __instance.PlayEffect(new ProjectileEffectInfo {Type=ProjectileEffectType.Explosion,Position=position,
                        HitNormal=__0.HitNormal,HitVelocity=__0.HitVelocity,Parent=__0.Parent,Calibre=35});
            }
            catch(Exception ex){Plugin.ModLog.LogWarning("[ERA] Visual unavailable: "+ex.Message);}
            finally{context.ArmourEffectPlaying=false;}
        }
        if(context is {LiveImpact:true,ChemicalVisualPlayed:false} && context.Behavior is "heat" or "hesh" && (__0.Type & ProjectileEffectType.Explosion)==0)
        { context.ChemicalVisualPlayed=true; context.ExplosionPending=true; context.ExplosionPosition=__0.Position; }
        if(context is not {LiveImpact:true,ExplosionPending:true} || context.Behavior is not ("aphe" or "heat" or "hesh"))return;
        context.ExplosionPending=false; // Also guards the nested visual-only PlayEffect call.
        if(context.Behavior=="aphe" && !RuntimeSpall.Settings.ApheExplosionEffect)return;
        try
        {
            __instance.PlayEffect(new ProjectileEffectInfo {
                Type=ProjectileEffectType.Explosion,Position=context.ExplosionPosition,
                HitNormal=__0.HitNormal,HitVelocity=__0.HitVelocity,Parent=__0.Parent,
                Calibre=(ushort)Math.Clamp(Math.Round(context.GunDiameter*1000),1,ushort.MaxValue)
            });
            Plugin.ModLog.LogInfo("[APHE Effect] Native explosion visual requested; no added blast damage.");
        }
        catch(Exception ex){Plugin.ModLog.LogError("[APHE Effect] Visual unavailable: "+ex);}
    }
}
