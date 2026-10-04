using System.Text.Json;
using System.Text.Json.Nodes;
namespace SprocketShellSelector;

internal static class ReleaseProfiles
{
    internal static string Defaults()
    {
        using var stream=typeof(ReleaseProfiles).Assembly.GetManifestResourceStream("ShellSelector.Defaults")
            ?? throw new InvalidOperationException("Missing built-in shell profiles.");
        using var reader=new StreamReader(stream);
        return reader.ReadToEnd();
    }
    internal static bool Upgrade(string path)
    {
        var original=File.ReadAllText(path);
        var parsed=ShellProfiles.Parse(original); // Never overwrite invalid custom JSON.
        var root=JsonNode.Parse(original)!;
        var entries=root["profiles"]!.AsArray();
        var changed=false;
        foreach(var old in entries.ToArray())
            if(old!["id"]!.GetValue<string>()=="apfsds" && old["label"]!.GetValue<string>() is "APFSDS (beta)" or "APFSDS standard")
            {entries.Remove(old);changed=true;}
        foreach(var preset in JsonNode.Parse(Defaults())!["profiles"]!.AsArray())
        {
            if(entries.Any(p=>p!["id"]!.GetValue<string>()==preset!["id"]!.GetValue<string>()) || entries.Count>=16)continue;
            var copy=JsonNode.Parse(preset!.ToJsonString())!;
            var baseLabel=copy["label"]!.GetValue<string>();var label=baseLabel;
            for(var suffix=2;entries.Any(p=>string.Equals(p!["label"]!.GetValue<string>(),label,StringComparison.OrdinalIgnoreCase));suffix++)label=$"{baseLabel} {suffix}";
            copy["label"]=label;entries.Add(copy);changed=true;
        }
        // Expose new motor controls without changing established custom ATGM flight.
        foreach(var entry in entries)
        {
            var profile=parsed.FirstOrDefault(p=>p.Id==entry!["id"]!.GetValue<string>());
            if(profile==null || !ShellBalance.IsAtgm(profile.Behavior) || profile.Atgm is not {} flight)continue;
            foreach(var setting in new Dictionary<string,JsonNode?> {
                ["launchSpeed"]=JsonValue.Create(flight.LaunchSpeed ?? flight.FlightSpeed),
                ["launchSpeedMode"]=JsonValue.Create(flight.LaunchSpeedMode),
                ["launchSpeedMultiplier"]=JsonValue.Create(flight.LaunchSpeedMultiplier),
                ["acceleration"]=JsonValue.Create(flight.Acceleration), ["motorDelay"]=JsonValue.Create(flight.MotorDelay), ["motorBurnTime"]=JsonValue.Create(flight.MotorBurnTime),
                ["coastDeceleration"]=JsonValue.Create(flight.CoastDeceleration)})
                if(!entry!.AsObject().ContainsKey(setting.Key)){entry[setting.Key]=setting.Value;changed=true;}
        }
        // Rename recognized stock experimental labels only; preserve custom names and settings.
        var labelUpdates=new Dictionary<string,(string Label,string[] Old)> {
            ["he"]=("HE",new[]{"HE (native blast experiment)"}),
            ["heat"]=("HEAT",new[]{"HEAT (jet proxy experiment)"}),
            ["hesh"]=("HESH",new[]{"HESH (spall proxy experiment)"}),
            ["atgm_konkurs_test"]=("SACLOS ATGM",new[]{"Konkurs-like ATGM","Konkurs-like ATGM (TEST)"}),
            ["atgm_mclos_test"]=("MCLOS ATGM",new[]{"MCLOS keyboard ATGM","MCLOS keyboard ATGM (TEST)"})
        };
        foreach(var entry in entries)
            if(labelUpdates.TryGetValue(entry!["id"]!.GetValue<string>(),out var update) &&
                update.Old.Contains(entry["label"]!.GetValue<string>()) &&
                !entries.Any(other=>string.Equals(other!["label"]!.GetValue<string>(),update.Label,StringComparison.OrdinalIgnoreCase)))
            {entry["label"]=update.Label;changed=true;}
        if(!changed)return false;
        var json=root.ToJsonString(new JsonSerializerOptions{WriteIndented=true});
        _=ShellProfiles.Parse(json);
        var backup=path+".pre-v094-backup";
        for(var suffix=2;File.Exists(backup);suffix++)backup=path+$".pre-v094-backup-{suffix}";
        File.Copy(path,backup,false);
        var temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try{File.WriteAllText(temp,json);File.Move(temp,path,true);}
        finally{if(File.Exists(temp))File.Delete(temp);}
        return true;
    }
}
