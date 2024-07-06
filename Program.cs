using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml;
using Sunpack;
using TeuJson;

const string sunpackDirectory = ".sunpack-dep";

if (args.Length == 0) 
{
    Console.WriteLine("add <git-repo> [branch] - \t\t Add a dependency to the project.");
    Console.WriteLine("remove <git-repo> - \t\t Remove a dependency to the project.");
    return;
}

SunpackProject project;
if (!File.Exists("sunpack.json")) 
{
    Console.WriteLine("Please enter a name of your program: ");
    string name = Console.ReadLine().Trim();
    if (string.IsNullOrEmpty(name)) 
    {
        Console.WriteLine("Empty name is not allowed");
        return;
    }
    project = new SunpackProject() 
    {
        Name = name,
        Version = "0.1.0"
    };
    JsonConvert.SerializeToFile(project, "sunpack.json");
}
else 
{
    project = JsonConvert.DeserializeFromFile<SunpackProject>("sunpack.json");
}

string command = args[0];

switch (command) 
{
    case "add": {
        if (args.Length < 1) 
        {
            Console.WriteLine("Invalid command arguments. Use help for more information.");
            return;
        }
        string url = args[1];
        string name = url.Substring(url.LastIndexOf('/') + 1);
        string repository = url.Substring(0, url.LastIndexOf('/'));
        string branch = "";

        if (args.Length >= 3) 
        {
            branch = args[2];
        }

        SunpackDependency dependency = new SunpackDependency() 
        {
            Name = name,
            Repository = repository,
            Branch = branch
        };

        AddDependency(dependency);
        break;
    }
    case "remove": {
        if (args.Length < 1) 
        {
            Console.WriteLine("Invalid command arguments. Use help for more information.");
            return;
        }
        string url = args[1];
        string name = url.Substring(url.LastIndexOf('/') + 1);
        string repository = url.Substring(0, url.LastIndexOf('/'));

        SunpackDependency dependency = new SunpackDependency() 
        {
            Name = name,
            Repository = repository
        };
        RemoveDependency(dependency);
        break;
    }
    case "sync": {
        SyncDependencies();
        CreateTarget();
        break;
    }
}

void AddDependency(SunpackDependency sunpackDependency) 
{
    const string sunpackDirectory = ".sunpack-dep";
    if (!Directory.Exists(sunpackDirectory)) 
    {
        Directory.CreateDirectory(sunpackDirectory);
    }

    foreach (SunpackDependency dep in project.Dependencies) 
    {
        if (dep.Name == sunpackDependency.Name) 
        {
            Console.WriteLine("Dependency already existed in this project.");
            return;
        }
    }

    Console.WriteLine($"Adding {sunpackDependency.Name} from {sunpackDependency.Repository} as a dependency...");

    if (!SyncDependency(sunpackDependency)) 
    {
        return;
    }
    SunpackProject depProj = JsonConvert.DeserializeFromFile<SunpackProject>(Path.Combine(sunpackDirectory, sunpackDependency.Name, "sunpack.json"));

    string selectedProj;
    if (depProj.Projects.Length > 1) 
    {
        int num = 0;
        foreach (var depP in depProj.Projects) 
        {
            Console.WriteLine($"[{num}] {depP}");
            num++;
        }
        Console.WriteLine("Please select the project you want to choose from this package (default = 0): ");
        string selected = Console.ReadLine().Trim();
        if (int.TryParse(selected, out int s)) 
        {
            selectedProj = depProj.Projects[s];
        }
        else 
        {
            Console.WriteLine("Invalid, 0 is selected by default. Just change it on sunpack.json later.");
            selectedProj = depProj.Projects[0];
        }
    }
    else 
    {
        selectedProj = depProj.Projects[0];
    }
    sunpackDependency.Project = selectedProj;

    project.Dependencies.Add(sunpackDependency);
    JsonTextWriter.WriteToFile("sunpack.json", project.Serialize());
    CreateTarget();
}

