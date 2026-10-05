namespace SprocketShellSelector;
internal static class ShellEraPolicy
{
    internal static int Rank(string? era)=>era?.ToLowerInvariant() switch
    {"ww1" or "wwi"=>0,"interwar"=>1,"earlywar"=>2,"midwar"=>3,"latewar"=>4,"coldwar"=>5,_=>-1};
    internal static int Floor(string behavior)=>behavior switch
    {"ap" or "he"=>0,"aphe" or "heat"=>2,"hesh" or "apfsds" or "atgm" or "atgm_gun"=>5,_=>6};
    internal static bool Allowed(ShellProfile profile,string? era)=>
        (profile.MinimumEra==null||Rank(profile.MinimumEra)>=0)&&Rank(era)>=Math.Max(Floor(profile.Behavior),profile.MinimumEra==null?0:Rank(profile.MinimumEra));
    internal static ShellProfile? Effective(ShellProfile? stored,string? era)=>stored!=null&&Allowed(stored,era)?stored:null;
}
