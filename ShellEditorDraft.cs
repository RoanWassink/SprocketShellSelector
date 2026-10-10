using System.Text.Json;
using System.Text.Json.Nodes;
using System.Globalization;

namespace SprocketShellSelector;

internal sealed class ShellEditorDraft
{
    private readonly string original;
    internal JsonObject Root { get; }
    internal JsonArray Profiles => Root["profiles"]!.AsArray();
    internal ShellEditorDraft(string json)
    {
        ShellProfiles.Parse(json);
        original = json;
        Root = JsonNode.Parse(json)!.AsObject();
    }
    internal void Set(int index, string field, string value)
    {
        var profile = Profiles[index]!.AsObject();
        if (field == "id") throw new InvalidOperationException("Existing shell IDs cannot be edited.");
        if (field is "label" or "behavior" or "minimumEra" or "guidanceMode" or "launchSpeedMode")
        {
            if (field == "minimumEra" && string.IsNullOrWhiteSpace(value)) profile.Remove(field);
            else profile[field] = value;
        }
        else
        {
            if (!double.TryParse(value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number))
                throw new FormatException("Enter a finite number.");
            profile[field] = number;
        }
    }
    internal void ChangeBehavior(int index,string behavior)
    {
        var supported=new[]{"ap","apfsds","aphe","he","heat","hesh","atgm","atgm_gun"};
        if(!supported.Contains(behavior))throw new FormatException("Unsupported shell behavior.");
        var profile=Profiles[index]!.AsObject();
        var presets=JsonNode.Parse(ReleaseProfiles.Defaults())!["profiles"]!.AsArray();
        var template=presets.FirstOrDefault(p=>p!["behavior"]?.GetValue<string>()==behavior)?.AsObject();
        if(template!=null)
            foreach(var field in template)
                if(field.Key is not ("id" or "label" or "behavior") && !profile.ContainsKey(field.Key))
                    profile[field.Key]=field.Value==null?null:JsonNode.Parse(field.Value.ToJsonString());
        profile["behavior"]=behavior;
        // Supply valid payload defaults when converting a kinetic shell to chemical/HE.
        foreach(var key in new[]{"chemicalPenetrationMm","nativeExplosivePower"})
            if(template?[key] is {} fallback && (profile[key]?.GetValue<double>()??0)<=0)
                profile[key]=JsonNode.Parse(fallback.ToJsonString());
    }
    internal int Duplicate(JsonObject template)
    {
        if (Profiles.Count >= 16) throw new InvalidOperationException("The catalogue supports 16 shells. No slots remain.");
        var copy = JsonNode.Parse(template.ToJsonString())!.AsObject();
        var ids = Profiles.Select(p => p!["id"]!.GetValue<string>()).ToHashSet();
        var labels = Profiles.Select(p => p!["label"]!.GetValue<string>()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        int suffix = 1;
        while (ids.Contains("custom_" + suffix) || labels.Contains("Custom shell " + suffix)) suffix++;
        copy["id"] = "custom_" + suffix;
        copy["label"] = "Custom shell " + suffix;
        Profiles.Add(copy);
        return Profiles.Count - 1;
    }
    internal string Serialize()
    {
        var json = Root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        ShellProfiles.Parse(json);
        return json;
    }
    internal string Save(string path)
    {
        var json = Serialize();
        if (File.ReadAllText(path) != original) throw new IOException("The shell file changed while this menu was open. Cancel and reopen it.");
        var temporary = path + ".editor-" + Guid.NewGuid().ToString("N") + ".tmp";
        var backup = path + ".backup-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(temporary, json);
            File.Replace(temporary, path, backup);
            return backup;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
