using DotNet.Globbing;

namespace CodeDigest.Services;

public class GitIgnoreMatcher
{
    private readonly List<Glob> _ignoreGlobs = new();
    private readonly List<Glob> _unignoreGlobs = new();

    public GitIgnoreMatcher(IEnumerable<string> rawPatterns, string rootPath)
    {
        // Normalize the root path to use forward slashes for consistent matching
        var normalizedRoot = rootPath.Replace('\\', '/').TrimEnd('/');

        foreach (var rawPattern in rawPatterns)
        {
            var pattern = rawPattern.Trim();
            if (string.IsNullOrEmpty(pattern) || pattern.StartsWith('#'))
            {
                continue; // Skip comments and empty lines
            }

            bool isNegation = pattern.StartsWith('!');
            if (isNegation)
            {
                pattern = pattern.Substring(1); // Remove the '!'
            }

            // If a pattern starts with '/', it's anchored to the root directory
            if (pattern.StartsWith('/'))
            {
                pattern = normalizedRoot + pattern;
            }
            // If a pattern does not contain a separator, it should match anywhere
            else if (!pattern.Contains('/'))
            {
                pattern = "**/" + pattern;
            }

            var glob = Glob.Parse(pattern, new GlobOptions { Evaluation = { CaseInsensitive = true } });

            if (isNegation)
            {
                _unignoreGlobs.Add(glob);
            }
            else
            {
                _ignoreGlobs.Add(glob);
            }
        }
    }

    /// <summary>
    /// Checks if a given file path should be ignored.
    /// A file is ignored if it matches an ignore pattern, unless it is then un-ignored by a subsequent negation pattern.
    /// </summary>
    /// <param name="absolutePath">The full path of the file or directory to check.</param>
    /// <returns>True if the path should be ignored, false otherwise.</returns>
    public bool IsMatch(string absolutePath)
    {
        var normalizedPath = absolutePath.Replace('\\', '/');
        Console.WriteLine($"Checking path: {normalizedPath}");

        bool isIgnored = _ignoreGlobs.Any(g => {
            var matches = g.IsMatch(normalizedPath);
            if (matches) Console.WriteLine($"  Matched by pattern: {g}");
            return matches;
        });

        return isIgnored && !_unignoreGlobs.Any(g => g.IsMatch(normalizedPath));
    }
}