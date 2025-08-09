using CodeDigest.Models;
using DotNet.Globbing;
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
        return AnalyzeDirectoryAsync(rootDirectoryInfo, rootDirectoryInfo.FullName, ignoreMatcher, settings, false);
    }

    private async Task<DirectoryNode> AnalyzeDirectoryAsync(DirectoryInfo dirInfo, string rootPath, GitIgnoreMatcher ignoreMatcher, AnalyzeSettings settings, bool isIgnored)
    {
        var children = new List<FileSystemNode>();
        long totalSize = 0;
        int totalTokens = 0;
        int fileCount = 0;
        int dirCount = 0;

        foreach (var fileSystemInfo in dirInfo.EnumerateFileSystemInfos("*", SearchOption.TopDirectoryOnly))
        {
            var isNodeIgnored = isIgnored || ignoreMatcher.IsMatch(fileSystemInfo.FullName);

            if (fileSystemInfo is FileInfo fileInfo)
            {
                var isText = IsTextFile(fileInfo.FullName);
                var content = isText ? await File.ReadAllTextAsync(fileInfo.FullName) : "[Non-text file]";

                if (isText && !settings.NoMinify)
                {
                    content = Minify(content);
                }

                var tokens = isText ? _cl100kBase.CountTokens(content) : 0;

                children.Add(new FileNode(fileInfo.Name, fileInfo.FullName, fileInfo.Length, tokens, content, isText, isNodeIgnored));
                if (isNodeIgnored) continue;
                totalSize += fileInfo.Length;
                totalTokens += tokens;
                fileCount++;
            }
            else if (fileSystemInfo is DirectoryInfo subDirInfo)
            {
                var subDirNode = await AnalyzeDirectoryAsync(subDirInfo, rootPath, ignoreMatcher, settings, isNodeIgnored);
                children.Add(subDirNode);
                if (isNodeIgnored) continue;
                totalSize += subDirNode.Size;
                totalTokens += subDirNode.TotalTokenCount;
                fileCount += subDirNode.FileCount;
                dirCount += 1 + subDirNode.DirCount;
            }
        }

        return new DirectoryNode(dirInfo.Name, dirInfo.FullName, totalSize, totalTokens, fileCount, dirCount, children, isIgnored);
    }

    private bool IsTextFile(string filePath)
    {
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            var buffer = new byte[1024];
            var bytesRead = stream.Read(buffer, 0, buffer.Length);
            for (int i = 0; i < bytesRead; i++)
            {
                if (buffer[i] == 0) return false;
            }
        }
        catch (IOException) { return false; }
        return true;
    }

    private string Minify(string content)
    {
        var lines = content.Split('\n')
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrEmpty(line));
        return string.Join("\n", lines);
    }
}
