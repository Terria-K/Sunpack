using System;
using System.Collections.Generic;
using TeuJson;

namespace Sunpack;

public class SunpackProject : IDeserialize, ISerialize
{
    public string Name { get; set; }
    public string Version { get; set; }
    public string[] Projects { get; set; }
    public List<SunpackDependency> Dependencies { get; set; } = new();
    public Dictionary<string, string> ResolvePackages { get; set; } = new();

    public void Deserialize(JsonObject value)
    {
        Name = value["Name"];
        Version = value["Version"];
        Projects = value["Projects"]?.ConvertToArrayString() ?? [];
        Dependencies = value["Dependencies"]?.ConvertToList<SunpackDependency>() ?? [];
        foreach (var (key, val) in value["ResolvePackages"]?.Pairs) 
        {
            ResolvePackages.Add(key, val);
        }
    }

    public JsonObject Serialize()
    {
        var resolvedPackage = new JsonObject();
        foreach (var (key, value) in ResolvePackages) 
        {
            resolvedPackage[key] = value;
        }

        return new JsonObject 
        {
            ["Name"] = Name,
            ["Version"] = Version,
            ["Projects"] = Projects.ConvertToJsonArray(),
            ["Dependencies"] = Dependencies.ConvertToJsonArray(),
            ["ResolvePackages"] = resolvedPackage
        };
    }
}

public partial class SunpackDependency : IDeserialize, ISerialize 
{
    public string Name { get; set; }
    public string Repository { get; set; }
    public string Branch { get; set; }
    public string Project { get; set; }
}