bool SyncDependency(SunpackDependency dependency) 
{
    string httpUrl = $"{dependency.Repository}/{dependency.Name}.git";
    string outputDir = Path.Combine(sunpackDirectory, dependency.Name);

    List<string> args = ["clone", httpUrl, outputDir, "--recursive"];
    if (!string.IsNullOrEmpty(dependency.Branch)) 
    {
        args.Add("--branch");
        args.Add(dependency.Branch);
    }

    ProcessStartInfo startInfo = new ProcessStartInfo("git", args);
    Process process = Process.Start(startInfo);
    process.WaitForExit();

    Console.WriteLine("Checking if it's a valid sunpack project.");

    HashSet<string> files = Directory.GetFiles(outputDir).ToHashSet();

    if (!files.Contains(Path.Combine(outputDir, "sunpack.json"))) 
    {
        Console.WriteLine($"{dependency.Name} from {dependency.Repository} does not contains sunpack.json. Please ensure that the project has this file.");
        Directory.Delete(outputDir, true);
        return false;
    }
    return true;
}

void SyncDependencies() 
{
    foreach (var dep in project.Dependencies) 
    {
        SyncDependency(dep);
    }
}

void RemoveDependency(SunpackDependency sunpackDependency) 
{
    if (!Directory.Exists(sunpackDirectory)) 
    {
        Directory.CreateDirectory(sunpackDirectory);
    }

    bool deletedSuccess = true;
    List<SunpackDependency> tobeRemoved = new List<SunpackDependency>();

    foreach (SunpackDependency dep in project.Dependencies)
    {
        if (dep.Name != sunpackDependency.Name)
        {
            continue;
        }

        string path = Path.Combine(sunpackDirectory, dep.Name);

        if (Directory.Exists(path)) 
        {
            Directory.Delete(path, true);
        }

        tobeRemoved.Add(dep);
    }

    if (!deletedSuccess) 
    {
        Console.WriteLine("Dependency does not exists.");
        return;
    }

    foreach (SunpackDependency dep in tobeRemoved) 
    {
        project.Dependencies.Remove(dep);
    }

    Console.WriteLine($"Dependency {sunpackDependency.Name} from {sunpackDependency.Repository} has been removed!");
    JsonTextWriter.WriteToFile("sunpack.json", project.Serialize());
    CreateTarget();
}

void CreateTarget() 
{
    XmlDocument document = new XmlDocument();
    XmlComment comment = document.CreateComment("Import this via <Import Project=\"Sunpack.targets\" /> in your .csproj file.");
    document.AppendChild(comment);

    XmlElement xmlProject = document.CreateElement("Project");
    document.AppendChild(xmlProject);

    XmlElement propGroup = document.CreateElement("PropertyGroup");
    xmlProject.AppendChild(propGroup);

    XmlElement sunpackDir = document.CreateElement("SunpackDir");
    sunpackDir.InnerText = "$(MSBuildThisFileDirectory)";
    propGroup.AppendChild(sunpackDir);

    // Create the references
    XmlElement referenceItemGroup = document.CreateElement("ItemGroup");
    xmlProject.AppendChild(referenceItemGroup);

    XmlElement excludeItemGroup = document.CreateElement("ItemGroup");
    xmlProject.AppendChild(excludeItemGroup);


    foreach (SunpackDependency dep in project.Dependencies) 
    {
        XmlElement projectReference = document.CreateElement("ProjectReference");
        string projectPath = dep.Project;
        projectReference.SetAttribute("Include", Path.Combine("$(SunpackDir)" + sunpackDirectory, dep.Name, projectPath));
        referenceItemGroup.AppendChild(projectReference);

        XmlElement compile = document.CreateElement("Compile");
        compile.SetAttribute("Remove", Path.Combine("$(SunpackDir)" + sunpackDirectory, dep.Name, "**", "*"));
        excludeItemGroup.AppendChild(compile);
    }


    document.Save("Sunpack.targets");
}