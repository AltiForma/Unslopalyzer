using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Altiforma.Unslopalyzer.Rules.UN001;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UN001Analyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "UN001";
    public const string MaxOperandsOption = "dotnet_diagnostic.UN001.max_operands";
    public const int DefaultMaxOperands = 2;

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        new LocalizableResourceString(
            nameof(Resources.UN001Title),
            Resources.ResourceManager,
            typeof(Resources)
        ),
        new LocalizableResourceString(
            nameof(Resources.UN001MessageFormat),
            Resources.ResourceManager,
            typeof(Resources)
        ),
        RuleCategories.Readability,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: new LocalizableResourceString(
            nameof(Resources.UN001Description),
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
        context.RegisterSyntaxNodeAction(
            AnalyzeNode,
            SyntaxKind.IfStatement,
            SyntaxKind.WhileStatement,
            SyntaxKind.DoStatement,
            SyntaxKind.ConditionalExpression
        );
    }

    private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        var condition = context.Node switch
        {
            IfStatementSyntax ifStatement => ifStatement.Condition,
            WhileStatementSyntax whileStatement => whileStatement.Condition,
            DoStatementSyntax doStatement => doStatement.Condition,
            ConditionalExpressionSyntax conditional => conditional.Condition,
            _ => null,
        };

        if (condition is null)
            return;

        var operands = CountOperands(condition);
        if (operands < 2)
            return;

        var maxOperands = ReadMaxOperands(context);
        var lineSpan = condition.GetLocation().GetLineSpan();
        var spansMultipleLines = lineSpan.StartLinePosition.Line != lineSpan.EndLinePosition.Line;
        var isTooComplex = operands > maxOperands || spansMultipleLines;

        if (isTooComplex)
            context.ReportDiagnostic(Diagnostic.Create(Rule, condition.GetLocation(), operands));
    }

    private static int CountOperands(ExpressionSyntax expression) =>
        expression switch
        {
            BinaryExpressionSyntax binary
                when binary.IsKind(SyntaxKind.LogicalAndExpression)
                    || binary.IsKind(SyntaxKind.LogicalOrExpression) => CountOperands(binary.Left)
                + CountOperands(binary.Right),
            ParenthesizedExpressionSyntax parenthesized => CountOperands(parenthesized.Expression),
            _ => 1,
        };

    private static int ReadMaxOperands(SyntaxNodeAnalysisContext context)
    {
        var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(
            context.Node.SyntaxTree
        );

        if (!options.TryGetValue(MaxOperandsOption, out var value))
            return DefaultMaxOperands;

        var isPositiveInteger =
            int.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var maxOperands
            )
            && maxOperands >= 1;

        return isPositiveInteger ? maxOperands : DefaultMaxOperands;
    }
}
