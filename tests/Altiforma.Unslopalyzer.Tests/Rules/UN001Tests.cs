using System.Threading.Tasks;
using Altiforma.Unslopalyzer.Rules.UN001;
using Xunit;
using Verifier = Altiforma.Unslopalyzer.Tests.Verifiers.CSharpAnalyzerVerifier<Altiforma.Unslopalyzer.Rules.UN001.UN001Analyzer>;

namespace Altiforma.Unslopalyzer.Tests.Rules;

public sealed class UN001Tests
{
    private const string SignInStubs = """
        using System.Threading.Tasks;

        public sealed class AppUser { }

        public sealed class SignInService
        {
            public Task<bool> CanSignInAsync(AppUser user) => Task.FromResult(true);
        }

        public sealed class UserService
        {
            public Task<bool> IsLockedOutAsync(AppUser user) => Task.FromResult(false);
        }

        public sealed class Principal
        {
            public bool HasScope(string scope) => true;
        }

        public abstract class ActionResult { }

        public sealed class ChallengeResult : ActionResult { }

        public sealed class OkResult : ActionResult { }
        """;

    [Fact]
    public async Task MultiLineOrChainWithAwaitTriggers()
    {
        var source =
            SignInStubs
            + """

                public sealed class UserInfoController
                {
                    private readonly SignInService signIn = new();
                    private readonly UserService users = new();
                    private readonly Principal User = new();

                    public async Task<ActionResult> UserInfo(AppUser? user)
                    {
                        if (
                            {|#0:user is null
                            || !await signIn.CanSignInAsync(user)
                            || await users.IsLockedOutAsync(user)
                            || !User.HasScope("openid")|}
                        )
                            return new ChallengeResult();

                        return new OkResult();
                    }
                }
                """;

        await Verifier.VerifyAnalyzerAsync(
            source,
            Verifier.Diagnostic(UN001Analyzer.DiagnosticId).WithLocation(0).WithArguments(4)
        );
    }

