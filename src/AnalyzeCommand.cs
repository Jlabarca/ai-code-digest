using CodeDigest.Models;
using CodeDigest.Services;
using DotNet.Globbing;
using Spectre.Console;
using Spectre.Console.Cli;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.ComponentModel;
using TextCopy;

[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
internal class AnalyzeCommand : AsyncCommand<AnalyzeSettings>
{
  private static readonly string[] DefaultIgnorePatterns = {
      "**/.git/**", "**/node_modules/**", "**/bin/**", "**/obj/**", "**/.vscode/**",
      "**/*.pyc", "**/*.pyd", "**/*.pyo", "**/*.egg-info/**",
      "**/__pycache__/**", "**/.DS_Store", "**/*.log"
  };

  public override async Task<int> ExecuteAsync(CommandContext context, AnalyzeSettings settings)
  {
    AnsiConsole.Write(new FigletText("CodeDigest").Centered().Color(Color.Aqua));

    var promptLibraryContent = await LoadPromptLibraryAsync(settings.PromptLibraryPath);
    var rawPatterns = await LoadIgnorePatternsAsync(settings);
    var ignoreMatcher = new GitIgnoreMatcher(rawPatterns, settings.Path);

    var analyzer = new AnalyzerService();
    DirectoryNode? analysisResult = null;
    await AnsiConsole.Status().StartAsync("Analyzing codebase...", async _ =>
    {
      analysisResult = await analyzer.AnalyzeAsync(settings.Path, ignoreMatcher, settings);
    });

    if (analysisResult is null)
    {
      AnsiConsole.MarkupLine("[red]Error: Analysis failed.[/]");
      return -1;
    }

    var report = GenerateTextReport(analysisResult, promptLibraryContent.ToString());
    var outputFileName = settings.OutputFile ?? $"{new DirectoryInfo(settings.Path).Name}_digest.txt";
    await File.WriteAllTextAsync(outputFileName, report);
    AnsiConsole.MarkupLine($"[green](Success) Analysis complete. Report saved to:[/] [blue]{Path.GetFullPath(outputFileName)}[/]");

    DisplaySummary(analysisResult);
    await ClipboardService.SetTextAsync(report);
    AnsiConsole.MarkupLine("[green](Success) Report copied to clipboard.[/]");

    return 0;
  }

  private async Task<StringBuilder> LoadPromptLibraryAsync(string? path)
  {
    var content = new StringBuilder();
    if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return content;

    content.AppendLine("--- PROMPT LIBRARY START ---");
    foreach (var file in Directory.EnumerateFiles(path))
    {
      content.AppendLine($"\n# --- From: {Path.GetFileName(file)} ---\n");
      content.AppendLine(await File.ReadAllTextAsync(file));
    }
    content.AppendLine("--- PROMPT LIBRARY END ---");
    return content;
  }

  private async Task<string[]> LoadIgnorePatternsAsync(AnalyzeSettings settings)
  {
    var ignoreFilePath = Path.Combine(settings.Path, ".aidigestignore");
    AnsiConsole.MarkupLine($"[dim]Attempting to load ignore file from: {ignoreFilePath}[/]");
    var fileIgnores = new List<string>();
    if (File.Exists(ignoreFilePath))
    {
      fileIgnores.AddRange(await File.ReadAllLinesAsync(ignoreFilePath));
      AnsiConsole.MarkupLine($"[dim]Loaded {fileIgnores.Count} patterns from .aidigestignore[/]");
    }
    else
    {
      AnsiConsole.MarkupLine("[dim].aidigestignore not found.[/]");
    }

    var allPatterns = DefaultIgnorePatterns
        .Concat(settings.IgnorePatterns)
        .Concat(fileIgnores)
        .Where(p => !string.IsNullOrWhiteSpace(p) && !p.Trim().StartsWith("#"))
        .ToList();
    
    AnsiConsole.MarkupLine($"[dim]Processing {allPatterns.Count} total patterns...[/]");

    var processedPatterns = new List<string>();
    foreach (var pattern in allPatterns)
    {
        var trimmedPattern = pattern.Trim();
        var addedPatterns = new List<string>();

        if (trimmedPattern.EndsWith('/') || trimmedPattern.EndsWith('\\'))
        {
            var newPattern = trimmedPattern + "**";
            processedPatterns.Add(newPattern);
            addedPatterns.Add(newPattern);
        }
        else if (!trimmedPattern.Contains('*') && !trimmedPattern.Contains('/') && !trimmedPattern.Contains('\\'))
        {
            var dirPattern = $"**/{trimmedPattern}/**";
            var filePattern = $"**/{trimmedPattern}";
            processedPatterns.Add(dirPattern);
            processedPatterns.Add(filePattern);
            addedPatterns.Add(dirPattern);
            addedPatterns.Add(filePattern);
        }
        else
        {
            processedPatterns.Add(trimmedPattern);
            addedPatterns.Add(trimmedPattern);
        }
        AnsiConsole.MarkupLine($"[dim]  - Raw: '{trimmedPattern}' -> Processed: '{string.Join("\', \'", addedPatterns)}'[/]");
    }

    return processedPatterns.ToArray();
  }

  private void DisplaySummary(DirectoryNode root)
  {
    AnsiConsole.WriteLine();
    var table = new Table().Title("Analysis Summary").Border(TableBorder.Rounded);
    table.AddColumn("Metric").AddColumn("Value");

    table.AddRow("Included Files", $"[yellow]{root.IncludedFileCount}[/]");
    table.AddRow("Included Dirs", $"[yellow]{root.IncludedDirCount}[/]");
    table.AddRow("Ignored Files", $"[yellow]{root.IgnoredFileCount}[/]");
    table.AddRow("Ignored Dirs", $"[yellow]{root.IgnoredDirCount}[/]");
    table.AddRow("Total Size (Included)", $"[yellow]{root.Size / 1024.0:F2} KB[/]");
    table.AddRow("Total Tokens (Included)", $"[yellow]{root.TotalTokenCount:N0}[/]");
    AnsiConsole.Write(table);

    AnsiConsole.WriteLine();
    var tree = new Tree("[aqua]Project Structure (included files)[/]");
    AddNodesToTree(tree, root);
    AnsiConsole.Write(tree);
  }

  private void AddNodesToTree(IHasTreeNodes parentNode, DirectoryNode dirNode)
  {
    // If a directory is marked as ignored, do not display it or any of its children.
    if (dirNode.IsIgnored) return;

    foreach (var child in dirNode.Children.OrderBy(c => c is DirectoryNode ? 0 : 1).ThenBy(c => c.Name))
    {
        // This check correctly skips ignored files and subdirectories within an included directory.
        if (child.IsIgnored) continue;

        var node = parentNode.AddNode(child is DirectoryNode
          ? $"[blue bold]{child.Name}[/]"
          : child.Name);

      if (child is DirectoryNode subDir) AddNodesToTree(node, subDir);
    }
  }


  private string GenerateProjectStructure(DirectoryNode root)
  {
    var sb = new StringBuilder();
    sb.AppendLine("# Project Structure (all files)");
    sb.AppendLine("```");

    void AppendNode(FileSystemNode node)
    {
        sb.Append(node.Name);
        if (node is DirectoryNode dir)
        {
            var children = dir.Children
                .OrderBy(c => c is DirectoryNode ? 0 : 1)
                .ThenBy(c => c.Name)
                .ToList();

            if (children.Count > 0)
            {
                sb.Append('(');
                for (var i = 0; i < children.Count; i++)
                {
                    AppendNode(children[i]);
                    if (i < children.Count - 1)
                    {
                        sb.Append(' ');
                    }
                }
                sb.Append(')');
            }
        }
    }

    AppendNode(root);

    sb.AppendLine();
    sb.AppendLine("```");
    sb.AppendLine();
    return sb.ToString();
  }

  private string GenerateTextReport(DirectoryNode root, string promptLibraryContent)
  {
    var sb = new StringBuilder();
    if (!string.IsNullOrEmpty(promptLibraryContent)) sb.Append(promptLibraryContent);
    
    sb.Append(GenerateProjectStructure(root));

    sb.AppendLine($"# Codebase Analysis for: {root.Name}");
    sb.AppendLine($"## Total Tokens (included files): {root.TotalTokenCount:N0}");
    sb.AppendLine("---");

    void AppendContent(DirectoryNode dir)
    {
      foreach (var child in dir.Children.OrderBy(c => c is DirectoryNode ? 0 : 1).ThenBy(c => c.Name))
      {
        if (child.IsIgnored) continue;

        if (child is FileNode file && file.IsTextFile)
        {
          var fileExtension = Path.GetExtension(file.Name).TrimStart('.');
          sb.AppendLine($"\n### File: {file.Path.Replace(root.Path, "").TrimStart(Path.DirectorySeparatorChar)}");
          sb.AppendLine($"```{fileExtension}");
          sb.AppendLine(file.Content);
          sb.AppendLine("```");
        }
        else if (child is DirectoryNode subDir)
        {
          AppendContent(subDir);
        }
      }
    }
    AppendContent(root);
    return sb.ToString();
  }
}


// ReSharper disable once ClassNeverInstantiated.Global
public class AnalyzeSettings : CommandSettings
{
  [CommandArgument(0, "<PATH>")]
  [Description("Path to the directory to analyze.")]
  public string Path { get; set; } = string.Empty;

  [CommandOption("-o|--output")]
  [Description("Output file path. Defaults to a file in the current directory.")]
  public string? OutputFile { get; set; }

  [CommandOption("--ignore")]
  [Description("Additional glob patterns to ignore, comma-separated.")]
  public string[] IgnorePatterns { get; set; } = [];

  [CommandOption("--prompt-library")]
  [Description("Path to a directory of prompt files to prepend to the report.")]
  public string? PromptLibraryPath { get; set; }

  [CommandOption("--no-minify")]
  [Description("Disable source code minification (whitespace removal).")]
  [DefaultValue(false)]
  public bool NoMinify { get; set; }
}
