using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Altiforma.Unslopalyzer.Rules.UN003;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UN003Analyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "UN003";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        new LocalizableResourceString(
            nameof(Resources.UN003Title),
            Resources.ResourceManager,
            typeof(Resources)
        ),
        new LocalizableResourceString(
            nameof(Resources.UN003MessageFormat),
            Resources.ResourceManager,
            typeof(Resources)
        ),
        RuleCategories.Design,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: new LocalizableResourceString(
            nameof(Resources.UN003Description),
            Resources.ResourceManager,
            typeof(Resources)
        ),
        helpLinkUri: HelpLinks.For(DiagnosticId)
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeTry, SyntaxKind.TryStatement);
    }

    private static void AnalyzeTry(SyntaxNodeAnalysisContext context)
    {
        var tryStatement = (TryStatementSyntax)context.Node;

        if (tryStatement.Parent is GlobalStatementSyntax)
            return;

        if (tryStatement.Parent is BlockSyntax block && WrapsWholeFunctionBody(block, tryStatement))
            return;

        context.ReportDiagnostic(Diagnostic.Create(Rule, tryStatement.TryKeyword.GetLocation()));
    }

    private static bool WrapsWholeFunctionBody(BlockSyntax block, TryStatementSyntax tryStatement)
    {
        var isFunctionBody =
            block.Parent
            is BaseMethodDeclarationSyntax
                or AccessorDeclarationSyntax
                or LocalFunctionStatementSyntax
                or AnonymousFunctionExpressionSyntax;

        if (!isFunctionBody || block.Statements[0] != tryStatement)
            return false;

        var trailingReturns = 0;
        for (var i = 1; i < block.Statements.Count; i++)
        {
            switch (block.Statements[i])
            {
                case LocalFunctionStatementSyntax:
                    continue;
                case ReturnStatementSyntax:
                    trailingReturns++;
                    break;
                default:
                    return false;
            }
        }

        return trailingReturns <= 1;
    }
}
