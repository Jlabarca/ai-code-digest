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

    // 1. Load prompt library content
    var promptLibraryContent = await LoadPromptLibraryAsync(settings.PromptLibraryPath);

    // 2. Load patterns and create the gitignore-style matcher
    var rawPatterns = await LoadIgnorePatternsAsync(settings);
    var ignoreMatcher = new GitIgnoreMatcher(rawPatterns, settings.Path);

    // 3. Analyze the directory
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

    // 4. Generate and save the report
    var report = GenerateTextReport(analysisResult, promptLibraryContent.ToString());
    var outputFileName = settings.OutputFile ?? $"{new DirectoryInfo(settings.Path).Name}_digest.txt";
    await File.WriteAllTextAsync(outputFileName, report);
        AnsiConsole.MarkupLine($"[green](Success) Analysis complete. Report saved to:[/] [blue]{Path.GetFullPath(outputFileName)}[/]");

    // 5. Display summary and handle clipboard
    DisplaySummary(analysisResult);
    if (AnsiConsole.Confirm("Copy full report to clipboard?"))
    {
      await ClipboardService.SetTextAsync(report);
      AnsiConsole.MarkupLine("[green](Success) Report copied to clipboard.[/]");
    }
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
    content.AppendLine("--- PROMPT LIBRARY END ---\n");
    return content;
  }

  private async Task<string[]> LoadIgnorePatternsAsync(AnalyzeSettings settings)
  {
    var ignoreFilePath = Path.Combine(settings.Path, ".aidigestignore");
    var fileIgnores = new List<string>();
    if (File.Exists(ignoreFilePath))
    {
      fileIgnores.AddRange(await File.ReadAllLinesAsync(ignoreFilePath));
    }

    var allPatterns = DefaultIgnorePatterns
        .Concat(settings.IgnorePatterns)
        .Concat(fileIgnores)
        .Where(p => !string.IsNullOrWhiteSpace(p) && !p.Trim().StartsWith("#"))
        .ToList();

    var processedPatterns = new List<string>();
    foreach (var pattern in allPatterns)
    {
      if (!pattern.Contains('*') && !pattern.Contains('/') && !pattern.Contains('\\'))
      {
        processedPatterns.Add($"**/{pattern}/**");
        processedPatterns.Add($"**/{pattern}");
      }
      else
      {
        processedPatterns.Add(pattern);
      }
    }

    return processedPatterns.ToArray();
  }

  private void DisplaySummary(DirectoryNode root)
  {
    AnsiConsole.WriteLine();
    var table = new Table().Title("Analysis Summary").Border(TableBorder.Rounded);
    table.AddColumn("Metric").AddColumn("Value");
    table.AddRow("Total Files", $"[yellow]{root.FileCount}[/]");
    table.AddRow("Total Directories", $"[yellow]{root.DirCount}[/]");
    table.AddRow("Total Size", $"[yellow]{root.Size / 1024.0:F2} KB[/]");
    table.AddRow("Total Tokens", $"[yellow]{root.TotalTokenCount:N0}[/]");
    AnsiConsole.Write(table);

    AnsiConsole.WriteLine();
    var tree = new Tree("[aqua]Project Structure[/]");
    AddNodesToTree(tree, root);
    AnsiConsole.Write(tree);
  }

  private void AddNodesToTree(IHasTreeNodes parentNode, DirectoryNode dirNode)
  {
    foreach (var child in dirNode.Children.OrderBy(c => c is DirectoryNode ? 0 : 1).ThenBy(c => c.Name))
    {
        var color = child.IsIgnored ? "grey" : "white";
        var node = parentNode.AddNode(child is DirectoryNode
          ? $"[blue bold]{child.Name}[/]"
          : $"[{color}]{child.Name}[/]");
      if (child is DirectoryNode subDir) AddNodesToTree(node, subDir);
    }
  }

  private string GenerateProjectStructure(DirectoryNode root)
  {
    var sb = new StringBuilder();
    sb.AppendLine("# Project Structure");
    sb.AppendLine("```");
    sb.AppendLine(root.Name);

    void AppendNodes(IReadOnlyList<FileSystemNode> nodes, string prefix)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            var isLast = i == nodes.Count - 1;
            var connector = isLast ? "└── " : "├── ";
            sb.AppendLine($"{prefix}{connector}{node.Name}");

            if (node is DirectoryNode dir)
            {
                var newPrefix = prefix + (isLast ? "    " : "│   ");
                var children = dir.Children.OrderBy(c => c is DirectoryNode ? 0 : 1).ThenBy(c => c.Name).ToList();
                AppendNodes(children, newPrefix);
            }
        }
    }

    var children = root.Children.OrderBy(c => c is DirectoryNode ? 0 : 1).ThenBy(c => c.Name).ToList();
    AppendNodes(children, "");

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
    sb.AppendLine($"## Total Tokens: {root.TotalTokenCount:N0}");
    sb.AppendLine("---");

    void AppendContent(DirectoryNode dir, string indent)
    {
      foreach (var child in dir.Children.OrderBy(c => c is DirectoryNode ? 0 : 1).ThenBy(c => c.Name))
      {
        if (child is FileNode file && file.IsTextFile)
        {
          sb.AppendLine($"\n### File: {file.Path.Replace(root.Path, "").TrimStart(Path.DirectorySeparatorChar)}");
          sb.AppendLine("`");
          sb.AppendLine(file.Content);
          sb.AppendLine("`");
        }
        else if (child is DirectoryNode subDir)
        {
          AppendContent(subDir, indent + "  ");
        }
      }
    }
    AppendContent(root, "");
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
