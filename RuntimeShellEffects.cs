using HarmonyLib;
using Sprocket.DamageModelling;
using Sprocket.Vehicles.Weapons;
namespace SprocketShellSelector;
[HarmonyPatch]
internal static class RuntimeShellEffects
{
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
