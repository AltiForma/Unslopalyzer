using System.Threading.Tasks;
using Altiforma.Unslopalyzer.Rules.UN003;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using Verifier = Altiforma.Unslopalyzer.Tests.Verifiers.CSharpAnalyzerVerifier<Altiforma.Unslopalyzer.Rules.UN003.UN003Analyzer>;

namespace Altiforma.Unslopalyzer.Tests.Rules;

public sealed class UN003Tests
{
    [Fact]
    public async Task TryAfterSetupStatementsTriggers()
    {
        const string source = """
            using System.IO;
            using System.Text.Json;

            public sealed record ReportSettings(string Title, int PageSize);

            public static class ReportLoader
            {
                public static ReportSettings Load(string directory)
                {
                    var path = Path.Combine(directory, "report.json");
                    var json = File.ReadAllText(path);

                    {|#0:try|}
                    {
                        return JsonSerializer.Deserialize<ReportSettings>(json)!;
                    }
                    catch (JsonException)
                    {
                        return new ReportSettings("Untitled", 50);
                    }
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(
            source,
            Verifier.Diagnostic(UN003Analyzer.DiagnosticId).WithLocation(0)
        );
    }

    [Fact]
    public async Task TryFollowedByMoreLogicTriggers()
    {
        const string source = """
            using System;
            using System.Globalization;

            public static class PriceFormatter
            {
                public static string Format(decimal amount, string cultureName)
                {
                    CultureInfo culture;
                    {|#0:try|}
                    {
                        culture = CultureInfo.GetCultureInfo(cultureName);
                    }
                    catch (CultureNotFoundException)
                    {
                        culture = CultureInfo.InvariantCulture;
                    }

                    var rounded = Math.Round(amount, 2, MidpointRounding.ToEven);
                    return rounded.ToString("C", culture);
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(
            source,
            Verifier.Diagnostic(UN003Analyzer.DiagnosticId).WithLocation(0)
        );
    }

    [Fact]
    public async Task TryInsideRetryLoopTriggers()
    {
        const string source = """
            using System;
            using System.Net.Http;
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class PaymentClient
            {
                private readonly HttpClient http = new();

                public async Task<bool> CaptureAsync(Uri endpoint, CancellationToken cancellationToken)
                {
                    for (var attempt = 1; attempt <= 3; attempt++)
                    {
                        {|#0:try|}
                        {
                            using var response = await http.PostAsync(endpoint, null, cancellationToken);
                            return response.IsSuccessStatusCode;
                        }
                        catch (HttpRequestException)
                        {
                            await Task.Delay(TimeSpan.FromSeconds(attempt), cancellationToken);
                        }
                    }

                    return false;
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(
            source,
            Verifier.Diagnostic(UN003Analyzer.DiagnosticId).WithLocation(0)
        );
    }

    [Fact]
    public async Task TryFinallyInMiddleOfBlockTriggers()
    {
        const string source = """
            using System.Collections.Generic;
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class ThumbnailCache
            {
                private readonly SemaphoreSlim gate = new(1, 1);
                private readonly Dictionary<string, byte[]> entries = new();

                public async Task<int> StoreAsync(string key, byte[] image, CancellationToken cancellationToken)
                {
                    var resized = image.Length > 4096 ? image[..4096] : image;

                    await gate.WaitAsync(cancellationToken);
                    {|#0:try|}
                    {
                        entries[key] = resized;
                    }
                    finally
                    {
                        gate.Release();
                    }

                    return resized.Length;
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(
            source,
            Verifier.Diagnostic(UN003Analyzer.DiagnosticId).WithLocation(0)
        );
    }

    [Fact]
    public async Task TryNestedInCatchTriggers()
    {
        const string source = """
            using System.IO;

            public static class Archive
            {
                public static void Move(string source, string destination)
                {
                    try
                    {
                        File.Move(source, destination);
                    }
                    catch (IOException)
                    {
                        {|#0:try|}
                        {
                            File.Copy(source, destination, overwrite: true);
                        }
                        catch (IOException)
                        {
                        }
                    }
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(
            source,
            Verifier.Diagnostic(UN003Analyzer.DiagnosticId).WithLocation(0)
        );
    }

    [Fact]
    public async Task TryBetweenDeclarationAndReturnTriggers()
    {
        const string source = """
            using System;

            public static class Ports
            {
                public static int Parse(string value)
                {
                    int port;
                    {|#0:try|}
                    {
                        port = int.Parse(value);
                    }
                    catch (FormatException)
                    {
                        port = 8080;
                    }
                    return port;
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(
            source,
            Verifier.Diagnostic(UN003Analyzer.DiagnosticId).WithLocation(0)
        );
    }

    [Fact]
    public async Task TryWrappingWholeBodiesDoesNotTrigger()
    {
        const string source = """
            using System;
            using System.IO;
            using System.Threading;

            public sealed class ConfigFile
            {
                private readonly string contents;
                private readonly object sync = new();
                private int reads;

                public ConfigFile(string path)
                {
                    try
                    {
                        contents = File.ReadAllText(path);
                    }
                    catch (FileNotFoundException)
                    {
                        contents = string.Empty;
                    }
                }

                public int Length
                {
                    get
                    {
                        try
                        {
                            return contents.Length;
                        }
                        finally
                        {
                            Interlocked.Increment(ref reads);
                        }
                    }
                }

                public Func<string, int> LineCounter() =>
                    text =>
                    {
                        try
                        {
                            return text.Split('\n').Length;
                        }
                        catch (ArgumentException)
                        {
                            return 0;
                        }
                    };

                public bool TryReadFirstLine(out string line)
                {
                    line = ReadFirst();
                    return line.Length > 0;

                    string ReadFirst()
                    {
                        try
                        {
                            using var reader = new StringReader(contents);
                            return reader.ReadLine() ?? string.Empty;
                        }
                        catch (IOException)
                        {
                            return string.Empty;
                        }
                    }
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task TryFollowedBySingleReturnDoesNotTrigger()
    {
        const string source = """
            using System;
            using System.Globalization;

            public static class Dates
            {
                public static DateOnly ParseOrToday(string value)
                {
                    try
                    {
                        return DateOnly.Parse(value, CultureInfo.InvariantCulture);
                    }
                    catch (FormatException)
                    {
                    }

                    return DateOnly.FromDateTime(DateTime.UtcNow);
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task LocalFunctionsAfterTryAreIgnored()
    {
        const string source = """
            using System.IO;

            public static class Manifests
            {
                public static string ReadName(string path)
                {
                    try
                    {
                        return Normalize(File.ReadAllText(path));
                    }
                    catch (FileNotFoundException)
                    {
                        return Normalize("unnamed");
                    }

                    static string Normalize(string name) => name.Trim().ToLowerInvariant();
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task TryInTopLevelStatementsDoesNotTrigger()
    {
        var test = new CSharpAnalyzerTest<UN003Analyzer, DefaultVerifier>
        {
            TestCode = """
                using System;

                var arguments = Environment.GetCommandLineArgs();
                try
                {
                    Console.WriteLine(arguments.Length);
                }
                catch (Exception exception)
                {
                    Console.Error.WriteLine(exception.Message);
                }
                """,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        test.TestState.OutputKind = OutputKind.ConsoleApplication;

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
