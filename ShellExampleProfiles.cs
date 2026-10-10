using System.Text.Json.Nodes;
namespace SprocketShellSelector;
internal static class ShellExampleProfiles
{
    internal static JsonObject Tow()
    {
        using var stream=typeof(ShellExampleProfiles).Assembly.GetManifestResourceStream("ShellSelector.TowExample")
            ??throw new InvalidOperationException("Missing TOW example.");
        using var reader=new StreamReader(stream);
        return JsonNode.Parse(reader.ReadToEnd())!.AsObject();
    }
    internal static JsonArray Templates()
    {
        var rows=JsonNode.Parse(ReleaseProfiles.Defaults())!["profiles"]!.AsArray();
        rows.Add(Tow());
        return rows;
    }
}
