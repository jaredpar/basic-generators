
using System.Diagnostics;
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
}