using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using SprocketJsonEditor;

namespace SprocketShellSelector;

// Authoring metadata is kept outside the unchanged schema-1 runtime catalogue.
// Independent module combinations reuse the existing flight and impact adapters.
internal sealed class ModularShellDraft
{
    internal const string Purpose = "modular-shell-design-preview";
    internal static readonly string[] ModuleKeys = { "tail", "propulsion", "body", "effect", "carrier" };
    internal static readonly string[] GroupNames = { "Tail", "Propulsion", "Body", "Effect", "Carrier" };
    internal static readonly IReadOnlyDictionary<string, string[]> Architectures = new Dictionary<string, string[]>
    {
        ["ap"] = new[] { "spin", "ballistic", "fullBore", "kinetic", "fullBore" },
        ["aphe"] = new[] { "spin", "ballistic", "fullBore", "spall", "fullBore" },
        ["he"] = new[] { "spin", "ballistic", "fullBore", "blast", "fullBore" },
        ["heat"] = new[] { "fins", "ballistic", "shapedCharge", "heat", "fullBore" },
        ["hesh"] = new[] { "spin", "ballistic", "fullBore", "hesh", "fullBore" },
        ["apfsds"] = new[] { "fins", "ballistic", "longRod", "kinetic", "sabot" },
        ["atgm"] = new[] { "guided", "rocket", "shapedCharge", "heat", "launcher" },
        ["atgm_gun"] = new[] { "guided", "rocket", "shapedCharge", "heat", "gunLaunch" }
    };
    internal static readonly IReadOnlyDictionary<string, string[]> Options = new Dictionary<string, string[]>
    {
        ["tail"] = new[] { "spin", "fins", "guided", "none" },
        ["propulsion"] = new[] { "ballistic", "rocket" },
        ["body"] = new[] { "fullBore", "longRod", "shapedCharge" },
        ["effect"] = new[] { "kinetic", "spall", "blast", "heat", "hesh" },
        ["carrier"] = new[] { "fullBore", "sabot", "launcher", "gunLaunch" }
    };
    internal sealed record Field(string Key, string Group, double Min, double Max, double Step, string Unit = "", string[]? Choices = null);
    internal static readonly Field[] Fields =
    {
        new("guidanceTransport","Tail",0,0,0,Choices:new[]{"current","wire"}),
        new("wireMaximumLength","Tail",10,20000,10,"m"),new("wireRetentionSeconds","Tail",0,60,1,"s"),
        new("wireDisplayWidth","Tail",.001,.05,.001,"m, visual width"),new("wireSampleDistance","Tail",.1,100,.1,"m"),
        new("guidanceMode","Tail",0,0,0,Choices:new[]{"sight","keyboard","none"}),
        new("maxTurnRate","Tail",0,90,1,"°/s"), new("guidanceDelay","Tail",0,5,.01,"s"),
        new("velocityEfficiency","Propulsion",.1,2,.01), new("velocityMultiplier","Propulsion",.1,4,.01),
        new("maximumVelocityFactor","Propulsion",1,4,.01), new("maximumVelocity","Propulsion",100,5000,10,"m/s"),
        new("flightSpeed","Propulsion",50,1000,1,"m/s"),
        new("launchSpeedMode","Propulsion",0,0,0,Choices:new[]{"fixed","cannon"}),
        new("launchSpeed","Propulsion",10,1000,1,"m/s"),new("launchSpeedMultiplier","Propulsion",.01,4,.01),
        new("acceleration","Propulsion",0,2000,1,"m/s²"),new("motorDelay","Propulsion",0,5,.01,"s"),
        new("motorBurnTime","Propulsion",0,60,.1,"s"),new("coastDeceleration","Propulsion",0,100,.1,"m/s²"),
        new("maximumFlightTime","Propulsion",1,60,.1,"s"),
        new("penetratorDiameterFactor","Body",.05,.9,.01),new("penetratorLengthInCalibres","Body",.5,10,.1),
        new("penetratorDensity","Body",1000,25000,100,"kg/m³"),
        new("penetrationQuality","Effect",.1,4,.01),new("fragmentDamageMultiplier","Effect",.01,1,.01),
        new("nativeExplosivePower","Effect",0,500,.1,"native units"),new("chemicalPenetrationMm","Effect",0,2000,1,"mm"),
        new("spallMultiplier","Effect",1,12,.1),new("coneHalfAngleDegrees","Effect",1,90,1,"°"),
        new("explosionScale","Effect",.1,3,.01),new("secondPlatePenetrationFactor","Effect",.01,1,.01),
        new("airGapLossPerCalibre","Effect",0,100,.1),new("referenceCalibreMm","Effect",10,500,1,"mm")
    };
    private static readonly HashSet<string> ModifierKeys = Fields.Select(f=>f.Key).Append("minimumEra").ToHashSet(StringComparer.Ordinal);
    private readonly string originalRuntime;
    private readonly string? originalAuthoring;
    internal JsonObject Root { get; }
    internal JsonArray Profiles => Root["profiles"]!.AsArray();
    internal sealed record Compilation(string? RuntimeJson, IReadOnlyList<string> Pending)
    { internal bool Ready => RuntimeJson != null; }

