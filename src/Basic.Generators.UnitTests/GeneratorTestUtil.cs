using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using System;
using Xunit;
using Microsoft.CodeAnalysis;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using Basic.Reference.Assemblies;
using Xunit.Sdk;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Basic.Generators.UnitTests;

public sealed class GeneratorTestUtil
{
    public IIncrementalGenerator Generator { get; }

    public GeneratorTestUtil(IIncrementalGenerator generator)
    {
        Generator = generator;
    }

    private Compilation GetCompilation(string sourceCode) =>
        GetCompilation(new[] { CSharpSyntaxTree.ParseText(sourceCode) });

    private Compilation GetCompilation(IEnumerable<SyntaxTree> syntaxTrees)
    {
        var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);

        var compilation = CSharpCompilation
            .Create(
                assemblyName: "TestAssembly",
                syntaxTrees: syntaxTrees,
                options: options)
            .WithReferences(Net60.References.All);

        return compilation;
    }

    private void VerifyNoDiagnostics(Compilation compilation)
    {
        var diagnostics = compilation
            .GetDiagnostics()
            .Where(x => x.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning);
        Assert.Empty(diagnostics);
    }

    public GeneratorDriverRunResult GetRunResult(string sourceCode)
    {
        var compilation = GetCompilation(sourceCode);
        var driver = CSharpGeneratorDriver.Create(Generator);
        return driver
            .RunGenerators(compilation)
            .GetRunResult();
    }

    private (Compilation Compilation, SyntaxTree GenerateTree) VerifyCore(
        string sourceCode,
        int generatedTreeIndex = 0)
    {
        var sourceCodeTree = CSharpSyntaxTree.ParseText(sourceCode);
        var result = GetRunResult(sourceCode);
        var compilation = GetCompilation(result.GeneratedTrees.Append(sourceCodeTree));

        var generatedTree = compilation.SyntaxTrees.ToList()[generatedTreeIndex];
        VerifyNoDiagnostics(compilation);
        return (compilation, generatedTree);
    }

    public void Verify(
        string sourceCode,
        string expectedGeneratedCode,
        int generatedTreeIndex = 0)
    {
        var (_, generatedTree) = VerifyCore(sourceCode, generatedTreeIndex);
        Assert.Equal(Trim(expectedGeneratedCode), Trim(generatedTree.ToString()));
    }

    public void VerifyMethod(
        string methodSignature,
        string sourceCode,
        string expectedGeneratedCode,
        int generatedTreeIndex = 0)
    {
        var (compilation, generatedTree) = VerifyCore(sourceCode, generatedTreeIndex);
        var semanticModel = compilation.GetSemanticModel(generatedTree);
        var format = new SymbolDisplayFormat();
        var method = generatedTree
            .GetRoot()
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Where(x =>
            {
                if (semanticModel.GetDeclaredSymbol(x) is IMethodSymbol symbol)
                {
                    var sig = symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                    return sig == methodSignature;
                }

                return false;
            })
            .Single();

        Assert.Equal(Trim(expectedGeneratedCode), Trim(method.ToString()));
    }

    private static string Trim(string s) => s.Trim(' ', '\n', '\r');
}
