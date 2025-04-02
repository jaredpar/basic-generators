using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Basic.Reference.Assemblies;
using Microsoft;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;
using Xunit;

using VerifyAutoEqualityGenerator = Basic.Generators.UnitTests.CSharpSourceGeneratorVerifier<Basic.Generators.AutoEqualityGenerator>;

namespace Basic.Generators.UnitTests;

public class AutoEqualityGeneratorUnitTests
{
    public static IEnumerable<MetadataReference> CoreReferences => Net80.References.All;
    public static IEnumerable<MetadataReference> FrameworkReferences => Net472.References.All;

    public AutoEqualityGeneratorUnitTests()
    {
    }

    [Fact]
    public async Task SimpleClass()
    {
        var source = """
            namespace Example;

            #pragma warning disable CS0649

            [AutoEquality]
            partial class Simple
            {
                int Field;
            }
            """;

        var expected = """
            using System;
            using System.Collections.Generic;

            #nullable enable

            namespace Example;

            partial class Simple : IEquatable<Simple?>
            {
                public static bool operator==(Simple? left, Simple? right) =>
                    left is not null ? left.Equals(right) : right is null;

                public static bool operator!=(Simple? left, Simple? right) =>
                    !(left == right);

                public override bool Equals(object? other) =>
                    other is Simple o && Equals(o);

                public bool Equals(Simple? other)
                {
                    if (other is null)
                        return false;

                    return
                        this.Field == other.Field;
                }

                public override int GetHashCode() =>
                    HashCode.Combine(
                        Field);
            }
            """;

        await VerifyAutoEqualityGenerator.VerifyAsync(
            [source],
            CoreReferences,
            [
                (AutoEqualityGenerator.AutoEqualityAttributeHintName, AutoEqualityGenerator.GetAutoEqualityAttributeCode()),
                ("AutoEquality.Example.Simple.Generated.cs", expected),
            ]);
    }

    [Fact]
    public async Task SimpleStruct()
    {
        var source = """
            namespace Example;

            #pragma warning disable CS0649

            [AutoEquality]
            partial struct Simple
            {
                int Field;
            }
            """;

        var expected = """
            using System;
            using System.Collections.Generic;

            #nullable enable

            namespace Example;

            partial struct Simple : IEquatable<Simple>
            {
                public static bool operator==(Simple left, Simple right) =>
                    left.Equals(right);

                public static bool operator!=(Simple left, Simple right) =>
                    !(left == right);

                public override bool Equals(object? other) =>
                    other is Simple o && Equals(o);

                public bool Equals(Simple other)
                {
                    return
                        this.Field == other.Field;
                }

                public override int GetHashCode() =>
                    HashCode.Combine(
                        Field);
            }
            """;

        await VerifyAutoEqualityGenerator.VerifyAsync(
            [source],
            CoreReferences,
            [
                (AutoEqualityGenerator.AutoEqualityAttributeHintName, AutoEqualityGenerator.GetAutoEqualityAttributeCode()),
                ("AutoEquality.Example.Simple.Generated.cs", expected),
            ]);
    }

    [Fact]
    public async Task NoNamespace()
    {
        var source = """
            #pragma warning disable CS0649

            [AutoEquality]
            partial class Simple
            {
                int Field;
            }
            """;

        var expected = """
            using System;
            using System.Collections.Generic;

            #nullable enable

            partial class Simple : IEquatable<Simple?>
            {
                public static bool operator==(Simple? left, Simple? right) =>
                    left is not null ? left.Equals(right) : right is null;

                public static bool operator!=(Simple? left, Simple? right) =>
                    !(left == right);

                public override bool Equals(object? other) =>
                    other is Simple o && Equals(o);

                public bool Equals(Simple? other)
                {
                    if (other is null)
                        return false;

                    return
                        this.Field == other.Field;
                }

                public override int GetHashCode() =>
                    HashCode.Combine(
                        Field);
            }
            """;

        await VerifyAutoEqualityGenerator.VerifyAsync(
            [source],
            CoreReferences,
            [
                (AutoEqualityGenerator.AutoEqualityAttributeHintName, AutoEqualityGenerator.GetAutoEqualityAttributeCode()),
                ("AutoEquality..Simple.Generated.cs", expected),
            ]);
    }

    [Fact]
    public async Task EqualsOperatorPrimitives()
    {
        var source = """
            namespace Example;

            #pragma warning disable CS0649

            [AutoEquality]
            partial class Simple
            {
                int Field1;
                object Field2;
                [AutoEqualityMember(AutoEqualityKind.Ordinal)]
                string Field3;
            }
            """;

        var expectedMethodCode = """
                public bool Equals(Simple? other)
                {
                    if (other is null)
                        return false;

                    return
                        this.Field1 == other.Field1 &&
                        EqualityComparer<object>.Default.Equals(this.Field2, other.Field2) &&
                        this.Field3 == other.Field3;
                }
            """;

        await VerifyAutoEqualityGenerator.VerifyMethodAsync(
            [source],
            CoreReferences,
            "AutoEquality.Example.Simple.Generated.cs",
            "bool Simple.Equals(Simple? other)",
            expectedMethodCode);
    }

