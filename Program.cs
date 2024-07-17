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
    Help();
    return;
}


JsonObject slock;
if (!File.Exists("sun.lock")) 
{
    slock = new JsonObject();
}
else 
{
    slock = JsonTextReader.FromFile("sun.lock").AsJsonObject;
}

string command = args[0];

SunpackProject project;

if (command == "init") 
{
    if (File.Exists("sunpack.lua")) 
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
        SunpackProject.CreateProject(name);
        return;
    }
    Console.WriteLine("Project already exists here.");
    return;
}


if (!File.Exists("sunpack.lua")) 
{
    Console.WriteLine("Project does not exists here, use \"sunpack init\" first.");
    return;
}

project = SunpackProject.LoadProject("sunpack.lua");

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
        CreateTarget();
        break;
    }
    case "sync": {
        if (project.Dependencies.Count == 0) 
        {
            Console.WriteLine("There is no dependencies to sync.");
            break;
        }
        SyncDependencies(project);
        CreateTarget();
        break;
    }
    case "help": {
        Help();
        break;
    }
}

JsonTextWriter.WriteToFile("sun.lock", slock);


void LockDependency(SunpackDependency dependency, string rev) 
{
    string url = dependency.Repository + "/" + dependency.Name;
    slock[url] = rev;
}

void Help() 
{
    Console.WriteLine("sync                    - Sync all dependencies that the sunpack.lua currently has. Note that this does not update the dependency.");
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
        RunGitOnDep(outputDir, "fetch", ["--depth", "999999", "--progress"]);
        string rev = RunGitOnDep(outputDir, "rev-parse", ["FETCH_HEAD"]);
        string url = dependency.Repository + "/" + dependency.Name;
        string revCompare = slock[url];
        if (rev == revCompare) 
        {
            Console.WriteLine(dependency.Repository + "/" + dependency.Name + " => Up to date.");
            return;
        }
        Directory.Delete(outputDir, true);
    }

    foreach (SunpackDependency dep in project.Dependencies) 
    {
        if (dep.Name == dependency.Name) 
        {
            dependency.Branch = dep.Branch;
            SyncDependency(project, dependency, true);
            Console.WriteLine(dep.Repository + "/" + dep.Name + " => UPDATED");
            return;
        }
    }
    Console.WriteLine($"There is no {dependency.Name} from {dependency.Repository} in this project.");
}

void UpdateDependencies() 
{
    foreach (var dep in project.Dependencies) 
    {
        UpdateDependency(dep);
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

    if (!SyncDependency(project, dependency, true)) 
    {
        return;
    }
    string file = Path.Combine(sunpackDirectory, dependency.Name, "sunpack.lua");
    if (!File.Exists(file)) 
    {
        file = project.ResolvePackages[dependency.Name];
    }
    SunpackProject depProj = SunpackProject.LoadProject(file);

    string selectedProj;
    try 
    {
        if (depProj.Projects.Length > 1) 
        {
            int num = 0;
            foreach (var depP in depProj.Projects) 
            {
                Console.WriteLine($"[{num}] {depP}");
                num++;
            }
            Console.WriteLine("Please select the project you want to choose from this package (default = 0): ");
            while (true) 
            {
                string selected = Console.ReadLine().Trim();
                if (int.TryParse(selected, out int s) && s < depProj.Projects.Length)
                {
                    selectedProj = depProj.Projects[s];
                    break;
                }
                Console.WriteLine("Invalid selection, please try again.");
            }
        }
        else 
        {
            selectedProj = depProj.Projects[0];
        }
    }
    catch (IndexOutOfRangeException) 
    {
        Console.WriteLine("No projects exists in this project, please specify the path of the project.");

        string path = Console.ReadLine();
        selectedProj = path;
    }

    dependency.Project = selectedProj;

    project.Dependencies.Add(dependency);
    project.Write();
    CreateTarget();
}

bool SyncDependency(SunpackProject project, SunpackDependency dependency, bool silent) 
{
    string httpUrl = $"{dependency.Repository}/{dependency.Name}.git";
    string outputDir = Path.Combine(sunpackDirectory, dependency.Name);

    if (Directory.Exists(outputDir)) 
    {
        Console.WriteLine(dependency.Repository + "/" + dependency.Name + " => OK");
        return true;
    }

    List<string> args = [httpUrl, outputDir, "--recursive"];
    if (!string.IsNullOrEmpty(dependency.Branch)) 
    {
        args.Add("--branch");
        args.Add(dependency.Branch);
    }

    RunGit("clone", args);

    Console.WriteLine("Checking if it's a valid sunpack project.");

    HashSet<string> files = Directory.GetFiles(outputDir).ToHashSet();

    if (!files.Contains(Path.Combine(outputDir, "sunpack.lua"))) 
    {
        if (project.ResolvePackages.ContainsKey(dependency.Name)) 
        {
            goto RESOLVE;
        }
        else 
        {
            Console.WriteLine($"{dependency.Name} from {dependency.Repository} does not contains sunpack.lua. Please ensure that the project has this file, or resolve the project.");
            Directory.Delete(outputDir, true);
            Console.WriteLine(dependency.Repository + "/" + dependency.Name + " => FAILED");
            return false;
        }
    }
    RESOLVE:
    string rev = RunGitOnDep(outputDir, "rev-parse", ["HEAD"]);
    LockDependency(dependency, rev);
    Console.WriteLine(dependency.Repository + "/" + dependency.Name + " => Added");

    string file = Path.Combine(sunpackDirectory, dependency.Name, "sunpack.lua");
    if (!File.Exists(file)) 
    {
        file = project.ResolvePackages[dependency.Name];
    }

    SunpackProject depProj = SunpackProject.LoadProject(file);

    string oldCurrDir = Environment.CurrentDirectory;
    Environment.CurrentDirectory = outputDir;
    SyncDependencies(depProj);
    Environment.CurrentDirectory = oldCurrDir;
    return true;
}

void SyncDependencies(SunpackProject project) 
{
    foreach (var dep in project.Dependencies) 
    {
        SyncDependency(project, dep, false);
    }
}

void RemoveDependency(SunpackDependency dependency) 
{
    if (!Directory.Exists(sunpackDirectory)) 
    {
        Directory.CreateDirectory(sunpackDirectory);
    }

    bool deletedSuccess = false;
    List<SunpackDependency> tobeRemoved = new List<SunpackDependency>();

    foreach (SunpackDependency dep in project.Dependencies)
    {
        if (dep.Name != dependency.Name)
        {
            continue;
        }

        deletedSuccess = true;

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
    project.Write();
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


string RunGit(string command, List<string> arguments) 
{
    List<string> commandArgs = [command];
    commandArgs.AddRange(arguments);
    ProcessStartInfo startInfo = new ProcessStartInfo("git", commandArgs);
    startInfo.RedirectStandardOutput = true;
    Process process = Process.Start(startInfo);
    process.WaitForExit();

    return process.StandardOutput.ReadToEnd().Trim();
}

string RunGitOnDep(string depPath, string command, List<string> arguments) 
{
    List<string> commandArgs = [command];
    commandArgs.AddRange(arguments);
    ProcessStartInfo startInfo = new ProcessStartInfo("git", commandArgs);
    startInfo.RedirectStandardOutput = true;
    startInfo.WorkingDirectory = depPath;
    Process process = Process.Start(startInfo);
    process.WaitForExit();

    return process.StandardOutput.ReadToEnd().Trim();
}