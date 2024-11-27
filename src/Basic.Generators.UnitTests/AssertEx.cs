
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Basic.Generators.UnitTests;

internal static class AssertEx
{
    public static void CodeEquals(string expected, string actual)
    {
        var expectedReader = new StringReader(expected);
        var actualReader = new StringReader(actual);
        var line = 0;
        do
        {
            var expectedLine = expectedReader.ReadLine();
            var actualLine = actualReader.ReadLine();
            if (expectedLine is null && actualLine is null)
            {
                return;
            }

            if (expectedLine is null)
            {
                Assert.Fail($"Actual has more lines than expected at line {line}: {actualLine}");
            }

            if (actualLine is null)
            {
                Assert.Fail($"Expected has more lines than actual at line {line}: {expectedLine}");
            }

            Assert.Equal(expectedLine, actualLine);
            line++;
        } while (true);
    }

    public static void Empty(IEnumerable<Diagnostic> diagnostics)
    {
        using var e = diagnostics.GetEnumerator();
        if (!e.MoveNext())
        {
            return;
        }

        var builder = new StringBuilder();
        do
        {
            var diagnostic = e.Current;
            builder.AppendLine(diagnostic.ToString());
        } while (e.MoveNext());
        Assert.Fail(builder.ToString());
    }
}