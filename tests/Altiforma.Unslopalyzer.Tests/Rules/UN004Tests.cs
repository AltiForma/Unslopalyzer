using System.Threading.Tasks;
using Altiforma.Unslopalyzer.Rules.UN004;
using Xunit;
using Verifier = Altiforma.Unslopalyzer.Tests.Verifiers.CSharpAnalyzerVerifier<Altiforma.Unslopalyzer.Rules.UN004.UN004Analyzer>;

namespace Altiforma.Unslopalyzer.Tests.Rules;

public sealed class UN004Tests
{
    private const string LicensingStubs = """
        using System.Threading;
        using System.Threading.Tasks;

        public sealed class LicensingService
        {
            public Task<bool> IsActivationValidAsync(
                int customerId,
                string productKey,
                string machineFingerprint,
                CancellationToken cancellationToken
            ) => Task.FromResult(true);

            public bool IsTrialActive(int customerId, string productKey, string machineFingerprint) =>
                false;
        }
        """;

    [Fact]
    public async Task WrappedNegatedAwaitTriggers()
    {
        var source =
            LicensingStubs
            + """

                public sealed class ActivationEndpoint
                {
                    private readonly LicensingService licensing = new();

                    public async Task<string> ActivateAsync(
                        int customerId,
                        string productKey,
                        string machineFingerprint,
                        CancellationToken cancellationToken
                    )
                    {
                        if (
                            {|#0:!await licensing.IsActivationValidAsync(
                                customerId,
                                productKey,
                                machineFingerprint,
                                cancellationToken
                            )|}
                        )
                            return "forbidden";

                        return "activated";
                    }
                }
                """;

        await Verifier.VerifyAnalyzerAsync(
            source,
            Verifier.Diagnostic(UN004Analyzer.DiagnosticId).WithLocation(0)
        );
    }

    [Fact]
    public async Task WrappedCallInElseIfTriggers()
    {
        var source =
            LicensingStubs
            + """

                public static class LicenseBadge
                {
                    public static string Describe(
                        LicensingService licensing,
                        bool isPaid,
                        int customerId,
                        string productKey,
                        string machineFingerprint
                    )
                    {
                        if (isPaid)
                            return "licensed";
                        else if (
                            {|#0:licensing.IsTrialActive(
                                customerId,
                                productKey,
                                machineFingerprint
                            )|}
                        )
                            return "trial";

                        return "expired";
                    }
                }
                """;

        await Verifier.VerifyAnalyzerAsync(
            source,
            Verifier.Diagnostic(UN004Analyzer.DiagnosticId).WithLocation(0)
        );
    }

    [Fact]
    public async Task WrappedWhileAndTernaryConditionsTrigger()
    {
        const string source = """
            using System.Collections.Generic;

            public static class Inventory
            {
                private static bool HasStockBelowThreshold(
                    IReadOnlyDictionary<string, int> stock,
                    string sku,
                    int threshold
                ) => stock.TryGetValue(sku, out var count) && count < threshold;

                public static string Restock(Dictionary<string, int> stock, string sku)
                {
                    while (
                        {|#0:HasStockBelowThreshold(
                            stock,
                            sku,
                            threshold: 10
                        )|}
                    )
                        stock[sku] = stock.GetValueOrDefault(sku) + 5;

                    return {|#1:HasStockBelowThreshold(
                        stock,
                        sku,
                        threshold: 20
                    )|}
                        ? "low"
                        : "ok";
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(
            source,
            Verifier.Diagnostic(UN004Analyzer.DiagnosticId).WithLocation(0),
            Verifier.Diagnostic(UN004Analyzer.DiagnosticId).WithLocation(1)
        );
    }

    [Fact]
    public async Task WrappedLambdaTriggers()
    {
        const string source = """
            using System.Collections.Generic;
            using System.Linq;

            public sealed record Shipment(bool IsDelayed, int DaysLate);

            public static class Shipments
            {
                public static bool NeedsEscalation(IEnumerable<Shipment> shipments)
                {
                    if (
                        {|#0:shipments.Any(shipment =>
                            shipment.IsDelayed && shipment.DaysLate > 3
                        )|}
                    )
                        return true;

                    return false;
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(
            source,
            Verifier.Diagnostic(UN004Analyzer.DiagnosticId).WithLocation(0)
        );
    }

    [Fact]
    public async Task SingleLineConditionsDoNotTrigger()
    {
        var source =
            LicensingStubs
            + """

                public static class LicenseCheck
                {
                    public static string Describe(LicensingService licensing, int customerId, string key, string machine)
                    {
                        if (licensing.IsTrialActive(customerId, key, machine))
                            return "trial";

                        if (
                            licensing.IsTrialActive(customerId, key, machine)
                        )
                            return "still trial";

                        return "expired";
                    }
                }
                """;

        await Verifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task WrappedMultiOperandConditionIsLeftToUN001()
    {
        const string source = """
            public static class Orders
            {
                public static string Describe(int quantity, decimal unitPriceIncludingTax)
                {
                    if (
                        quantity > 1000
                        && unitPriceIncludingTax * quantity > 1_000_000m
                    )
                        return "bulk";

                    return "standard";
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task ExtractedConditionDoesNotTrigger()
    {
        var source =
            LicensingStubs
            + """

                public sealed class ActivationEndpoint
                {
                    private readonly LicensingService licensing = new();

                    public async Task<string> ActivateAsync(
                        int customerId,
                        string productKey,
                        string machineFingerprint,
                        CancellationToken cancellationToken
                    )
                    {
                        var activationIsValid = await licensing.IsActivationValidAsync(
                            customerId,
                            productKey,
                            machineFingerprint,
                            cancellationToken
                        );

                        if (!activationIsValid)
                            return "forbidden";

                        return "activated";
                    }
                }
                """;

        await Verifier.VerifyAnalyzerAsync(source);
    }
}
