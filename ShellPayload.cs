namespace SprocketShellSelector;
internal static class ShellPayload
{
 internal static void Validate(ShellProfile p)
 {
  if(p.Behavior is not ("ap" or "apfsds" or "aphe" or "he" or "heat" or "hesh" or "atgm"))
   throw new FormatException($"Profile '{p.Id}': behavior is '{p.Behavior}'; expected ap, apfsds, aphe, he, heat, hesh or atgm.");
  void Range(string field,double n,double min,double max)
  {if(!double.IsFinite(n)||n<min||n>max)throw new FormatException(ShellBallistics.RangeError(p.Id,field,n,min,max));}
  Range("chemicalPenetrationMm",p.ChemicalPenetrationMm,0,2000);
  Range("nativeExplosivePower",p.NativeExplosivePower,0,500);
  Range("spallMultiplier",p.SpallMultiplier,1,12);
  Range("coneHalfAngleDegrees",p.ConeHalfAngleDegrees,1,90);
  Range("explosionScale",p.ExplosionScale,.1,3);
  Range("secondPlatePenetrationFactor",p.SecondPlatePenetrationFactor,.01,1);
  Range("airGapLossPerCalibre",p.AirGapLossPerCalibre,0,100);
  Range("referenceCalibreMm",p.ReferenceCalibreMm,10,500);
  AtgmGuidance.Validate(p);
  if(p.Behavior=="atgm" && p.Atgm is null)throw new FormatException($"Profile '{p.Id}': atgm requires flight settings.");
  if(p.Behavior is "heat" or "hesh" or "atgm" && p.ChemicalPenetrationMm<=0)
   throw new FormatException($"Profile '{p.Id}': chemicalPenetrationMm is 0; expected >0–2000 for {p.Behavior}.");
  if(p.Behavior=="he" && p.NativeExplosivePower<=0)
   throw new FormatException($"Profile '{p.Id}': nativeExplosivePower is 0; expected >0–500 for he.");
 }
}
