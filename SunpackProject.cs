using System.Collections.Generic;
using System.IO;
using System.Text;
using NLua;
using TeuJson;

namespace Sunpack;

public class SunpackProject : IDeserialize, ISerialize
{
    public string Name { get; set; }
    public string Version { get; set; }
    public string[] Projects { get; set; }
    public List<SunpackDependency> Dependencies { get; set; } = new();
    public Dictionary<string, string> ResolvePackages { get; set; } = new();

    public static SunpackProject LoadProject(string path) 
    {
        using Stream stream = typeof(SunpackProject).Assembly.GetManifestResourceStream("init.lua");
        using TextReader reader = new StreamReader(stream);
        string init = reader.ReadToEnd();
        NLua.Lua state = new NLua.Lua();
        state.DoString(init, "init");
        state.DoFile(path);
        LuaTable global = (LuaTable)state["_Global"]; 

        SunpackProject project = new SunpackProject();
        project.Name = (string)global["Name"];
        project.Version = (string)global["Version"];
        LuaTable projects = (LuaTable)global["Projects"];
        LuaTable deps = (LuaTable)global["Dependencies"];
        LuaTable resolvePackages = (LuaTable)global["ResolvePackages"];
        project.Projects = new string[projects.Values.Count];

        int i = 0;
        foreach (string obj in projects.Values) 
        {
            project.Projects[i] = obj;
        }

        foreach (string o in deps.Keys) 
        {
            SunpackDependency dependency = new SunpackDependency();
            dependency.Name = o;
            LuaTable values = (LuaTable)deps[o];
            foreach (LuaTable o2 in values.Values) 
            {
                foreach (string o3 in o2.Keys) 
                {
                    if (o3 == "Repository") 
                    {
                        dependency.Repository = (string)o2[o3];
                    }
                    else if (o3 == "Branch") 
                    {
                        dependency.Branch = (string)o2[o3];
                    }
                    else if (o3 == "Project") 
                    {
                        dependency.Project = (string)o2[o3];
                    }

                }
            }
            project.Dependencies.Add(dependency);
        }

        foreach (string table in resolvePackages.Keys) 
        {
            project.ResolvePackages.Add(table, (string)resolvePackages[table]);
        }

        return project;
    }

    public static void CreateProject(string name) 
    {
        string projectContent = $$"""
        Package "{{name}}"
            Version "0.1.0"
            Projects {}
        """;

        using StreamWriter writer = File.CreateText("sunpack.lua");
        writer.Write(projectContent);
    }

    public void Write() 
    {
        StringBuilder builder = new StringBuilder();
        foreach (var project in Projects) 
        {
            builder.AppendLine($"\"{project}\",");
        }

        string projects = builder.ToString();
        builder.Clear();
        foreach (var dep in Dependencies) 
        {
            builder.AppendLine(dep.AsLua() + ",");
        }
        string deps = builder.ToString();

        string projectContent = $$"""
        Package "{{Name}}"
            Version "{{Version}}"
            Projects {
                {{projects}}    }
            Dependencies {
        {{deps}}    }
        """;

        using StreamWriter writer = File.CreateText("sunpack.lua");
        writer.Write(projectContent);
    }

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

    public string AsLua() 
    {
        StringBuilder builder = new StringBuilder();

        string content = $$"""
                {{Name}} = {
                    Repository("{{Repository}}"),
                    Branch("{{Branch}}"),
                    Project("{{Project}}"),
                }
        """;
        return content;
    }
}