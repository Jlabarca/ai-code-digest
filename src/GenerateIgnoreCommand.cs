using CodeDigest.Models;
using CodeDigest.Services;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class GenerateIgnoreCommand : AsyncCommand<GenerateIgnoreSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, GenerateIgnoreSettings settings)
    {
        AnsiConsole.Write(new FigletText("CodeDigest").Centered().Color(Color.Aqua));
        AnsiConsole.MarkupLine("Generating .aidigestignore file...");

        // 1. Get the directory structure
        var analyzer = new AnalyzerService();
        var root = await analyzer.AnalyzeAsync(settings.Path, new GitIgnoreMatcher(new string[0], settings.Path), new AnalyzeSettings { Path = settings.Path, NoMinify = true });
        var structure = GetDirectoryStructure(root);

        // 2. Load the prompt
        var promptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "prompt_library", "generate_aidigestignore.md");
        var prompt = await File.ReadAllTextAsync(promptPath);

        // 3. (Simulated) Call Gemini API
        var ignoreContent = await Task.FromResult("bin/\nobj/\n.idea/\n.vscode/\n*.log");

        // 4. Write the .aidigestignore file
        var ignoreFilePath = Path.Combine(settings.Path, ".aidigestignore");
        await File.WriteAllTextAsync(ignoreFilePath, ignoreContent);

        AnsiConsole.MarkupLine("[green](Success) .aidigestignore file generated at:[/] [blue]{ignoreFilePath}[/]");

        if (settings.RunAnalysis)
        {
            AnsiConsole.MarkupLine("Running analysis...");
            var analyzeCommand = new AnalyzeCommand();
            var analyzeSettings = new AnalyzeSettings { Path = settings.Path };
            return await analyzeCommand.ExecuteAsync(context, analyzeSettings);
        }

        return 0;
    }

    private string GetDirectoryStructure(DirectoryNode root)
    {
        var sb = new System.Text.StringBuilder();
        void AppendStructure(DirectoryNode dir, string indent)
        {
            sb.AppendLine($"{indent}{dir.Name}/");
            foreach (var child in dir.Children.OrderBy(c => c is DirectoryNode ? 0 : 1).ThenBy(c => c.Name))
            {
                if (child is DirectoryNode subDir)
                {
                    AppendStructure(subDir, indent + "  ");
                }
                else
                {
                    sb.AppendLine($"{indent}  {child.Name}");
                }
            }
        }
        AppendStructure(root, "");
        return sb.ToString();
    }
}

public class GenerateIgnoreSettings : CommandSettings
{
    [CommandArgument(0, "<PATH>")]
    [Description("Path to the directory to analyze.")]
    public string Path { get; set; } = string.Empty;

    [CommandOption("--run-analysis")]
    [Description("Run the analysis after generating the ignore file.")]
    [DefaultValue(false)]
    public bool RunAnalysis { get; set; }
}