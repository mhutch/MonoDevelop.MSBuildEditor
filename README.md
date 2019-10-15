# MonoDevelop.MSBuildEditor

The MSBuild Editor extension provides improved support for editing MSBuild files in Visual Studio for Mac and MonoDevelop. It can be installed from the Extension Manager.

![](https://github.com/mhutch/MonoDevelop.MSBuildEditor/workflows/.github/workflows/vswin.yml/badge.svg)
![](https://github.com/mhutch/MonoDevelop.MSBuildEditor/workflows/.github/workflows/vsmac.yml/badge.svg)

## Features

### IntelliSense

MSBuild-specific IntelliSense helps you write your project and target files, with rich contextual completion for MSBuild elements, attributes and expressions.

The completion for `PackageReference` attributes queries NuGet.org as you type.

![](images/completion.gif)

There's completion for condition comparisons:

![](images/condition-completion.png)

And there's also completion for property functions:

![](images/property-function-completion.png)

### Navigation

You can use the *Go to Definition* command or *Command*-click to navigate to any import, SDK or filename. If an import has multiple valid ways it can be evaluated, you can navigate to any of them. When navigating to an SDK, you can navigate to any of the `.props` and `.targets` in it.

The *Find References* command can accurately and precisely find all references to items, properties, metadata and tasks throughout your project and its imports.

![](images/find-references.png)

If you have "Highlight Identifiers" feature enabled, it'll work for MSBuild files too.

### Imports

The extension resolves your project's imports recursively, and scans all the found MSBuild files for items, properties, metadata, targets and tasks to be included in IntelliSense and *Find References*. It attempts to resolve imports as broadly as possible, ignoring conditions and checking multiple values. It also has full support for SDKs that are resolved via SDK resolvers.

### Tooltips

Tooltips for items, properties and metadata allow you to see their descriptions and expected value types, and see which imports they have been referenced in.

![](images/tooltip.png)

Tooltips for imports and SDKs show you the paths of the imported files.

![](images/import-tooltip.png)

### Schemas

In addition to the schema inferred from the items, metadata, properties and tasks used in a project's imports, the extension also defines a schema format for describing them in more detail. The IntelliSense system uses these to provide a richer editing and validation experience.

Targets can provide a schema 'sidecar', which has the same name as the targets file except with the suffix `.buildschema.json`.

The extension includes built-in schemas for `Microsoft.Common.targets` and other common targets.

### Validation

The editor validates your document against the MSBuild language and schema, and shows these errors and warnings as you type.

![](images/validation.png)

### Documentation

The extension includes documentation tooltips for the MSBuild language and many common items, properties and metadata.

### Formatting Style

The extension adds a formatting policy for MSBuild files, allowing you to customize the formatting behaviour. The default formatting policy uses two spaces for indentation, matching the project files created by Visual Studio.

## TODO

The following feature are not yet implemented. Please contact Mikayla if you are interested in helping out.

* Port to Visual Studio for Windows
* Snippets
* Add more unit tests
* Add logic to figure out context of unqualified metadata
* In addition to brute forcing imports, resolve using full conditioned state
* Implement completion for more item and value types
* Completion of inline C#
* show default value of property/metadata/items in tooltips
* error when assigning values to reserved properties and metadata
* parameter info tooltip when completing values
* go to package page command on nugets
* prettier package tooltips
* filter disallowed and existing attributes and elements from completion
* property comparand validation
* validate metadata refs are valid in context
* use new expression parser for triggering intellisense
  for example when multivalued language imports cause
  multiple imports of common targets
* project kind completion
* support encoding all over
* better highlighting colors - the default MD theme doesn't define many we can use, but other themes have more
* trigger intellisense on |, indexed against | separated comparands
* validate property/items types passed to/from task parameters
* add documentation for task parameters
* fix some of the [FIXMEs](https://github.com/mhutch/MonoDevelop.MSBuildEditor/search?utf8=%E2%9C%93&q=fixme&type=)
* Fix MSBuildProjectExtensionsPath eval with nonstandard intermediate dir
* Analyzer that finds DefaultItemExcludes assignment that doesn't include previous value
* API to dump undocumented items/properties/metadata/tasks
* Go to definition on tasks
* Infer default values from <foo condition="$(foo)==''">default</foo>
* Basic checking on item and property functions
* Infer bool type from bool assignment or comparison
* Do some perf work, cache inferred schemas?
* Expression type resolution (including coercion e.g. string+path = path?)
* Validate invalid chars in paths
* Warn on assigning wrong typed expression to property or task arg
* Evaluation context selection toolbar
* Go to definition on nuget goes to nuget.org or package folder?
* Publish json schema
* Test schema loader
* Test schema composition
* Test schema inference
* Classname kind with metadata for which msbuild metadata/property points to the assembly
* Additional schema metadata for expected extensions for files
* Additional schema metadata for which msbuild metadata/property points to the base directory for filenames
* Squiggle assignments to nonexistent task params
* Metadata to mark symbols as aliases for another
* Improve implicit and explicit triggering for file path segments