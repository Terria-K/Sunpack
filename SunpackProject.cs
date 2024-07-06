using System.Collections.Generic;
using TeuJson;

namespace Sunpack;

public partial class SunpackProject : IDeserialize, ISerialize
{
    public string Name { get; set; }
    public string Version { get; set; }
    public string[] Projects { get; set; }
    public List<SunpackDependency> Dependencies { get; set; } = new();
}

public partial class SunpackDependency : IDeserialize, ISerialize 
{
    public string Name { get; set; }
    public string Repository { get; set; }
    public string Branch { get; set; }
    public string Project { get; set; }
}