using CodeDigest.Models;
using System.Text.RegularExpressions;
using Encoding = Tiktoken.Encoding;

namespace CodeDigest.Services;

public class AnalyzerService
{
  private readonly Encoding _cl100kBase = Encoding.Get("cl100k_base");

  public Task<DirectoryNode> AnalyzeAsync(string path, GitIgnoreMatcher ignoreMatcher, AnalyzeSettings settings)
  {
    var rootDirectoryInfo = new DirectoryInfo(path);
    // Start the recursive analysis, passing isParentIgnored = false for the root.
    return AnalyzeDirectoryAsync(rootDirectoryInfo, ignoreMatcher, settings, false);
  }

  private async Task<DirectoryNode> AnalyzeDirectoryAsync(DirectoryInfo dirInfo, GitIgnoreMatcher ignoreMatcher, AnalyzeSettings settings, bool isParentIgnored)
  {
    var isCurrentDirIgnored = isParentIgnored || ignoreMatcher.IsMatch(dirInfo.FullName, true);

    var children = new List<FileSystemNode>();
    long includedSize = 0;
    int includedTokens = 0;
    int includedFileCount = 0;
    int includedDirCount = 0;
    int ignoredFileCount = 0;
    int ignoredDirCount = 0;

    foreach (var fileSystemInfo in dirInfo.EnumerateFileSystemInfos("*", SearchOption.TopDirectoryOnly))
    {
      if (fileSystemInfo is FileInfo fileInfo)
      {
        var isFileIgnored = isCurrentDirIgnored || ignoreMatcher.IsMatch(fileInfo.FullName, false);
        var isText = IsTextFile(fileInfo.FullName);
        string content;
        int tokens = 0;

        if (isText)
        {
          try
          {
            content = await File.ReadAllTextAsync(fileInfo.FullName);
            if (content.Contains('\0'))
            {
              isText = false; // Correct our initial assumption.
              content = "[Binary file]";
            } else
            {
              // Only minify and count tokens if it's confirmed to be text.
              if (!settings.NoMinify)
              {
                content = Minify(content);
              }
              tokens = _cl100kBase.CountTokens(content);
            }
          }
          catch (Exception)
          {
            // If reading as text fails for any reason, treat it as binary.
            isText = false;
            content = "[Unreadable file]";
          }
        } else
        {
          content = "[Binary file]";
        }

        children.Add(new FileNode(fileInfo.Name, fileInfo.FullName, fileInfo.Length, tokens, content, isText, isFileIgnored));

        if (isFileIgnored)
        {
          ignoredFileCount++;
        } else
        {
          includedSize += fileInfo.Length;
          includedTokens += tokens;
          includedFileCount++;
        }
      } else if (fileSystemInfo is DirectoryInfo subDirInfo)
      {
        // Recurse into the subdirectory. isCurrentDirIgnored is passed as the next isParentIgnored.
        var subDirNode = await AnalyzeDirectoryAsync(subDirInfo, ignoreMatcher, settings, isCurrentDirIgnored);
        children.Add(subDirNode);

        // FIX: Aggregate counts based on the actual ignored status of the subdirectory node.
        if (subDirNode.IsIgnored)
        {
          ignoredDirCount++;
          // If a directory is ignored, all its contents are considered ignored by the parent.
          ignoredFileCount += subDirNode.IncludedFileCount + subDirNode.IgnoredFileCount;
          ignoredDirCount += subDirNode.IncludedDirCount + subDirNode.IgnoredDirCount;
        } else
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

    var node = new DirectoryNode(dirInfo.Name, dirInfo.FullName, children, isCurrentDirIgnored)
    {
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

      // Check for a UTF-8 BOM (Byte Order Mark)
      if (bytesRead >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
      {
        return true;
      }

      for(var i = 0; i < bytesRead; i++)
      {
        if (buffer[i] == 0) // Null characters are a strong indicator of a binary file
        {
          return false;
        }
      }
    }
    catch (IOException)
    {
      return false;
    }
    return true;
  }

  private string Minify(string content)
  {
    // Replace any sequence of one or more whitespace characters (including newlines) with a single space,
    // then trim any leading/trailing space from the result.
    return Regex.Replace(content, @"\s+", " ").Trim();
  }
}
