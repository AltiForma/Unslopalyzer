using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Altiforma.Unslopalyzer;

internal static class Conditions
{
    public static readonly ImmutableArray<SyntaxKind> Kinds = ImmutableArray.Create(
        SyntaxKind.IfStatement,
        SyntaxKind.WhileStatement,
        SyntaxKind.DoStatement,
        SyntaxKind.ConditionalExpression
    );

    public static ExpressionSyntax? GetCondition(SyntaxNode node) =>
        node switch
        {
            IfStatementSyntax ifStatement => ifStatement.Condition,
            WhileStatementSyntax whileStatement => whileStatement.Condition,
            DoStatementSyntax doStatement => doStatement.Condition,
            ConditionalExpressionSyntax conditional => conditional.Condition,
            _ => null,
        };

    public static int CountOperands(ExpressionSyntax expression) =>
        expression switch
        {
            BinaryExpressionSyntax binary
                when binary.IsKind(SyntaxKind.LogicalAndExpression)
                    || binary.IsKind(SyntaxKind.LogicalOrExpression) => CountOperands(binary.Left)
                + CountOperands(binary.Right),
            ParenthesizedExpressionSyntax parenthesized => CountOperands(parenthesized.Expression),
            _ => 1,
        };

    public static bool SpansMultipleLines(SyntaxNode node)
    {
        var lineSpan = node.GetLocation().GetLineSpan();
        return lineSpan.StartLinePosition.Line != lineSpan.EndLinePosition.Line;
    }
}
