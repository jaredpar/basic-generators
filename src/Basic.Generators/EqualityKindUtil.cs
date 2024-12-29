
using System;
using System.Diagnostics;

namespace Basic.Generators;

internal static class EqualityKindUtil
{
    /// <summary>
    /// Is this one of the built in string equality kinds
    /// </summary>
    internal static bool IsStringEquality(EqualityKind kind) => kind switch
    {
        EqualityKind.Ordinal => true,
        EqualityKind.OrdinalIgnoreCase => true,
        EqualityKind.CurrentCulture => true,
        EqualityKind.CurrentCultureIgnoreCase => true,
        EqualityKind.InvariantCulture => true,
        EqualityKind.InvariantCultureIgnoreCase => true,
        _ => false,
    };

    internal static string GetStringComparerTypeName(EqualityKind kind)
    {
        Debug.Assert(IsStringEquality(kind));
        return kind switch
        {
            EqualityKind.Ordinal => "StringComparer.Ordinal",
            EqualityKind.OrdinalIgnoreCase => "StringComparer.OrdinalIgnoreCase",
            EqualityKind.CurrentCulture => "StringComparer.CurrentCulture",
            EqualityKind.CurrentCultureIgnoreCase => "StringComparer.CurrentCultureIgnoreCase",
            EqualityKind.InvariantCulture => "StringComparer.InvariantCulture",
            EqualityKind.InvariantCultureIgnoreCase => "StringComparer.InvariantCultureIgnoreCase",
            _ => throw new InvalidOperationException($"Not a string equality kind: {kind}"),
        };
    }
}
