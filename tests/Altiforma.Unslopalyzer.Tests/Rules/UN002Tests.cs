using System.Threading.Tasks;
using Altiforma.Unslopalyzer.Rules.UN002;
using Xunit;
using Verifier = Altiforma.Unslopalyzer.Tests.Verifiers.CSharpAnalyzerVerifier<Altiforma.Unslopalyzer.Rules.UN002.UN002Analyzer>;

namespace Altiforma.Unslopalyzer.Tests.Rules;

public sealed class UN002Tests
{
    private const string RepositoryStubs = """
        using System.Collections.Generic;
        using System.Threading;
        using System.Threading.Tasks;

        public sealed record Invoice(int Id, decimal Total);

        public sealed class InvoiceRepository
        {
            public Task<Invoice?> FindAsync(int id, CancellationToken cancellationToken) =>
                Task.FromResult<Invoice?>(null);

            public Task SaveAsync(Invoice invoice, CancellationToken cancellationToken) =>
                Task.CompletedTask;

            public async IAsyncEnumerable<Invoice> StreamAsync(
                [System.Runtime.CompilerServices.EnumeratorCancellation]
                    CancellationToken cancellationToken
            )
            {
                await Task.Yield();
                yield break;
            }
        }
        """;

    [Fact]
    public async Task CallsOutsideLoopsTrigger()
    {
        var source =
            RepositoryStubs
            + """

                public sealed class InvoiceService
                {
                    private readonly InvoiceRepository repository = new();

                    public async Task<Invoice?> ApproveAsync(int id, CancellationToken cancellationToken)
                    {
                        {|#0:cancellationToken.ThrowIfCancellationRequested()|};

                        var invoice = await repository.FindAsync(id, cancellationToken);
                        if (invoice is null)
                            return null;

                        {|#1:cancellationToken.ThrowIfCancellationRequested()|};

                        await repository.SaveAsync(invoice, cancellationToken);

                        {|#2:cancellationToken.ThrowIfCancellationRequested()|};
                        return invoice;
                    }
                }
                """;

        await Verifier.VerifyAnalyzerAsync(
            source,
            Verifier.Diagnostic(UN002Analyzer.DiagnosticId).WithLocation(0),
            Verifier.Diagnostic(UN002Analyzer.DiagnosticId).WithLocation(1),
            Verifier.Diagnostic(UN002Analyzer.DiagnosticId).WithLocation(2)
        );
    }

    [Fact]
    public async Task CallInIfOutsideLoopTriggers()
    {
        const string source = """
            using System.Threading;

            public static class Exporter
            {
                public static void Export(bool includeArchived, CancellationToken cancellationToken)
                {
                    if (includeArchived)
                        {|#0:cancellationToken.ThrowIfCancellationRequested()|};
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(
            source,
            Verifier.Diagnostic(UN002Analyzer.DiagnosticId).WithLocation(0)
        );
    }

    [Fact]
    public async Task CallInLambdaOutsideLoopTriggers()
    {
        const string source = """
            using System;
            using System.Threading;
            using System.Threading.Tasks;

            public static class Jobs
            {
                public static Task RunAsync(CancellationToken cancellationToken) =>
                    Task.Run(
                        () =>
                        {
                            {|#0:cancellationToken.ThrowIfCancellationRequested()|};
                            Console.WriteLine("working");
                        },
                        cancellationToken
                    );
            }
            """;

        await Verifier.VerifyAnalyzerAsync(
            source,
            Verifier.Diagnostic(UN002Analyzer.DiagnosticId).WithLocation(0)
        );
    }

    [Fact]
    public async Task LocalFunctionDeclaredInLoopTriggers()
    {
        const string source = """
            using System.Collections.Generic;
            using System.Threading;

            public static class Batches
            {
                public static int Sum(IEnumerable<int> values, CancellationToken cancellationToken)
                {
                    var total = 0;
                    foreach (var value in values)
                    {
                        total += Square(value);

                        int Square(int number)
                        {
                            {|#0:cancellationToken.ThrowIfCancellationRequested()|};
                            return number * number;
                        }
                    }

                    return total;
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(
            source,
            Verifier.Diagnostic(UN002Analyzer.DiagnosticId).WithLocation(0)
        );
    }

    [Fact]
    public async Task ConditionalAccessCallTriggers()
    {
        const string source = """
            using System.Threading;

            public static class Exporter
            {
                public static void Export(CancellationToken? cancellationToken)
                {
                    {|#0:cancellationToken?.ThrowIfCancellationRequested()|};
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(
            source,
            Verifier.Diagnostic(UN002Analyzer.DiagnosticId).WithLocation(0)
        );
    }

    [Fact]
    public async Task CallsInsideLoopsDoNotTrigger()
    {
        var source =
            RepositoryStubs
            + """

                public static class Reports
                {
                    public static async Task<decimal> TotalAsync(
                        InvoiceRepository repository,
                        IReadOnlyList<int[]> matrix,
                        CancellationToken cancellationToken
                    )
                    {
                        var total = 0m;

                        await foreach (var invoice in repository.StreamAsync(cancellationToken))
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            total += invoice.Total;
                        }

                        foreach (var row in matrix)
                        {
                            if (row.Length > 1000)
                                cancellationToken.ThrowIfCancellationRequested();

                            for (var i = 0; i < row.Length; i++)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                total += row[i];
                            }
                        }

                        var remaining = 10;
                        while (remaining > 0)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            remaining--;
                        }

                        do
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            remaining++;
                        } while (remaining < 5);

                        return total;
                    }
                }
                """;

        await Verifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task UnrelatedMethodWithSameNameDoesNotTrigger()
    {
        const string source = """
            public sealed class Workflow
            {
                public bool IsCancelled { get; init; }

                public void ThrowIfCancellationRequested()
                {
                    if (IsCancelled)
                        throw new System.InvalidOperationException("Workflow cancelled.");
                }
            }

            public static class Runner
            {
                public static void Run(Workflow workflow) => workflow.ThrowIfCancellationRequested();
            }
            """;

        await Verifier.VerifyAnalyzerAsync(source);
    }
}
