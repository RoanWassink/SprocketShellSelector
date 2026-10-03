namespace SprocketShellSelector;
internal static class ShellPayload
{
 internal static void Validate(ShellProfile p)
 {
  if(p.Behavior is not ("ap" or "apfsds" or "aphe" or "he" or "heat" or "hesh")) throw new FormatException("Unknown shell behavior: "+p.Behavior);
  static void Range(double n,double min,double max){if(!double.IsFinite(n)||n<min||n>max)throw new FormatException("Invalid shell payload setting.");}
  Range(p.ChemicalPenetrationMm,0,2000); Range(p.NativeExplosivePower,0,500); Range(p.SpallMultiplier,1,12); Range(p.ConeHalfAngleDegrees,1,90); Range(p.ExplosionScale,.1,3);
  Range(p.SecondPlatePenetrationFactor,.01,1);
  if(p.Behavior is "heat" or "hesh" && p.ChemicalPenetrationMm<=0) throw new FormatException("HEAT/HESH require chemicalPenetrationMm.");
  if(p.Behavior=="he" && p.NativeExplosivePower<=0)throw new FormatException("HE requires nativeExplosivePower.");
 }
}
