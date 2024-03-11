
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using MonoDevelop.MSBuild;
using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.Xml.Parser;

class SchemaCommands
{
	public static ExitCode GenerateSchema (ILoggerFactory loggerFactory, IEnumerable<string> projectFiles, string? schemaFile, CancellationToken cancellationToken)
	{
		var logger = loggerFactory.CreateLogger (nameof (GenerateSchema));

		if (!projectFiles.Any ()) {
			logger.LogError ("No project files specified");
			return ExitCode.ProjectFileNotFound;
		}

		if (!TryGetImplicitSchemaFile (projectFiles, logger, out ExitCode exitCode, ref schemaFile)) {
			return exitCode;
		}

		if (!TryReadProjectFiles (projectFiles, logger, out exitCode, out var rootDocuments, cancellationToken)) {
			return exitCode;
		}

		var schema = GetCombinedInferredSchema (rootDocuments);

		if (File.Exists (schemaFile)) {
			logger.LogInformation ("Updating schema file `{schemaFile}`", schemaFile);

			using var reader = File.OpenText (schemaFile);
			var existingSchema = MSBuildSchema.Load (reader, out var loadErrors, schemaFile);
			if (loadErrors is not null && loadErrors.Count > 0) {
				MSBuildSchemaUtils.PrintSchemaErrors (loadErrors);
				return ExitCode.InvalidSchemaFile;
			}

			// combine the existing schema with the inferred schemas
			// prioritizing symbols from the existing schema
			schema = MSBuildSchemaUtils.CreateCombinedSchema ([existingSchema, schema]);
		} else {
			logger.LogInformation ("Writing schema file `{schemaFile}`", schemaFile);
		}

		using var writer = File.CreateText (schemaFile);
		using var schemaWriter = new MSBuildSchemaWriter (writer);
		schemaWriter.Write (schema);

		return ExitCode.Success;
	}

	static bool TryGetImplicitSchemaFile (
		IEnumerable<string> projectFiles,
		ILogger logger,
		out ExitCode exitCode,
		[NotNullWhen(true)] ref string? schemaFile)
	{
		if (schemaFile is not null) {
			exitCode = ExitCode.Success;
			return true;
		}

		List<string>? ambiguousSchemaFiles = null;
		foreach (var projectFile in projectFiles) {
			var possibleSchemaFile = Path.ChangeExtension (projectFile, ".buildschema.json");
			if (File.Exists (possibleSchemaFile)) {
				if (schemaFile != null) {
					(ambiguousSchemaFiles ??= []).Add (schemaFile);
				}
				schemaFile = possibleSchemaFile;
			}
		}

		if (ambiguousSchemaFiles != null) {
			logger.LogError (
				"When generating a schema file from multiple project files, the `--schema` option may only " +
				"be omitted when only one of the project files has a sidecar schema file. Multiple sidecar " +
				"schema files were found:\n\t{ambiguousSchemaFiles}",
				string.Join ("\n\t", ambiguousSchemaFiles)
			);
			schemaFile = null;
			exitCode = ExitCode.MissingSchemaOption;
			return false;
		}

		schemaFile ??= Path.ChangeExtension (projectFiles.First (), ".buildschema.json");
		exitCode = ExitCode.Success;
		return true;
	}

	static MSBuildSchema GetCombinedInferredSchema (IEnumerable<MSBuildRootDocument> rootDocuments)
	{
		// Gather all the "external schemas" - schemas and inferred schemas from the import
		// graph of all of the project files, but not from the project files themselves.
		// Note that some of the project files may be in the import graph of the other project files,
		// so make sure not to include them in the external schemas by pre-marking them as seen.
		List<IMSBuildSchema> externalSchemas = [];
		HashSet<string> gatheredSchemas = new (
			rootDocuments.Select (d => Path.GetFullPath(d.Filename ?? throw new ArgumentException ("Root document has no filename"))),
			StringComparer.OrdinalIgnoreCase
		);

		foreach (var rootDocument in rootDocuments) {
			foreach (var document in rootDocument.GetDescendentDocuments ()) {
				if (document.Filename is not null && !gatheredSchemas.Add (Path.GetFullPath (document.Filename))) {
					continue;
				}
				if (document.Schema is not null) {
					externalSchemas.Add (document.Schema);
				}
				externalSchemas.Add (document.InferredSchema);
			}
		}

		// combine the inferred schemas of all of the project files
		// as we will be writing them into a single schema file
		var combined = MSBuildSchemaUtils.CreateCombinedSchema (
			rootDocuments.Select (d => d.InferredSchema),
			excludeSymbolsFrom: externalSchemas
		);

		return combined;
	}

