# Altiforma.Unslopalyzer

Roslyn analyzers that flag code smells commonly produced by AI coding agents in C# projects.

## Rules

| Id | Category | Description | Default severity | Enabled | Code fix |
|----|----------|-------------|------------------|---------|----------|
| [UN001](docs/rules/UN001.md) | Readability | Extract complex condition into a named variable | Warning | ✅ | ❌ |
| [UN002](docs/rules/UN002.md) | Redundancy | Avoid needless `ThrowIfCancellationRequested` calls | Warning | ✅ | ❌ |
