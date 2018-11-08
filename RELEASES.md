MSBuild Editor Release Notes
===

2.1 (2018-11-08)
---

* Added IntelliSense for tasks defined in current project.
* Added command to toggle showing 'private' symbols - properties, items
  and targets that start with an underscore. This command is not surfaced
  in a menu but can be activated from global search.
* Private symbols are now sorted to the bottom of the completion list.
* Improved task resolution. All tasks in the .NET SDK are now resolved.
* When editing a props file, if there is a sibling tasks file with the
  same name it will be implicitly imported, and vice versa.
* MSBuild-specific syntax highlighting is now enabled for csproj/vbproj/fsproj files.
* buildschema.json files can now define additional files to be imported
  at edit time with a new `intellisenseImports` array. This is useful
  for providing parent context to files that are expected to be imported
  by other files.
* A [JSON schema](MonoDevelop.MSBuildEditor/Schemas/buildschema.json)
  for buildschema.json files is now available.
* The NuGet Pack schema has been updated with more properties and metadata.
* buildschema.json files can now add metadata to item types defined in
  other schemas. For example, the NuGet Pack schema uses this to add
  `Pack` metadata to `None` items.
* Simple property functions no longer break expression parsing and resolution.
* There is basic IntelliSense for a subset of property functions.
* Assigning default values to properties is no longer be a warning
  in props and targets files, only project files.
* Removed noisy log messages.