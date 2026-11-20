using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Altiforma.Unslopalyzer.Tests;

public sealed class RuleCatalogTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    public static TheoryData<string> RuleIds()
    {
        var data = new TheoryData<string>();
        foreach (var descriptor in AllDescriptors())
            data.Add(descriptor.Id);
        return data;
    }

    [Fact]
    public void CatalogIsNotEmpty() => Assert.NotEmpty(AllDescriptors());

    [Theory]
    [MemberData(nameof(RuleIds))]
    public void RuleHasDocumentationPage(string ruleId) =>
        Assert.True(
            File.Exists(Path.Combine(RepositoryRoot, "docs", "rules", ruleId + ".md")),
            $"docs/rules/{ruleId}.md is missing"
        );

    [Theory]
    [MemberData(nameof(RuleIds))]
    public void RuleIsListedInReadme(string ruleId)
    {
        var readme = File.ReadAllText(Path.Combine(RepositoryRoot, "README.md"));
        Assert.Contains(
            $"| [{ruleId}]({HelpLinks.For(ruleId)}) |",
            readme,
            StringComparison.Ordinal
        );
    }

    [Theory]
    [MemberData(nameof(RuleIds))]
    public void RuleHelpLinkPointsToDocumentation(string ruleId)
    {
        var descriptor = AllDescriptors().Single(d => d.Id == ruleId);
        Assert.Equal(HelpLinks.For(ruleId), descriptor.HelpLinkUri);
    }

    private static List<DiagnosticDescriptor> AllDescriptors() =>
        typeof(HelpLinks)
            .Assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(DiagnosticAnalyzer).IsAssignableFrom(t))
            .Select(t => (DiagnosticAnalyzer)Activator.CreateInstance(t)!)
            .SelectMany(a => a.SupportedDiagnostics)
            .DistinctBy(d => d.Id)
            .ToList();

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && directory.GetFiles("*.slnx").Length == 0)
            directory = directory.Parent;

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate the repository root.");
    }
}
