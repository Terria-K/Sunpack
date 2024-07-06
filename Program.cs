﻿using System;
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
    Help();
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
        if (args.Length < 2) 
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
        if (args.Length < 2) 
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
    case "update": {
        if (args.Length < 2) 
        {
            Console.WriteLine("Invalid command arguments. Use help for more information.");
            return;
        }
        string url = args[1];
        if (url == "all") 
        {
            UpdateDependencies();
            break;
        }
        
        string name = url.Substring(url.LastIndexOf('/') + 1);
        string repository = url.Substring(0, url.LastIndexOf('/'));

        SunpackDependency dependency = new SunpackDependency() 
        {
            Name = name,
            Repository = repository
        };
        UpdateDependency(dependency);
        break;
    }
    case "sync": {
        if (project.Dependencies.Count == 0) 
        {
            Console.WriteLine("There is no dependencies to sync.");
            return;
        }
        SyncDependencies();
        CreateTarget();
        break;
    }
    case "help": {
        Help();
        break;
    }
}

void Help() 
{
    Console.WriteLine("sync                    - Sync all dependencies that the sunpack.json currently has. Note that this does not update the dependency.");
    Console.WriteLine("update <git-repo>|all   - Update the selected dependency, or all.");
    Console.WriteLine("add <git-repo> [branch] - Add a dependency to the project.");
    Console.WriteLine("remove <git-repo>       - Remove a dependency to the project.");
    Console.WriteLine("help                    - Show a help message.");
}

void UpdateDependency(SunpackDependency dependency) 
{
    string outputDir = Path.Combine(sunpackDirectory, dependency.Name);

    if (Directory.Exists(outputDir)) 
    {
        Directory.Delete(outputDir);
    }

    SyncDependency(dependency, true);
}

void UpdateDependencies() 
{
    foreach (var dep in project.Dependencies) 
    {
        UpdateDependency(dep);
        Console.WriteLine(dep.Repository + "/" + dep.Name + " => UPDATED");
    }

    CreateTarget();
}

void AddDependency(SunpackDependency dependency) 
{
    const string sunpackDirectory = ".sunpack-dep";
    if (!Directory.Exists(sunpackDirectory)) 
    {
        Directory.CreateDirectory(sunpackDirectory);
    }

    foreach (SunpackDependency dep in project.Dependencies) 
    {
        if (dep.Name == dependency.Name) 
        {
            Console.WriteLine("Dependency already existed in this project.");
            return;
        }
    }

    Console.WriteLine($"Adding {dependency.Name} from {dependency.Repository} as a dependency...");

    if (!SyncDependency(dependency, true)) 
    {
        return;
    }
    SunpackProject depProj = JsonConvert.DeserializeFromFile<SunpackProject>(Path.Combine(sunpackDirectory, dependency.Name, "sunpack.json"));

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
    dependency.Project = selectedProj;

    project.Dependencies.Add(dependency);
    JsonTextWriter.WriteToFile("sunpack.json", project.Serialize());
    CreateTarget();
}

bool SyncDependency(SunpackDependency dependency, bool silent) 
{
    string httpUrl = $"{dependency.Repository}/{dependency.Name}.git";
    string outputDir = Path.Combine(sunpackDirectory, dependency.Name);

    if (Directory.Exists(outputDir)) 
    {
        Console.WriteLine(dependency.Repository + "/" + dependency.Name + " => OK");
        return true;
    }

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
        Console.WriteLine(dependency.Repository + "/" + dependency.Name + " => FAILED");
        return false;
    }
    Console.WriteLine(dependency.Repository + "/" + dependency.Name + " => Added");
    return true;
}

void SyncDependencies() 
{
    foreach (var dep in project.Dependencies) 
    {
        SyncDependency(dep, false);
    }
}

void RemoveDependency(SunpackDependency dependency) 
{
    if (!Directory.Exists(sunpackDirectory)) 
    {
        Directory.CreateDirectory(sunpackDirectory);
    }

    bool deletedSuccess = true;
    List<SunpackDependency> tobeRemoved = new List<SunpackDependency>();

    foreach (SunpackDependency dep in project.Dependencies)
    {
        if (dep.Name != dependency.Name)
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

    Console.WriteLine($"Dependency {dependency.Name} from {dependency.Repository} has been removed!");
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