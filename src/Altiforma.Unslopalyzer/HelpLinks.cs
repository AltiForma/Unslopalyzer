namespace Altiforma.Unslopalyzer;

internal static class HelpLinks
{
    private const string BaseUri =
        "https://github.com/AltiForma/Unslopalyzer/blob/main/docs/rules/";

    public static string For(string ruleId) => BaseUri + ruleId + ".md";
}
