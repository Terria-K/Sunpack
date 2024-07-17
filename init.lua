_Global = {
    Name = "",
    Version = "",
    Projects = {},
    Dependencies = {},
    ResolvePackages = {}
}

function Package(name)
    _Global.Name = name
end

function Version(version)
    _Global.Version = version
end

function Projects(projects)
    _Global.Projects = projects
end

function Dependencies(deps)
    _Global.Dependencies = deps
end

function ResolvePackages(packages)
    _Global.ResolvePackages = packages
end

function Repository(url)
    return { Repository = url }
end

function Branch(name)
    return { Branch = name }
end

function Project(name)
    return { Project = name }
end
