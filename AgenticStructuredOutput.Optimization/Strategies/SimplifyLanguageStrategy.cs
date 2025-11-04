using AgenticStructuredOutput.Optimization.Core;
using AgenticStructuredOutput.Optimization.Models;
using System.Text.RegularExpressions;

namespace AgenticStructuredOutput.Optimization.Strategies;

/// <summary>
/// Simplifies prompt language to reduce verbosity and improve focus.
/// </summary>
public partial class SimplifyLanguageStrategy : IPromptMutationStrategy
{
    public string Name => "SimplifyLanguage";
    public string Description => "Reduce prompt verbosity while preserving essential instructions";

    private readonly double _targetReduction;

    public SimplifyLanguageStrategy(double targetReduction = 0.20)
    {
        _targetReduction = targetReduction;
    }

    public Task<string> MutateAsync(string basePrompt, MutationContext context)
    {
        var simplified = SimplifyText(basePrompt);
        return Task.FromResult(simplified);
    }

    private string SimplifyText(string text)
    {
        // Step 1: Remove redundant phrases
        text = RemoveRedundantPhrases(text);

        // Step 2: Simplify complex sentences
        text = SimplifyComplexSentences(text);

        // Step 3: Remove excessive whitespace
        text = NormalizeWhitespace(text);

        // Step 4: Consolidate bullet points
        text = ConsolidateBulletPoints(text);

        return text;
    }

    private static string RemoveRedundantPhrases(string text)
    {
        var redundantPatterns = new Dictionary<string, string>
        {
            // Remove redundant qualifiers
            { "very important", "important" },
            { "extremely critical", "critical" },
            { "highly recommended", "recommended" },
            
            // Simplify wordy phrases
            { "in order to", "to" },
            { "due to the fact that", "because" },
            { "at this point in time", "now" },
            { "for the purpose of", "for" },
            { "with the exception of", "except" },
            
            // Remove filler words at start of sentences
            { @"^\s*Basically,?\s*", "" },
            { @"^\s*Essentially,?\s*", "" },
            { @"^\s*Generally,?\s*", "" }
        };

        foreach (var (pattern, replacement) in redundantPatterns)
        {
            text = Regex.Replace(text, pattern, replacement, RegexOptions.IgnoreCase | RegexOptions.Multiline);
        }

        return text;
    }

    private static string SimplifyComplexSentences(string text)
    {
        // Replace passive voice with active where possible
        text = Regex.Replace(text, @"should be (\w+ed)", "must $1", RegexOptions.IgnoreCase);
        
        // Simplify "You are an expert" type preambles
        text = Regex.Replace(
            text,
            @"You are an expert in .+?\.",
            "",
            RegexOptions.IgnoreCase | RegexOptions.Multiline);

        // Simplify "Your task is to" constructions
        text = Regex.Replace(
            text,
            @"Your task is to (.+?)\.",
            "$1.",
            RegexOptions.IgnoreCase);

        return text;
    }

    private static string NormalizeWhitespace(string text)
    {
        // Remove multiple blank lines
        text = Regex.Replace(text, @"\n\s*\n\s*\n", "\n\n");
        
        // Remove trailing whitespace
        text = Regex.Replace(text, @"[ \t]+$", "", RegexOptions.Multiline);
        
        // Normalize line endings
        text = text.Replace("\r\n", "\n");

        return text.Trim();
    }

    private static string ConsolidateBulletPoints(string text)
    {
        // If there are too many bullet points, keep only the most important ones
        var lines = text.Split('\n');
        var bulletPattern = BulletPointRegex();
        var bulletLines = lines.Where(l => bulletPattern.IsMatch(l)).ToList();

        // If more than 7 bullet points, it's getting too long
        if (bulletLines.Count > 7)
        {
            // Keep the first 5 and add a summary line
            var keptBullets = bulletLines.Take(5).ToHashSet();
            var newLines = new List<string>();

            foreach (var line in lines)
            {
                if (bulletPattern.IsMatch(line))
                {
                    if (keptBullets.Contains(line))
                    {
                        newLines.Add(line);
                    }
                }
                else
                {
                    newLines.Add(line);
                }
            }

            text = string.Join("\n", newLines);
        }

        return text;
    }

    [GeneratedRegex(@"^\s*[-*•]\s+")]
    private static partial Regex BulletPointRegex();
}
