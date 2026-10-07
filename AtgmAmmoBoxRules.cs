namespace SprocketShellSelector;
internal static class AtgmAmmoBoxRules
{
    internal const string Guid="c04c9eec-84d9-543b-8449-4c065f5a0b52";
    internal const double Reach=2;
    internal static string ReferenceLabel(string name,int calibre,int id,string? profile,string? guidance)=>
        $"{name} | {profile??"profile unavailable"} ({(guidance=="keyboard"?"MCLOS":guidance=="sight"?"SACLOS":"unavailable")}) | {calibre} mm | ID {id}";
    internal static bool WaivesLoader(bool sameVehicle,bool dedicatedLauncher,int cannonId,int breechCannonId,bool compatibleBox)=>
        sameVehicle&&dedicatedLauncher&&cannonId==breechCannonId&&compatibleBox;
    internal static bool Matches(bool sameVehicle,string boxProfile,string? launcherProfile,int boxCalibre,int launcherCalibre,int boxPropellant,int launcherPropellant,double distance)=>
        sameVehicle&&!string.IsNullOrEmpty(boxProfile)&&boxProfile==launcherProfile&&boxCalibre>0&&boxCalibre==launcherCalibre&&
        boxPropellant==launcherPropellant&&double.IsFinite(distance)&&distance>=0&&distance<=Reach;
    internal static double Seconds(double calibre,double propellant)=>15*Math.Pow(Math.Clamp(calibre/135,.25,3),.65)*Math.Pow(Math.Clamp((calibre*3+propellant)/505,.25,5),.2);
    internal static double MechanismMass(int capacity)=>10+2*Math.Max(0,capacity);
    internal static double MechanismCost(int capacity)=>250+40*Math.Max(0,capacity);
}
