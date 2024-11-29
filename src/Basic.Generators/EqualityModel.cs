
using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Basic.Generators;

/// <summary>
/// The kind of equality that should be used for a member
/// </summary>
internal enum EqualityKind
{
    /// <summary>
    /// Use simple null checks and .Equals calls
    /// </summary>
    Default,

    /// <summary>
    /// Use the == / != operators
    /// </summary>
    Operator,

    /// <summary>
    /// Compare strings with case sensitive equality
    /// </summary>
    StringCaseSensitive,

    /// <summary>
    /// Compare the strings with case insensitive equality
    /// </summary>
    StringCaseInsensitive,

    /// <summary>
    /// Use Enumerable.SequenceEqual for the comparison
    /// </summary>
    SequenceEqual,
}

internal record struct FieldModel(string Name, TypeKind TypeKind, string TypeFullName, EqualityKind EqualityKind);

internal sealed class EqualityModel
{
    internal string? Namespace { get; }
    internal string TypeName { get; }
    internal bool IsClass { get; }
    internal bool SimpleHashing { get; }
    internal FieldModel[] Fields { get; }

    internal EqualityModel(
        string? @namespace,
        string typeName,
        bool isClass,
        bool simpleHashing,
        FieldModel[] fields)
    {
        Namespace = @namespace;
        TypeName = typeName;
        IsClass = isClass;
        SimpleHashing = simpleHashing;
        Fields = fields;
    }
}

internal sealed class EqualityModelComparer : IEqualityComparer<EqualityModel?>
{
    internal static readonly EqualityModelComparer Instance = new();

    public bool Equals(EqualityModel? x, EqualityModel? y)
    {
        if (ReferenceEquals(x, y))
        {
            return true;
        }

        if (x is null || y is null)
        {
            return false;
        }

        return
            x.Namespace == y.Namespace &&
            x.TypeName == y.TypeName &&
            x.IsClass == y.IsClass && 
            x.SimpleHashing == y.SimpleHashing &&
            x.Fields.AsSpan().SequenceEqual(y.Fields.AsSpan());
    }

    public int GetHashCode(EqualityModel? obj) => obj?.TypeName.GetHashCode() ?? 0;
}