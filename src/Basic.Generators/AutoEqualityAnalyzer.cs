// write a C# analyzer that checks the arguments of attributes named AutoEqualityMember for validity

using System;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Basic.Generators;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AutoEqualityAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
        new DiagnosticDescriptor(
            "AE0001",
            "Invalid AutoEqualityMember attribute",
            "The AutoEqualityMember attribute must have a valid kind specified.",
            "Usage",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true));

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeAttribute, SyntaxKind.FieldDeclaration);
    }

    private void AnalyzeAttribute(SyntaxNodeAnalysisContext context)
    {
        var fieldNode = (FieldDeclarationSyntax)context.Node;
        if (fieldNode.AttributeLists.Count == 0)
            return;

        var symbolInfo = context.SemanticModel.GetSymbolInfo(fieldNode, context.CancellationToken);
        if (symbolInfo.Symbol is not IFieldSymbol { Type: { } } fieldSymbol)
            return;

        foreach (var attributeList in fieldNode.AttributeLists)
        {
            foreach (var attribute in attributeList.Attributes)
            {
                symbolInfo = context.SemanticModel.GetSymbolInfo(attribute, context.CancellationToken);
                if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
                {
                    continue;
                }

                Console.WriteLine(methodSymbol.Name);
            }
        }
    }
}