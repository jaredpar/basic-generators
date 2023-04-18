using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Demo.Json;

public class BuildResult
{
    public bool Passed { get; set; }
    public string? Commit { get; set; }
    public int PullRequestId { get; set; }
}

[JsonSerializable(typeof(BuildResult))]
internal partial class SourceGenerationContext : JsonSerializerContext
{

}

