using System.Threading.Tasks;
using Altiforma.Unslopalyzer.Rules.UN001;
using Xunit;
using Verifier = Altiforma.Unslopalyzer.Tests.Verifiers.CSharpAnalyzerVerifier<Altiforma.Unslopalyzer.Rules.UN001.UN001Analyzer>;

namespace Altiforma.Unslopalyzer.Tests.Rules;

public sealed partial class UN001Tests
{
    [Fact]
    public async Task MessageNamesTheOperandLimit()
    {
        const string source = """
            public static class Orders
            {
                public static bool CanShip(bool isPaid, bool isPacked, bool hasAddress) =>
                    {|#0:isPaid && isPacked && hasAddress|} ? true : false;
            }
            """;

        await Verifier.VerifyAnalyzerAsync(
            source,
            Verifier
                .Diagnostic(UN001Analyzer.DiagnosticId)
                .WithLocation(0)
                .WithMessage(
                    "Condition has 3 operands, more than the maximum of 2; assign it to a variable whose name explains the intent"
                )
        );
    }

    [Fact]
    public async Task MessageNamesTheLineBreak()
    {
        const string source = """
            public static class Orders
            {
                public static string Describe(int quantity, decimal unitPriceIncludingTax)
                {
                    if (
                        {|#0:quantity > 1000
                        && unitPriceIncludingTax * quantity > 1_000_000m|}
                    )
                        return "bulk";

                    return "standard";
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(
            source,
            Verifier
                .Diagnostic(UN001Analyzer.DiagnosticId)
                .WithLocation(0)
                .WithMessage(
                    "Condition has 2 operands and spans multiple lines; assign it to a variable whose name explains the intent"
                )
        );
    }

    [Fact]
    public async Task NegatedPairOnOneLineDoesNotTrigger()
    {
        const string source = """
            public sealed class ModelState
            {
                public bool IsValid { get; init; }
            }

            public sealed class ValidationResult
            {
                public bool IsValid { get; init; }
            }

            public static class Forms
            {
                public static string Submit(ModelState modelState, ValidationResult validation)
                {
                    if (!modelState.IsValid || !validation.IsValid)
                        return "invalid";

                    return "ok";
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task PatternsCountAsOneOperand()
    {
        const string source = """
            public sealed record Account(string? Name, bool IsActive);

            public static class Accounts
            {
                public static bool IsUsable(Account? account)
                {
                    if (account is not null && account.IsActive)
                        return true;

                    if (account?.Name is null or "" or "unknown")
                        return false;

                    return account.IsActive;
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task NonShortCircuitAndConditionalOperatorsCountAsOneOperand()
    {
        const string source = """
            public static class Flags
            {
                public static bool Check(bool a, bool b, bool c, bool? d, bool e)
                {
                    if (a & b & c)
                        return true;

                    if ((d ?? false) && (e ? a : b))
                        return true;

                    return false;
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(source);
    }
}
