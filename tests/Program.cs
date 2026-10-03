using SprocketShellSelector;
var checks = 0; void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
var defaults = new DartSettings();
var dart120 = ShellBallistics.Calculate(120, 800, 1800, defaults);
Check(Math.Abs(dart120.Diameter - .0264) < 1e-7, "120mm example subcalibre diameter");
Check(Math.Abs(dart120.Length - .6) < 1e-6, "120mm example rod length");
Check(dart120.Mass > 5.74 && dart120.Mass < 5.76, "120mm example cylinder mass");
Check(dart120.Velocity > 1480 && dart120.Velocity < 1490, "120mm example bounded velocity");
Check(dart120.PenetratorConstant > 1800, "quality below one must reduce penetration at equal geometry");
foreach (var calibre in new[] { 10d, 20d, 75d, 90d, 150d, 300d })
{
    var dart = ShellBallistics.Calculate(calibre, 800, 1800, defaults);
    var scale = calibre / 120;
    Check(Math.Abs(dart.Diameter / dart120.Diameter - scale) < 1e-6, "diameter scales with selected calibre");
    Check(Math.Abs(dart.Length / dart120.Length - scale) < 1e-6, "length scales with selected calibre");
    Check(Math.Abs(dart.Mass / dart120.Mass - scale * scale * scale) < 1e-4, "mass scales with volume");
    Check(Math.Abs(dart.Velocity - dart120.Velocity) < .001, "scale preserves uncapped mass-based velocity factor");
}
Check(ShellBallistics.Calculate(120, 2000, 1800, defaults).Velocity == 2200, "absolute speed cap");
Check(ShellBallistics.Calculate(120, 800, 1800, defaults with { MaxVelocityFactor = 1.2 }).Velocity == 960, "relative speed cap");
var heavier = ShellBallistics.Calculate(120, 800, 1800, defaults with { Density = 22000 });
Check(heavier.Mass > dart120.Mass && heavier.Velocity < dart120.Velocity, "heavier rod trades speed for mass");
Check(ShellBallistics.Calculate(120, 800, 1800, defaults with { PenetrationQuality = 1.5 }).PenetratorConstant < dart120.PenetratorConstant,
    "higher quality improves K");
Check(ShellBallistics.Calculate(120, 800, 1800, defaults with { DamageMultiplier = .2 }) with { DamageMultiplier = dart120.DamageMultiplier } == dart120,
    "damage tuning leaves ballistics unchanged");
void Reject(Action work, string message)
{
    var rejected = false;
    try { work(); } catch (ArgumentOutOfRangeException) { rejected = true; }
    Check(rejected, message);
}
foreach (var invalid in new[] { 0d, -1d, double.NaN, double.PositiveInfinity, 70000d })
    Reject(() => ShellBallistics.Calculate(invalid, 800, 1800, defaults), "reject invalid calibre");
foreach (var invalid in new[] { 0d, double.NaN, double.PositiveInfinity })
    Reject(() => ShellBallistics.Calculate(120, invalid, 1800, defaults), "reject invalid velocity");
