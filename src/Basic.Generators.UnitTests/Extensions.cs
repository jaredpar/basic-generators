
using Microsoft.CodeAnalysis.Testing;

namespace Basic.Generators.UnitTests;

public static class Extensions
{
    public static void AddRange(this SourceFileList @this, string[] sources)
    {
        foreach (var source in sources)
        {
            @this.Add(source);
        }
    }

    public static string TrimWhitespaceAndNewLines(this string @this) =>
        @this.Trim(' ', '\n', '\r');
}