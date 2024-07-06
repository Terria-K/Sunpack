> [!WARNING]
> This is currently work in progress, it still a proof of concept that this is possible in C# projects.

# Sunpack
Sunpack is an alternative package manager that uses source file instead of a compiled DLLs like what nuget does.
Unlike Nuget, Sunpack only works on git repository for now, but other alternative packaging solution will be implemented
soon in the project.

# How to use Sunpack
To add a package, use:
```bash
sunpack add <git_url> [branch]
```

To add a package to your sunpack project. It will ask you to write a name for your project if you haven't had one.
It will also prompt you a project to use if the package has multiple projects.

Remove a package by also specifying the git url.
```bash
sunpack remove <git_url>
```

Sync the project in case of updating it or losing your deps folder.
```bash
sunpack sync
```