    internal ModularShellDraft(string runtimeJson, string? authoringJson = null, string? importedJson = null)
    {
        ShellProfiles.Parse(runtimeJson);
        originalRuntime=runtimeJson; originalAuthoring=authoringJson;
        var input=importedJson??authoringJson;
        Root=input==null ? ImportLegacy(runtimeJson) : ReadDocument(input);
        ValidateIdentity();
        var runtime=JsonNode.Parse(runtimeJson)!;
        if(Root["sourceEnvelope"] is {} envelope && !SameJson(envelope.ToJsonString(),runtime.ToJsonString()))
            throw new IOException("The runtime shell catalogue changed since this modular draft was made. Import the current shell JSON explicitly; no draft or runtime file was changed.");
        var existing=ShellProfiles.Parse(runtimeJson).Select(p=>p.Id).ToHashSet(StringComparer.Ordinal);
        if(!existing.IsSubsetOf(Profiles.Select(p=>p!["id"]!.GetValue<string>())))
            throw new FormatException("Import would remove existing shell IDs referenced by saved vehicles. Retain every existing profile.");
    }
    private static JsonNode Copy(JsonNode value)=>JsonNode.Parse(value.ToJsonString())!;
    internal static bool SameJson(string a,string b)
    {
        using var left=JsonDocument.Parse(a);using var right=JsonDocument.Parse(b);
        bool Equal(JsonElement x,JsonElement y)
        {
            if(x.ValueKind!=y.ValueKind)return false;
            if(x.ValueKind==JsonValueKind.Object)
            {
                var first=x.EnumerateObject().ToArray();var second=y.EnumerateObject().ToArray();
                return first.Length==second.Length&&first.All(p=>y.TryGetProperty(p.Name,out var v)&&Equal(p.Value,v));
            }
            if(x.ValueKind==JsonValueKind.Array)return x.GetArrayLength()==y.GetArrayLength()&&x.EnumerateArray().Zip(y.EnumerateArray()).All(p=>Equal(p.First,p.Second));
            if(x.ValueKind==JsonValueKind.Number)return x.TryGetDecimal(out var xd)&&y.TryGetDecimal(out var yd)?xd==yd:x.GetDouble()==y.GetDouble();
            if(x.ValueKind==JsonValueKind.String)return x.GetString()==y.GetString();
            return true;
        }
        return Equal(left.RootElement,right.RootElement);
    }
    internal static string Infer(JsonObject legacy)=>legacy["behavior"]?.GetValue<string>()??
        (legacy["id"]?.GetValue<string>() is "apfsds" or "aphe" ? legacy["id"]!.GetValue<string>() : "ap");
    internal static JsonObject FromLegacy(JsonObject legacy)
    {
        var behavior=Infer(legacy);
        if(!Architectures.TryGetValue(behavior,out var kinds))throw new FormatException("Unknown shell behavior: "+behavior);
        var modules=new JsonObject();
        for(int i=0;i<ModuleKeys.Length;i++)modules[ModuleKeys[i]]=new JsonObject { ["kind"]=kinds[i] };
        if(legacy["flight"] is JsonObject flight)
        {
            modules["tail"]!["kind"]=flight["guidance"]!.GetValue<string>()=="none"?(kinds[0]=="guided"?"none":kinds[0]):"guided";
            modules["propulsion"]!["kind"]=flight["propulsion"]!.GetValue<string>();
            modules["carrier"]!["kind"]=flight["carrier"]!.GetValue<string>();
        }
        return new JsonObject
        {
            ["id"]=Copy(legacy["id"]!),["label"]=Copy(legacy["label"]!),["originBehavior"]=behavior,
            ["legacyProfile"]=Copy(legacy),["modifiers"]=new JsonObject(),["nativeTechnologyIds"]=legacy["nativeTechnologyIds"] is {} required?Copy(required):new JsonArray(),["modules"]=modules
        };
    }
    private static JsonObject ImportLegacy(string json)
    {
        ShellProfiles.Parse(json);
        var runtime=JsonNode.Parse(json)!.AsObject();
        return new JsonObject { ["schemaVersion"]=1,["purpose"]=Purpose,
            ["profiles"]=new JsonArray(runtime["profiles"]!.AsArray().Select(p=>(JsonNode)FromLegacy(p!.AsObject())).ToArray()),["sourceEnvelope"]=Copy(runtime) };
    }
    private static JsonObject ReadDocument(string json)
    {
        // JsonNode accepts duplicate keys lazily. Reject duplicates at all depths before authoring.
        using var parsed=JsonDocument.Parse(json);
        void Unique(JsonElement e)
        {
            if(e.ValueKind==JsonValueKind.Object)
            {
                var names=new HashSet<string>(StringComparer.Ordinal);
                foreach(var p in e.EnumerateObject()) { if(!names.Add(p.Name))throw new FormatException("Duplicate authoring field: "+p.Name); Unique(p.Value); }
            }
            else if(e.ValueKind==JsonValueKind.Array)foreach(var item in e.EnumerateArray())Unique(item);
        }
        Unique(parsed.RootElement);
        var node=JsonNode.Parse(json)!.AsObject();
        if(node["purpose"]==null)return ImportLegacy(json);
        if(node["schemaVersion"]?.GetValue<int>()!=1||node["purpose"]?.GetValue<string>()!=Purpose)
            throw new FormatException("Expected modular shell schemaVersion 1 and purpose "+Purpose+".");
        return node;
    }
    private void ValidateIdentity()
    {
        if(Profiles.Count is <1 or >16)throw new FormatException("Use 1–16 shell profiles.");
        var ids=new HashSet<string>(StringComparer.Ordinal){"vanilla"};
        var labels=new HashSet<string>(StringComparer.OrdinalIgnoreCase){"Vanilla ammunition"};
        foreach(var node in Profiles)
        {
            var p=node!.AsObject();var id=p["id"]!.GetValue<string>();var label=p["label"]!.GetValue<string>();
            if(id.Length is <1 or >40||id.Any(c=>!(c is >= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '-'))||!ids.Add(id))
                throw new FormatException("Invalid, duplicate or reserved shell ID: "+id);
            if(string.IsNullOrWhiteSpace(label)||label.Length>80||label.Any(char.IsControl)||!labels.Add(label))throw new FormatException("Invalid or duplicate shell name: "+label);
            var legacy=p["legacyProfile"]!.AsObject();
            if(legacy["id"]?.GetValue<string>()!=id||p["originBehavior"]?.GetValue<string>()!=Infer(legacy))
                throw new FormatException("Authoring origin and legacy identity must match: "+id);
            _=ShellProfiles.Parse(new JsonObject{["schemaVersion"]=1,["profiles"]=new JsonArray(Copy(legacy))}.ToJsonString());
            if(p["nativeTechnologyIds"] is not JsonArray requiredTech)throw new FormatException("nativeTechnologyIds must be an array of strings.");
            var requirements=new HashSet<string>(StringComparer.Ordinal);
            foreach(var requirement in requiredTech)
            {
                var key=requirement?.GetValue<string>()??"";
                if(string.IsNullOrWhiteSpace(key)||key.Length>128||key.Any(char.IsControl)||!requirements.Add(key))
                    throw new FormatException("Invalid or duplicate nativeTechnologyIds requirement.");
            }
            foreach(var modifier in p["modifiers"]!.AsObject())
            {
                if(!ModifierKeys.Contains(modifier.Key))throw new FormatException("Unknown modifier: "+modifier.Key);
                if(modifier.Key=="minimumEra")
                {
                    var era=modifier.Value!.GetValue<string>();
                    if(era.Length>80||era.Any(char.IsControl))throw new FormatException("Invalid minimumEra.");
                    continue;
                }
                var field=Fields.Single(f=>f.Key==modifier.Key);
                if(field.Choices!=null)
                {
                    if(!field.Choices.Contains(modifier.Value!.GetValue<string>()))throw new FormatException("Invalid "+modifier.Key+" choice.");
                }
                else
                {
                    var number=modifier.Value!.GetValue<double>();
                    if(!double.IsFinite(number)||number<field.Min||number>field.Max)
                        throw new FormatException(ShellBallistics.RangeError(id,modifier.Key,number,field.Min,field.Max));
                }
            }
            var moduleObject=p["modules"]!.AsObject();
            if(moduleObject.Select(x=>x.Key).Except(ModuleKeys).Any())throw new FormatException("Unknown module group; runtime integration required.");
            foreach(var key in ModuleKeys)if(string.IsNullOrWhiteSpace(p["modules"]?[key]?["kind"]?.GetValue<string>()))throw new FormatException("Missing module: "+key);
            foreach(var key in ModuleKeys)
                if(moduleObject[key]!.AsObject().Any(x=>x.Key!="kind"))throw new FormatException("Unknown module parameter in "+key+"; runtime integration required.");
            if(moduleObject["propulsion"]?["kind"]?.GetValue<string>()=="rocket")
            {
                var index=Profiles.IndexOf(node);
                if(Number(index,"launchSpeed")>Number(index,"flightSpeed"))throw new FormatException("launchSpeed must not exceed flightSpeed, including pending designs.");
            }
        }
    }
    internal static string? Behavior(JsonObject p)
    {
        if(ModuleKeys.Any(g=>!Options[g].Contains(p["modules"]?[g]?["kind"]?.GetValue<string>()??"")))return null;
        if(UnchangedArchitecture(p))return Infer(p["legacyProfile"]!.AsObject());
        return p["modules"]!["effect"]!["kind"]!.GetValue<string>() switch
        {
            "kinetic"=>p["modules"]!["body"]!["kind"]!.GetValue<string>()=="longRod"?"apfsds":"ap",
            "spall"=>"aphe","blast"=>"he","heat"=>"heat","hesh"=>"hesh",_=>null
        };
    }
    private static bool UnchangedArchitecture(JsonObject p)
    {
        var original=FromLegacy(p["legacyProfile"]!.AsObject())["modules"]!;
        return ModuleKeys.All(g=>p["modules"]?[g]?["kind"]?.GetValue<string>()==original[g]!["kind"]!.GetValue<string>());
    }
    internal JsonNode Value(int index,string key)
    {
        var p=Profiles[index]!.AsObject();
        if(key=="label")return p["label"]!;
        if(key.StartsWith("modules.",StringComparison.Ordinal))return p["modules"]![key.Split('.')[1]]!["kind"]!;
        var field=key.StartsWith("modifiers.",StringComparison.Ordinal)?key[10..]:key;
        if(p["modifiers"]?[field] is {} changed)return changed;
        if(p["legacyProfile"]?[field] is {} original)return original;
        if(field=="minimumEra")return JsonValue.Create("")!;
        var target=Behavior(p);
        if(target!=null&&target!=p["originBehavior"]!.GetValue<string>())
        {
            var defaults=DefaultProfile(target);
            if(defaults[field] is {} fallback)return fallback;
        }
        var defaultValue=field switch
        {
            "guidanceTransport"=>JsonValue.Create("current"),"wireMaximumLength"=>JsonValue.Create(4000d),
            "wireRetentionSeconds"=>JsonValue.Create(20d),"wireDisplayWidth"=>JsonValue.Create(.008d),"wireSampleDistance"=>JsonValue.Create(2d),
            "guidanceMode"=>JsonValue.Create("sight"),"launchSpeedMode"=>JsonValue.Create("fixed"),
            "flightSpeed"=>JsonValue.Create(200d),"maxTurnRate"=>JsonValue.Create(20d),"maximumFlightTime"=>JsonValue.Create(25d),
            "guidanceDelay"=>JsonValue.Create(.25d),"launchSpeed"=>JsonValue.Create(Number(index,"flightSpeed")),
            "launchSpeedMultiplier"=>JsonValue.Create(1d),"spallMultiplier"=>JsonValue.Create(1d),"coneHalfAngleDegrees"=>JsonValue.Create(90d),
            "explosionScale"=>JsonValue.Create(1d),"secondPlatePenetrationFactor"=>JsonValue.Create((Behavior(p)??Infer(p["legacyProfile"]!.AsObject()))=="hesh"?.1:.15),
            "airGapLossPerCalibre"=>JsonValue.Create((Behavior(p)??Infer(p["legacyProfile"]!.AsObject()))=="hesh"?12d:.35),"referenceCalibreMm"=>JsonValue.Create(100d),
            _=>JsonValue.Create(0d)
        };
        return defaultValue!;
    }
    internal double Number(int index,string field)=>Value(index,"modifiers."+field).GetValue<double>();
    internal sealed record Capability(string Status,string? Impact,IReadOnlyList<string> Warnings,IReadOnlyList<string> TechnologyIds);
    internal Capability Analyze(int index)
    {
        var p=Profiles[index]!.AsObject();var behavior=Behavior(p);
        if(behavior==null)return new("Pending unknown module",null,new[]{"No adapter for this module; the entire live catalogue stays unchanged."},Array.Empty<string>());
        var warnings=new List<string>();
        var row=Project(p,behavior);
        ShellProfile compiled;
        try{compiled=ShellProfiles.Parse(new JsonObject{["schemaVersion"]=1,["profiles"]=new JsonArray(row)}.ToJsonString())[0];}
        catch(FormatException ex){return new("Invalid draft — correct before Save",behavior,new[]{ex.Message},Array.Empty<string>());}
        if(compiled.Atgm is {} s)
        {
            if(s.GuidanceMode!="none"&&s.MaxTurnRate==0)warnings.Add("Turn rate is zero: guidance cannot turn the projectile.");
            if(s.GuidanceMode!="none"&&s.GuidanceDelay>=s.MaximumFlightTime)warnings.Add("Guidance starts after expiry: this flight remains unguided.");
            if(ShellBalance.Powered(compiled)&&s.MotorDelay>=s.MaximumFlightTime)warnings.Add("Motor starts after expiry: this rocket never ignites.");
        }
        return new(compiled.Flight==null?"Existing native profile":"Playable native proxy",ShellBalance.ImpactBehavior(compiled),warnings,ShellBalance.RequiredTechnologyIds(compiled).Distinct().ToArray());
    }
    internal bool Active(int index,string key)
    {
        if(key=="guidanceTransport")return true;
        if(key.StartsWith("wire",StringComparison.Ordinal))return Value(index,"modifiers.guidanceTransport").GetValue<string>()=="wire";
        var p=Profiles[index]!.AsObject();
        var rocket=p["modules"]!["propulsion"]!["kind"]!.GetValue<string>()=="rocket";
        var guided=p["modules"]!["tail"]!["kind"]!.GetValue<string>()=="guided";
        var steering=guided&&Value(index,"modifiers.guidanceMode").GetValue<string>()!="none";
        var effect=p["modules"]!["effect"]!["kind"]!.GetValue<string>();
        if(key=="guidanceMode")return guided;
        if(key is "maxTurnRate" or "guidanceDelay")return steering;
        if(key is "flightSpeed" or "maximumFlightTime")return rocket||steering;
        if(key=="launchSpeed")return rocket&&Value(index,"modifiers.launchSpeedMode").GetValue<string>()=="fixed";
        if(key=="launchSpeedMultiplier")return rocket&&Value(index,"modifiers.launchSpeedMode").GetValue<string>()=="cannon";
        if(key is "launchSpeedMode" or "acceleration" or "motorDelay" or "motorBurnTime" or "coastDeceleration")return rocket;
        if(key is "velocityEfficiency" or "velocityMultiplier" or "maximumVelocityFactor" or "maximumVelocity")return !rocket;
        if(key is "penetratorDiameterFactor" or "penetratorLengthInCalibres" or "penetratorDensity")return true;
        if(key=="penetrationQuality")return effect is "kinetic" or "spall";
        if(key=="fragmentDamageMultiplier")return Behavior(p) is "ap" or "he";
        if(key=="nativeExplosivePower")return effect=="blast";
        if(key is "chemicalPenetrationMm" or "referenceCalibreMm" or "secondPlatePenetrationFactor" or "airGapLossPerCalibre" or "spallMultiplier" or "coneHalfAngleDegrees")return effect is "heat" or "hesh";
        return key=="explosionScale"&&effect is "heat" or "hesh" or "blast";
    }
    private static JsonObject DefaultProfile(string behavior)=>JsonNode.Parse(ReleaseProfiles.Defaults())!["profiles"]!.AsArray()
        .FirstOrDefault(p=>p?["behavior"]?.GetValue<string>()==behavior)?.AsObject()??new JsonObject();
    internal void Set(int index,string key,string value)
    {
        var p=Profiles[index]!.AsObject();
        if(key=="label") { p["label"]=value; return; }
        if(key=="id")throw new InvalidOperationException("Saved shell IDs cannot be edited.");
        if(key.StartsWith("modules.",StringComparison.Ordinal))
        {
            var group=key.Split('.')[1];if(!Options.TryGetValue(group,out var options)||(!options.Contains(value)&&p["modules"]?[group]?["kind"]?.GetValue<string>()!=value))throw new FormatException("Unsupported module choice.");
            p["modules"]![group]!["kind"]=value;return;
        }
        if(!key.StartsWith("modifiers.",StringComparison.Ordinal)||!ModifierKeys.Contains(key[10..]))throw new FormatException("Unknown modifier.");
        var name=key[10..];var modifiers=p["modifiers"]!.AsObject();
        if(name is "minimumEra" or "guidanceMode" or "launchSpeedMode" or "guidanceTransport")
        {
            var choices=Fields.FirstOrDefault(f=>f.Key==name)?.Choices;
            if(choices!=null&&!choices.Contains(value))throw new FormatException("Unsupported "+name+" choice.");
            modifiers[name]=value;return;
        }
        if(!double.TryParse(value.Replace(',','.'),NumberStyles.Float,CultureInfo.InvariantCulture,out var number)||!double.IsFinite(number))throw new FormatException("Enter a finite number.");
        var field=Fields.Single(f=>f.Key==name);
        var max=name=="launchSpeed"?Number(index,"flightSpeed"):field.Max;
        if(number<field.Min||number>max)throw new FormatException(ShellBallistics.RangeError(p["id"]!.GetValue<string>(),name,number,field.Min,max));
        modifiers[name]=number;
    }
    internal int Duplicate(JsonObject template)
    {
        if(Profiles.Count>=16)throw new InvalidOperationException("The catalogue supports 16 shells. No slots remain.");
        var copy=Copy(template).AsObject();var ids=Profiles.Select(p=>p!["id"]!.GetValue<string>()).ToHashSet();
        var labels=Profiles.Select(p=>p!["label"]!.GetValue<string>()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        int n=1;while(ids.Contains("custom_"+n)||labels.Contains("Custom shell "+n))n++;
        var legacy=copy["legacyProfile"]!.AsObject();
        if(!legacy.ContainsKey("behavior"))legacy["behavior"]=copy["originBehavior"]!.GetValue<string>();
        copy["id"]="custom_"+n;copy["label"]="Custom shell "+n;legacy["id"]="custom_"+n;legacy["label"]="Custom shell "+n;
        Profiles.Add(copy);return Profiles.Count-1;
    }
    private static JsonObject Project(JsonObject p,string behavior)
    {
        var row=Copy(p["legacyProfile"]!).AsObject();
        foreach(var modifier in p["modifiers"]!.AsObject())row[modifier.Key]=modifier.Value==null?null:Copy(modifier.Value);
        row["id"]=Copy(p["id"]!);row["label"]=Copy(p["label"]!);
        if(row.ContainsKey("behavior")||Infer(row)!=behavior)row["behavior"]=behavior;
        if(row["minimumEra"] is JsonValue era && string.IsNullOrWhiteSpace(era.GetValue<string>()))row.Remove("minimumEra");
        // No omitted settings are materialized for an unchanged architecture.
        // Structural conversion only seeds relevant missing fields from examples.
        if(!UnchangedArchitecture(p))
        {
            var guided=p["modules"]!["tail"]!["kind"]!.GetValue<string>()=="guided";
            var guidance=guided?(p["modifiers"]?["guidanceMode"]??p["legacyProfile"]?["guidanceMode"])?.GetValue<string>()??"sight":"none";
            row["flight"]=new JsonObject{["propulsion"]=p["modules"]!["propulsion"]!["kind"]!.GetValue<string>(),["guidance"]=guidance,["carrier"]=p["modules"]!["carrier"]!["kind"]!.GetValue<string>()};
            row["guidanceMode"]=guidance;
            if(behavior!=p["originBehavior"]!.GetValue<string>())foreach(var field in DefaultProfile(behavior))
                if(field.Key is not ("id" or "label" or "behavior" or "minimumEra")&&!row.ContainsKey(field.Key))
                    row[field.Key]=field.Value==null?null:Copy(field.Value);
        }
        else if(row["flight"] is JsonObject originalFlight&&p["modifiers"]?["guidanceMode"] is {} guidanceChanged)
            originalFlight["guidance"]=p["modules"]!["tail"]!["kind"]!.GetValue<string>()=="guided"?Copy(guidanceChanged):JsonValue.Create("none");
        if(p["nativeTechnologyIds"]!.AsArray().Count>0||row.ContainsKey("nativeTechnologyIds"))row["nativeTechnologyIds"]=Copy(p["nativeTechnologyIds"]!);
        return row;
    }
    internal Compilation Compile()
    {
        ValidateIdentity();
        var pending=new List<string>();var rows=new JsonArray();
        foreach(var node in Profiles)
        {
            var p=node!.AsObject();var behavior=Behavior(p);
            if(behavior==null)
            {
                pending.Add(p["label"]!.GetValue<string>()+": unknown module has no runtime adapter. Design retained without changing live shells.");
                continue;
            }
            var row=Project(p,behavior);
            // Actual managed validators are final compiler authority, including inactive fields.
            _=ShellProfiles.Parse(new JsonObject{["schemaVersion"]=1,["profiles"]=new JsonArray(Copy(row))}.ToJsonString());
            rows.Add(row);
        }
        if(pending.Count>0)return new(null,pending);
        var result=JsonNode.Parse(originalRuntime)!.AsObject();result["profiles"]=rows;
        var json=result.ToJsonString(new JsonSerializerOptions{WriteIndented=true});ShellProfiles.Parse(json);
        return new(json,pending);
    }
    internal string SerializeAuthoring(string? committedRuntime=null)
    {
        ValidateIdentity();
        var copy=Copy(Root).AsObject();copy["sourceEnvelope"]=JsonNode.Parse(committedRuntime??originalRuntime);
        // Explicit requirements are user data, not inferred unlocks. Never rewrite them.
        return copy.ToJsonString(new JsonSerializerOptions{WriteIndented=true});
    }
    internal Compilation Save(string runtimePath,string authoringPath)
    {
        var compilation=Compile();
        var authoring=SerializeAuthoring(compilation.RuntimeJson);
        var changes=new List<JsonFileChange>{new(authoringPath,originalAuthoring,authoring)};
        // Even a draft-only save verifies the current runtime has not changed externally.
        changes.Add(new(runtimePath,originalRuntime,compilation.RuntimeJson??originalRuntime));
        JsonFileTransaction.Commit(changes);
        return compilation;
    }
}
