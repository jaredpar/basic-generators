using Microsoft.CodeAnalysis;
using Xunit;

namespace Basic.Generators.UnitTests;

public class AutoEqualityGeneratorUnitTests
{
    public GeneratorTestUtil GeneratorTestUtil { get; }

    public AutoEqualityGeneratorUnitTests()
    {
        GeneratorTestUtil = new GeneratorTestUtil(new AutoEqualityGenerator());
    }

    [Fact]
    public void Simple()
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

        GeneratorTestUtil.Verify(source, expected, generatedTreeIndex: 1);
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
                        this.Field1 == other.Field1 &&
                        EqualityComparer<object>.Default.Equals(this.Field2, other.Field2) &&
                        string.Equals(this.Field3, other.Field3, StringComparison.OrdinalIgnoreCase);
                }

                public override int GetHashCode()
                {
                    var hash = new HashCode();
                    hash.Add(Field1);
                    hash.Add(Field1);
                    hash.Add(Field1);
                    return hash.ToHashCode();
                }

            }
            """;

        GeneratorTestUtil.Verify(source, expected, generatedTreeIndex: 1);
    }
}
