using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Basic.Generators;

internal static class AutoEqualityWriter
{
    internal static void WriteEqualityModel(SourceProductionContext context, EqualityModel model)
    {
        var builder = new CodeBuilder();
        var annotatedName = model.IsClass ? $"{model.TypeName}?" : model.TypeName;

        builder.AppendLine($$"""
            using System;
            using System.Collections.Generic;
            """);
        
        if (model.DataMembers.Any(x => x.EqualityKind == EqualityKind.SequenceEqual))
        {
            builder.AppendLine("using System.Linq;");
        }

        builder.AppendLine("""

            #nullable enable

            """);

        if (model.Namespace is not null)
            builder.Append($"""
            namespace {model.Namespace};


            """);

        var opEquals = model.IsClass
            ? "left is not null ? left.Equals(right) : right is null"
            : "left.Equals(right)";

        builder.Append($$"""
            partial {{(model.IsClass ? "class" : "struct")}} {{model.TypeName}} : IEquatable<{{annotatedName}}>
            {
                public static bool operator==({{annotatedName}} left, {{annotatedName}} right) =>
                    {{opEquals}};

                public static bool operator!=({{annotatedName}} left, {{annotatedName}} right) =>
                    !(left == right);

                public override bool Equals(object? other) =>
                    other is {{model.TypeName}} o && Equals(o);


            """);

        WriteEquals(model, builder);
        WriteGetHashCode(model, builder);

        builder.Append("""
            }
            """);

        context.AddSource($"AutoEquality.{model.Namespace}.{model.TypeName}.Generated.cs", builder.ToString());

    }

    internal static IEnumerable<(DataMemberModel DataMemberModel, int Index)> GetDataMembers(EqualityModel model)
    {
        var index = 0;
        foreach (var member in model.DataMembers)
        {
            if (member.EqualityKind == EqualityKind.None)
            {
                continue;
            }

            yield return (member, index);
            index++;
        }
    }

    internal static void WriteEquals(EqualityModel model, CodeBuilder builder)
    {
        var annotatedName = model.IsClass ? $"{model.TypeName}?" : model.TypeName;
        builder.Append($$"""
            public bool Equals({{annotatedName}} other)
            {

        """);

        if (model.IsClass)
        {
            builder.Append(""""
                        if (other is null)
                            return false;

                        return

                """");
        }
        else
        {
            builder.Append("""
                        return

                """);
        }

        foreach (var (member, index) in GetDataMembers(model))
        {
            if (index > 0)
            {
                builder.AppendLine(" &&");
            }

            var name = member.Name;
            var comp = GetEqualsExpression(member.EqualityKind, name, member.TypeFullName);
            builder.Append(12, comp);
        }

        builder.AppendLine(";");
        builder.Append("""
                }


            """);
    }

    internal static void WriteGetHashCode(EqualityModel model, CodeBuilder builder)
    {
        if (model.SimpleHashing)
        {
            WriteGetHashCodeSimple();
        }
        else
        {
            WriteGetHashCodeOld();
        }

        void WriteGetHashCodeSimple()
        {
            builder.Append($$"""
                    public override int GetHashCode() =>
                        HashCode.Combine(

                """);

            foreach (var (member, index) in GetDataMembers(model))
            {
                if (index > 0)
                {
                    builder.AppendLine(",");
                }

                if (EqualityKindUtil.IsStringEquality(member.EqualityKind))
                {
                    var typeName = EqualityKindUtil.GetStringComparerTypeName(member.EqualityKind);
                    builder.Append(12, $"{typeName}.GetHashCode({member.Name})");
                }
                else
                {
                    builder.Append(12, member.Name);
                }
            }

            builder.AppendLine(");");
        }

        void WriteGetHashCodeOld()
        {
            builder.Append($$"""
                    public override int GetHashCode()
                    {
                        int hash = 17;

                """);

            foreach (var (member, index) in GetDataMembers(model))
            {
                if (member.EqualityKind != EqualityKind.Ordinal && EqualityKindUtil.IsStringEquality(member.EqualityKind))
                {
                    var typeName = EqualityKindUtil.GetStringComparerTypeName(member.EqualityKind);
                    builder.AppendLine(8, $"hash = (hash * 23) + {typeName}.GetHashCode({member.Name});");
                }
                else if (member.TypeKind is TypeKind.Enum or TypeKind.Struct or TypeKind.Structure)
                {
                    builder.AppendLine(8, $"hash = (hash * 23) + {member.Name}.GetHashCode();");
                }
                else
                {
                    builder.AppendLine(8, $"hash = (hash * 23) + ({member.Name}?.GetHashCode() ?? 0);");
                }
            }

            builder.AppendLine(8, $"return hash;");

            builder.Append("""
                    }

                """);
        }
    }

    internal static string GetEqualsExpression(EqualityKind kind, string memberName, string typeFullName)
    {
        if (EqualityKindUtil.IsStringEquality(kind))
        {
            if (kind == EqualityKind.Ordinal)
            {
                return $"this.{memberName} == other.{memberName}";
            }

            var typeName = EqualityKindUtil.GetStringComparerTypeName(kind);
            return $"{typeName}.Equals(this.{memberName}, other.{memberName})";
        }

        return kind switch
        {
            EqualityKind.Operator => $"this.{memberName} == other.{memberName}",
            EqualityKind.Default => $"EqualityComparer<{typeFullName}>.Default.Equals(this.{memberName}, other.{memberName})",
            EqualityKind.SequenceEqual => $"Enumerable.SequenceEqual(this.{memberName}, other.{memberName})",
            _ => throw new Exception($"invalid {kind}")
        };
    }
}