	static bool TryReadProjectFiles (
		IEnumerable<string> projectFiles,
		ILogger logger,
		out ExitCode exitCode,
		[NotNullWhen(true)] out List<MSBuildRootDocument>? rootDocuments,
		CancellationToken cancellationToken)
	{
		RegisterMSBuildAssemblies (); // CurrentProcessMSBuildEnvironment needs this
		var environment = new CurrentProcessMSBuildEnvironment (logger);
		var schemaProvider = new MSBuildSchemaProvider ();
		var taskBuilder = new NoopTaskMetadataBuilder ();

		rootDocuments = [];
		foreach (var projectFile in projectFiles) {
			try {
				var text = File.ReadAllText (projectFile);
				var textSource = new StringTextSource (text);
				var rootDocument = MSBuildRootDocument.Parse (textSource, projectFile, null, schemaProvider, environment, taskBuilder, logger, cancellationToken);
				rootDocuments.Add (rootDocument);
			} catch (FileNotFoundException) {
				logger.LogError ("Did not find project file `{projectFile}`", projectFile);
				exitCode = ExitCode.ProjectFileNotFound;
				return false;
			} catch (Exception ex) {
				if (logger.IsEnabled (LogLevel.Debug)) {
					logger.LogError (ex, "Error reading project file `{projectFile}`", projectFile);
				} else {
					logger.LogError ("Error reading project file `{projectFile}`: {message}", projectFile, ex.Message);
				}
				exitCode = ExitCode.InvalidProjectFile;
				return false;
			}
		}

		exitCode = ExitCode.Success;
		return true;
	}

	public static ExitCode ValidateSchema (ILoggerFactory loggerFactory, IEnumerable<string> schemaFilesOrDirectories, CancellationToken cancellationToken)
	{
		var schemaFiles = new List<string> ();

		foreach (var fileOrDirectory in schemaFilesOrDirectories) {
			if (Directory.Exists (fileOrDirectory)) {
				var filesInDirectory = Directory.GetFiles (fileOrDirectory, "*.buildschema.json", SearchOption.AllDirectories);
				if (filesInDirectory.Length == 0) {
					Console.Error.WriteLine ($"No `*.buildschema.json` files found in directory '{fileOrDirectory}'");
					return ExitCode.NoSchemaFiles;
				}
				schemaFiles.AddRange (filesInDirectory);
			} else if (File.Exists (fileOrDirectory)) {
				schemaFiles.Add (fileOrDirectory);
			} else {
				Console.Error.WriteLine ($"Path'{fileOrDirectory}' is neither a file nor a directory");
				return ExitCode.SchemaFileNotFound;
			}
		}

		if (!schemaFiles.Any ()) {
			Console.Error.WriteLine ("No schema files specified");
			return ExitCode.NoSchemaFiles;
		}

		bool hasErrors = false;
		foreach (var schemaFile in schemaFiles) {
			try {
				using var reader = File.OpenText(schemaFile);
				var schema = MSBuildSchema.Load (reader, out var loadErrors, schemaFile);
				if (loadErrors.Count > 0) {
					hasErrors = true;
					MSBuildSchemaUtils.PrintSchemaErrors (loadErrors);
				}
			} catch (FileNotFoundException) {
				Console.Error.WriteLine ($"Schema file '{schemaFile}' does not exist");
				return ExitCode.SchemaFileNotFound;
			}
		}

		return hasErrors ? ExitCode.InvalidSchemaFile : ExitCode.Success;
	}

	public static void FormatSchema (string schemaFile)
	{
	}

	static void RegisterMSBuildAssemblies ()
	{
		var dotnetInstance = Microsoft.Build.Locator.MSBuildLocator.QueryVisualStudioInstances ()
			.FirstOrDefault (x => x.DiscoveryType == Microsoft.Build.Locator.DiscoveryType.DotNetSdk && x.Version.Major >= 6.0);
		if (dotnetInstance == null) {
			throw new InvalidOperationException ("Did not find instance of .NET 6.0 or later");
		}
		Microsoft.Build.Locator.MSBuildLocator.RegisterInstance (dotnetInstance);
	}
}
