using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Basic.Generators;

/// <summary>
/// Represents a member of the type that needs to be considered for equality. This
/// can be a field or a property.
/// </summary>
internal sealed record class DataMemberModel(
    string Name,
    TypeKind TypeKind,
    string TypeFullName,
    EqualityKind EqualityKind);

internal sealed class EqualityModel
{
    internal string? Namespace { get; }
    internal string TypeName { get; }
    internal bool IsClass { get; }
    internal bool SimpleHashing { get; }
    internal DataMemberModel[] DataMembers { get; }

    internal EqualityModel(
        string? @namespace,
        string typeName,
        bool isClass,
        bool simpleHashing,
        DataMemberModel[] dataMembers)
    {
        Namespace = @namespace;
        TypeName = typeName;
        IsClass = isClass;
        SimpleHashing = simpleHashing;
        DataMembers = dataMembers;
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
            x.DataMembers.AsSpan().SequenceEqual(y.DataMembers.AsSpan());
    }

    public int GetHashCode(EqualityModel? obj) => obj?.TypeName.GetHashCode() ?? 0;
}