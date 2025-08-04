using Spectre.Console.Cli;

var app = new CommandApp<AnalyzeCommand>();
app.Configure(config =>
{
    config.SetApplicationName("code-consolidator");
    config.AddCommand<AnalyzeCommand>("analyze").WithDescription("Analyzes a codebase and generates a digest.");
});
return await app.RunAsync(args);

