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

            var typeUtil = TypeUtil.GetOrCreate(context.SemanticModel.Compilation);
            var fields = symbol
                .GetMembers()
                .OfType<IFieldSymbol>()
                .Select(x => new FieldModel(x.Name, x.Type?.TypeKind ?? TypeKind.Unknown, x.Type?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ?? "", GetEqualityKind(x.Type, typeUtil, caseInsensitive)))
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
            var builder = new CodeBuilder();
            var annotatedName = model.IsClass ? $"{model.TypeName}?" : model.TypeName;

            builder.AppendLine($$"""
                using System;
                using System.Collections.Generic;
                """);
            
            if (model.Fields.Any(x => x.EqualityKind == EqualityKind.SequenceEqual))
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

                for (var i = 0; i < model.Fields.Length; i++)
                {
                    var field = model.Fields[i];
                    var name = field.Name;
                    var comp = GetEqualsExpression(field.EqualityKind, name, field.TypeFullName);
                    builder.Append(12, comp);
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

                for (var i = 0; i < model.Fields.Length; i++)
                {
                    var field = model.Fields[i];
                    if (field.TypeKind is TypeKind.Enum or TypeKind.Struct or TypeKind.Structure)
                    {
                        builder.AppendLine(8, $"hash = (hash * 23) + {field.Name}.GetHashCode();");
                    }
                    else
                    {
                        builder.AppendLine(8, $"hash = (hash * 23) + ({field.Name}?.GetHashCode() ?? 0);");
                    }
                }

                builder.AppendLine(8, $"return hash;");

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
    }

    internal static string GetEqualsExpression(EqualityKind kind, string memberName, string typeFullName)
    {
        return kind switch
        {
            EqualityKind.Operator => $"this.{memberName} == other.{memberName}",
            EqualityKind.StringCaseSensitive => $"this.{memberName} == other.{memberName}",
            EqualityKind.StringCaseInsensitive => $"string.Equals(this.{memberName}, other.{memberName}, StringComparison.OrdinalIgnoreCase)",
            EqualityKind.Default => $"EqualityComparer<{typeFullName}>.Default.Equals(this.{memberName}, other.{memberName})",
            EqualityKind.SequenceEqual => $"Enumerable.SequenceEqual(this.{memberName}, other.{memberName})",
            _ => throw new Exception($"invalid {kind}")
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

    internal static EqualityKind GetEqualityKind(ITypeSymbol? typeSymbol, TypeUtil typeUtil, bool caseInsensitive)
    {
        if (typeSymbol is null)
        {
            return EqualityKind.Default;
        }

        if (GetOperatorType(typeSymbol.SpecialType, caseInsensitive) is { } op)
        {
            return op;
        }

        if (typeUtil.IEnumerableT is { } ienumerableT && 
            RoslynUtil.DoesTypeImplementOriginal(typeSymbol.OriginalDefinition, ienumerableT))
        {
            return EqualityKind.SequenceEqual;
        }

        return EqualityKind.Default;
    }

    internal static EqualityKind? GetOperatorType(SpecialType specialType, bool caseInsensitive) =>
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
            SpecialType.System_String => caseInsensitive ? EqualityKind.StringCaseInsensitive : EqualityKind.StringCaseSensitive,
            _ => null,
        };
}
