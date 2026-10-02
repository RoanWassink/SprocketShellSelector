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
        if(RuntimeSpall.Impact is not {ProfileId:"aphe",LiveImpact:true} || (__0.Type & ProjectileEffectType.Explosion)==0)return;
        var assets=__instance.explosionAssets;
        if(assets==null)return;
        __state=new(__instance,assets,assets.MinScale,assets.MaxScale);
        assets.MinScale*=(float)RuntimeSpall.Settings.ApheExplosionScale;
        assets.MaxScale*=(float)RuntimeSpall.Settings.ApheExplosionScale;
        __instance.explosionAssets=assets;
        Plugin.ModLog.LogInfo($"[APHE Effect] Actual asset scale bounds multiplied by {RuntimeSpall.Settings.ApheExplosionScale:0.00}");
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
        if(context is not {ProfileId:"aphe",LiveImpact:true,ExplosionPending:true})return;
        context.ExplosionPending=false; // Also guards the nested visual-only PlayEffect call.
        if(!RuntimeSpall.Settings.ApheExplosionEffect)return;
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
