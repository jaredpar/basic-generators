using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace Basic.Generators;

[Generator(LanguageNames.CSharp)]
public sealed class AutoEqualityGenerator : IIncrementalGenerator
{
    private const string AutoEqualityAttributeName = "AutoEqualityAttribute";
    private const string AutoEqualityMemberAttributeName = "AutoEqualityMemberAttribute";

    private static readonly Lazy<string> s_autoEqualityKindCode = new(GetAutoEqualityKindCode);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(callback =>
        {
            callback.AddSource(
                "AutoEqualityAttribute.cs",
                $$"""
using System;
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
internal sealed class {{AutoEqualityAttributeName}} : Attribute
{
    public AutoEqualityAttribute()
    {
    }
}

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
internal sealed class {{AutoEqualityMemberAttributeName}} : Attribute
{
    public AutoEqualityKind Kind { get; set; }
    public AutoEqualityMemberAttribute(AutoEqualityKind kind)
    {
        Kind = kind;
    }
}

{{s_autoEqualityKindCode.Value}}

""");
        });

        var results = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                AutoEqualityAttributeName,
                CheckEqualityModel,
                GetEqualityModel)
            .Where(x => x is not null)
            .Select((x, _) => x!)
            .WithComparer(EqualityModelComparer.Instance);
        context.RegisterSourceOutput(results, AutoEqualityWriter.WriteEqualityModel);

        static bool CheckEqualityModel(SyntaxNode node, CancellationToken cancellationToken) =>
            node.IsKind(SyntaxKind.ClassDeclaration) || node.IsKind(SyntaxKind.StructDeclaration);

        static EqualityModel? GetEqualityModel(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
        {
            if (context.TargetSymbol is not INamedTypeSymbol { TypeKind: TypeKind.Class or TypeKind.Struct } symbol)
            {
                return null;
            }

            var typeUtil = TypeUtil.GetOrCreate(context.SemanticModel.Compilation);
            var list = new List<DataMemberModel>();

            foreach (var (memberSymbol, memberType) in GetDataMembers(symbol))
            {
                if (GetEqualityKind(memberSymbol, memberType, typeUtil) is not EqualityKind equalityKind)
                {
                    continue;
                }

                list.Add(new DataMemberModel(
                    memberSymbol.Name,
                    memberType?.TypeKind ?? TypeKind.Unknown,
                    memberType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ?? "",
                    equalityKind));
            }

            var @namespace = symbol.ContainingNamespace.IsGlobalNamespace
                ? null
                : symbol.ContainingNamespace.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

            return new EqualityModel(
                @namespace,
                symbol.Name,
                symbol.TypeKind == TypeKind.Class,
                simpleHashing: RoslynUtil.HasSimpleHashing(typeUtil),
                list.ToArray());
        }
    }

    /// <summary>
    /// Get all of the <see cref="ISymbol"/> members that should be considered for equality.
    /// </summary>
    internal static IEnumerable<(ISymbol Symbol, ITypeSymbol? Type)> GetDataMembers(INamedTypeSymbol typeSymbol)
    {
        foreach (var symbol in typeSymbol.GetMembers())
        {
            if (symbol is IFieldSymbol { AssociatedSymbol: null } fieldSymbol)
            {
                yield return (fieldSymbol, fieldSymbol.Type);
            }
            else if (
                symbol is IPropertySymbol propertySymbol &&
                propertySymbol.GetMethod is not null &&
                (propertySymbol.SetMethod is not null || GetEqualityKindFromAttribute(propertySymbol) is not null))
            {
                yield return (propertySymbol, propertySymbol.Type);
            }
        }
    }

    internal static EqualityKind? GetEqualityKind(ISymbol symbol, ITypeSymbol? typeSymbol, TypeUtil typeUtil)
    {
        if (typeSymbol is null)
        {
            return EqualityKind.Default;
        }

        if (GetEqualityKindFromAttribute(symbol) is { } kind)
        {
            return kind;
        }

        return GetEqualityKindForType(typeSymbol, typeUtil);
    }

    /// <summary>
    /// Get the equality kind from the AutoEqualityMemberAttribute if it exists on the <see cref="symbol"/>.
    /// </summary>
    internal static EqualityKind? GetEqualityKindFromAttribute(ISymbol symbol)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass?.Name == AutoEqualityMemberAttributeName &&
                attribute.ConstructorArguments.Length == 1 &&
                attribute.ConstructorArguments[0].Value is int i)
            {
                return (EqualityKind)i;
            }
        }

        return null;
    }

    /// <summary>
    /// Get the equality kind for the specified type. This does not consider attributes, it is just
    /// the default equality kind for this given type. 
    /// </summary>
    internal static EqualityKind GetEqualityKindForType(ITypeSymbol typeSymbol, TypeUtil typeUtil)
    {
        if (GetOperatorType(typeSymbol.SpecialType) is { } op)
        {
            return op;
        }

        if (typeUtil.IEnumerableT is { } ienumerableT && 
            RoslynUtil.IsOrImplementsOriginal(typeSymbol.OriginalDefinition, ienumerableT))
        {
            return EqualityKind.SequenceEqual;
        }

        return EqualityKind.Default;
    }

    internal static EqualityKind? GetOperatorType(SpecialType specialType) =>
        specialType switch
        {
            SpecialType.System_Int16 => EqualityKind.Operator,
            SpecialType.System_Int32 => EqualityKind.Operator,
            SpecialType.System_Int64 => EqualityKind.Operator,
            SpecialType.System_UInt16 => EqualityKind.Operator,
            SpecialType.System_UInt32 => EqualityKind.Operator,
            SpecialType.System_UInt64 => EqualityKind.Operator,
            SpecialType.System_IntPtr => EqualityKind.Operator,
            SpecialType.System_UIntPtr => EqualityKind.Operator,
            SpecialType.System_String => EqualityKind.Ordinal,
            _ => null,
        };

    internal static string GetAutoEqualityKindCode()
    {
        var builder = new StringBuilder();
        builder.AppendLine("internal enum AutoEqualityKind");
        using var stream = ResourceLoader.GetResourceStream("Basic.Generators.EqualityAttribute");
        using var reader = new StreamReader(stream);

        var inBody = false;
        while (reader.ReadLine() is string line)
        {
            if (line.Length > 0 && line[0] == '{')
            {
                inBody = true;
            }

            if (inBody)
            {
                builder.AppendLine(line);
            }

            if (line.Length > 0 && line[0] == '}')
            {
                break;
            }
        }

        return builder.ToString();
    }
}
