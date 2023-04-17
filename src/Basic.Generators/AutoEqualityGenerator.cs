using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;

namespace Basic.Generators;

[Generator(LanguageNames.CSharp)]
public sealed class AutoEqualityGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(callback =>
        {
            callback.AddSource(
                "AutoEqualityAttribute.cs",
                """
using System;
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
internal sealed class AutoEqualityAttribute : Attribute
{
    public bool CaseInsensitive { get; set; }

    public AutoEqualityAttribute(bool caseInsensitive = false) =>
        CaseInsensitive = caseInsensitive;
}
""");
        });

        var results = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "AutoEqualityAttribute",
                CheckEqualityModel,
                GetEqualityModel)
            .Where(x => x is not null)
            .Select((x, _) => x!)
            .WithComparer(EqualityModelComparer.Instance);
        context.RegisterSourceOutput(results, WriteEqualityModel);

        static bool CheckEqualityModel(SyntaxNode node, CancellationToken cancellationToken) =>
            node.IsKind(SyntaxKind.ClassDeclaration) || node.IsKind(SyntaxKind.StructDeclaration);

        static EqualityModel? GetEqualityModel(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
        {
            if (context.TargetSymbol is not INamedTypeSymbol { TypeKind: TypeKind.Class or TypeKind.Struct } symbol)
            {
                return null;
            }

            var fields = symbol
                .GetMembers()
                .OfType<IFieldSymbol>()
                .Select(x => x.Name)
                .ToArray();

            return new EqualityModel(
                symbol.ContainingNamespace.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                symbol.Name,
                symbol.TypeKind == TypeKind.Class,
                fields);
        }

        static void WriteEqualityModel(SourceProductionContext context, EqualityModel model)
        {
            var builder = new StringBuilder();
            var indent = new IndentUtil();
            var annotatedName = model.IsClass ? $"{model.TypeName}?" : model.TypeName;

            builder.Append($$"""
                using System;

                #nullable enable
                
                namespace {{model.Namespace}};

                partial class {{model.TypeName}} : IEquatable<{{annotatedName}}>
                {
                    public static bool operator==({{annotatedName}} left, {{annotatedName}} right) =>
                        left is not null ? left.Equals(right) : right is null;

                    public static bool operator!=({{annotatedName}} left, {{annotatedName}} right) =>
                        !(left == right);

                    public override bool Equals(object? other) =>
                        other is {{model.TypeName}} o && Equals(o);

                    public bool Equals({{annotatedName}} other)
                    {
                        if (other is null) 
                            return false;

                        return

                """);

            {
                using var _ = indent.Increase(3);
                for (var i = 0; i < model.Fields.Length; i++)
                {
                    var field = model.Fields[0];
                    builder.Append($"{indent.Value}this.{field} == other.{field}");
                    if (i + 1 == model.Fields.Length)
                    {
                        builder.AppendLine(";");
                    }
                    else
                    {
                        builder.AppendLine(" &&");
                    }
                }
            }

            builder.Append("""
                    }

                    public override int GetHashCode()
                    {
                        var hash = new HashCode();

                """);

            {
                using var _ = indent.Increase(2);
                for (var i = 0; i < model.Fields.Length; i++)
                {
                    var field = model.Fields[0];
                    builder.AppendLine($"{indent.Value}hash.Add({field});");
                }
            }


            builder.AppendLine("""
                        return hash.ToHashCode();
                    }
                }
                """);

            context.AddSource($"AutoEquality.{model.Namespace}.{model.TypeName}.Generated.cs", builder.ToString());
        }
    }
}

file sealed class EqualityModel
{
    internal string Namespace { get; }
    internal string TypeName { get; }
    internal bool IsClass { get; }
    internal string[] Fields { get; }

    internal EqualityModel(
        string @namespace,
        string typeName,
        bool isClass,
        string[] fields)
    {
        Namespace = @namespace;
        TypeName = typeName;
        IsClass = isClass;
        Fields = fields;
    }
}

file sealed class EqualityModelComparer : IEqualityComparer<EqualityModel?>
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
            x.Namespace == x.Namespace &&
            x.TypeName == x.TypeName &&
            x.IsClass == x.IsClass &&
            x.Fields.AsSpan().SequenceEqual(y.Fields.AsSpan());
    }

    public int GetHashCode(EqualityModel? obj) => obj?.TypeName.GetHashCode() ?? 0;
}
