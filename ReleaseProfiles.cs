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
