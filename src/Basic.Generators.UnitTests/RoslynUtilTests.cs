
using System.Linq;
using Basic.Reference.Assemblies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Basic.Generators.UnitTests;

public sealed class RoslynUtilTests
{
    [Theory]
    [InlineData("System.Collections.Generic.List<int>", "System.Collections.Generic.IEnumerable`1")]
    [InlineData("System.Collections.Generic.IEnumerable<int>", "System.Collections.Generic.IEnumerable`1")]
    [InlineData("int[]", "System.Collections.Generic.IEnumerable`1")]
    public void IsOrImplementsOriginal(string typeName, string interfaceName)
    {
        var code = $$"""
            public class Program
            {
                public static void Main()
                {
                    {{typeName}} local = default;
                    Use(local);

                    void Use({{typeName}} arg) { }
                }
            }
            """;

        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create(
            "test",
            [syntaxTree],
            Net472.References.All);
        AssertEx.Empty(compilation.GetDiagnostics());
        
        var model = compilation.GetSemanticModel(syntaxTree);
        var node = syntaxTree
            .GetRoot()
            .DescendantNodes()
            .OfType<LocalDeclarationStatementSyntax>()
            .Single();
        var localSymbol = (ILocalSymbol?)model.GetDeclaredSymbol(node.Declaration.Variables.Single());
        Assert.NotNull(localSymbol);
        Assert.NotNull(localSymbol.Type);

        var interfaceType = compilation.GetTypeByMetadataName(interfaceName);
        Assert.NotNull(interfaceType);
        Assert.True(RoslynUtil.IsOrImplementsOriginal(localSymbol.Type, interfaceType));
    }
}