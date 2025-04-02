using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Basic.Reference.Assemblies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Model;
using Microsoft.CodeAnalysis.Text;
using Xunit;
using Xunit.Sdk;

namespace Basic.Generators.UnitTests;

public sealed class CSharpSourceGeneratorVerifier<TSourceGenerator>
    where TSourceGenerator : IIncrementalGenerator, new()
{
    public sealed class Test : CSharpSourceGeneratorTest<TSourceGenerator, DefaultVerifier>
    {
        public Compilation? CompilationAfterGenerators { get; private set; }
        public List<SyntaxTree> GeneratedSyntaxTrees { get; } = new();

        public Test(IEnumerable<MetadataReference> references)
        {
            ReferenceAssemblies = new(string.Empty);
            TestState.AdditionalReferences.AddRange(references);
        }

        protected override async Task<(Compilation compilation, ImmutableArray<Diagnostic> generatorDiagnostics)> GetProjectCompilationAsync(Project project, IVerifier verifier, CancellationToken cancellationToken)
        {
            var (compilation, generatorDiagnostics) = await base.GetProjectCompilationAsync(project, verifier, cancellationToken);
            CompilationAfterGenerators = compilation;
            GeneratedSyntaxTrees.AddRange(compilation.SyntaxTrees.Skip(TestState.Sources.Count));
            return (compilation, generatorDiagnostics);
        }

        public SyntaxTree GetGeneratedFile(string filePath)
        {
            var syntaxTree = GeneratedSyntaxTrees
                .Where(x => x.FilePath == filePath)
                .FirstOrDefault();
            if (syntaxTree is null)
            {
                Verify.Fail($"Generated file not found: {filePath}");
            }

            return syntaxTree;
        }
    }

    public static async Task VerifyAsync(
        string[] sources,
        IEnumerable<MetadataReference> references,
        (string HintName, string Content)[] generatedSources,
        params DiagnosticResult[] expected)
    {
        var test = new Test(references);
        test.TestState.Sources.AddRange(sources);
        AddGeneratedFiles(test.TestState, generatedSources);
        test.ExpectedDiagnostics.AddRange(expected);
        await test.RunAsync(CancellationToken.None);
    }

    public static async Task VerifyMethodAsync(
        string[] sources,
        IEnumerable<MetadataReference> references,
        string hintName,
        Action<Compilation, SyntaxTree> callback,
        params DiagnosticResult[] expected)
    {
        var test = new Test(references);
        test.TestState.Sources.AddRange(sources);
        test.TestBehaviors |= TestBehaviors.SkipGeneratedSourcesCheck;
        test.ExpectedDiagnostics.AddRange(expected);
        await test.RunAsync(CancellationToken.None);

        var generatedSyntaxTree = test.GetGeneratedFile(GetGeneratedFileName(hintName));
        Assert.NotNull(test.CompilationAfterGenerators);
        callback(test.CompilationAfterGenerators!, generatedSyntaxTree);
    }

    public static async Task VerifyMethodAsync(
        string[] sources,
        IEnumerable<MetadataReference> references,
        string hintName,
        (string ExpectedMethodSignature, string ExpectedCode)[] expectedMethods,
        params DiagnosticResult[] expected)
    {
        await VerifyMethodAsync(sources, references, hintName, (compilation, generatedTree) =>
        {
            var semanticModel = compilation.GetSemanticModel(generatedTree);
            foreach (var (signature, code) in expectedMethods)
            {
                var method = generatedTree
                    .GetRoot()
                    .DescendantNodes()
                    .OfType<MethodDeclarationSyntax>()
                    .Where(x =>
                    {
                        if (semanticModel.GetDeclaredSymbol(x) is IMethodSymbol symbol)
                        {
                            var sig = symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                            return sig == signature;
                        }

                        return false;
                    })
                    .Single();

                AssertEx.CodeEquals(code, method.ToString());
            }
        }, expected);
    }

    public static async Task VerifyMethodAsync(
        string[] sources,
        IEnumerable<MetadataReference> references,
        string hintName,
        string expectedMethodSignature,
        string expectedMethodCode,
        params DiagnosticResult[] expected) =>
        await VerifyMethodAsync(
            sources,
            references,
            hintName,
            [(expectedMethodSignature, expectedMethodCode)],
            expected);

    private static void AddGeneratedFiles(SolutionState testState, (string HintName, string Content)[] generatedSources)
    {
        foreach (var (hintName, content) in generatedSources)
        {
            var modFileName = GetGeneratedFileName(hintName);
            testState.GeneratedSources.Add((modFileName, SourceText.From(content, Encoding.UTF8)));
        }
    }

    public static async Task<(string AssemblyName, Stream Assembly)> VerifyEmit(
        string[] sources,
        IEnumerable<MetadataReference> references,
        OutputKind outputKind = OutputKind.DynamicallyLinkedLibrary,
        DiagnosticResult[]? expectedDiagnostics = null)
    {
        var test = new Test(references);
        test.TestState.Sources.AddRange(sources);
        test.TestState.OutputKind = outputKind;
        test.TestBehaviors |= TestBehaviors.SkipGeneratedSourcesCheck;
        test.ExpectedDiagnostics.AddRange(expectedDiagnostics ?? []);
        await test.RunAsync(CancellationToken.None);

        Assert.NotNull(test.CompilationAfterGenerators);

        var stream = new MemoryStream();
        EmitResult result = test.CompilationAfterGenerators.Emit(stream);
        Assert.True(result.Success);
        stream.Position = 0;
        return (test.TestState.AssemblyName, stream);
    }

    public static async Task VerifyOutput(
        string[] sources,
        IEnumerable<MetadataReference> references,
        string expectedOutput,
        DiagnosticResult[]? expectedDiagnostics = null)
    {
        var (assemblyName, stream) = await VerifyEmit(sources, references, OutputKind.ConsoleApplication, expectedDiagnostics);
        var (exitCode, output) = Execute(assemblyName, stream);
        Assert.Equal(0, exitCode);
        Assert.Equal(output, expectedOutput);
    }

    private static (int ExitCode, string Output) Execute(
        string assemblyName,
        Stream assemblyContent)
    {
        // Hook the output for the process. Wish there was a way to isolate this to the
        // single AssemblyLoadContext but not aware of one.
        var original = Console.Out;
        var outputStream = new MemoryStream();
        using var outputWriter = new StreamWriter(outputStream);

        var alc = new AssemblyLoadContext(assemblyName, isCollectible: true);
        int exitCode;
        try
        {
            Console.SetOut(outputWriter);

            var assembly = alc.LoadFromStream(assemblyContent);
            var entryPoint = assembly.EntryPoint;
            Assert.NotNull(entryPoint);

            var result = entryPoint.Invoke(null, [(string[])[]]);
            exitCode = result is null ? 0 : (int)result;
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

    public static string GetGeneratedFileName(string hintName)
    {
        var type = typeof(TSourceGenerator);
        var assemblyName = type.Assembly.GetName().Name!;
        var typeName = type.FullName!;
        return Path.Combine(assemblyName, typeName, hintName);
    }
}