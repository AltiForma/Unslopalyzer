# Altiforma.Unslopalyzer — Agent Guide

## Overview

Altiforma.Unslopalyzer is a set of Roslyn analyzers for C# projects, with code fixes where a fix is safe. It catches the code smells that AI coding agents tend to produce ("slop"). 

The conventions follow [DogmaSolutions/Analyzers](https://github.com/DogmaSolutions/Analyzers). Above all, **every rule has its own documentation page**, and the diagnostic links to it.

Repository: <https://github.com/AltiForma/Unslopalyzer>

## Tech Stack

- **Analyzer:** `netstandard2.0`, Microsoft.CodeAnalysis.CSharp, and Microsoft.CodeAnalysis.CSharp.Workspaces for code fixes
- **Tests:** .NET 10, xUnit, Microsoft.CodeAnalysis.Testing
- **Analysis of our own code:** DogmaSolutions.Analyzers and the .NET analyzers
- **Formatting:** CSharpier

## Architecture

```
Altiforma.Unslopalyzer.slnx
Directory.Build.props                   # LangVersion latest, Nullable enable, TreatWarningsAsErrors
.editorconfig
src/Altiforma.Unslopalyzer/             # netstandard2.0, EnforceExtendedAnalyzerRules=true
  Rules/UN001/UN001Analyzer.cs
  Rules/UN001/UN001CodeFixProvider.cs   # only if the rule has a code fix
  RuleCategories.cs                     # fixed set of category constants
  HelpLinks.cs                          # builds helpLinkUri from a rule ID
  Resources.resx                        # titles, messages, descriptions
  AnalyzerReleases.Shipped.md
  AnalyzerReleases.Unshipped.md
tests/Altiforma.Unslopalyzer.Tests/
  Rules/UN001Tests.cs
  Rules/UN001CodeFixTests.cs
  RuleCatalogTests.cs                   # docs/README/helpLink invariants
  Verifiers/
docs/rules/UN001.md                     # one page per rule
README.md                               # rules table
```

- Organise by vertical slices. Each rule's analyzer and code fix live together under `Rules/UNnnn/`.
- Rules are independent of each other. Extract shared code only once two or more rules actually use it.
- Avoid unnecessary abstractions. Don't add base classes or helpers "just in case".
- Code fixes ship in the analyzer assembly. There is no separate CodeFixes project and no VSIX.
- Respect cancellation. Analyzers use `context.CancellationToken`, and code fixes are async and pass the `CancellationToken` through.

## Rule IDs

- The format is `UN` plus three zero-padded digits: `UN001`, `UN002`, …
- Assign IDs in sequence. Never reuse or renumber one.
- A retired rule keeps its ID and its doc page. Mark the page **Deprecated** and disable the descriptor by default.
- Every rule belongs to exactly one category in `RuleCategories.cs`, for example `Noise`, `Redundancy`, `ErrorHandling` or `Design`. Add a new category only when none of the existing ones fit.

## Definition of done for a rule

A rule is not complete until **all** of the following exist and agree with each other:

- [ ] **Analyzer:** `src/Altiforma.Unslopalyzer/Rules/UNnnn/UNnnnAnalyzer.cs`
  - One `sealed` class per rule, with `public const string DiagnosticId = "UNnnn";`.
  - The title, message format and description come from `Resources.resx` under the keys `UNnnnTitle`, `UNnnnMessageFormat` and `UNnnnDescription`, loaded with `LocalizableResourceString`.
  - Set `helpLinkUri: HelpLinks.For(DiagnosticId)` and never hard-code the URL. `HelpLinks.For` returns `https://github.com/AltiForma/Unslopalyzer/blob/main/docs/rules/<Id>.md`. While the repository is private, these links return 404 for outside users.
- [ ] **Documentation:** `docs/rules/UNnnn.md`, following the template below.
- [ ] **Tests:** at least one case that triggers the diagnostic, and at least one realistic look-alike that must *not* trigger it. If the rule has a code fix, add code-fix tests too.
- [ ] **README:** a row in the rules table.
- [ ] **Release tracking:** an entry in `AnalyzerReleases.Unshipped.md`. RS2008 enforces this.
- [ ] **Code fix (optional):** `UNnnnCodeFixProvider.cs`, with FixAll support.

`RuleCatalogTests` enforces the documentation requirement mechanically. For every exported `DiagnosticDescriptor` it checks that:

- `docs/rules/<Id>.md` exists
- the README table has a row for `<Id>`
- `HelpLinkUri` equals `HelpLinks.For(<Id>)`

When a rule's behaviour changes, update its analyzer, doc page, tests and README row in the same change.

## Rule documentation template

`docs/rules/UNnnn.md`:

````markdown
# UNnnn: <Title>

<One-sentence summary of what is flagged.>

| Property             | Value                 |
|----------------------|-----------------------|
| Rule ID              | UNnnn                 |
| Title                | <Title>               |
| Category             | <Category>            |
| Default severity     | Warning               |
| Enabled by default   | Yes                   |
| Code fix available   | Yes / No              |

## Rationale

Why agents produce this pattern, and why it is harmful (noise, hidden bugs, maintenance cost).

## Examples

### Non-compliant

```csharp
// code that triggers UNnnn
```

### Compliant

```csharp
// equivalent code that does not
```

## Code fix

What the fix does. Leave this section out if the rule has no fix.

## Known limitations

Cases the rule deliberately ignores, and any known false positives or false negatives.

## Configuration

```ini
[*.cs]
dotnet_diagnostic.UNnnn.severity = warning
```

Document any rule-specific options as `dotnet_diagnostic.UNnnn.<option> = <value>`.

## References

Links to supporting material, if any.
````

## README rules table

```markdown
| Id | Category | Description | Default severity | Enabled | Code fix |
|----|----------|-------------|------------------|---------|----------|
| [UN001](docs/rules/UN001.md) | Noise | <short description> | Warning | ✅ | ❌ |
```

Write the severity as `Error`, `Warning` or `Info`. Use ✅ and ❌ for yes/no columns.

## Writing analyzers

- In `Initialize`, call `context.EnableConcurrentExecution()` and `context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None)`.
- Prefer syntax-node and operation actions. Use the semantic model only when syntax alone can't decide.
- Keep analyzers stateless and free of side effects: no I/O and no mutable static state.
- Keep hot paths cheap. Return early, and avoid LINQ and allocations inside per-node callbacks.
- Report the narrowest meaningful location.
- **Prefer false negatives to false positives.** A noisy slop detector is itself slop.
- Code fixes must preserve trivia and formatting. Support `WellKnownFixAllProviders.BatchFixer` unless there is a documented reason not to.

## Coding Standards

- Follow DogmaSolutions.Analyzers, the .NET analyzers and the existing conventions. Never weaken analyzer rules or suppress warnings to get past errors.
- Format all C# code with CSharpier.
- Keep nullable reference types enabled.
- Prefer immutable types.
- Avoid unnecessary dependencies, reflection and `dynamic`.
- Omit braces wherever they can be omitted, for example around a single-line `if` body.
- Do not add comments to code.
- Don't change the README except for the rules table when a rule is added or changed.
- Don't write documentation unless instructed to. The exception is the per-rule `docs/rules/UNnnn.md`, which is part of the definition of done.
- Don't add speculative abstractions, unused parameters or hooks "for future extensibility".
- Don't add defensive checks for states that can't occur.
- Never change an existing rule ID or what it means. Deprecate the rule and add a new one instead.

## Testing & Verification

- Use `Microsoft.CodeAnalysis.Testing` verifiers with xUnit. Each rule gets one test class, `UNnnnTests.cs`, and code-fix tests go in `UNnnnCodeFixTests.cs`.
- Mark expected locations with markup: `{|#0:...|}` together with `.WithLocation(0)`, or `[|...|]`.
- Add focused tests for every behaviour you change. Test the diagnostics and fixes users can observe, not implementation details.
- Write test inputs as realistic agent-generated code, not only minimal toy snippets.
- Never delete or weaken tests to hide failures.
- Before finishing, run `dotnet build`, the relevant tests, and the CSharpier check.

```bash
dotnet build
dotnet test
dotnet csharpier check .
dotnet csharpier format .
dotnet pack -c Release
```

The NuGet package is a development dependency. The analyzer goes in `analyzers/dotnet/cs`, with no `lib/` output and no transitive dependencies. Pin package versions and never use wildcards.