Reject(() => ShellBallistics.Calculate(120, 800, 0, defaults), "reject zero K");
Reject(() => ShellBallistics.Calculate(120, 800, 1800, defaults with { DiameterRatio = double.NaN }), "reject NaN setting");
Reject(() => ShellBallistics.Calculate(120, 800, 1800, defaults with { Density = -1 }), "reject invalid density");
Reject(() => ShellBallistics.Calculate(120, 800, 1800, defaults with { DamageMultiplier = 2 }), "reject invalid damage scale");
var profileJson = ShellProfiles.CreateDefault(defaults);
var profileList = ShellProfiles.Parse(profileJson);
Check(profileList.Count == 2 && profileList[0].Id == "apfsds", "default profile ID");
Check(profileList[0].Settings == defaults, "JSON preserves complete default settings");
Check(ShellBallistics.Calculate(120, 800, 1800, profileList[0].Settings) == dart120, "profile migration preserves ballistics");
Check(ShellProfiles.Resolve(null, true, profileList) == "apfsds", "old selected save migrates");
Check(ShellProfiles.Resolve(null, false, profileList) == "vanilla", "old unselected save stays vanilla");
Check(ShellProfiles.Resolve("vanilla", true, profileList) == "vanilla", "new selection overrides legacy flag");
Check(ShellProfiles.Resolve("removed", true, profileList) == "vanilla", "missing ID falls back to vanilla");
Check(ShellProfiles.Resolve("apfsds", false, profileList) == "apfsds", "new custom selection overrides legacy flag");
var secondJson = profileJson.Replace("apfsds", "apds").Replace("APFSDS (beta)", "APDS experiment");
var reordered = ShellProfiles.Parse(secondJson);
Check(ShellProfiles.Resolve("apds", false, reordered) == "apds", "selection is by stable ID");
var slower = defaults with { VelocityMultiplier = .8 };
Check(ShellBallistics.Calculate(120, 800, 1800, slower).Velocity < dart120.Velocity, "explicit velocity multiplier works");
Reject(() => ShellBallistics.Calculate(120, 800, 1800, defaults with { VelocityMultiplier = double.NaN }), "invalid velocity multiplier");
void BadJson(string json, string message)
{
    var rejected = false;
    try { ShellProfiles.Parse(json); }
    catch (Exception ex) when (ex is FormatException or System.Text.Json.JsonException or ArgumentOutOfRangeException or InvalidOperationException)
    { rejected = true; }
    Check(rejected, message);
}
BadJson(profileJson.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 2"), "unsupported schema");
BadJson(profileJson.Replace("\"apfsds\"", "\"vanilla\""), "reserved ID");
BadJson(profileJson.Replace("\"apfsds\"", "\"AP FSDS\""), "invalid ID");
BadJson(profileJson.Replace("\"penetratorDensity\": 17500", "\"penetratorDensity\": -1"), "invalid profile density");
BadJson(profileJson.Replace("\"penetratorDensity\"", "\"densityTypo\""), "unknown field rejected");
BadJson(profileJson.Replace("\"penetratorDensity\": 17500,", ""), "missing field rejected");
BadJson(profileJson.Replace("\"penetratorDensity\": 17500,", "\"penetratorDensity\": 17500, \"penetratorDensity\": 17500,"), "duplicate field rejected");
var objectJson = System.Text.Json.JsonDocument.Parse(profileJson).RootElement.GetProperty("profiles")[0].GetRawText();
BadJson("{\"schemaVersion\":1,\"profiles\":[" + objectJson + "," + objectJson + "]}", "duplicate profile IDs rejected");
BadJson("{\"schemaVersion\":1,\"profiles\":[]}", "empty profile list rejected");
var twoProfiles = ShellProfiles.Parse("{\"schemaVersion\":1,\"profiles\":[" + objectJson + "," +
    objectJson.Replace("apfsds", "apds").Replace("APFSDS (beta)", "APDS experiment") + "]}");
Check(twoProfiles.Count == 2 && ShellProfiles.Resolve("apds", false, twoProfiles) == "apds", "multiple profiles supported");
Console.WriteLine($"PASS: {checks} shell ballistics and profile checks.");

var temp = Path.Combine(Path.GetTempPath(), "shell-selector-" + Guid.NewGuid());
Directory.CreateDirectory(temp);
try
{
    var legacy = Path.Combine(temp, "legacy.json");
    var cfg = Path.Combine(temp, "legacy.cfg");
    var dest = Path.Combine(temp, "new.json");
    File.WriteAllText(legacy, profileJson);
    File.WriteAllText(cfg, "[APFSDS Beta]\nDensity = 22000\n");
    ShellConfigMigration.EnsureProfileFile(dest, legacy, cfg, defaults);
    Check(File.ReadAllText(dest) == profileJson, "legacy JSON imported exactly");
    File.WriteAllText(legacy, secondJson);
    ShellConfigMigration.EnsureProfileFile(dest, legacy, cfg, defaults);
    Check(File.ReadAllText(dest) == profileJson, "migration runs once");
    Check(File.ReadAllText(legacy) == secondJson, "legacy JSON unchanged");
    var numericDest = Path.Combine(temp, "numeric.json");
    ShellConfigMigration.EnsureProfileFile(numericDest, Path.Combine(temp,"missing.json"), cfg, defaults);
    Check(ShellProfiles.Parse(File.ReadAllText(numericDest))[0].Settings.Density == 22000, "legacy numeric CFG migration");
    Check(File.ReadAllText(cfg) == "[APFSDS Beta]\nDensity = 22000\n", "material CFG unchanged");
    File.WriteAllText(legacy, "{}");
    var invalidDest = Path.Combine(temp, "invalid.json");
    BadJson(File.ReadAllText(legacy), "invalid migration source rejected");
    try { ShellConfigMigration.EnsureProfileFile(invalidDest, legacy, cfg, defaults); } catch (FormatException) { }
    Check(!File.Exists(invalidDest), "invalid import creates no destination");
}
finally { Directory.Delete(temp, true); }
Console.WriteLine($"PASS total: {checks} shell checks including migration.");
var spallSettings = new SpallSettings();
SpallBalance.Validate(spallSettings);
Check(SpallBalance.Parse(SpallBalance.ToJson(spallSettings)) == spallSettings, "complete spall JSON round trip");
Check(SpallBalance.ThicknessRatio(.15, spallSettings) == .08, "thin anchor 8 percent AP volume");
Check(SpallBalance.ThicknessRatio(.75, spallSettings) == 1, "middle anchor same AP volume");
Check(SpallBalance.ThicknessRatio(1.5, spallSettings) == 1.25, "thick anchor 125 percent AP volume");
Check(SpallBalance.ThicknessRatio(10, spallSettings) == 1.25, "bounded thick response");
Check(SpallBalance.ReferenceApVolume(.13,.13) == SpallBalance.ReferenceApVolume(.13,.26), "native AP spall fraction saturates");
Check(SpallBalance.ReferenceApVolume(.13,.004) == 0, "native AP below four percent calibre produces no spall");
Check(SpallBalance.ReferenceApVolume(.13,.1) * SpallBalance.ThicknessRatio(.1/.13,spallSettings) > SpallBalance.ReferenceApVolume(.13,.02) * SpallBalance.ThicknessRatio(.02/.13,spallSettings), "thin mass lower than thick mass");
var previousRatio=0d;
var monotonic=true;
for(var i=0;i<=1000;i++) { var ratio=SpallBalance.ThicknessRatio(i*.003,spallSettings); monotonic &= ratio>=previousRatio; previousRatio=ratio; }
Check(monotonic,"continuous thickness curve monotonic over full sweep");
foreach(var bad in new[] {spallSettings with {ConeMultiplier=0},spallSettings with {ThinCalibres=2},spallSettings with {ApheFragmentCount=257},spallSettings with {ApheFragmentMass=double.NaN},spallSettings with {ApheFragmentK=2000}})
{
    var invalid=false; try {SpallBalance.Validate(bad);} catch(FormatException){invalid=true;}
    Check(invalid,"invalid experimental spall setting rejected");
}
var unknown=false; try {SpallBalance.Parse(SpallBalance.ToJson(spallSettings).Replace("coneMultiplier","coneTypo"));} catch(FormatException){unknown=true;}
Check(unknown,"unknown spall field rejected");
var apheSettings=new DartSettings(.9,2,7800,.1,1,2200,.65,1);
var aphe=ShellBallistics.Calculate(130,861,1800,apheSettings);
var aphePen=Math.Pow(Math.Sqrt(aphe.Mass)*aphe.Velocity/(aphe.PenetratorConstant*Math.Pow(aphe.Diameter*1000*.01,.75)),1.43);
var apPen=Math.Pow(Math.Sqrt(1.59e-5*130*130*130)*861/(1800*Math.Pow(130*.01,.75)),1.43);
Check(aphePen<apPen,"APHE experiment lower native penetration than vanilla AP");
Check(aphe.Velocity==861,"APHE does not gain dart muzzle speed");
Console.WriteLine($"PASS total: {checks} shell, migration and spall checks.");
var sumX=0d;var sumY=0d;var sumZ=0d;var unitSphere=true;
for(var i=0;i<2048;i++) {
    var v=SpallBalance.Sphere((i*.6180339887498949)%1,(i+.5)/2048);
    unitSphere &= Math.Abs(v.X*v.X+v.Y*v.Y+v.Z*v.Z-1)<1e-6;
    sumX+=v.X;sumY+=v.Y;sumZ+=v.Z;
}
Check(unitSphere,"spherical fragments keep normalized directions");
Check(Math.Abs(sumX/2048)<.001 && Math.Abs(sumY/2048)<.001 && Math.Abs(sumZ/2048)<.001,"spherical distribution has no preferred hemisphere");
Check(SpallBalance.Sphere(0,0).Z==-1 && SpallBalance.Sphere(0,1).Z==1,"sphere includes forward and backward directions");
var packageConfig=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../outputs/v0.2.0/config"));
if(Directory.Exists(packageConfig)) {
    var packageProfiles=ShellProfiles.Parse(File.ReadAllText(Path.Combine(packageConfig,"nl.roan.sprocket.shellselector.shells.json")));
    Check(packageProfiles.Any(p=>p.Id=="apfsds") && packageProfiles.Any(p=>p.Id=="aphe"),"test package includes both experiments");
    _=SpallBalance.Parse(File.ReadAllText(Path.Combine(packageConfig,"nl.roan.sprocket.shellselector.spall.json")));
    Check(true,"test package spall JSON validates");
}
Console.WriteLine($"FINAL PASS: {checks} checks.");

var oldQuality=ShellBallistics.Calculate(125,890,1800,new DartSettings() with {PenetrationQuality=.75});
var newQuality=ShellBallistics.Calculate(125,890,1800,new DartSettings());
Check(newQuality with {PenetratorConstant=oldQuality.PenetratorConstant} == oldQuality,"quality adjustment changes only K, not geometry/mass/speed");
var relativePen=Math.Pow((double)oldQuality.PenetratorConstant/newQuality.PenetratorConstant,1.43);
Check(Math.Abs(relativePen-.6)<.001,"quality constant reduces native penetration to sixty percent across calibres");
var calibreInvariant=true;
foreach(var calibre in new[]{20d,75d,125d,200d}) {
 var oldK=ShellBallistics.Calculate(calibre,900,1800,new DartSettings() with{PenetrationQuality=.75}).PenetratorConstant;
 var newK=ShellBallistics.Calculate(calibre,900,1800,new DartSettings()).PenetratorConstant;
 calibreInvariant &= oldK==oldQuality.PenetratorConstant && newK==newQuality.PenetratorConstant;
}
Check(calibreInvariant,"balance constant has no special case for 125mm");
Console.WriteLine($"v0.3.0 PASS: {checks} checks.");
Check(SpallBalance.RemainingPenetrationRatio(1,spallSettings)==spallSettings.ThinRatio,"full remaining penetration produces minimum spall");
Check(SpallBalance.RemainingPenetrationRatio(0,spallSettings)==spallSettings.ThickRatio,"exhausted penetration produces maximum spall ratio");
Check(SpallBalance.RemainingPenetrationRatio(2,spallSettings)==spallSettings.ThinRatio,"remaining fraction above one is bounded");
Check(Math.Abs(SpallBalance.RemainingPenetrationRatio(.5,spallSettings)-.665)<1e-12,"half remaining penetration gives broad linear response");
var priorRemainingRatio=double.PositiveInfinity;
var remainingMonotonic=true;
for(var i=0;i<=1000;i++) {
 var ratio=SpallBalance.RemainingPenetrationRatio(i*.001,spallSettings);
 remainingMonotonic &= ratio<=priorRemainingRatio && ratio>=spallSettings.ThinRatio && ratio<=spallSettings.ThickRatio;
 priorRemainingRatio=ratio;
}
Check(remainingMonotonic,"spall decreases monotonically with remaining penetration across full sweep");
Check(SpallBalance.RemainingPenetrationRatio(.4,spallSettings with{ThinCalibres=.1,MiddleCalibres=1,ThickCalibres=2})==SpallBalance.RemainingPenetrationRatio(.4,spallSettings),"remaining model independent of legacy plate thickness anchors");
foreach(var invalidRemaining in new[]{double.NaN,double.PositiveInfinity,-.1}) {
 var rejected=false;try{SpallBalance.RemainingPenetrationRatio(invalidRemaining,spallSettings);}catch(ArgumentOutOfRangeException){rejected=true;}
 Check(rejected,"invalid remaining penetration rejected");
}
var oldSpallJson=SpallBalance.ToJson(spallSettings).Replace(",\n  \"remainingPenetrationExponent\": 1","");
Check(SpallBalance.Parse(oldSpallJson).RemainingPenetrationExponent==1,"existing spall configuration defaults to linear remaining penetration model");
Console.WriteLine($"v0.4.0 PASS: {checks} checks.");
Check(SpallBalance.ConeFactor(0,spallSettings)==.3,"exhausted kinetic energy retains base cone");
Check(Math.Abs(SpallBalance.ConeFactor(.5,spallSettings)-.3225)<1e-12,"half kinetic energy widens cone by seven point five percent");
Check(Math.Abs(SpallBalance.ConeFactor(1,spallSettings)-.345)<1e-12,"full kinetic energy widens cone by fifteen percent");
Check(SpallBalance.ConeFactor(2,spallSettings)==SpallBalance.ConeFactor(1,spallSettings),"cone widening capped at fifteen percent");
var configTemp=Path.Combine(Path.GetTempPath(),"shell-names-"+Guid.NewGuid());
Directory.CreateDirectory(configTemp);
try {
 var previousFile=Path.Combine(configTemp,"nl.roan.sprocket.shellselector.shells.json");
 File.WriteAllText(previousFile,profileJson);
 var currentFile=ShellConfigMigration.EnsureCurrentProfileFile(configTemp,defaults);
 Check(Path.GetFileName(currentFile)=="sprocket.shellselector.shells.json","current profile filename contains no author name");
 Check(File.ReadAllText(currentFile)==profileJson && File.ReadAllText(previousFile)==profileJson,"renaming preserves existing settings and source backup");
 File.WriteAllText(previousFile,secondJson);
 ShellConfigMigration.EnsureCurrentProfileFile(configTemp,defaults);
 Check(File.ReadAllText(currentFile)==profileJson,"new filename remains authoritative after migration");
} finally {Directory.Delete(configTemp,true);}
Console.WriteLine($"v0.5.0 PASS: {checks} checks.");
foreach(var invalidMultiplier in new[]{0d,13d,double.NaN}){
 var rejected=false;try{SpallBalance.Validate(spallSettings with{ApheSpallMultiplier=invalidMultiplier});}catch(FormatException){rejected=true;}
 Check(rejected,"invalid APHE native spall multiplier rejected");
}
var legacyBurstConfig=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../outputs/v0.5.0/config/sprocket.shellselector.spall.json"));
if(File.Exists(legacyBurstConfig)){
 var legacyBurst=SpallBalance.Parse(File.ReadAllText(legacyBurstConfig));
 Check(legacyBurst.ApheSpallMultiplier==4 && legacyBurst.ApheExplosionEffect,"previous fuse config imports new native AP defaults without reset");
}
Console.WriteLine($"v0.6.0 PASS: {checks} checks.");
foreach(var halfAngle in new[]{30d,60d,90d}){
 var bounded=true;var normalized=true;
 for(var i=0;i<2048;i++){
  var point=SpallBalance.Cone((i*.6180339887498949)%1,(i+.5)/2048,halfAngle);
  bounded &= point.Z>=Math.Cos(halfAngle*Math.PI/180)-1e-6 && point.Z<=1;
  normalized &= Math.Abs(point.X*point.X+point.Y*point.Y+point.Z*point.Z-1)<1e-6;
 }
 Check(bounded,"cone stays within requested forward half-angle");
 Check(normalized,"cone directions remain unit length");
}
Check(Math.Abs(SpallBalance.Cone(0,1,90).Z)<1e-6,"180 degree full cone reaches hemisphere boundary without backwards fragments");
foreach(var bad in new[]{new SpallSettings() with{ApheConeHalfAngleDegrees=91},new SpallSettings() with{ApheExplosionScale=0}}){
 var rejected=false;try{SpallBalance.Validate(bad);}catch(FormatException){rejected=true;}
 Check(rejected,"invalid experimental cone or effect scale rejected");
}
Console.WriteLine($"v0.7.0 EXPERIMENT PASS: {checks} checks.");
var incoming=new System.Numerics.Vector3(1,0,0);
var slopeNormal=System.Numerics.Vector3.Normalize(new System.Numerics.Vector3(1,1,0));
var downward=PlateDeflection.Bend(incoming,slopeNormal,8,30);
Check(downward.Y<0,"sloping plate bends shot downward without gravity hardcoding");
Check(System.Numerics.Vector3.Dot(downward,slopeNormal)>0,"deflected shot remains directed into plate");
Check(Math.Abs(downward.Length()-1)<1e-6,"deflection preserves unit direction and thus speed");
Check(System.Numerics.Vector3.Distance(downward,PlateDeflection.Bend(incoming,-slopeNormal,8,30))<1e-6,"plate-normal orientation does not change deflection");
Check(PlateDeflection.Bend(incoming,new(1,-1,0),8,30).Y>0,"mirrored slope produces mirrored bend");
Check(PlateDeflection.Bend(incoming,incoming,8,30)==incoming,"normal incidence stays straight");
Check(PlateDeflection.Bend(incoming,slopeNormal,0,30)==incoming,"zero maximum angle disables bend");
var turn=System.Numerics.Quaternion.CreateFromAxisAngle(System.Numerics.Vector3.UnitZ,1);
Check(System.Numerics.Vector3.Distance(System.Numerics.Vector3.Transform(downward,turn),PlateDeflection.Bend(System.Numerics.Vector3.Transform(incoming,turn),System.Numerics.Vector3.Transform(slopeNormal,turn),8,30))<1e-6,"deflection rotates with plate and incoming shot");
Console.WriteLine($"v0.8.0 EXPERIMENT PASS: {checks} checks.");
var repairDir = Path.Combine(Path.GetTempPath(), "shell-repair-"+Guid.NewGuid());
Directory.CreateDirectory(repairDir);
try {
 var path=Path.Combine(repairDir,"sprocket.shellselector.shells.json");
 var old="{\"schemaVersion\":1,\"profiles\":["+objectJson+"]}";
 File.WriteAllText(path,old);
 Check(ShellConfigMigration.EnsureApheProfile(path),"missing APHE repaired");
 Check(ShellProfiles.Parse(File.ReadAllText(path)).Count==2,"repair adds APHE");
 Check(ShellProfiles.Parse(File.ReadAllText(path))[0].Settings==defaults,"repair preserves APFSDS values");
 Check(File.ReadAllText(path+".pre-aphe-backup")==old,"repair backs up exact original");
 var repaired=File.ReadAllText(path);
 Check(!ShellConfigMigration.EnsureApheProfile(path)&&File.ReadAllText(path)==repaired,"repair is idempotent");
 File.WriteAllText(path,profileJson.Replace("\"penetrationQuality\": 0.65","\"penetrationQuality\": 0.7"));
 var custom=File.ReadAllText(path);
 Check(!ShellConfigMigration.EnsureApheProfile(path)&&File.ReadAllText(path)==custom,"existing APHE preserved byte for byte");
 File.WriteAllText(path,"{}");
 try {ShellConfigMigration.EnsureApheProfile(path);} catch(FormatException) {}
 Check(File.ReadAllText(path)=="{}","invalid configuration not overwritten");
} finally {Directory.Delete(repairDir,true);}
Console.WriteLine($"v0.8.2 PASS: {checks} checks.");
var copyNode=System.Text.Json.Nodes.JsonNode.Parse(profileJson)!;
copyNode["profiles"]![0]!["id"]="long_rod";
copyNode["profiles"]![0]!["label"]="Long rod";
copyNode["profiles"]![0]!["behavior"]="apfsds";
var copied=ShellProfiles.Parse(copyNode.ToJsonString())[0];
Check(copied.Behavior=="apfsds" && copied.Id=="long_rod","APFSDS behavior independent of id");
Check(profileList[0].Behavior=="apfsds" && profileList[1].Behavior=="aphe","old configurations retain dedicated behaviors");
copyNode["profiles"]![0]!["behavior"]="ap";
Check(ShellProfiles.Parse(copyNode.ToJsonString())[0].Behavior=="ap","explicit AP overrides legacy semantics");
foreach(var behavior in new[]{"heat","hesh"}){
 copyNode["profiles"]![0]!["behavior"]=behavior;
 copyNode["profiles"]![0]!["chemicalPenetrationMm"]=350;
 var p=ShellProfiles.Parse(copyNode.ToJsonString())[0];
 Check(p.Behavior==behavior && p.ChemicalPenetrationMm==350,"chemical payload parses");
}
copyNode["profiles"]![0]!["behavior"]="he";
copyNode["profiles"]![0]!["nativeExplosivePower"]=40;
Check(ShellProfiles.Parse(copyNode.ToJsonString())[0].NativeExplosivePower==40,"native HE power parses");
foreach(var invalid in new[]{copied with{Behavior="unknown"},copied with{Behavior="heat"},copied with{Behavior="he"},copied with{ChemicalPenetrationMm=2001},copied with{NativeExplosivePower=501},copied with{ExplosionScale=double.NaN},copied with{ConeHalfAngleDegrees=91}}){
 var rejected=false;try{ShellPayload.Validate(invalid);}catch(FormatException){rejected=true;}
 Check(rejected,"unsupported payload rejected");
}
Console.WriteLine($"v0.9.0 EXPERIMENT PASS: {checks} checks.");
if(args.Length>0){
 var presets=ShellProfiles.Parse(File.ReadAllText(args[0]));
 Check(presets.Count is 5 or 6,"preset file loads");
 Check(presets.Count(p=>p.Behavior=="apfsds")==2,"two APFSDS variants load");
 Check(presets.Any(p=>p.Behavior=="he")&&presets.Any(p=>p.Behavior=="heat")&&presets.Any(p=>p.Behavior=="hesh"),"all payload examples present");
 Console.WriteLine($"PRESETS PASS: {checks} checks.");
}
var heatPreset=copied with{Behavior="heat",ChemicalPenetrationMm=400,NativeExplosivePower=40};
Check(ShellBalance.ChemicalPenetration(heatPreset,75)==300,"HEAT scales penetration with calibre");
Check(ShellBalance.ChemicalPenetration(heatPreset,125)==500,"125mm chemical capacity");
Check(ShellBalance.BlastPower(heatPreset,100)==40 && Math.Abs(ShellBalance.BlastPower(heatPreset,200)-320)<1e-9,"HE power scales with calibre cubed");
Check(ShellBalance.BlastPower(heatPreset,500)==500,"HE power has upper bound");
Check(ShellBalance.FragmentTarget("heat",75)>=12,"75mm HEAT has useful fragment budget");
Check(ShellBalance.FragmentTarget("hesh",300)==32,"large HESH stays within native fragment cap");
Check(Math.Abs(ShellBalance.FragmentVolume("heat",200)/ShellBalance.FragmentVolume("heat",100)-8)<1e-9,"spall volume scales with calibre cubed");
var shortProfile=copied with{Settings=copied.Settings with{LengthInCalibres=3}};
var longProfile=copied with{Settings=copied.Settings with{LengthInCalibres=6}};
Check(ShellBalance.BallisticSettings(shortProfile).PenetrationQuality<copied.Settings.PenetrationQuality,"short rod penetration efficiency lower");
Check(ShellBalance.BallisticSettings(longProfile).PenetrationQuality>copied.Settings.PenetrationQuality,"long rod penetration efficiency higher");
Check(ShellBalance.RodCone(shortProfile.Settings)>ShellBalance.RodCone(longProfile.Settings),"short rod has wider cone than long rod");
Check(ShellBalance.BallisticSettings(profileList[1])==profileList[1].Settings,"APHE geometry not affected by rod modifier");
Check(ShellBalance.Visual(.65,125)<ShellBalance.Visual(1.5,125),"HE visual larger than APHE at equal calibre");
Check(ShellBalance.FragmentSpeed("heat",100)>ShellBalance.FragmentSpeed("hesh",100),"HEAT concentrated fast fragments HESH slower broad fragments");
Console.WriteLine($"v0.9.1 BALANCE PASS: {checks} checks.");
var layers=new ChemicalLayers();
Check(!layers.Observe(false),"initial air does not consume first plate");
Check(!layers.Observe(true),"first solid keeps initial budget");
Check(!layers.Observe(true),"contiguous solid does not count as spaced plate");
Check(!layers.Observe(false),"air gap arms degradation without changing air simulation");
Check(layers.Observe(true)&&layers.Degraded,"second solid after gap degrades budget");
Check(layers.Observe(true),"degraded budget persists through later solids");
Check(ShellBalance.SecondPlateBudget(heatPreset,100,350)==60,"HEAT second plate capped to 15 percent");
Check(ShellBalance.SecondPlateBudget(heatPreset,100,20)==20,"second plate never restores lost penetration");
Check(ShellBalance.SecondPlateBudget(heatPreset,200,700)==120,"second plate capacity scales with calibre");
Check(ShellBalance.FragmentTarget("aphe",75)>=21 && ShellBalance.FragmentTarget("hesh",100)==32,"stronger APHE HESH fragment budgets");
copyNode["profiles"]![0]!["behavior"]="heat";
copyNode["profiles"]![0]!["secondPlatePenetrationFactor"]=.2;
Check(ShellProfiles.Parse(copyNode.ToJsonString())[0].SecondPlatePenetrationFactor==.2,"second plate factor configurable");
foreach(var factor in new[]{0d,1.1,double.NaN}){
 var rejected=false;try{ShellPayload.Validate(heatPreset with{SecondPlatePenetrationFactor=factor});}catch(FormatException){rejected=true;}
 Check(rejected,"invalid layer factor rejected");
}
Console.WriteLine($"v0.9.2 LAYERS PASS: {checks} checks.");
foreach(var behavior in new[]{"aphe","hesh","heat"}){
 var target=ShellBalance.FragmentSpeed(behavior,100);
 Check(ShellBalance.PayloadFragmentSpeed(behavior,100,0)==target,"barely perforating payload retains fragment energy");
 Check(ShellBalance.PayloadFragmentSpeed(behavior,100,5000)==target*1.25,"payload fragment energy bounded");
 Check(ShellBalance.PayloadFragmentSpeed(behavior,100,double.NaN)==target,"invalid native speed has finite fallback");
 Check(ShellBalance.FragmentTarget(behavior,75)>=26,"75mm payload has dense burst");
}
Check(ShellBalance.FragmentVolume("hesh",100)>ShellBalance.FragmentVolume("aphe",100),"HESH heavier broad burst than APHE");
Check(ShellBalance.FragmentVolume("aphe",100)>ShellBalance.FragmentVolume("heat",100),"APHE broad damage HEAT concentrated damage");
Console.WriteLine($"v0.9.3 PAYLOAD PASS: {checks} checks.");
var releaseDefaults=ShellProfiles.Parse(ReleaseProfiles.Defaults());
Check(releaseDefaults.Count==6 && !releaseDefaults.Any(p=>p.Id=="apfsds"),"release has six presets without standard dart");
Check(releaseDefaults.Count(p=>p.Behavior=="apfsds")==2,"release includes long and short rods");
Check(ShellProfiles.Resolve("apfsds",false,releaseDefaults)=="apfsds_long","old dart saves map to long rod");
Check(ShellProfiles.Resolve(null,true,releaseDefaults)=="apfsds_long","legacy selected flag maps to long rod");
var upgradeDir=Path.Combine(Path.GetTempPath(),"shell-release-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(upgradeDir);
try{
 var path=Path.Combine(upgradeDir,"profiles.json");File.WriteAllText(path,profileJson);
 Check(ReleaseProfiles.Upgrade(path),"old config upgraded");
 var upgraded=ShellProfiles.Parse(File.ReadAllText(path));
 Check(upgraded.Count==6 && !upgraded.Any(p=>p.Id=="apfsds"),"stock old dart removed while new presets added");
 Check(upgraded.Single(p=>p.Id=="aphe").Settings==profileList[1].Settings,"existing APHE tuning preserved");
 Check(File.ReadAllText(path+".pre-v094-backup")==profileJson,"upgrade backs up exact config");
 Check(!ReleaseProfiles.Upgrade(path),"release upgrade idempotent");
 File.WriteAllText(path,profileJson.Replace("APFSDS (beta)","My custom dart"));ReleaseProfiles.Upgrade(path);
 Check(ShellProfiles.Parse(File.ReadAllText(path)).Any(p=>p.Id=="apfsds"),"named custom old dart retained");
 File.WriteAllText(path,"{}");try{ReleaseProfiles.Upgrade(path);}catch(Exception){}
 Check(File.ReadAllText(path)=="{}","invalid upgrade input preserved");
}finally{Directory.Delete(upgradeDir,true);}
Console.WriteLine($"v0.9.4 RELEASE PASS: {checks} checks.");
var heatGap=heatPreset with{AirGapLossPerCalibre=.35,SecondPlatePenetrationFactor=.15};
var heshGap=heatGap with{Behavior="hesh",AirGapLossPerCalibre=12,SecondPlatePenetrationFactor=.1};
Check(ShellBalance.SpacedRetention(heatGap,100,10,0)==1,"no gap no added HEAT loss");
Check(ShellBalance.SpacedRetention(heatGap,100,10,5)==1,"tiny gap preserves HEAT jet budget");
Check(ShellBalance.SpacedRetention(heatGap,100,10,100)>.8,"thin skirt and modest gap cannot erase HEAT capacity");
Check(ShellBalance.SpacedRetention(heatGap,100,50,100)<ShellBalance.SpacedRetention(heatGap,100,5,100),"thicker front plate disrupts HEAT more");
Check(ShellBalance.SpacedRetention(heatGap,100,10,500)<ShellBalance.SpacedRetention(heatGap,100,10,100),"longer gap reduces HEAT retention");
Check(ShellBalance.SpacedRetention(heshGap,100,10,50)<.11,"separated HESH strongly decoupled");
Check(ShellBalance.SpacedRetention(heshGap,100,10,0)==1,"contiguous HESH has no added loss");
Check(ShellBalance.SpacedRetention(heshGap,100,10,5)<ShellBalance.SpacedRetention(heatGap,100,10,5),"HESH more sensitive to small real gap");
Check(Math.Abs(ShellBalance.SpacedRetention(heatGap,100,10,100)-ShellBalance.SpacedRetention(heatGap,200,20,200))<1e-10,"relative plate and gap scale with calibre");
Check(ShellBalance.SpacedRetention(heatGap with{AirGapLossPerCalibre=0},100,10,500)==1,"per-shell zero sensitivity disables additional loss");
var distanceLayers=new ChemicalLayers();
Check(distanceLayers.ObserveBlock(false,1000)==null,"initial free flight excluded");
Check(distanceLayers.ObserveBlock(true,4)==null,"first solid no degradation");
Check(distanceLayers.ObserveBlock(true,6)==null,"contiguous solid thickness accumulates");
Check(distanceLayers.ObserveBlock(false,20)==null && distanceLayers.ObserveBlock(false,30)==null,"gap segments accumulate without loss in air");
var measuredGap=distanceLayers.ObserveBlock(true,40);
Check(measuredGap is {} mg && mg.PlateRhaMm==10 && mg.GapMm==50,"next solid reports exact preceding plate and gap once");
Check(distanceLayers.ObserveBlock(true,5)==null,"same solid never repeats previous gap penalty");
distanceLayers.ObserveBlock(false,100);
Check(distanceLayers.ObserveBlock(true,5) is {} nextGap && nextGap.PlateRhaMm==45 && nextGap.GapMm==100,"later gap tracked independently");
foreach(var behavior in new[]{heatGap,heshGap}){
 double last=1;var bounded=true;
 for(var gap=0;gap<2000;gap++){var value=ShellBalance.SpacedRetention(behavior,100,20,gap);bounded &= value<=last && value>=behavior.SecondPlatePenetrationFactor && value<=1;last=value;}
 Check(bounded,"gap curve monotonic bounded and cannot restore penetration");
}
Console.WriteLine($"v0.9.6 DISTANCE PASS: {checks} checks.");
Check(!ShellBalance.SuppressPayloadBurst("heat",true,true),"HEAT original can spall again after a later perforation");
Check(ShellBalance.SuppressPayloadBurst("heat",false,true),"HEAT secondary fragments do not trigger repeated payload bursts");
Check(ShellBalance.SuppressPayloadBurst("hesh",true,true),"HESH retains single payload burst");
Check(ShellBalance.SuppressPayloadBurst("aphe",true,true),"APHE retains single payload burst");
Check(!ShellBalance.SuppressPayloadBurst("heat",true,false),"first HEAT burst allowed");
Console.WriteLine($"v0.9.7 MULTIPLATE PASS: {checks} checks.");

// A valid APFSDS entry must not be blamed for another behavior's invalid settings.
var earlyHeatNode = System.Text.Json.Nodes.JsonNode.Parse(objectJson)!;
earlyHeatNode["id"] = "early_heat";
earlyHeatNode["label"] = "Early HEAT";
earlyHeatNode["behavior"] = "heat";
earlyHeatNode["chemicalPenetrationMm"] = 250;
earlyHeatNode["maximumVelocityFactor"] = .75;
var mixedInvalidJson = "{\"schemaVersion\":1,\"profiles\":[" + objectJson + "," + earlyHeatNode.ToJsonString() + "]}";
var previousCulture = System.Globalization.CultureInfo.CurrentCulture;
try
{
 System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("nl-NL");
 try { ShellProfiles.Parse(mixedInvalidJson); Check(false,"invalid HEAT setting rejected"); }
 catch(ArgumentOutOfRangeException ex)
 {
  Check(ex.Message.StartsWith("Profile 'early_heat': maximumVelocityFactor is 0.75; expected 1–4"),"diagnostic identifies HEAT profile, JSON field, value and inclusive limits using invariant numbers");
  Check(ex.ParamName=="maximumVelocityFactor" && !ex.Message.Contains("APFSDS"),"shared validation no longer incorrectly names APFSDS");
 }
} finally { System.Globalization.CultureInfo.CurrentCulture = previousCulture; }
earlyHeatNode["maximumVelocityFactor"] = 1;
Check(ShellProfiles.Parse("{\"schemaVersion\":1,\"profiles\":["+objectJson+","+earlyHeatNode.ToJsonString()+"]}").Count==2,"minimum valid factor still accepts APFSDS and HEAT together");
earlyHeatNode["spallMultiplier"] = 13;
try { ShellProfiles.Parse("{\"schemaVersion\":1,\"profiles\":["+objectJson+","+earlyHeatNode.ToJsonString()+"]}"); Check(false,"invalid payload rejected"); }
catch(FormatException ex) { Check(ex.Message.Contains("Profile 'early_heat': spallMultiplier is 13; expected 1–12"),"payload diagnostic includes profile and field limits"); }
var diagnosticsDir=Path.Combine(Path.GetTempPath(),"shell-diagnostics-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(diagnosticsDir);
try
{
 var path=Path.Combine(diagnosticsDir,"sprocket.shellselector.shells.json");
 File.WriteAllText(path,mixedInvalidJson);
 foreach(var migrate in new Func<string,bool>[] {ShellConfigMigration.EnsureApheProfile,ReleaseProfiles.Upgrade})
 {
  try {migrate(path);Check(false,"migration rejects invalid custom profile");}
  catch(ArgumentOutOfRangeException ex) {Check(ex.Message.Contains("Profile 'early_heat': maximumVelocityFactor is 0.75"),"migration preserves actionable parse error");}
  Check(File.ReadAllText(path)==mixedInvalidJson && Directory.GetFiles(diagnosticsDir).Length==1,"invalid custom config remains exact with no backups or temporary writes");
 }
} finally {Directory.Delete(diagnosticsDir,true);}
Console.WriteLine($"PROFILE DIAGNOSTICS PASS: {checks} checks.");
