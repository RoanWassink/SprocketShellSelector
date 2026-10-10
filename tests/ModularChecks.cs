using System.Text.Json.Nodes;
using SprocketShellSelector;

internal static class ModularChecks
{
    internal static void Run(Action<bool,string> check)
    {
        var json=ReleaseProfiles.Defaults();
        var draft=new ModularShellDraft(json);
        check(ModularShellDraft.SameJson(json,draft.Compile().RuntimeJson!),"modular default roundtrip preserves exact fields");
        void Reject(Action operation,string message)
        {
            bool rejected=false;try{operation();}catch(Exception){rejected=true;}
            check(rejected,message);
        }
        Reject(()=>draft.Set(0,"id","new_id"),"stable saved IDs cannot be edited");
        Reject(()=>draft.Set(0,"modifiers.maximumVelocity","NaN"),"nonfinite modifier rejected");
        Reject(()=>draft.Set(0,"modifiers.maximumVelocityFactor","0.75"),"native range enforced");
        draft.Set(0,"label","Custom player name");draft.Set(0,"modifiers.maximumVelocity","2100,5");
        var compiled=ShellProfiles.Parse(draft.Compile().RuntimeJson!);
        check(compiled[0].Label=="Custom player name"&&compiled[0].Settings.MaxVelocity==2100.5,"names and decimal comma edits compile");
        foreach(var behavior in ModularShellDraft.Architectures.Keys)
        {
            var conversion=new ModularShellDraft(json);
            var kinds=ModularShellDraft.Architectures[behavior];
            for(int i=0;i<kinds.Length;i++)conversion.Set(0,"modules."+ModularShellDraft.ModuleKeys[i]+".kind",kinds[i]);
            check(ShellProfiles.Parse(conversion.Compile().RuntimeJson!)[0].Behavior==(behavior is "atgm" or "atgm_gun"?"heat":behavior),"supported architecture compiles: "+behavior);
        }
        var implicitRoot=JsonNode.Parse(json)!.AsObject();
        var rod=implicitRoot["profiles"]!.AsArray().First(p=>p!["behavior"]?.GetValue<string>()=="apfsds")!.AsObject();
        rod["id"]="apfsds";rod.Remove("behavior");
        var implicitDraft=new ModularShellDraft(implicitRoot.ToJsonString());
        int original=implicitDraft.Profiles.ToList().FindIndex(p=>p!["id"]!.GetValue<string>()=="apfsds");
        int copied=implicitDraft.Duplicate(implicitDraft.Profiles[original]!.AsObject());
        check(ShellProfiles.Parse(implicitDraft.Compile().RuntimeJson!)[copied].Behavior=="apfsds","duplicated implicit rod retains rod behavior");
        var pending=new ModularShellDraft(json);
        pending.Set(0,"modules.tail.kind","guided");pending.Set(0,"modules.propulsion.kind","rocket");
        pending.Set(0,"modules.body.kind","longRod");pending.Set(0,"modules.effect.kind","kinetic");pending.Set(0,"modules.carrier.kind","launcher");
        check(pending.Compile().Ready&&ShellProfiles.Parse(pending.Compile().RuntimeJson!)[0].Behavior=="apfsds","guided kinetic rod compiles independently"); pending.Profiles[0]!["modules"]!["body"]!["kind"]="unknown_future_body";
        var tech=new ModularShellDraft(json);tech.Profiles[0]!["nativeTechnologyIds"]!.AsArray().Add("custom_unlock");
        check(tech.Compile().Ready,"explicit technology can be runtime enforced");
        check(tech.SerializeAuthoring().Contains("custom_unlock"),"explicit technology requirement preserved");
        var limited=new ModularShellDraft(json);while(limited.Profiles.Count<16)limited.Duplicate(limited.Profiles[0]!.AsObject());
        Reject(()=>limited.Duplicate(limited.Profiles[0]!.AsObject()),"modular capacity enforced");
        var folder=Path.Combine(Path.GetTempPath(),"modular-shell-"+Guid.NewGuid());Directory.CreateDirectory(folder);
        try
        {
            var runtime=Path.Combine(folder,"shells.json");var authoring=Path.Combine(folder,"modules.json");File.WriteAllText(runtime,json);
            check(!File.Exists(authoring),"draft edits do not write before save");
            var result=pending.Save(runtime,authoring);
            check(!result.Ready&&File.ReadAllText(runtime)==json&&File.Exists(authoring),"pending save preserves live catalogue byte for byte");
            var reopened=new ModularShellDraft(json,File.ReadAllText(authoring));
            check(!reopened.Compile().Ready,"pending design survives reopen");
            File.AppendAllText(runtime,"\n");var authoringBefore=File.ReadAllText(authoring);
            Reject(()=>reopened.Save(runtime,authoring),"external runtime mutation blocks pending save");
            check(File.ReadAllText(authoring)==authoringBefore,"failed transaction preserves sidecar");
            File.WriteAllText(runtime,json);File.Delete(authoring);
            var ready=new ModularShellDraft(json);ready.Set(0,"label","Saved name");ready.Save(runtime,authoring);
            check(ShellProfiles.Parse(File.ReadAllText(runtime))[0].Label=="Saved name","ready draft commits live catalogue");
            check(new ModularShellDraft(File.ReadAllText(runtime),File.ReadAllText(authoring)).Compile().Ready,"ready sidecar reopens against committed envelope");
        }
        finally{Directory.Delete(folder,true);}
        var fixtureDirectory=Path.Combine(AppContext.BaseDirectory,"modular-fixtures");
        var legacy=File.ReadAllText(Path.Combine(fixtureDirectory,"shells.example.legacy.json"));
        var modular=File.ReadAllText(Path.Combine(fixtureDirectory,"shells.example.modular.json"));
        check(ModularShellDraft.SameJson(legacy,new ModularShellDraft(legacy,null,modular).Compile().RuntimeJson!),"JS eight-family fixture matches native export");
        var pendingJson=File.ReadAllText(Path.Combine(fixtureDirectory,"shells.example.pending.json"));
        var pendingRoot=JsonNode.Parse(pendingJson)!;
        var pendingLegacy=new JsonObject{["schemaVersion"]=1,["profiles"]=new JsonArray(JsonNode.Parse(pendingRoot["profiles"]![0]!["legacyProfile"]!.ToJsonString()))}.ToJsonString();
        check(new ModularShellDraft(pendingLegacy,null,pendingJson).Compile().Ready,"JS guided rod fixture becomes playable");
    }
}

