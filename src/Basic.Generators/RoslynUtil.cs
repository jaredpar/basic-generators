
using System;
using System.Linq;
using System.Diagnostics;
using Basic.Generators;
using Microsoft.CodeAnalysis;

internal static class RoslynUtil
{
    /// <summary>
    /// Does the type in question implement the provided interface? This will consider original
    /// definitions, not the substituted ones.
    /// </summary>
    internal static bool IsOrImplementsOriginal(ITypeSymbol typeSymbol, ITypeSymbol interfaceSymbol)
    {
        Debug.Assert(interfaceSymbol.Equals(interfaceSymbol.OriginalDefinition, SymbolEqualityComparer.Default));

        if (typeSymbol.OriginalDefinition.Equals(interfaceSymbol, SymbolEqualityComparer.Default))
        {
            return true;
        }

        foreach (var currentInterface in typeSymbol.OriginalDefinition.AllInterfaces)
        {
            if (currentInterface.OriginalDefinition is { } originalInterface)
            {
                if (originalInterface.Equals(interfaceSymbol, SymbolEqualityComparer.Default))
                {
                    return true;
                }
            }
        }

        return false;
    }

    internal static bool HasSimpleHashing(TypeUtil typeUtil)
    {
        if (typeUtil.HashCode is not { } type)
        {
            return false;
        }

        return type
            .GetMembers("Combine")
            .OfType<IMethodSymbol>()
            .Any(x => x.Arity == 7);
    }
}