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

    public void Verify(
        string sourceCode,
        string expectedGeneratedCode,
        int generatedTreeIndex = 0)
    {
        var sourceCodeTree = CSharpSyntaxTree.ParseText(sourceCode);
        var result = GetRunResult(sourceCode);
        var compilation = GetCompilation(result.GeneratedTrees.Append(sourceCodeTree));

        var generatedTree = compilation.SyntaxTrees.ToList()[generatedTreeIndex];
        var actualCode = Trim(generatedTree.ToString());
        Assert.Equal(Trim(expectedGeneratedCode), actualCode);

        VerifyNoDiagnostics(compilation);
        string Trim(string s) => s.Trim(' ', '\n', '\r');
    }
}
