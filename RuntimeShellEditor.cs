using BepInEx;
using System.Text.Json.Nodes;
using SprocketJsonEditor;
namespace SprocketShellSelector;
internal static class RuntimeShellEditor
{
 internal static void Open(Action refresh)=>JsonEditor.Open(new ShellEditorSession(refresh));
}
internal sealed class ShellEditorSession:EditorSession
{
 private readonly ShellEditorDraft draft;
 private readonly Action refresh;
 private readonly string path=System.IO.Path.Combine(Paths.ConfigPath,"sprocket.shellselector.shells.json");
 internal ShellEditorSession(Action refresh){this.refresh=refresh;draft=new ShellEditorDraft(File.ReadAllText(path));}
 public override string Title=>"Shell editor";
 public override string ItemName=>"shell";
 public override JsonArray Items=>draft.Profiles;
 public override JsonArray Templates=>JsonNode.Parse(ReleaseProfiles.Defaults())!["profiles"]!.AsArray();
 public override IEnumerable<string> Fields(int index)
 {
   var p=Items[index]!.AsObject();yield return "behavior";
   foreach(var key in p.Select(x=>x.Key))if(key is not ("id" or "behavior")&&Relevant(key,p["behavior"]?.GetValue<string>()??"ap"))yield return key;
   if(!p.ContainsKey("minimumEra"))yield return "minimumEra";
 }
 public override JsonNode Value(int index,string field)=>Items[index]![field]??JsonValue.Create(field=="behavior"?ShellProfiles.Parse(draft.Root.ToJsonString())[index].Behavior:"")!;
 public override string Caption(int index,string field)=>Friendly(field)+RangeHint(field);
 public override string[]? Choices(string field)=>field switch {"behavior"=>new[]{"ap","apfsds","aphe","he","heat","hesh","atgm","atgm_gun"},"guidanceMode"=>new[]{"sight","keyboard","none"},"launchSpeedMode"=>new[]{"fixed","cannon"},_=>null};
 public override string ChoiceLabel(string field,string value)=>ChoiceLabel(value);
 public override void Set(int index,string field,string value)=>draft.Set(index,field,value);
 public override void SetChoice(int index,string field,string value){if(field=="behavior")draft.ChangeBehavior(index,value);else draft.Set(index,field,value);}
 public override int Duplicate(JsonObject template)=>draft.Duplicate(template);
 public override void Save(){draft.Save(path);refresh();}
    private static bool Relevant(string key, string behavior)
    {
        if(key is "flightSpeed" or "maxTurnRate" or "maximumFlightTime" or "guidanceDelay" or "guidanceMode" or "launchSpeed" or "launchSpeedMode" or "launchSpeedMultiplier" or "acceleration" or "motorDelay" or "motorBurnTime" or "coastDeceleration")return behavior is "atgm" or "atgm_gun";
        if(key is "chemicalPenetrationMm" or "referenceCalibreMm" or "secondPlatePenetrationFactor" or "airGapLossPerCalibre")return behavior is "heat" or "hesh" or "atgm" or "atgm_gun";
        if(key is "nativeExplosivePower" or "explosionScale")return behavior is "he" or "heat" or "hesh" or "atgm" or "atgm_gun";
        return true;
    }

    private static string ChoiceLabel(string value)=>value switch {"atgm"=>"ATGM launcher","atgm_gun"=>"Gun-launched ATGM","sight"=>"Sight guidance (SACLOS)","keyboard"=>"Manual guidance (MCLOS)","none"=>"Unguided","fixed"=>"Fixed launch speed","cannon"=>"Cannon launch speed",_=>value.ToUpperInvariant()};
    private static string RangeHint(string key)=>key switch
    {
        "penetratorDiameterFactor"=>" — 0.05–0.9", "penetratorLengthInCalibres"=>" — 0.5–10", "penetratorDensity"=>" — 1000–25000",
        "velocityEfficiency"=>" — 0.1–2", "velocityMultiplier"=>" — 0.1–4", "maximumVelocityFactor"=>" — 1–4", "maximumVelocity"=>" — 100–5000", "penetrationQuality"=>" — 0.1–4", "fragmentDamageMultiplier"=>" — 0.01–1",
        "chemicalPenetrationMm"=>" — 0–2000; must be >0 for chemical shells", "nativeExplosivePower"=>" — 0–500; must be >0 for HE", "spallMultiplier"=>" — 1–12", "coneHalfAngleDegrees"=>" — 1–90", "explosionScale"=>" — 0.1–3", "secondPlatePenetrationFactor"=>" — 0.01–1", "airGapLossPerCalibre"=>" — 0–100", "referenceCalibreMm"=>" — 10–500",
        "flightSpeed"=>" — 50–1000", "launchSpeed"=>" — 10–cruise speed", "launchSpeedMultiplier"=>" — 0.01–4", "acceleration"=>" — 0–2000", "motorBurnTime"=>" — 0–60", "coastDeceleration"=>" — 0–100", "motorDelay"=>" — 0–5; below flight time when powered", "maxTurnRate"=>" — 0–90", "maximumFlightTime"=>" — 1–60", "guidanceDelay"=>" — 0–5; below flight time",_=>""
    };
    private static string Friendly(string key)
    {
        var words=System.Text.RegularExpressions.Regex.Replace(key,"([a-z])([A-Z])","$1 $2");
        var units=key switch {"penetratorDensity"=>" (kg/m³)","maximumVelocity" or "flightSpeed" or "launchSpeed"=>" (m/s)","acceleration" or "coastDeceleration"=>" (m/s²)","maxTurnRate"=>" (°/s)","coneHalfAngleDegrees"=>" (°)","chemicalPenetrationMm" or "referenceCalibreMm"=>" (mm)","maximumFlightTime" or "guidanceDelay" or "motorDelay" or "motorBurnTime"=>" (s)",_=>""};
        return char.ToUpperInvariant(words[0])+words.Substring(1)+units;
    }

}
