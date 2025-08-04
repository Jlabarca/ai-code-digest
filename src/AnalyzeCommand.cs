
using CodeConsolidator.Models;
using CodeConsolidator.Services;
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
    AnsiConsole.Write(new FigletText("CodeConsolidator").Centered().Color(Color.Aqua));

    // 1. Load prompt library content
    var promptLibraryContent = await LoadPromptLibraryAsync(settings.PromptLibraryPath);

    // 2. Combine all ignore patterns
    var allIgnores = await LoadIgnorePatternsAsync(settings);
    var ignoreGlob = Glob.Parse(string.Join(",", allIgnores), new GlobOptions { Evaluation = { CaseInsensitive = true } });

    // 3. Analyze the directory
    var analyzer = new AnalyzerService();
    DirectoryNode? analysisResult = null;
    await AnsiConsole.Status().StartAsync("Analyzing codebase...", async _ =>
    {
      analysisResult = await analyzer.AnalyzeAsync(settings.Path, ignoreGlob);
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
    AnsiConsole.MarkupLine($"[green]✔ Analysis complete. Report saved to:[/] [blue]{Path.GetFullPath(outputFileName)}[/]");

    // 5. Display summary and handle clipboard
    DisplaySummary(analysisResult);
    if (AnsiConsole.Confirm("Copy full report to clipboard?"))
    {
      await ClipboardService.SetTextAsync(report);
      AnsiConsole.MarkupLine("[green]✔ Report copied to clipboard.[/]");
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
    return DefaultIgnorePatterns
        .Concat(settings.IgnorePatterns)
        .Concat(fileIgnores)
        .Where(p => !string.IsNullOrWhiteSpace(p) && !p.Trim().StartsWith("#"))
        .ToArray();
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
      var node = parentNode.AddNode(child is DirectoryNode
          ? $"[blue]:file_folder: {child.Name}[/]"
          : $"[grey]:page_facing_up: {child.Name}[/]");

      if (child is DirectoryNode subDir) AddNodesToTree(node, subDir);
    }
  }

  private string GenerateTextReport(DirectoryNode root, string promptLibraryContent)
  {
    var sb = new StringBuilder();
    if (!string.IsNullOrEmpty(promptLibraryContent)) sb.Append(promptLibraryContent);

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
internal class AnalyzeSettings : CommandSettings
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
}
