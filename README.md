# MonoDevelop.MSBuildEditor

The MSBuild Editor extension provides improved support for editing MSBuild files in MonoDevelop.

## Features

### IntelliSense

Rich, MSBuild-specific IntelliSense helps you write your project and target files, with completions for MSBuild elements, attributes and expressions.

### Navigation

The *Go to Definition* command can be used to navigate to an import or an SDK. If the import has multiple valid ways it can be evaluated, you can navigate to any of them. When navigating to an SDK, you can navigate to any of the `.props` and `.targets` in it.

The *Find References* command can accurately and precisely find all references to items, properties, metadata and tasks throughout your project and its imports.

### Imports

The extension resolves your project's imports recursively, and scans all the found MSBuild files for items, properties, metadata, targets and tasks to be included in IntelliSense and *Find References*. It attempts to resolve imports as broadly as possible, ignoring conditions and checking multiple values. It also has full support for SDKs that are resolved via SDK resolvers.

### Tooltips

Tooltips for items, properties and metadata allow you to see their descriptions and see which imports they have been referenced in.

Tooltips for imports and SDKs show you the paths of the imported files.

### Schemas

In addition to the schema inferred from the items, metadata, properties and tasks used in a project's imports, the extension also defines a schema format for describing them in more detail. The IntelliSense system uses these to provide a richer editing and validation experience.

Targets can provide a schema 'sidecar', which has the same name as the targets file except with the suffix `.buildschema.json`.

The extension includes built-in schemas for `Microsoft.Common.targets` and other common targets.

### Documentation

The extension includes documentation tooltips for the MSBuild language and many common items, properties and metadata

### Formatting Style

There is a formatting policy for MSBuild files, allowing you to customize the formatting behaviour. The default formatting policy uses two spaces for indentation, matching the project files created by Visual Studio.

## TODO

The following feature are not yet implemented. Please contact Mikayla if you are interested in helping out.

* Port to Visual Studio
* Snippets
* Task parameters from assembly metadata
* File templates
* Add more unit tests
* Use a real expression parser for expression intellisense
* Improve logic for figuring out context of unqualified metadata
* In addition to brute forcing imports, resolve using full conditioned state
* Validate expression/metadata values against schema
* Write a json schema for the schema
* Validate required attributes
* Squiggle redundant values
* Completion for filenames
* Completion for metadata and property functions in expressions
* Implement completion for the many item and value types
* Completion of inline C#
* show type and default value of property/metadata/items in tooltips
* error when assigning values to reserved properties and metadata
* better targetframework completions
* go to definition on targets
* tooltips on values
* parameter info tooltip when completing values
* go to package page on nugets
* prettier package tooltips
* filter disallowed attributes and elements from completion