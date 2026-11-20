# Altiforma.Unslopalyzer

Roslyn analyzers that catch the code smells AI coding agents leave behind in C#.

Agents write code that compiles and passes tests but is harder to read than it needs to be: sprawling conditions, defensive calls that do nothing, and error handling dropped into the middle of methods. Unslopalyzer flags these patterns as you type and when you build, so they get cleaned up before they reach review.

## Install

```bash
dotnet add package Altiforma.Unslopalyzer
```

Or add it to your project file:

```xml
<PackageReference Include="Altiforma.Unslopalyzer" Version="0.2.0" PrivateAssets="all" />
```

## Rules

| Id | Category | Description | Default severity | Enabled | Code fix |
|----|----------|-------------|------------------|---------|----------|
| [UN001](https://github.com/AltiForma/Unslopalyzer/blob/main/docs/rules/UN001.md) | Readability | Extract complex condition into a named variable | Warning | ✅ | ❌ |
| [UN002](https://github.com/AltiForma/Unslopalyzer/blob/main/docs/rules/UN002.md) | Redundancy | Avoid needless `ThrowIfCancellationRequested` calls | Warning | ✅ | ❌ |
| [UN003](https://github.com/AltiForma/Unslopalyzer/blob/main/docs/rules/UN003.md) | Design | Try statement should wrap an entire method body | Warning | ✅ | ❌ |
| [UN004](https://github.com/AltiForma/Unslopalyzer/blob/main/docs/rules/UN004.md) | Readability | Extract multi-line condition into a named variable | Warning | ✅ | ❌ |

## Strict configuration

Add this to your `.editorconfig` for strictest compliance:

```ini
[*.cs]
# UN001: Extract complex condition into a named variable
dotnet_diagnostic.UN001.severity = error
dotnet_diagnostic.UN001.max_operands = 2

# UN002: Avoid needless ThrowIfCancellationRequested calls
dotnet_diagnostic.UN002.severity = error

# UN003: Try statement should wrap an entire method body
dotnet_diagnostic.UN003.severity = error

# UN004: Extract multi-line condition into a named variable
dotnet_diagnostic.UN004.severity = error
```

## License

[MIT](https://github.com/AltiForma/Unslopalyzer/blob/main/LICENSE)
