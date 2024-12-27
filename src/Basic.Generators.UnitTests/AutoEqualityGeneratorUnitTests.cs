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

    // TODO: test none
    // TODO: test use the string comparison enum names
}