    [Theory]
    [InlineData("int[]")]
    [InlineData("IEnumerable<int>")]
    [InlineData("List<char>")]
    public async Task EqualsCollections(string typeName)
    {
        var source = $$"""
            namespace Example;
            using System.Collections.Generic;

            #pragma warning disable CS0649

            [AutoEquality]
            partial class Simple
            {
                {{typeName}} Field;
            }
            """;

        var expectedMethodCode = """
                public bool Equals(Simple? other)
                {
                    if (other is null)
                        return false;

                    return
                        Enumerable.SequenceEqual(this.Field, other.Field);
                }
            """;

        await VerifyAutoEqualityGenerator.VerifyMethodAsync(
            [source],
            CoreReferences,
            "AutoEquality.Example.Simple.Generated.cs",
            "bool Simple.Equals(Simple? other)",
            expectedMethodCode);
    }

    [Fact]
    public async Task EqualsProperty()
    {
        var source = $$"""
            namespace Example;
            using System.Collections.Generic;

            #pragma warning disable CS0649

            [AutoEquality]
            partial class Simple
            {
                public int Prop1 { get; set; }
                public int Prop2 { get; init; }
                public int Prop3 { get { throw null!; } }
                public int Prop4 { set { throw null!; } }
            }
            """;

        var expectedMethodCode = """
                public bool Equals(Simple? other)
                {
                    if (other is null)
                        return false;

                    return
                        this.Prop1 == other.Prop1 &&
                        this.Prop2 == other.Prop2;
                }
            """;

        await VerifyAutoEqualityGenerator.VerifyMethodAsync(
            [source],
            CoreReferences,
            "AutoEquality.Example.Simple.Generated.cs",
            "bool Simple.Equals(Simple? other)",
            expectedMethodCode);
    }

    [Fact]
    public async Task GetHashCodeSimple()
    {
        var source = """
            #pragma warning disable CS0649

            [AutoEquality]
            partial class Simple
            {
                int Field1;
                int Field2;
            }
            """;

        var expectedMethodCode = """
                public override int GetHashCode() =>
                    HashCode.Combine(
                        Field1,
                        Field2);
            """;

        await VerifyAutoEqualityGenerator.VerifyMethodAsync(
            [source],
            CoreReferences,
            "AutoEquality..Simple.Generated.cs",
            "int Simple.GetHashCode()",
            expectedMethodCode);
    }

    [Fact]
    public async Task GetHashCodeOldStructFields()
    {
        var source = """
            #pragma warning disable CS0649

            [AutoEquality]
            partial class Simple
            {
                int Field1;
                int Field2;
            }
            """;

        var expectedMethodCode = """
                public override int GetHashCode()
                {
                    int hash = 17;
                    hash = (hash * 23) + Field1.GetHashCode();
                    hash = (hash * 23) + Field2.GetHashCode();
                    return hash;
                }
            """;

        await VerifyAutoEqualityGenerator.VerifyMethodAsync(
            [source],
            FrameworkReferences,
            "AutoEquality..Simple.Generated.cs",
            "int Simple.GetHashCode()",
            expectedMethodCode);
    }

    [Fact]
    public async Task GetHashCodeOldStructClass()
    {
        var source = """
            #pragma warning disable CS0649

            [AutoEquality]
            partial class Simple
            {
                int Field1;
                string Field2;
            }
            """;

        var expectedMethodCode = """
                public override int GetHashCode()
                {
                    int hash = 17;
                    hash = (hash * 23) + Field1.GetHashCode();
                    hash = (hash * 23) + (Field2?.GetHashCode() ?? 0);
                    return hash;
                }
            """;

        await VerifyAutoEqualityGenerator.VerifyMethodAsync(
            [source],
            FrameworkReferences,
            "AutoEquality..Simple.Generated.cs",
            "int Simple.GetHashCode()",
            expectedMethodCode);
    }

    [Fact]
    public async Task MemberSequence()
    {
        var source = """
            #pragma warning disable CS0649

            [AutoEquality]
            partial class Simple
            {
                int Field1;
                [AutoEqualityMember(AutoEqualityKind.OrdinalIgnoreCase)]
                string Field2;
            }
            """;

        var expectedHashCode = """
                public override int GetHashCode() =>
                    HashCode.Combine(
                        Field1,
                        StringComparer.OrdinalIgnoreCase.GetHashCode(Field2));
            """;

        var expectedEquals = """
                public bool Equals(Simple? other)
                {
                    if (other is null)
                        return false;

                    return
                        this.Field1 == other.Field1 &&
                        StringComparer.OrdinalIgnoreCase.Equals(this.Field2, other.Field2);
                }
            """;

        await VerifyAutoEqualityGenerator.VerifyMethodAsync(
            [source],
            CoreReferences,
            "AutoEquality..Simple.Generated.cs",
            [
                ("int Simple.GetHashCode()", expectedHashCode),
                ("bool Simple.Equals(Simple? other)", expectedEquals)
            ]);
    }

