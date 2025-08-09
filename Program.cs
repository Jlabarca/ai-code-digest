using Spectre.Console.Cli;

var app = new CommandApp<AnalyzeCommand>();
app.Configure(config =>
{
    config.SetApplicationName("code-digest");
    config.AddCommand<AnalyzeCommand>("analyze").WithDescription("Analyzes a codebase and generates a digest.");
    config.AddCommand<GenerateIgnoreCommand>("generate-ignore").WithDescription("Generates a .aidigestignore file using AI.");
});
return await app.RunAsync(args);

