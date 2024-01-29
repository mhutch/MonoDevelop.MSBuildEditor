# TODO

The following feature are not yet implemented. Please contact Mikayla if you are interested in helping out.

## Features

* simple project system. go to definition should open target file in the context of the original root.
* toolbar for picking root evaluation context to use for current file
* tool window that shows import hierarchy
* switch between related files command
* snippets
* feature usage and performance metrics in telemetry
* Load actual MSBuild engine and use to to evaluate imports and show evaluated property values

## Xml

* XML file formatter, extensible for MSBuild formatter
* Outlining tagger that's better than the one from textmate, extensible for MSBuild
* Document outline tool window
* Breadcrumb bar at top of file, extensible for msbuild expressions
* Editorconfig support for formatting, extensible for MSBuild formatter

## Parsing

* improve the background parser scheduling
* keep parsed expressions in the AST instead of reparsing as needed
* switch the XDom to use a red-green tree like Roslyn
* implement some incremental parsing. it should be fairly straightforward to do it for a subset of insertions, especially without < chars
* add an import cache model that allows sharing/reuse of parsed imported files between multiple root documents

## Completion

* Improve implicit and explicit triggering for file path segments
* trigger intellisense on |, indexed against | separated comparands
* use RID graph for RID completion
* context based filtering of disallowed/existing attributes and elements
* operators e.g. And/Or/-> and other non-memorable syntax e.g. static property function
* auto insert matching quotes and parens in expressions
* automatically update closing tag when editing opening tag name
* make metadata completion more context-specific
* make completion in conditions more robust
* add parameter completion for item/property/condition functions
* file extension filters on path completion

## Validation

* Check for invalid chars in paths
* Warn on assigning wrongly typed expression to property or task arg
* condition comparand validation, including |-separated properties
* validate condition functions e.g. Exists
* RID validation
* NuGet package ID validation
* error when assigning values to reserved properties and metadata
* check type of expressions assigned to properties/metadata/task params
* item and property function names and arguments
* check metadata refs have sufficient context

## Type resolution

* Compute type of expression/subexpression when possible
* Some basic coercion e.g. string+path = path
* Add logic to figure out context of unqualified metadata

## Analyzers

* DefaultItemExcludes assignment that doesn't include previous value
* Unnecessary package references
* Use property before it's assigned
* remove empty propertygroup/itemgroup
* move conditioned property identical under all conditions to unconditioned propertygroup
* defining a target that's subsequently redefined (e.g. BeforeBuild in sdk style project)
* NuGet PackageReference has unknown ID or version
* Warn when appending trailing slash to path that has one already (or to propert with type that indicates it has trailing slash)
* Warn when assigning path without trailing slash to property of type that indicates it has trailing slash
* Error on properties/items/metadata with disallowed names such as ItemGroup
* Error when assigning value to reserved property/metadata
* NuGet package reference has newer version available
* NuGet package reference has (transitive) security update available
* File referenced in property/item is missing (this may require basePath metadata of some kind)
* Path is not absolute when type metadata indicates it is
* Unconditional reassignment of property assigned in same file

## Fixes & refactorings

* convert SDK import into props/targets imports (and reverse)
* create non-existent target
* move property to directory props
* move property/item to new conditioned propertygroup/itemgroup
* group items by type (preserving dependencies)
* sort items/properties in group by identity (preserving dependencies)
* merge adjacent itemgroups/propertygroups
* convert BeforeFoo target override to -> BeforeTargets=Foo
* Upgrade/downgrade NuGet package to latest stable or latest preview
* Extract PackageReference version to Directory.Packages.props
* Remove redundant PackageReference

## Schema & type system

* Publish json schema schema
* Classname type with metadata for which msbuild metadata/property points to the assembly
* Metadata for filename type with expected extensions, and filter/validate filenames based on this
* Metadata for filename type with property to use as base directory
* Infer default values from `<foo condition="$(foo)==''">default</foo>`
* Infer type from assignment from known type and comparison to known type
* Infer bool type from bool literal assigment
* API to dump barebones schema from inferred symbols
* pull task definitions from current roslyn workspace, if any
* arbitrary metadata on properties/items/types that analyzers/fixes can use
* Add a oneOf type system that allows a value to have multiple types (see e.g. file or url for nuget feeds)
* Allow specialized error code types that derive form base error code type
* Define error code types for C#/NuGet/MSBuild schemas
* Allow custom types to define a validation regex
* Property/item functions should support return value docs
* Allow custom type to add values to existing custom type in another schema
* For custom types that don't disallow unknown values, harvest all known values and add to completion
* Support marking properties/items with schemas with categories/keywords for filtering in completion

## Tests

Although there are hundred of unit tests covering the lower level details, there are few/no tests in the following areas:

* validation
* schema based completion
* completion for various value types
* function completion & resolution
* schema inference
* schema composition
* targets imported from NuGet packages

## Misc

* Completion of inline C#
* parameter info tooltip when completing values
* properly handle MSBuild escaping and XML entities
* better highlighting colors
* add documentation for task parameters
* fix some of the [FIXMEs](https://github.com/mhutch/MonoDevelop.MSBuildEditor/search?utf8=%E2%9C%93&q=fixme&type=)
* Fix MSBuildProjectExtensionsPath eval with nonstandard intermediate dir
* Go to definition on tasks
* Rename command
* Do some perf work, cache inferred schemas?
* Show package version of PackageReference in tooltip when using centralized package maangement
* CLI tool for running analyzers
* UI to trace target dependency graph
* When root file is a project file, limit imports to only those for the project's language
* Debugger based on a custom build logger that blocks MSBuild engine when on breakpoints and stepping
