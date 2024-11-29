using System;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;

namespace Basic.Generators;

internal sealed class TypeUtil
{
    private static ConditionalWeakTable<Compilation, TypeUtil> Cache { get; } = new();

    internal readonly Lazy<ITypeSymbol?> _ienumerableT;

    internal Compilation Compilation { get; }

    internal ITypeSymbol? IEnumerableT => _ienumerableT.Value;

    private TypeUtil(Compilation compilation)
    {
        Compilation = compilation;
        _ienumerableT = new Lazy<ITypeSymbol?>(() => GetSymbol("System.Collections.Generic.IEnumerable`1"));
    }

    internal static TypeUtil GetOrCreate(Compilation compilation)
    {
        if (Cache.TryGetValue(compilation, out var util))
        {
            return util;
        }

        util = new TypeUtil(compilation);
        Cache.Add(compilation, util);
        return util;
    }

    private INamedTypeSymbol? GetSymbol(string typeName) =>
        Compilation.GetTypeByMetadataName(typeName);
}