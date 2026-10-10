namespace SprocketShellSelector;
internal static class PlacedBrickTrigger
{
    internal static bool Reached(bool exactCassette,bool original,bool enter,bool exit,double distance,double physicalDepthMm)=>exactCassette&&original&&enter&&!exit&&double.IsFinite(distance)&&distance>=0&&double.IsFinite(physicalDepthMm)&&physicalDepthMm>0;
    internal static bool CanConsume(bool eligibleThreat,bool eligibleGeometry,double angularWeight,bool alreadyApplied)=>eligibleThreat&&eligibleGeometry&&double.IsFinite(angularWeight)&&angularWeight>0&&!alreadyApplied;
}
