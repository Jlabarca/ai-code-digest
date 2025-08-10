using CodeDigest.Models;
using DotNet.Globbing;
using System.Text.RegularExpressions;
using Encoding = Tiktoken.Encoding;

namespace CodeDigest.Services;

public class AnalyzerService
{
    private readonly Encoding _cl100kBase;

    public AnalyzerService()
    {
        _cl100kBase = Tiktoken.Encoding.Get("cl100k_base");
    }

    public Task<DirectoryNode> AnalyzeAsync(string path, GitIgnoreMatcher ignoreMatcher, AnalyzeSettings settings)
    {
        var rootDirectoryInfo = new DirectoryInfo(path);
        // Start the recursive analysis, passing isParentIgnored = false for the root.
        return AnalyzeDirectoryAsync(rootDirectoryInfo, ignoreMatcher, settings, false);
    }

    private async Task<DirectoryNode> AnalyzeDirectoryAsync(DirectoryInfo dirInfo, GitIgnoreMatcher ignoreMatcher, AnalyzeSettings settings, bool isParentIgnored)
    {
        // BUG FIX: A directory is ignored if its parent is, or if it matches a pattern itself.
        var isCurrentDirIgnored = isParentIgnored || ignoreMatcher.IsMatch(dirInfo.FullName);

        var children = new List<FileSystemNode>();
        long includedSize = 0;
        int includedTokens = 0;
        int includedFileCount = 0;
        int includedDirCount = 0;
        int ignoredFileCount = 0;
        int ignoredDirCount = 0;

        foreach (var fileSystemInfo in dirInfo.EnumerateFileSystemInfos("*", SearchOption.TopDirectoryOnly))
        {
            var isChildNodeIgnored = isCurrentDirIgnored || ignoreMatcher.IsMatch(fileSystemInfo.FullName);

            if (fileSystemInfo is FileInfo fileInfo)
            {
                var isText = IsTextFile(fileInfo.FullName);
                var content = isText ? await File.ReadAllTextAsync(fileInfo.FullName) : "[Non-text file]";
                if (isText && !settings.NoMinify)
                {
                    content = Minify(content);
                }
                var tokens = isText ? _cl100kBase.CountTokens(content) : 0;

                children.Add(new FileNode(fileInfo.Name, fileInfo.FullName, fileInfo.Length, tokens, content, isText, isChildNodeIgnored));

                if (isChildNodeIgnored)
                {
                    ignoredFileCount++;
                }
                else
                {
                    includedSize += fileInfo.Length;
                    includedTokens += tokens;
                    includedFileCount++;
                }
            }
            else if (fileSystemInfo is DirectoryInfo subDirInfo)
            {
                // Recurse into the subdirectory
                var subDirNode = await AnalyzeDirectoryAsync(subDirInfo, ignoreMatcher, settings, isCurrentDirIgnored);
                children.Add(subDirNode);

                if (isChildNodeIgnored)
                {
                    ignoredDirCount++;
                    // If a directory is ignored, all its contents count as ignored.
                    ignoredFileCount += subDirNode.IncludedFileCount + subDirNode.IgnoredFileCount;
                    ignoredDirCount += subDirNode.IncludedDirCount + subDirNode.IgnoredDirCount;
                }
                else
                {
                    // If a directory is included, its metrics contribute to the parent's metrics.
                    includedSize += subDirNode.Size;
                    includedTokens += subDirNode.TotalTokenCount;
                    includedFileCount += subDirNode.IncludedFileCount;
                    includedDirCount += 1 + subDirNode.IncludedDirCount; // +1 for the subdirectory itself
                    ignoredFileCount += subDirNode.IgnoredFileCount;
                    ignoredDirCount += subDirNode.IgnoredDirCount;
                }
            }
        }

        // Create the node for the current directory, now marked with its own correct ignored status.
        var node = new DirectoryNode(dirInfo.Name, dirInfo.FullName, children, isCurrentDirIgnored)
        {
            // Populate the node with the calculated metrics.
            Size = includedSize,
            TotalTokenCount = includedTokens,
            IncludedFileCount = includedFileCount,
            IncludedDirCount = includedDirCount,
            IgnoredFileCount = ignoredFileCount,
            IgnoredDirCount = ignoredDirCount
        };

        return node;
    }

    private bool IsTextFile(string filePath)
    {
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            var buffer = new byte[1024];
            var bytesRead = stream.Read(buffer, 0, buffer.Length);
            for (var i = 0; i < bytesRead; i++)
            {
                if (buffer[i] == 0) return false;
            }
        }
        catch (IOException) { return false; }
        return true;
    }

    private string Minify(string content)
    {
        // Replace any sequence of one or more whitespace characters (including newlines) with a single space,
        // then trim any leading/trailing space from the result.
        return Regex.Replace(content, @"\s+", " ").Trim();
    }
}