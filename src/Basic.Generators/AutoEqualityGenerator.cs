using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace Basic.Generators;

[Generator(LanguageNames.CSharp)]
public sealed class AutoEqualityGenerator : IIncrementalGenerator
{
    private const string AttributeName = "AutoEqualityAttribute";
    private const string AttributeMetadataName = AttributeName;

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(callback =>
        {
            callback.AddSource(
                "AutoEqualityAttribute.cs",
                $$"""
using System;
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
internal sealed class {{AttributeName}} : Attribute
{
    public bool CaseInsensitive { get; set; }

    public AutoEqualityAttribute(bool caseInsensitive = false) =>
        CaseInsensitive = caseInsensitive;
}
""");
        });

        var results = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                AttributeMetadataName,
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

            var attribute = context.Attributes.FirstOrDefault(x => x.AttributeClass?.Name == AttributeName);
            if (attribute is null)
            {
                return null;
            }

            var caseInsensitive =
                attribute.ConstructorArguments.Length > 0 &&
                attribute.ConstructorArguments[0].Value is true;

            var fields = symbol
                .GetMembers()
                .OfType<IFieldSymbol>()
                .Select(x => new FieldModel(x.Name, x.Type?.TypeKind ?? TypeKind.Unknown, x.Type?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ?? "", UseOperatorField(x, caseInsensitive)))
                .ToArray();

            var @namespace = symbol.ContainingNamespace.IsGlobalNamespace
                ? null
                : symbol.ContainingNamespace.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

            return new EqualityModel(
                @namespace,
                symbol.Name,
                symbol.TypeKind == TypeKind.Class,
                simpleHashing: HasSimpleHashing(context.SemanticModel.Compilation),
                fields);
        }

        static void WriteEqualityModel(SourceProductionContext context, EqualityModel model)
        {
            var builder = new StringBuilder();
            var indent = new IndentUtil();
            var annotatedName = model.IsClass ? $"{model.TypeName}?" : model.TypeName;

            builder.Append($$"""
                using System;
                using System.Collections.Generic;

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

            WriteEquals();
            WriteGetHashCode();

            builder.Append("""
                }
                """);

            context.AddSource($"AutoEquality.{model.Namespace}.{model.TypeName}.Generated.cs", builder.ToString());

            void WriteEquals()
            {
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

                using var _ = indent.Increase(3);
                for (var i = 0; i < model.Fields.Length; i++)
                {
                    var field = model.Fields[i];
                    var name = field.Name;
                    var comp = field.CompareKind switch
                    {
                        CompareKind.Operator => $"this.{name} == other.{name}",
                        CompareKind.CaseInsensitive => $"string.Equals(this.{name}, other.{name}, StringComparison.OrdinalIgnoreCase)",
                        CompareKind.Equals => $"EqualityComparer<{field.TypeFullName}>.Default.Equals(this.{name}, other.{name})",
                        _ => throw new Exception($"invalid {field.CompareKind}")
                    };

                    builder.Append($"{indent.Value}{comp}");
                    if (i + 1 == model.Fields.Length)
                    {
                        builder.AppendLine(";");
                    }
                    else
                    {
                        builder.AppendLine(" &&");
                    }
                }

                builder.Append("""
                        }


                    """);
            }

            void WriteGetHashCode()
            {
                if (model.SimpleHashing)
                {
                    WriteGetHashCodeSimple();
                }
                else
                {
                    WriteGetHashCodeOld();
                }
            }

            void WriteGetHashCodeOld()
            {
                builder.Append($$"""
                        public override int GetHashCode()
                        {
                            int hash = 17;

                    """);

                using var _ = indent.Increase(2);
                for (var i = 0; i < model.Fields.Length; i++)
                {
                    var field = model.Fields[i];
                    if (field.TypeKind is TypeKind.Enum or TypeKind.Struct or TypeKind.Structure)
                    {
                        builder.AppendLine($"{indent.Value}hash = (hash * 23) + {field.Name}.GetHashCode();");
                    }
                    else
                    {
                        builder.AppendLine($"{indent.Value}hash = (hash * 23) + ({field.Name}?.GetHashCode() ?? 0);");
                    }
                }

                builder.AppendLine($"{indent.Value}return hash;");

                builder.Append("""
                        }

                    """);
            }

            void WriteGetHashCodeSimple()
            {
                builder.Append($$"""
                        public override int GetHashCode() =>
                            HashCode.Combine(

                    """);

                for (var i = 0; i < model.Fields.Length; i++)
                {
                    var field = model.Fields[i];
                    var suffix = i + 1 == model.Fields.Length ? ");" : ",";
                    builder.AppendLine($"            {field.Name}{suffix}");
                }
            }
        }

        static CompareKind UseOperatorField(IFieldSymbol field, bool caseInsensitive)
        {
            if (!(field.Type is { } type))
            {
                return CompareKind.Equals;
            }

            return UseOperatorType(type.SpecialType, caseInsensitive);
        }

        static CompareKind UseOperatorType(SpecialType specialType, bool caseInsensitive) =>
            specialType switch
            {
                SpecialType.System_Int16 => CompareKind.Operator,
                SpecialType.System_Int32 => CompareKind.Operator,
                SpecialType.System_Int64 => CompareKind.Operator,
                SpecialType.System_UInt16 => CompareKind.Operator,
                SpecialType.System_UInt32 => CompareKind.Operator,
                SpecialType.System_UInt64 => CompareKind.Operator,
                SpecialType.System_IntPtr => CompareKind.Operator,
                SpecialType.System_UIntPtr => CompareKind.Operator,
                SpecialType.System_String => caseInsensitive ? CompareKind.CaseInsensitive : CompareKind.Operator,
                _ => CompareKind.Equals,
            };
    }

    internal static bool HasSimpleHashing(Compilation compilation)
    {
        var types = compilation.GetTypesByMetadataName("System.HashCode");
        if (types.Length != 1)
        {
            return false;
        }

        var type = types[0];
        return type
            .GetMembers("Combine")
            .OfType<IMethodSymbol>()
            .Any(x => x.Arity == 7);
    }
}

file enum CompareKind
{
    Operator,
    Equals,
    CaseInsensitive,
}

file record struct FieldModel(string Name, TypeKind TypeKind, string TypeFullName, CompareKind CompareKind);

file sealed class EqualityModel
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
            x.Namespace == y.Namespace &&
            x.TypeName == y.TypeName &&
            x.IsClass == y.IsClass && 
            x.SimpleHashing == y.SimpleHashing &&
            x.Fields.AsSpan().SequenceEqual(y.Fields.AsSpan());
    }

    public int GetHashCode(EqualityModel? obj) => obj?.TypeName.GetHashCode() ?? 0;
}
