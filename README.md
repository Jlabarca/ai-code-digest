# AI Code Digest

AI Code Digest is a command-line tool that analyzes a codebase, providing a detailed digest of the project's structure, file contents, and token count. This tool is designed to help developers quickly understand a new codebase, identify key areas of interest, and prepare the code for analysis by large language models (LLMs).

## Installation

To install AI Code Digest, you will need the .NET 9.0 SDK. Once you have the SDK installed, you can install the tool from the root of the project directory:

```bash
dotnet tool install --global --add-source ./nupkg codedigest
```

## Usage

### Analyzing a Codebase

To analyze a codebase, use the `analyze` command, providing the path to the directory you want to analyze:

```bash
code-digest <PATH_TO_CODEBASE>
```

This will generate a digest file in the current directory, named `<CODEBASE_NAME>_digest.txt`. You can specify a different output file using the `-o` or `--output` option:

```bash
code-digest <PATH_TO_CODEBASE> -o my_digest.txt
```

### Generating an Ignore File

To generate a `.aidigestignore` file for your project, use the `generate-ignore` command:

```bash
code-digest generate-ignore <PATH_TO_CODEBASE>
```

This will create a `.aidigestignore` file in the root of your project. You can then review and customize this file to meet your needs.

To generate the ignore file and immediately run the analysis, use the `--run-analysis` flag:

```bash
code-digest generate-ignore <PATH_TO_CODEBASE> --run-analysis
```

### The `.aidigestignore` File

The `.aidigestignore` file is a simple text file that contains a list of glob patterns. Any file or directory that matches one of these patterns will be excluded from the analysis. The file works just like a `.gitignore` file.

### Prompt Library

The prompt library is a directory of text files that can be included in the analysis digest. This is useful for providing context or instructions to an LLM that will be consuming the digest. To use the prompt library, you can use the `--prompt-library` option:

```bash
code-digest analyze <PATH_TO_CODEBASE> --prompt-library <PATH_TO_PROMPT_LIBRARY>
```

## Contributing

Contributions are welcome! Please feel free to submit a pull request or open an issue on the project's GitHub repository.
