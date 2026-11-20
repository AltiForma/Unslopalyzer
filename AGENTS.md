# AGENTS.md — Altiforma.Unslopalyzer

Guidance for coding agents (and humans) working in this repository.

## Purpose

Altiforma.Unslopalyzer is a set of Roslyn analyzers, plus code fixes where a fix is safe, for C# projects. It catches the code smells that AI coding agents tend to produce ("slop"). Typical targets:

- comments that restate the code
- defensive null checks on values that cannot be null
- `catch (Exception) { throw; }` and other no-op error handling
- speculative abstractions and pass-through wrappers
- leftover placeholders (`// TODO: implement`, `NotImplementedException` stubs)

These are examples, not a commitment. Rules are defined one at a time in `docs/rules/`.

The project's conventions come from [DogmaSolutions/Analyzers](https://github.com/DogmaSolutions/Analyzers). Most important among them: **every rule has its own documentation page**, and the diagnostic links to it.

Repository: <https://github.com/AltiForma/Unslopalyzer> (private for now).

## Repository layout

This is the target structure. Scaffold new pieces to match it.

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

Code fixes ship in the analyzer assembly. There is no separate CodeFixes project and no VSIX.

## Rule IDs

- The format is `UN` followed by three zero-padded digits: `UN001`, `UN002`, …
- IDs are assigned in order. Never reuse or renumber one.
- A retired rule keeps its ID and its doc page. The page is marked **Deprecated** and the descriptor is disabled by default.
- Every rule belongs to exactly one category from `RuleCategories.cs`, for example `Noise`, `Redundancy`, `ErrorHandling` or `Design`. Add a new category only when no existing one fits.

## Definition of done for a rule

A rule is not complete until **all** of these exist and agree with one another:

- [ ] **Analyzer**: `src/Altiforma.Unslopalyzer/Rules/UNnnn/UNnnnAnalyzer.cs`
  - One `sealed` class per rule, with `public const string DiagnosticId = "UNnnn";`.
  - Title, message format and description come from `Resources.resx` under the keys `UNnnnTitle`, `UNnnnMessageFormat` and `UNnnnDescription`, loaded through `LocalizableResourceString`.
  - `helpLinkUri: HelpLinks.For(DiagnosticId)`. Never hard-code the URL. `HelpLinks.For` returns `https://github.com/AltiForma/Unslopalyzer/blob/main/docs/rules/<Id>.md`. The repository is private for now, so these links return 404 for outside users until it is made public.
- [ ] **Documentation**: `docs/rules/UNnnn.md`, following the template below.
- [ ] **Tests**: at least one case that triggers the diagnostic and at least one realistic look-alike that must *not* trigger it. If the rule has a code fix, add code-fix tests too.
- [ ] **README**: a row in the rules table.
- [ ] **Release tracking**: an entry in `AnalyzerReleases.Unshipped.md`. The RS2008 analyzer enforces this.
- [ ] **Code fix** (optional): `UNnnnCodeFixProvider.cs`, supporting FixAll.

`RuleCatalogTests` enforces the documentation requirement mechanically. For every exported `DiagnosticDescriptor` it checks that:

- `docs/rules/<Id>.md` exists
- the README table has a row for `<Id>`
- `HelpLinkUri` equals `HelpLinks.For(<Id>)`

When a rule changes behaviour, update its analyzer, doc, tests and README row in the same change.

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

What the fix does. Omit this section if there is no fix.

## Known limitations

Cases the rule deliberately ignores, and known false positives or negatives.

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

Severity is written out as `Error`, `Warning` or `Info`. Booleans use ✅ / ❌.

## Writing analyzers

- In `Initialize`, call `context.EnableConcurrentExecution()` and `context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None)`.
- Prefer syntax-node and operation actions. Use the semantic model only when syntax alone cannot decide.
- Analyzers must be stateless and side-effect free: no I/O, no static mutable state.
- Keep hot paths cheap. Bail out early and avoid LINQ and allocations in per-node callbacks.
- Report on the narrowest meaningful location.
- **Prefer false negatives to false positives.** A noisy slop detector is slop itself.
- Code fixes must preserve trivia and formatting, and must support `WellKnownFixAllProviders.BatchFixer` unless there is a documented reason not to.

## Testing

- Use `Microsoft.CodeAnalysis.Testing` (C# analyzer/code-fix verifiers) with xUnit.
- Each rule gets one test class, `UNnnnTests.cs`. Code-fix tests go in `UNnnnCodeFixTests.cs`.
- Mark expected locations with markup: `{|#0:...|}` together with `.WithLocation(0)`, or `[|...|]`.
- Write test inputs as realistic agent-generated code, not minimal toy snippets only.

## Commands

```bash
dotnet build
dotnet test
dotnet pack -c Release
```

The NuGet package is a development dependency. The analyzer goes in `analyzers/dotnet/cs`, with no `lib/` output and no transitive dependencies. Package versions are pinned, never wildcards.

## Ground rules for agents in this repo

This project exists to remove agent slop, so don't add any:

- No comments that narrate what the code does. Comment only the *why*, and only when it is non-obvious.
- No speculative abstractions, unused parameters, or "for future extensibility" hooks.
- No files, docs or helpers that the task did not ask for.
- No defensive checks for states that cannot occur.
- Never change an existing rule ID or its meaning. Deprecate and add a new rule instead.
- A change that adds or modifies a rule must cover the full definition of done above in the same change.
