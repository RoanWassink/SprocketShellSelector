namespace SprocketShellSelector;
internal static class AtgmOpticalScope
{
    internal static bool ValidProjection(float x,float y,float depth)
        =>float.IsFinite(x)&&float.IsFinite(y)&&float.IsFinite(depth)&&depth>0;
    internal static bool Allows(bool playerVehicle,bool activeSight,int ownAssociations,bool sharedOrdinary,bool nativeWeaponSight,bool allowedProfile)
        =>playerVehicle&&activeSight&&ownAssociations==1&&!sharedOrdinary&&nativeWeaponSight&&allowedProfile;
}
