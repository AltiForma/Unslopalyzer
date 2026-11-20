using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Altiforma.Unslopalyzer.Rules.UN002;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UN002Analyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "UN002";
    private const string MethodName = "ThrowIfCancellationRequested";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        new LocalizableResourceString(
            nameof(Resources.UN002Title),
            Resources.ResourceManager,
            typeof(Resources)
        ),
        new LocalizableResourceString(
            nameof(Resources.UN002MessageFormat),
            Resources.ResourceManager,
            typeof(Resources)
        ),
        RuleCategories.Redundancy,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: new LocalizableResourceString(
            nameof(Resources.UN002Description),
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
        context.RegisterCompilationStartAction(compilationContext =>
        {
            var cancellationToken = compilationContext.Compilation.GetTypeByMetadataName(
                "System.Threading.CancellationToken"
            );

            if (cancellationToken is null)
                return;

            compilationContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeInvocation(nodeContext, cancellationToken),
                SyntaxKind.InvocationExpression
            );
        });
    }

    private static void AnalyzeInvocation(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol cancellationToken
    )
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        var invokedName = invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            MemberBindingExpressionSyntax memberBinding => memberBinding.Name.Identifier.ValueText,
            _ => null,
        };

        if (invokedName != MethodName)
            return;

        var symbol = context
            .SemanticModel.GetSymbolInfo(invocation, context.CancellationToken)
            .Symbol;
        var isCancellationTokenCall = SymbolEqualityComparer.Default.Equals(
            symbol?.ContainingType,
            cancellationToken
        );

        if (!isCancellationTokenCall || IsInsideLoop(invocation))
            return;

        SyntaxNode reported = invocation.Parent
            is ConditionalAccessExpressionSyntax conditionalAccess
            ? conditionalAccess
            : invocation;

        context.ReportDiagnostic(Diagnostic.Create(Rule, reported.GetLocation()));
    }

    private static bool IsInsideLoop(SyntaxNode node)
    {
        foreach (var ancestor in node.Ancestors())
        {
            switch (ancestor)
            {
                case ForStatementSyntax:
                case CommonForEachStatementSyntax:
                case WhileStatementSyntax:
                case DoStatementSyntax:
                    return true;
                case AnonymousFunctionExpressionSyntax:
                case LocalFunctionStatementSyntax:
                case MemberDeclarationSyntax:
                    return false;
            }
        }

        return false;
    }
}
