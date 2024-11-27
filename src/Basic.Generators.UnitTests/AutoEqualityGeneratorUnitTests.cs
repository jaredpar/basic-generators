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

                public override int GetHashCode()
                {
                    var hash = new HashCode();
                    hash.Add(Field);
                    return hash.ToHashCode();
                }

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

                public override int GetHashCode()
                {
                    var hash = new HashCode();
                    hash.Add(Field);
                    return hash.ToHashCode();
                }

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

                public override int GetHashCode()
                {
                    var hash = new HashCode();
                    hash.Add(Field);
                    return hash.ToHashCode();
                }

            }
            """;

        GeneratorTestUtil.Verify(source, CoreReferences, expected, generatedTreeIndex: 1);
    }

    [Fact]
    public void OperatorAndEquals()
    {
        var source = """
            namespace Example;

            #pragma warning disable CS0649

            [AutoEquality(true)]
            partial class Simple
            {
                int Field1;
                object Field2;
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
                        string.Equals(this.Field3, other.Field3, StringComparison.OrdinalIgnoreCase);
                }
            """;

        GeneratorTestUtil.VerifyMethod(
            "bool Simple.Equals(Simple? other)",
            source,
            CoreReferences,
            expected,
            generatedTreeIndex: 1);

        // GeneratorTestUtil.Verify(source, expected, generatedTreeIndex: 1);
    }
}
