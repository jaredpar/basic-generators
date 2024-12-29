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
using System.IO;
using System.Runtime.Loader;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.Emit;

namespace Basic.Generators.UnitTests;

public sealed class GeneratorTestUtil
{
    private static CSharpCompilationOptions LibraryCompilationOptions = new (OutputKind.DynamicallyLinkedLibrary);

    public IIncrementalGenerator Generator { get; }

    public GeneratorTestUtil(IIncrementalGenerator generator)
    {
        Generator = generator;
    }

    private Compilation GetCompilation(
        IEnumerable<SyntaxTree> syntaxTrees,
        IEnumerable<MetadataReference> references,
        CSharpCompilationOptions options)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: syntaxTrees,
            options: options,
            references: references);

        return compilation;
    }

    public GeneratorDriverRunResult GetRunResult(IEnumerable<SyntaxTree> syntaxTrees, IEnumerable<MetadataReference> references, CSharpCompilationOptions options)
    {
        var compilation = GetCompilation(syntaxTrees, references, options);
        var driver = CSharpGeneratorDriver.Create(Generator);
        return driver
            .RunGenerators(compilation)
            .GetRunResult();
    }

    public Compilation GetCompilationAfterGenerators(
        string sourceCode,
        IEnumerable<MetadataReference> references,
        CSharpCompilationOptions options,
        out GeneratorDriverRunResult result)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        SyntaxTree[] syntaxTrees = [syntaxTree];
        result = GetRunResult(syntaxTrees, references, options);
        var compilation = GetCompilation([.. result.GeneratedTrees, syntaxTree], references, options);
        return compilation;
    }

    private void VerifyNoDiagnostics(Compilation compilation)
    {
        var diagnostics = compilation
            .GetDiagnostics()
            .Where(x => x.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning);
        AssertEx.Empty(diagnostics);
    }

    private (Compilation Compilation, SyntaxTree GenerateTree) VerifyCore(
        string sourceCode,
        IEnumerable<MetadataReference> references,
        int generatedTreeIndex = 0)
    {
        var compilation = GetCompilationAfterGenerators(sourceCode, references, LibraryCompilationOptions, out _);
        var generatedTree = compilation.SyntaxTrees.ToList()[generatedTreeIndex];
        VerifyNoDiagnostics(compilation);
        return (compilation, generatedTree);
    }

    public void Verify(
        string sourceCode,
        IEnumerable<MetadataReference> references,
        string expectedGeneratedCode,
        int generatedTreeIndex = 0)
    {
        var (_, generatedTree) = VerifyCore(sourceCode, references, generatedTreeIndex);
        AssertEx.CodeEquals(Trim(expectedGeneratedCode), Trim(generatedTree.ToString()));
    }

    public void VerifyMethod(
        string methodSignature,
        string sourceCode,
        IEnumerable<MetadataReference> references,
        string expectedGeneratedCode,
        int generatedTreeIndex = 0)
    {
        var (compilation, generatedTree) = VerifyCore(sourceCode, references, generatedTreeIndex);
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

        AssertEx.CodeEquals(Trim(expectedGeneratedCode), Trim(method.ToString()));
    }

    /// <summary>
    /// Verify the output of the assembly when executed
    /// </summary>
    /// <param name="assembly"></param>
    /// <param name="expectedOutput"></param>
    public void VerifyOutput(
        string sourceCode,
        IEnumerable<MetadataReference> references,
        string expectedOutput,
        [CallerMemberName] string? assemblyName = null)
    {
        var options = new CSharpCompilationOptions(OutputKind.ConsoleApplication);
        var compilation = GetCompilationAfterGenerators(sourceCode, references, options, out _);
        VerifyNoDiagnostics(compilation);

        var stream = EmitToStream(compilation);
        var (exitCode, output) = Execute(stream, assemblyName ?? "TestAssembly");
        Assert.Equal(0, exitCode ?? 0);
        Assert.Equal(expectedOutput, output);

        static Stream EmitToStream(Compilation compilation)
        {
            var stream = new MemoryStream();
            EmitResult result = compilation.Emit(stream);
            Assert.True(result.Success);
            stream.Position = 0;
            return stream;
        }
    }

    private (int? ExitCode, string Output) Execute(
        Stream assemblyContent,
        string assemblyName)
    {
        // Hook the output for the process. Wish there was a way to isolate this to the
        // single AssemblyLoadContext but not aware of one.
        var original = Console.Out;
        var outputStream = new MemoryStream();
        using var outputWriter = new StreamWriter(outputStream);

        var alc = new AssemblyLoadContext(assemblyName, isCollectible: true);
        int? exitCode;
        try
        {
            Console.SetOut(outputWriter);

            var assembly = alc.LoadFromStream(assemblyContent);
            var entryPoint = assembly.EntryPoint;
            Assert.NotNull(entryPoint);

            var result = entryPoint.Invoke(null, [(string[])[]]);
            exitCode = result is null ? null : (int)result;
        }
        finally
        {
            if (!object.ReferenceEquals(original, Console.Out))
            {
                Console.SetOut(original);
            }

            alc.Unload();
        }

        outputWriter.Flush();
        outputStream.Position = 0;
        using var reader = new StreamReader(outputStream);
        var output = reader.ReadToEnd();
        return (exitCode, output);
    }


    private static string Trim(string s) => s.Trim(' ', '\n', '\r');
}
