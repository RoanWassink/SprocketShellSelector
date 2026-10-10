using System.Text.Json.Nodes;
using SprocketShellSelector;

internal static class FunctionalChecks
{
 internal static void Run(Action<bool,string> check)
 {
  check(ModularShellDraft.GroupNames.SequenceEqual(new[]{"Tail","Propulsion","Body","Effect","Carrier"}),"native category names fully English");
  check(ModularShellDraft.Fields.All(f=>ModularShellDraft.GroupNames.Contains(f.Group)),"all controls assigned to English native categories");
  var defaults=ReleaseProfiles.Defaults();
  int combinations=0;
  foreach(var tail in ModularShellDraft.Options["tail"])
  foreach(var propulsion in ModularShellDraft.Options["propulsion"])
  foreach(var body in ModularShellDraft.Options["body"])
  foreach(var effect in ModularShellDraft.Options["effect"])
  foreach(var carrier in ModularShellDraft.Options["carrier"])
  {
   var draft=new ModularShellDraft(defaults);var values=new[]{tail,propulsion,body,effect,carrier};
   for(int i=0;i<values.Length;i++)draft.Set(0,"modules."+ModularShellDraft.ModuleKeys[i]+".kind",values[i]);
   var compiled=draft.Compile();check(compiled.Ready,"known combination playable: "+string.Join("/",values));
   var p=ShellProfiles.Parse(compiled.RuntimeJson!)[0];
   check(ShellBalance.ImpactBehavior(p)==(effect=="kinetic"?(body=="longRod"?"apfsds":"ap"):effect=="spall"?"aphe":effect=="blast"?"he":effect),"effect independent of flight: "+string.Join("/",values));
   combinations++;
  }
  check(combinations==480,"all480 native combinations covered");
  var rod=ShellProfiles.Parse(defaults).First(p=>p.Behavior=="apfsds");
  var powered=rod with {Flight=new("rocket","sight","sabot"),Atgm=new AtgmSettings(LaunchSpeed:50,Acceleration:150)};
  ShellPayload.Validate(powered);
  check(ShellBalance.ImpactBehavior(powered)=="apfsds"&&ShellBalance.Powered(powered),"powered rod remains rod impact");
  check(ShellBalance.RequiredTechnologyIds(powered).Distinct().Order().SequenceEqual(new[]{"shellSelector_apfsds","shellSelector_atgm"}.Order()),"powered rod requires both native technologies");
  var ballistic=powered with {Flight=new("ballistic","sight","fullBore")};
  check(ShellBalance.Calculate(120,800,1800,ballistic).Velocity==ShellBalance.Calculate(120,800,1800,rod).Velocity,"guided ballistic launch velocity remains native ballistic calculation");
  check(ShellBalance.Calculate(120,800,1800,powered).Velocity==50,"powered rod uses fixed motor launch velocity");
  check(!ShellBalance.Powered(ballistic)&&ShellBalance.FlightEnabled(ballistic),"ballistic guidance independent motor");
  var delayed=powered with {Atgm=powered.Atgm! with {MaximumFlightTime=1,GuidanceDelay=2,MotorDelay=3}};ShellPayload.Validate(delayed);
  check(true,"explicit modular late delays remain valid inert choices");
  var context=new ShellEraContext(ShellNativeDate.Maximum,new[]{"All"},new[]{new ShellNativeDate(1900,1,1)},0,new HashSet<string>{"apfsds"});
  check(!ShellEraPolicy.Allowed(powered,context),"missing missile native tech blocks powered rod");
  context=context with {AvailableBehaviors=new HashSet<string>{"apfsds","atgm"}};
  check(ShellEraPolicy.Allowed(powered,context),"both native techs allow powered rod");
  var restricted=powered with {NativeTechnologyIds=new[]{"custom_enabled"}};
  check(!ShellEraPolicy.Allowed(restricted,context),"explicit missing native tech blocks");
  context=context with {AvailableTechnologies=new HashSet<string>{"shellSelector_apfsds","shellSelector_atgm","custom_enabled"}};
  check(ShellEraPolicy.Allowed(restricted,context),"loaded explicit native tech allows");
  var uiDraft=new ModularShellDraft(defaults);
  uiDraft.Set(0,"modules.propulsion.kind","rocket");uiDraft.Set(0,"modules.tail.kind","none");
  check(!uiDraft.Active(0,"guidanceMode")&&uiDraft.Active(0,"acceleration"),"unguided rocket exposes motor but hides guidance");
  uiDraft.Set(0,"modules.propulsion.kind","ballistic");uiDraft.Set(0,"modules.tail.kind","guided");
  check(uiDraft.Active(0,"maxTurnRate")&&!uiDraft.Active(0,"acceleration")&&uiDraft.Active(0,"maximumVelocity"),"guided ballistic exposes steering and native velocity without motor");
  uiDraft.Set(0,"modifiers.maximumFlightTime","1");uiDraft.Set(0,"modifiers.guidanceDelay","2");
  check(uiDraft.Analyze(0).Warnings.Any(w=>w.Contains("after expiry")),"late guidance is visible compiler warning");
  uiDraft.Set(0,"modifiers.maxTurnRate","0");
  check(uiDraft.Analyze(0).Warnings.Any(w=>w.Contains("zero")),"zero turn rate weak design warning");
  var before=uiDraft.Value(0,"modifiers.guidanceDelay").GetValue<double>();uiDraft.Set(0,"modules.tail.kind","none");
  check(!uiDraft.Active(0,"guidanceDelay")&&uiDraft.Value(0,"modifiers.guidanceDelay").GetValue<double>()==before,"inactive values retained");
  uiDraft.Set(0,"modules.propulsion.kind","rocket");uiDraft.Set(0,"modifiers.launchSpeed","180");uiDraft.Set(0,"modifiers.flightSpeed","100");
  check(uiDraft.Analyze(0).Status.StartsWith("Invalid draft"),"invalid cross-field draft remains analyzable for correction");
  var syncRoot=JsonNode.Parse(defaults)!.AsObject();
  var syncProfile=syncRoot["profiles"]![0]!.AsObject();syncProfile["flight"]=new JsonObject{["propulsion"]="rocket",["guidance"]="sight",["carrier"]="sabot"};
  var sync=new ModularShellDraft(syncRoot.ToJsonString());sync.Set(0,"modifiers.guidanceMode","keyboard");
  check(ShellProfiles.Parse(sync.Compile().RuntimeJson!)[0].Flight?.Guidance=="keyboard","existing explicit descriptor consumes changed guidance mode");
  var fixtures=JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"modular-fixtures","functional.json")))!["cases"]!.AsArray();
  foreach(var f in fixtures)
  {
   var expected=f!["expected"]!["runtimeProfile"]!.AsObject();
   var baseline=f["legacy"] is JsonObject legacy?JsonNode.Parse(legacy.ToJsonString())!.AsObject():JsonNode.Parse(f["modifiers"]!.ToJsonString())!.AsObject();
   if(!baseline.ContainsKey("behavior")&&f["legacy"]==null)baseline["behavior"]=expected["behavior"]!.GetValue<string>();
   var doc=new JsonObject{["schemaVersion"]=1,["profiles"]=new JsonArray(baseline)};
   var draft=new ModularShellDraft(doc.ToJsonString());
   if(f["modules"]!=null)foreach(var g in ModularShellDraft.ModuleKeys)draft.Set(0,"modules."+g+".kind",f["modules"]![g]!.GetValue<string>());
   var result=JsonNode.Parse(draft.Compile().RuntimeJson!)!["profiles"]![0]!;
   check(ModularShellDraft.SameJson(expected.ToJsonString(),result.ToJsonString()),"JS/native compiler fixture parity: "+f["case"]);
   var p=ShellProfiles.Parse(draft.Compile().RuntimeJson!)[0];
   check(ShellBalance.RequiredTechnologyIds(p).Distinct().Order().SequenceEqual(f["expected"]!["requiredTechnologyIds"]!.AsArray().Select(n=>n!.GetValue<string>()).Order()),"JS/native tech parity: "+f["case"]);
  }
 }
}


