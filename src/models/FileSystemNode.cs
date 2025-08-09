namespace CodeDigest.Models;

public abstract record FileSystemNode(string Name, string Path, long Size, bool IsIgnored);

public record FileNode(
    string Name,
    string Path,
    long Size,
    int TokenCount,
    string Content,
    bool IsTextFile,
    bool IsIgnored
) : FileSystemNode(Name, Path, Size, IsIgnored);

public record DirectoryNode(
    string Name,
    string Path,
    long Size,
    int TotalTokenCount,
    int FileCount,
    int DirCount,
    List<FileSystemNode> Children,
    bool IsIgnored
) : FileSystemNode(Name, Path, Size, IsIgnored);
