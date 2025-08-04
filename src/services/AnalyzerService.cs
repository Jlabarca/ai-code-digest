// src/services/AnalyzerService.cs
using System.Text;
using CodeConsolidator.Models;
using DotNet.Globbing;
using Tiktoken;
using Encoding = Tiktoken.Encoding;

namespace CodeConsolidator.Services;

public class AnalyzerService
{
    private readonly Encoding _cl100kBase;

    public AnalyzerService()
    {
        _cl100kBase = Tiktoken.Encoding.Get("cl100k_base");
    }

    public async Task<DirectoryNode> AnalyzeAsync(string path, Glob ignoreGlob)
    {
        var rootDirectoryInfo = new DirectoryInfo(path);
        return await AnalyzeDirectoryAsync(rootDirectoryInfo, rootDirectoryInfo.FullName, ignoreGlob);
    }

    private async Task<DirectoryNode> AnalyzeDirectoryAsync(DirectoryInfo dirInfo, string rootPath, Glob ignoreGlob)
    {
        var children = new List<FileSystemNode>();
        long totalSize = 0;
        int totalTokens = 0;
        int fileCount = 0;
        int dirCount = 0;

        foreach (var fileSystemInfo in dirInfo.EnumerateFileSystemInfos("*", SearchOption.TopDirectoryOnly))
        {
            if (ignoreGlob.IsMatch(fileSystemInfo.FullName)) continue;

            if (fileSystemInfo is FileInfo fileInfo)
            {
                var isText = IsTextFile(fileInfo.FullName);
                var content = isText ? await File.ReadAllTextAsync(fileInfo.FullName) : "[Non-text file]";
                var tokens = isText ? _cl100kBase.CountTokens(content) : 0;
                
                children.Add(new FileNode(fileInfo.Name, fileInfo.FullName, fileInfo.Length, tokens, content, isText));
                totalSize += fileInfo.Length;
                totalTokens += tokens;
                fileCount++;
            }
            else if (fileSystemInfo is DirectoryInfo subDirInfo)
            {
                var subDirNode = await AnalyzeDirectoryAsync(subDirInfo, rootPath, ignoreGlob);
                children.Add(subDirNode);
                totalSize += subDirNode.Size;
                totalTokens += subDirNode.TotalTokenCount;
                fileCount += subDirNode.FileCount;
                dirCount += 1 + subDirNode.DirCount;
            }
        }

        return new DirectoryNode(dirInfo.Name, dirInfo.FullName, totalSize, totalTokens, fileCount, dirCount, children);
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
}
