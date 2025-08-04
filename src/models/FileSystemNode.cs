// src/models/FileSystemNode.cs
namespace CodeConsolidator.Models;

public abstract record FileSystemNode(string Name, string Path, long Size);

public record FileNode(
    string Name,
    string Path,
    long Size,
    int TokenCount,
    string Content,
    bool IsTextFile
) : FileSystemNode(Name, Path, Size);

public record DirectoryNode(
    string Name,
    string Path,
    long Size,
    int TotalTokenCount,
    int FileCount,
    int DirCount,
    List<FileSystemNode> Children
) : FileSystemNode(Name, Path, 0);
