You are an expert in software development and project structure analysis.
Your task is to generate a `.aidigestignore` file for a given project to be used with the `ai-code-digest` tool.
The `.aidigestignore` file works like a `.gitignore` file and specifies files and directories that should be excluded from the analysis.

Based on the following project structure, please generate a comprehensive `.aidigestignore` file.

The file should exclude:
- Dependencies (e.g., `node_modules`, `packages`, `vendor`)
- Build artifacts (e.g., `bin`, `obj`, `dist`, `build`)
- IDE and editor configuration files (e.g., `.idea`, `.vscode`, `.vs`)
- Log files (`*.log`)
- Temporary files
- Secrets and local configuration files.
- Large binary files that are not relevant for code analysis.

Here is the project structure:

{PROJECT_STRUCTURE}

Please provide only the content of the `.aidigestignore` file, with each pattern on a new line. Do not include any other text or explanation.