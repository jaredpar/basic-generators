using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace Basic.Generators;
internal static class ResourceLoader
{
    internal static Stream GetResourceStream(string name)
    {
        var assembly = typeof(ResourceLoader).GetTypeInfo().Assembly;

        var stream = assembly.GetManifestResourceStream(name);
        if (stream == null)
        {
            throw new InvalidOperationException($"Resource '{name}' not found in {assembly.FullName}.");
        }

        return stream;
    }
}