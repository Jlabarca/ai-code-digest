using DotNet.Globbing;

namespace CodeDigest.Services;

public class GitIgnoreMatcher
{
    // A private record to hold the parsed glob and its properties.
    private record GitIgnoreGlob(Glob Glob, bool IsNegation, bool IsDirectoryOnly);

    private readonly List<GitIgnoreGlob> _globs = new();

    public GitIgnoreMatcher(IEnumerable<string> rawPatterns, string rootPath)
    {
        // Normalize the root path for consistent matching.
        var normalizedRoot = rootPath.Replace('\\', '/').TrimEnd('/');

        foreach (var rawPattern in rawPatterns)
        {
            var pattern = rawPattern.Trim();
            if (string.IsNullOrEmpty(pattern) || pattern.StartsWith('#'))
            {
                continue; // Skip comments and empty lines.
            }

            bool isNegation = pattern.StartsWith('!');
            if (isNegation)
            {
                pattern = pattern.Substring(1); // Remove the '!'.
            }

            // A pattern ending with a slash is a directory-only pattern.
            bool isDirectoryOnly = pattern.EndsWith('/');
            if (isDirectoryOnly)
            {
                pattern = pattern.TrimEnd('/');
            }

            // Handle patterns anchored to the root or those that should match anywhere.
            if (pattern.StartsWith('/'))
            {
                pattern = normalizedRoot + pattern;
            }
            else if (!pattern.Contains('/'))
            {
                pattern = "**/" + pattern;
            }

            var glob = Glob.Parse(pattern, new GlobOptions { Evaluation = { CaseInsensitive = true } });
            _globs.Add(new GitIgnoreGlob(glob, isNegation, isDirectoryOnly));
        }
    }

    /// <summary>
    /// Checks if a given path should be ignored.
    /// The last matching pattern in the list determines the outcome.
    /// </summary>
    /// <param name="absolutePath">The full path of the file or directory to check.</param>
    /// <param name="isDirectory">Whether the path refers to a directory.</param>
    /// <returns>True if the path should be ignored, false otherwise.</returns>
    public bool IsMatch(string absolutePath, bool isDirectory)
    {
        var normalizedPath = absolutePath.Replace('\\', '/');

        if (isDirectory)
        {
            normalizedPath = normalizedPath.TrimEnd('/');
        }

        // Default to not ignored. The last matching pattern wins.
        bool ignored = false;

        foreach (var g in _globs)
        {
            // A directory-only pattern cannot match a file.
            if (g.IsDirectoryOnly && !isDirectory)
            {
                continue;
            }

            if (g.Glob.IsMatch(normalizedPath))
            {
                // Update the status based on the last match.
                // A negation pattern means it's NOT ignored.
                ignored = !g.IsNegation;
            }
        }

        return ignored;
    }
}