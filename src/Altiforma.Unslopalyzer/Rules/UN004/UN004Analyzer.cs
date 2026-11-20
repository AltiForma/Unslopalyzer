using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Altiforma.Unslopalyzer.Rules.UN004;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UN004Analyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "UN004";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        new LocalizableResourceString(
            nameof(Resources.UN004Title),
            Resources.ResourceManager,
            typeof(Resources)
        ),
        new LocalizableResourceString(
            nameof(Resources.UN004MessageFormat),
            Resources.ResourceManager,
            typeof(Resources)
        ),
        RuleCategories.Readability,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: new LocalizableResourceString(
            nameof(Resources.UN004Description),
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
        context.RegisterSyntaxNodeAction(AnalyzeNode, Conditions.Kinds);
    }

    private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        var condition = Conditions.GetCondition(context.Node);
        if (condition is null)
            return;

        var isWrappedSingleOperand =
            Conditions.CountOperands(condition) == 1 && Conditions.SpansMultipleLines(condition);

        if (isWrappedSingleOperand)
            context.ReportDiagnostic(Diagnostic.Create(Rule, condition.GetLocation()));
    }
}