    [Fact]
    public async Task MemberNone()
    {
        var source = """
            #pragma warning disable CS0649
            #pragma warning disable CS0169

            [AutoEquality]
            partial class Simple
            {
                int Field1;
                [AutoEqualityMember(AutoEqualityKind.None)]
                string Field2;
            }
            """;

        var expectedHashCode = """
                public override int GetHashCode() =>
                    HashCode.Combine(
                        Field1);
            """;

        var expectedEquals = """
                public bool Equals(Simple? other)
                {
                    if (other is null)
                        return false;

                    return
                        this.Field1 == other.Field1;
                }
            """;

        await VerifyAutoEqualityGenerator.VerifyMethodAsync(
            [source],
            CoreReferences,
            "AutoEquality..Simple.Generated.cs",
            [
                ("int Simple.GetHashCode()", expectedHashCode),
                ("bool Simple.Equals(Simple? other)", expectedEquals)
            ]);
    }

    [Fact]
    public async Task MemberProperty()
    {
        var source = """
            #pragma warning disable CS0649

            [AutoEquality]
            partial class Simple
            {
                public string Prop1 { get; set; }
                [AutoEqualityMember(AutoEqualityKind.None)]
                public string Prop2 { get; set; }
                [AutoEqualityMember(AutoEqualityKind.OrdinalIgnoreCase)]
                public string Prop3 { get { throw null!; } }
            }
            """;

        var expectedEquals = """
                public bool Equals(Simple? other)
                {
                    if (other is null)
                        return false;

                    return
                        this.Prop1 == other.Prop1 &&
                        StringComparer.OrdinalIgnoreCase.Equals(this.Prop3, other.Prop3);
                }
            """;


        await VerifyAutoEqualityGenerator.VerifyMethodAsync(
            [source],
            CoreReferences,
            "AutoEquality..Simple.Generated.cs",
            "bool Simple.Equals(Simple? other)",
            expectedEquals);
    }

    [Theory]
    [InlineData("Ordinal", "test", "test", "diff")]
    [InlineData("OrdinalIgnoreCase", "test", "TEST", "diff")]
    [InlineData("InvariantCulture", "test", "test", "diff")]
    [InlineData("InvariantCultureIgnoreCase", "test", "TEST", "diff")]
    [InlineData("CurrentCulture", "test", "test", "diff")]
    [InlineData("CurrentCultureIgnoreCase", "test", "TEST", "diff")]
    public async Task MemberStringField(string kind, string value, string valueEqual, string valueDifferent)
    {
        var code = $$"""
            #pragma warning disable CS0649
            using System;

            var v1 = new Simple("{{value}}");
            var v2 = new Simple("{{valueEqual}}");
            var v3 = new Simple("{{valueDifferent}}");

            Test(v1 == v2);
            Test(v2 == v1);
            Test(v1 == v3);

            void Test(bool cond)
            {
                Console.Write(cond ? "1" : "0");
            }

            [AutoEquality]
            partial class Simple(string field)
            {
                [AutoEqualityMember(AutoEqualityKind.{{kind}})]
                public string Field = field;
            }
            """;

        await VerifyAutoEqualityGenerator.VerifyOutput(
            [code],
            CoreReferences,
            "110");
    }

    [Theory]
    [InlineData("Ordinal", "test", "test", "diff")]
    [InlineData("OrdinalIgnoreCase", "test", "TEST", "diff")]
    [InlineData("InvariantCulture", "test", "test", "diff")]
    [InlineData("InvariantCultureIgnoreCase", "test", "TEST", "diff")]
    [InlineData("CurrentCulture", "test", "test", "diff")]
    [InlineData("CurrentCultureIgnoreCase", "test", "TEST", "diff")]
    public async Task MemberStringProperty(string kind, string value, string valueEqual, string valueDifferent)
    {
        var code = $$"""
            #pragma warning disable CS0649
            using System;

            var v1 = new Simple("{{value}}");
            var v2 = new Simple("{{valueEqual}}");
            var v3 = new Simple("{{valueDifferent}}");

            Test(v1 == v2);
            Test(v2 == v1);
            Test(v1 == v3);

            void Test(bool cond)
            {
                Console.Write(cond ? "1" : "0");
            }

            [AutoEquality]
            partial class Simple(string field)
            {
                [AutoEqualityMember(AutoEqualityKind.{{kind}})]
                public string Field { get; } = field;
            }
            """;

        await VerifyAutoEqualityGenerator.VerifyOutput(
            [code],
            CoreReferences,
            "110");
    }
}
