using System.Collections;
using System.Collections.Generic;
using Basic.Reference.Assemblies;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Basic.Generators.UnitTests;

public class AutoEqualityGeneratorUnitTests
{
    public static IEnumerable<MetadataReference> CoreReferences => Net80.References.All;
    public static IEnumerable<MetadataReference> FrameworkReferences => Net472.References.All;

    public GeneratorTestUtil GeneratorTestUtil { get; }

    public AutoEqualityGeneratorUnitTests()
    {
        GeneratorTestUtil = new GeneratorTestUtil(new AutoEqualityGenerator());
    }

    [Fact]
    public void SimpleClass()
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

        GeneratorTestUtil.Verify(source, CoreReferences, expected, generatedTreeIndex: 1);
    }

    [Fact]
    public void SimpleStruct()
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

        GeneratorTestUtil.Verify(source, CoreReferences, expected, generatedTreeIndex: 1);
    }

    [Fact]
    public void NoNamespace()
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

        GeneratorTestUtil.Verify(source, CoreReferences, expected, generatedTreeIndex: 1);
    }

    [Fact]
    public void EqualsOperatorPrimitives()
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

        var expected = """
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

        GeneratorTestUtil.VerifyMethod(
            "bool Simple.Equals(Simple? other)",
            source,
            CoreReferences,
            expected,
            generatedTreeIndex: 1);
    }

    [Theory]
    [InlineData("int[]")]
    [InlineData("IEnumerable<int>")]
    [InlineData("List<char>")]
    public void EqualsCollections(string typeName)
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

        var expected = """
                public bool Equals(Simple? other)
                {
                    if (other is null)
                        return false;

                    return
                        Enumerable.SequenceEqual(this.Field, other.Field);
                }
            """;

        GeneratorTestUtil.VerifyMethod(
            "bool Simple.Equals(Simple? other)",
            source,
            CoreReferences,
            expected,
            generatedTreeIndex: 1);
    }

    [Fact]
    public void EqualsProperty()
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

        var expected = """
                public bool Equals(Simple? other)
                {
                    if (other is null)
                        return false;

                    return
                        this.Prop1 == other.Prop1 &&
                        this.Prop2 == other.Prop2;
                }
            """;

        GeneratorTestUtil.VerifyMethod(
            "bool Simple.Equals(Simple? other)",
            source,
            CoreReferences,
            expected,
            generatedTreeIndex: 1);
    }

    [Fact]
    public void GetHashCodeSimple()
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

        var expected = """
                public override int GetHashCode() =>
                    HashCode.Combine(
                        Field1,
                        Field2);
            """;

        GeneratorTestUtil.VerifyMethod(
            "int Simple.GetHashCode()",
            source,
            CoreReferences,
            expected,
            generatedTreeIndex: 1);
    }

    [Fact]
    public void GetHashCodeOldStructFields()
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

        var expected = """
                public override int GetHashCode()
                {
                    int hash = 17;
                    hash = (hash * 23) + Field1.GetHashCode();
                    hash = (hash * 23) + Field2.GetHashCode();
                    return hash;
                }
            """;

        GeneratorTestUtil.VerifyMethod(
            "int Simple.GetHashCode()",
            source,
            FrameworkReferences,
            expected,
            generatedTreeIndex: 1);
    }

    [Fact]
    public void GetHashCodeOldStructClass()
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

        var expected = """
                public override int GetHashCode()
                {
                    int hash = 17;
                    hash = (hash * 23) + Field1.GetHashCode();
                    hash = (hash * 23) + (Field2?.GetHashCode() ?? 0);
                    return hash;
                }
            """;

        GeneratorTestUtil.VerifyMethod(
            "int Simple.GetHashCode()",
            source,
            FrameworkReferences,
            expected,
            generatedTreeIndex: 1);
    }

    [Fact]
    public void MemberSequence()
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

        GeneratorTestUtil.VerifyMethod(
            "int Simple.GetHashCode()",
            source,
            CoreReferences,
            expectedHashCode,
            generatedTreeIndex: 1);

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

        GeneratorTestUtil.VerifyMethod(
            "bool Simple.Equals(Simple? other)",
            source,
            CoreReferences,
            expectedEquals,
            generatedTreeIndex: 1);
    }

    [Fact]
    public void MemberNone()
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

        GeneratorTestUtil.VerifyMethod(
            "int Simple.GetHashCode()",
            source,
            CoreReferences,
            expectedHashCode,
            generatedTreeIndex: 1);

        var expectedEquals = """
                public bool Equals(Simple? other)
                {
                    if (other is null)
                        return false;

                    return
                        this.Field1 == other.Field1;
                }
            """;

        GeneratorTestUtil.VerifyMethod(
            "bool Simple.Equals(Simple? other)",
            source,
            CoreReferences,
            expectedEquals,
            generatedTreeIndex: 1);
    }

    [Fact]
    public void MemberProperty()
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

        GeneratorTestUtil.VerifyMethod(
            "bool Simple.Equals(Simple? other)",
            source,
            CoreReferences,
            expectedEquals,
            generatedTreeIndex: 1);
    }

    [Theory]
    [InlineData("Ordinal", "test", "test", "diff")]
    [InlineData("OrdinalIgnoreCase", "test", "TEST", "diff")]
    [InlineData("InvariantCulture", "test", "test", "diff")]
    [InlineData("InvariantCultureIgnoreCase", "test", "TEST", "diff")]
    [InlineData("CurrentCulture", "test", "test", "diff")]
    [InlineData("CurrentCultureIgnoreCase", "test", "TEST", "diff")]
    public void MemberStringField(string kind, string value, string valueEqual, string valueDifferent)
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
        GeneratorTestUtil.VerifyOutput(
            code,
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
    public void MemberStringProperty(string kind, string value, string valueEqual, string valueDifferent)
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
        GeneratorTestUtil.VerifyOutput(
            code,
            CoreReferences,
            "110");
    }
}
