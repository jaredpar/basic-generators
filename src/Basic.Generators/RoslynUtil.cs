
using System.Diagnostics;
using Microsoft.CodeAnalysis;

internal static class RoslynUtil
{
    /// <summary>
    /// Does the type in question implement the provided interface? This will consider original
    /// definitions, not the substitued ones.
    /// </summary>
    internal static bool DoesTypeImplementOriginal(ITypeSymbol typeSymbol, ITypeSymbol interfaceSymbol)
    {
        Debug.Assert(interfaceSymbol.Equals(interfaceSymbol.OriginalDefinition, SymbolEqualityComparer.Default));

        if (typeSymbol.OriginalDefinition.Equals(interfaceSymbol, SymbolEqualityComparer.Default))
        {
            return true;
        }

        var currentType = typeSymbol;
        do
        {
            foreach (var currentInterface in currentType.Interfaces)
            {
                if (currentInterface.OriginalDefinition is { } originalInterface)
                {
                    if (originalInterface.Equals(interfaceSymbol, SymbolEqualityComparer.Default))
                    {
                        return true;
                    }

                    if (DoesTypeImplementOriginal(originalInterface, interfaceSymbol))
                    {
                        return true;
                    }
                }
            }

            currentType = currentType.BaseType;
        } while (currentType is not null);

        return false;
    }
}