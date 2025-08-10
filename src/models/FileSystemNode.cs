namespace CodeDigest.Models;

public abstract record FileSystemNode(string Name, string Path, bool IsIgnored)
{
    public long Size { get; set; }
}

public record FileNode(
    string Name,
    string Path,
    long InitialSize,
    int TokenCount,
    string Content,
    bool IsTextFile,
    bool IsIgnored
) : FileSystemNode(Name, Path, IsIgnored)
{
    public new long Size { get => InitialSize; set => base.Size = value; }
}

public record DirectoryNode(
    string Name,
    string Path,
    List<FileSystemNode> Children,
    bool IsIgnored
) : FileSystemNode(Name, Path, IsIgnored)
{
    public int TotalTokenCount { get; set; }
    public int IncludedFileCount { get; set; }
    public int IncludedDirCount { get; set; }
    public int IgnoredFileCount { get; set; }
    public int IgnoredDirCount { get; set; }
}
