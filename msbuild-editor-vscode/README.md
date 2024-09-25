# MSBuild Editor

MSBuild Editor is an open-source language service that provides enhanced support for editing MSBuild files in Visual Studio Code. It includes IntelliSense, Quick Info, validation, code fixes, and refactorings, all driven by a powerful and customizable schema-based type system.

⚠️ **The editor is currently in preview. <br>
📝 Please report bugs and feature requests in the [GitHub repo](https://github.com/mhutch/MonoDevelop.MSBuildEditor/issues).<br>
🎉 Pull requests are also much appreciated!**

We appreciate your feedback, and it will help guide how the experiment develops and whether the MSBuild Editor becomes an officially supported part of the .NET development experience.

The extension is also available [for Visual Studio](https://marketplace.visualstudio.com/items?itemName=mhutch.MSBuildEditor).

## Features

### IntelliSense

MSBuild-specific IntelliSense helps you write your project and target files, with rich contextual completion for MSBuild elements, attributes and expressions.

![Property completion](./images/vscode-nowarn-completion.png)

The completion for `PackageReference` attributes queries NuGet.org as you type, and provides completion for package names and package versions.

![Package reference completion](./images/vscode-packageref-completion.png)

There's completion for MSBuild expressions, including condition comparisons, property functions and item functions. The editor also supports *Expand Selection* within MSBuild expressions.

![Expression completion](./images/vscode-expression-completion.png)

![Condition completion](./images/vscode-condition-completion.png)

### Navigation

You can use the *Go to Definition* command or *Ctrl-Click* to navigate to any import, SDK or filename. If an import has multiple valid ways it can be evaluated, you can navigate to any of them. When navigating to an SDK, you can navigate to any of the `.props` and `.targets` in it.

The *Find References* command can accurately and precisely find all references to items, properties, metadata and tasks throughout your project and its imports, including in expressions.

![Find References](./images/vscode-find-references.png)

### Quick Info

Quick Info tooltips for items, properties, metadata and values allow you to see their descriptions and expected value types, with deep links into documentation. Tooltips for imports and SDKs show you the paths of the imported files.

![Quick info](./images/vscode-quick-info.png)

### Validation and Analyzers

The editor validates your document against the MSBuild language and any imported schemas, and shows these errors and warnings as you type.

![Unknown value validation](./images/vscode-validation.png)

The core validator performs several other diagnostics such as warning about unused symbols, and there is a Roslyn-like analyzer mechanism, and examples of current built-in analyzers include:

* Package references should only pivot on target framework
* Use `<TargetFramework>` instead of `<TargetFrameworks>` when there is a single value, and vice versa

### Code Fixes and Refactorings

The editor supports code fixes for several diagnostics, and has refactorings such as *Extract Expression*.

![Code fix for misspelled property value](./images/vscode-code-fix.png)

### Schemas

In addition to the schema inferred from the items, metadata, properties and tasks used in a project's imports, the extension also defines a json-based MSBuild-specific schema format that can be used to provide documentation, type annotations, allowed values, and other information that is used to provide a richer editing and validation experience.

Any targets file can provide a schema 'sidecar', which has the same name as the targets file except with the suffix `.buildschema.json`. The editor will load the sidecar schemas for any targets that it imports. This allows MSBuild targets to provide their own documentation.

![Schema for MSBuild items](./images/vscode-schema.png)

The extension includes built-in schemas for `Microsoft.Common.targets`, `Microsoft.NET.Sdk`, and other common targets and MSBuild SDKs.

### Imports

The extension resolves your project's imports recursively, and scans all the found MSBuild files for items, properties, metadata, targets and tasks to be included in IntelliSense and *Find References*. It attempts to resolve imports as broadly as possible so that IntelliSense and navigation are not dependent on the current evaluated state of your project. It ignoring conditions on imports and attempts to evaluate them with multiple property values. It also has full support for SDKs that are resolved via SDK resolvers.

## Requirements

If you have the `tintoy.msbuild-project-tools` extension, you must uninstall it. If you have the two extensions
installed at the same time, their features will conflict.

## Extension Settings

This extension contributes the following settings:

* `msbuild.server.trace`: Change the trace level. You may need to change this to gather more information to submit with a bug report.

There are several other settings prefixed with `msbuild.server` that may be useful when debugging the language server.

## Release Notes

### 2.9.x

Initial previews of VS Code port