    [Fact]
    public async Task CertificateValidityCheckTriggers()
    {
        const string source = """
            using System;
            using System.Security.Cryptography;

            public sealed class Certificate
            {
                public DateTime NotBefore { get; init; }
                public DateTime NotAfter { get; init; }
            }

            public static class KeyEncryption
            {
                public static void EnsureUsable(object? privateKey, Certificate certificate, DateTimeOffset now)
                {
                    if (
                        {|#0:privateKey is null
                        || certificate.NotBefore.ToUniversalTime() > now.UtcDateTime
                        || certificate.NotAfter.ToUniversalTime() <= now.UtcDateTime|}
                    )
                        throw new CryptographicException(
                            "The certificate is not usable for key encryption."
                        );
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(
            source,
            Verifier.Diagnostic(UN001Analyzer.DiagnosticId).WithLocation(0).WithArguments(3)
        );
    }

    [Fact]
    public async Task SingleLineThreeOperandsTriggers()
    {
        const string source = """
            public static class Orders
            {
                public static bool CanShip(bool isPaid, bool isPacked, bool hasAddress)
                {
                    if ({|#0:isPaid && isPacked && hasAddress|})
                        return true;

                    return false;
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(
            source,
            Verifier.Diagnostic(UN001Analyzer.DiagnosticId).WithLocation(0).WithArguments(3)
        );
    }

    [Fact]
    public async Task MultiLineTwoOperandsTriggers()
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
            Verifier.Diagnostic(UN001Analyzer.DiagnosticId).WithLocation(0).WithArguments(2)
        );
    }

    [Fact]
    public async Task ElseIfTriggers()
    {
        const string source = """
            public static class Grading
            {
                public static string Grade(int score, bool submittedLate, bool hasExtension)
                {
                    if (score >= 90)
                        return "A";
                    else if ({|#0:submittedLate && !hasExtension && score < 50|})
                        return "F";

                    return "B";
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(
            source,
            Verifier.Diagnostic(UN001Analyzer.DiagnosticId).WithLocation(0).WithArguments(3)
        );
    }

    [Fact]
    public async Task WhileAndDoWhileTrigger()
    {
        const string source = """
            using System.Collections.Generic;

            public static class Queues
            {
                public static void Drain(Queue<int> queue, bool cancelled, int limit)
                {
                    var processed = 0;
                    while ({|#0:queue.Count > 0 && !cancelled && processed < limit|})
                    {
                        queue.Dequeue();
                        processed++;
                    }

                    do
                        processed--;
                    while ({|#1:processed > 0 && !cancelled && queue.Count == 0|});
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(
            source,
            Verifier.Diagnostic(UN001Analyzer.DiagnosticId).WithLocation(0).WithArguments(3),
            Verifier.Diagnostic(UN001Analyzer.DiagnosticId).WithLocation(1).WithArguments(3)
        );
    }

    [Fact]
    public async Task TernaryConditionTriggers()
    {
        const string source = """
            public static class Labels
            {
                public static string Status(bool isActive, bool isVerified, bool isBanned) =>
                    {|#0:isActive && isVerified && !isBanned|} ? "ok" : "blocked";
            }
            """;

        await Verifier.VerifyAnalyzerAsync(
            source,
            Verifier.Diagnostic(UN001Analyzer.DiagnosticId).WithLocation(0).WithArguments(3)
        );
    }

    [Fact]
    public async Task NestedParenthesizedOperandsAreCounted()
    {
        const string source = """
            public static class Access
            {
                public static bool Check(bool isAdmin, bool isOwner, bool isPublished)
                {
                    if ({|#0:isAdmin || (isOwner && isPublished)|})
                        return true;

                    return false;
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(
            source,
            Verifier.Diagnostic(UN001Analyzer.DiagnosticId).WithLocation(0).WithArguments(3)
        );
    }

    [Fact]
    public async Task TwoOperandsOnOneLineDoesNotTrigger()
    {
        const string source = """
            public static class Orders
            {
                public static bool CanShip(bool isPaid, bool isPacked)
                {
                    if (isPaid && isPacked)
                        return true;

                    return false;
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task SingleMultiLineInvocationDoesNotTrigger()
    {
        const string source = """
            public static class Validation
            {
                private static bool IsWithinRange(int value, int minimum, int maximum) =>
                    value >= minimum && value <= maximum;

                public static string Check(int value)
                {
                    if (
                        IsWithinRange(
                            value,
                            minimum: 0,
                            maximum: 100
                        )
                    )
                        return "ok";

                    return "out of range";
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task LogicalChainInsideLambdaDoesNotTrigger()
    {
        const string source = """
            using System.Collections.Generic;
            using System.Linq;

            public sealed record Order(bool IsPaid, bool IsPacked, bool HasAddress);

            public static class Orders
            {
                public static bool AnyReady(IEnumerable<Order> orders)
                {
                    if (orders.Any(o => o.IsPaid && o.IsPacked && o.HasAddress))
                        return true;

                    return false;
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task ExtractedConditionDoesNotTrigger()
    {
        var source =
            SignInStubs
            + """

                public sealed class UserInfoController
                {
                    private readonly SignInService signIn = new();
                    private readonly UserService users = new();
                    private readonly Principal User = new();

                    public async Task<ActionResult> UserInfo(AppUser? user)
                    {
                        var cannotSignIn =
                            user is null
                            || !await signIn.CanSignInAsync(user)
                            || await users.IsLockedOutAsync(user)
                            || !User.HasScope("openid");

                        if (cannotSignIn)
                            return new ChallengeResult();

                        return new OkResult();
                    }
                }
                """;

        await Verifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task ReturnAndAssignmentAreOutOfScope()
    {
        const string source = """
            public static class Orders
            {
                public static bool CanShip(bool isPaid, bool isPacked, bool hasAddress)
                {
                    var ready = isPaid && isPacked && hasAddress;
                    return ready && isPaid && isPacked;
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task MaxOperandsOptionRaisesThreshold()
    {
        const string source = """
            public static class Orders
            {
                public static bool CanShip(bool isPaid, bool isPacked, bool hasAddress)
                {
                    if (isPaid && isPacked && hasAddress)
                        return true;

                    return false;
                }
            }
            """;

        const string editorConfig = """
            root = true

            [*.cs]
            dotnet_diagnostic.UN001.max_operands = 4
            """;

        await Verifier.VerifyAnalyzerAsync(source, editorConfig);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-3")]
    [InlineData("many")]
    public async Task InvalidMaxOperandsFallsBackToDefault(string configuredValue)
    {
        const string source = """
            public static class Orders
            {
                public static bool CanShip(bool isPaid, bool isPacked, bool hasAddress)
                {
                    if ({|#0:isPaid && isPacked && hasAddress|})
                        return true;

                    return false;
                }
            }
            """;

        var editorConfig = $"""
            root = true

            [*.cs]
            dotnet_diagnostic.UN001.max_operands = {configuredValue}
            """;

        await Verifier.VerifyAnalyzerAsync(
            source,
            editorConfig,
            Verifier.Diagnostic(UN001Analyzer.DiagnosticId).WithLocation(0).WithArguments(3)
        );
    }
}
