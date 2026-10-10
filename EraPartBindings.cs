using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json;

namespace SprocketEraBindings;

// Immutable classification data only. No native state, file IO, ledger or fallback.
public sealed class EraPartBinding
{
    public string CassetteComponentId { get; }
    public string MountComponentId { get; }
    public string MaterialId { get; }
    public string ResponseId { get; }
    public string Kind { get; }
    internal EraPartBinding(string cassette, string mount, string material, string response, string kind)
    { CassetteComponentId=cassette; MountComponentId=mount; MaterialId=material; ResponseId=response; Kind=kind; }

    // Must still be combined with native date, fingerprint, thickness and threat gates.
    public bool MatchesRecipe(string? responseId, string? kind, IEnumerable<string>? compatibleMaterialIds)
        => string.Equals(ResponseId,responseId,StringComparison.Ordinal)
        && string.Equals(Kind,kind,StringComparison.Ordinal)
        && compatibleMaterialIds!=null && compatibleMaterialIds.Contains(MaterialId,StringComparer.Ordinal);
}

public sealed class EraPartBindingCatalogue
{
    public const string MatchMode = "componentId";
    public IReadOnlyList<EraPartBinding> Bindings { get; }
    private readonly IReadOnlyDictionary<string,EraPartBinding> cassettes;
    private readonly IReadOnlyDictionary<string,EraPartBinding> mounts;
    internal EraPartBindingCatalogue(List<EraPartBinding> entries)
    {
        Bindings=Array.AsReadOnly(entries.ToArray());
        cassettes=new ReadOnlyDictionary<string,EraPartBinding>(entries.ToDictionary(x=>x.CassetteComponentId,StringComparer.Ordinal));
        mounts=new ReadOnlyDictionary<string,EraPartBinding>(entries.ToDictionary(x=>x.MountComponentId,StringComparer.Ordinal));
    }
    public bool TryGetCassette(string? componentId,[NotNullWhen(true)] out EraPartBinding? binding)
    { binding=null;return componentId!=null && cassettes.TryGetValue(componentId,out binding); }
    public bool TryGetMount(string? componentId,[NotNullWhen(true)] out EraPartBinding? binding)
    { binding=null;return componentId!=null && mounts.TryGetValue(componentId,out binding); }
}

public static class EraPartBindings
{
    public static EraPartBindingCatalogue Parse(string json)
    {
        if(json==null)throw new FormatException("ERA binding JSON is required.");
        try
        {
            using var document=JsonDocument.Parse(json,new JsonDocumentOptions { AllowTrailingCommas=false,CommentHandling=JsonCommentHandling.Disallow });
            var root=document.RootElement;
            Fields(root,"schemaVersion","matchMode","bindings");
            var version=root.GetProperty("schemaVersion");
            if(version.ValueKind!=JsonValueKind.Number||!version.TryGetInt32(out var number)||number!=1)
                throw new FormatException("Unsupported ERA binding schema; expected integer 1.");
            if(Text(root,"matchMode")!=EraPartBindingCatalogue.MatchMode)
                throw new FormatException("Only exact componentId matching is supported in schema 1.");
            var array=root.GetProperty("bindings");
            if(array.ValueKind!=JsonValueKind.Array)throw new FormatException("ERA bindings must be an array.");
            var entries=new List<EraPartBinding>();
            var roles=new HashSet<string>(StringComparer.Ordinal);
            var materialRoutes=new Dictionary<string,(string Response,string Kind)>(StringComparer.Ordinal);
            var responseKinds=new Dictionary<string,string>(StringComparer.Ordinal);
            foreach(var item in array.EnumerateArray())
            {
                Fields(item,"cassetteComponentId","mountComponentId","materialId","responseId","kind");
                var cassette=Text(item,"cassetteComponentId");var mount=Text(item,"mountComponentId");
                var material=Text(item,"materialId");var response=Text(item,"responseId");var kind=Text(item,"kind");
                if(kind!="lightEra"&&kind!="heavyEra")throw new FormatException("Unsupported placed ERA kind: "+kind);
                // Unique across both roles: no mount can accidentally become active.
                if(!roles.Add(cassette)||!roles.Add(mount))throw new FormatException("Duplicate or conflicting ERA component role.");
                var route=(Response:response,Kind:kind);
                if(materialRoutes.TryGetValue(material,out var existing)&&existing!=route)
                    throw new FormatException("A material is bound to conflicting ERA responses.");
                if(responseKinds.TryGetValue(response,out var previousKind)&&previousKind!=kind)
                    throw new FormatException("An ERA response is bound to conflicting kinds.");
                materialRoutes[material]=route;responseKinds[response]=kind;
                entries.Add(new(cassette,mount,material,response,kind));
            }
            return new(entries);
        }
        catch(JsonException ex){throw new FormatException("Invalid ERA binding JSON.",ex);}
    }

    private static void Fields(JsonElement item,params string[] expected)
    {
        if(item.ValueKind!=JsonValueKind.Object)throw new FormatException("An ERA binding object is required.");
        var keys=new HashSet<string>(StringComparer.Ordinal);
        foreach(var property in item.EnumerateObject())
            if(!expected.Contains(property.Name,StringComparer.Ordinal)||!keys.Add(property.Name))
                throw new FormatException("Unknown or duplicate ERA binding field: "+property.Name);
        if(keys.Count!=expected.Length)throw new FormatException("Required ERA binding fields are missing.");
    }
    private static string Text(JsonElement item,string key)
    {
        var value=item.GetProperty(key);
        if(value.ValueKind!=JsonValueKind.String)throw new FormatException("ERA binding "+key+" must be a string.");
        var text=value.GetString()!;
        if(text.Length==0||text.Length>128||text!=text.Trim()||text.Any(c=>char.IsControl(c)))
            throw new FormatException("ERA binding "+key+" must be a nonempty, unpadded identifier of at most 128 characters.");
        return text;
    }
}
