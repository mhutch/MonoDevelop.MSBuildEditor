// Copyright (c) 2016 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

using Microsoft.Extensions.Logging;

using MonoDevelop.MSBuild.Evaluation;
using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.Xml.Dom;
using MonoDevelop.Xml.Parser;

using NuGet.Frameworks;

namespace MonoDevelop.MSBuild.Language
{
	partial class MSBuildRootDocument : MSBuildDocument, IEnumerable<IMSBuildSchema>
	{
		MSBuildToolsVersion? toolsVersion;

		public IReadOnlyList<NuGetFramework> Frameworks { get; private set; }
		public ITextSource Text { get; private set; }
		public XDocument XDocument { get; internal set; }
		public IMSBuildEnvironment Environment { get; private set; }

		public IMSBuildEvaluationContext FileEvaluationContext { get; private set; }

		public static MSBuildRootDocument Empty { get; } = new MSBuildRootDocument (null) { XDocument = new XDocument (), Environment = new NullMSBuildEnvironment () };

		public MSBuildRootDocument (string filename) : base (filename, true)
		{
		}

		public string GetTargetFrameworkNuGetSearchParameter ()
		{
			if (Frameworks.Count == 1) {
				return Frameworks[0].DotNetFrameworkName;
			}
			//TODO properly support multiple filters
			return null;
		}

		public static MSBuildRootDocument Parse (
			ITextSource textSource, string filePath, MSBuildRootDocument previous,
			MSBuildSchemaProvider schemaProvider, IMSBuildEnvironment environment,
			ITaskMetadataBuilder taskBuilder,
			ILogger logger,
			CancellationToken token)
		{
			var xmlParser = new XmlTreeParser (new XmlRootState ());
			var (xdocument, _) = xmlParser.Parse (textSource.CreateReader ());

			var propVals = new PropertyValueCollector (true);

			var doc = new MSBuildRootDocument (filePath) {
				XDocument = xdocument,
				Text = textSource,
				Environment = environment
			};

			var importedFiles = new HashSet<string> (StringComparer.OrdinalIgnoreCase);

			if (filePath != null) {
				try {
					doc.Schema = previous?.Schema ?? schemaProvider.GetSchema (filePath, null, logger);
				} catch (Exception ex) {
					LogUnhandledErrorLoadingSchema (logger, ex);
				}
				importedFiles.Add (filePath);
			}

			var parseContext = new MSBuildParserContext (
				environment,
				doc,
				previous,
				importedFiles,
				filePath,
				propVals,
				taskBuilder,
				logger,
				schemaProvider,
				token);

			if (filePath != null) {
				doc.FileEvaluationContext = new MSBuildFileEvaluationContext (parseContext.RuntimeEvaluationContext, logger, filePath, filePath);
			} else {
				doc.FileEvaluationContext = parseContext.RuntimeEvaluationContext;
			}

			string MakeRelativeMSBuildPathAbsolute (string path)
			{
				var dir = Path.GetDirectoryName (doc.Filename);
				path = path.Replace ('\\', Path.DirectorySeparatorChar);
				return Path.GetFullPath (Path.Combine (dir, path));
			}

			Import TryImportFile (string label, string possibleFile)
			{
				try {
					var fi = new FileInfo (possibleFile);
					if (fi.Exists) {
						var imp = parseContext.GetCachedOrParse (label, possibleFile, null, null, fi.LastWriteTimeUtc);
						doc.AddImport (imp);
						return imp;
					}
				} catch (Exception ex) when (parseContext.IsNotCancellation (ex)) {
					LogUnhandledErrorImportingFile (logger, ex, possibleFile);
				}
				return null;
			}

			Import TryImportSibling (string ifHasThisExtension, string thenTryThisExtension)
			{
				if (filePath == null) {
					return null;
				}
				var extension = Path.GetExtension (filePath);
				if (string.Equals (ifHasThisExtension, extension, StringComparison.OrdinalIgnoreCase)) {
					var siblingFilename = Path.ChangeExtension (filePath, thenTryThisExtension);
					return TryImportFile ("(implicit)", siblingFilename);
				}
				return null;
			}

			void TryImportIntellisenseImports (MSBuildSchema schema)
			{
				foreach (var intellisenseImport in schema.IntelliSenseImports) {
					TryImportFile ("(from schema)", MakeRelativeMSBuildPathAbsolute (intellisenseImport));
				}
			}

			try {
				//if this is a targets file, try to import the props _at the top_
				var propsImport = TryImportSibling (".targets", ".props");

				// this currently only happens in the root file
				// it's a quick hack to allow files to get some basic intellisense by
				// importing the files _that they themselves expect to be imported from_.
				// we also try to load them from the sibling props, as a paired targets/props
				// will likely share a schema file.
				var schema = doc.Schema ?? propsImport?.Document?.Schema;
				if (schema != null) {
					TryImportIntellisenseImports (doc.Schema);
				}

				doc.Build (xdocument, parseContext);

				//if this is a props file, try to import the targets _at the bottom_
				var targetsImport = TryImportSibling (".props", ".targets");

				//and if we didn't load intellisense import already, try to load them from the sibling targets
				if (schema == null && targetsImport?.Document?.Schema != null) {
					TryImportIntellisenseImports (targetsImport.Document.Schema);
				}
			} catch (Exception ex) when (parseContext.IsNotCancellation (ex)) {
				LogUnhandledErrorBuildingDocumentModel (logger, ex, filePath ?? "[unnamed]");
			}

			try {
				var env = parseContext.Environment;
				foreach (var t in env.EnumerateFilesInToolsPath ("*.tasks")) {
					doc.LoadTasks (parseContext, "(core tasks)", t);
				}
				foreach (var t in env.EnumerateFilesInToolsPath ("*.overridetasks")) {
					doc.LoadTasks (parseContext, "(core overridetasks)", t);
				}
			} catch (Exception ex) when (parseContext.IsNotCancellation (ex)) {
				LogUnhandledErrorResolvingTasksFiles (logger, ex);
			}

			try {
				if (previous != null) {
					// try to recover some values that may have been collected from the imports, as they
					// will not have been re-evaluated
					var fx = previous.Frameworks.FirstOrDefault ();
					if (fx != null) {
						propVals.Collect (doc.FileEvaluationContext, "TargetFramework", new ExpressionText (0, fx.GetShortFolderName (), true));
						propVals.Collect (doc.FileEvaluationContext, "TargetFrameworkVersion", new ExpressionText (0, FrameworkInfoProvider.FormatDisplayVersion (fx.Version), true));
						propVals.Collect (doc.FileEvaluationContext, "TargetFrameworkIdentifier", new ExpressionText (0, fx.Framework, true));
					}
				}
				doc.Frameworks = propVals.GetFrameworks ();
			} catch (Exception ex) {
				LogUnhandledErrorDeterminingTargetFramework (logger, ex);
				doc.Frameworks = new List<NuGetFramework> ();
			}

			try {
				//this has to run in a second pass so that it runs after all the schemas are loaded
				var validator = new MSBuildDocumentValidator (doc, textSource, logger);
				validator.Run (doc.XDocument.RootElement, token: token);
			} catch (Exception ex) when (parseContext.IsNotCancellation (ex)) {
				LogUnhandledErrorValidatingDocument (logger, ex);
			}

			return doc;
		}

		void LoadTasks (MSBuildParserContext context, string label, string filename)
		{
			try {
				var import = context.GetCachedOrParse (label, filename, null, null, File.GetLastWriteTimeUtc (filename));
				AddImport (import);
			} catch (Exception ex) when (context.IsNotCancellation (ex)) {
				LogUnhandledErrorResolvingTasksFile (context.Logger, ex, filename);
			}
		}

		//hack for MSBuildParserContext to access
		internal readonly Dictionary<string, Import> resolvedImportsMap = new Dictionary<string, Import> ();

		public override void AddImport (Import import)
		{
			base.AddImport (import);
			if (import.IsResolved) {
				resolvedImportsMap[import.Filename] = import;
			}
		}

		public IEnumerator<IMSBuildSchema> GetEnumerator ()
		{
			return GetSchemas ().GetEnumerator ();
		}

		IEnumerator IEnumerable.GetEnumerator ()
		{
			return GetSchemas ().GetEnumerator ();
		}

		public MSBuildToolsVersion ToolsVersion {
			get {
				if (toolsVersion.HasValue) {
					return toolsVersion.Value;
				}

				if (XDocument.RootElement != null) {
					var sdkAtt = XDocument.RootElement.Attributes["Sdk"];
					if (sdkAtt != null) {
						toolsVersion = MSBuildToolsVersion.V15_0;
						return toolsVersion.Value;
					}

					var tvAtt = XDocument.RootElement.Attributes["ToolsVersion"];
					if (tvAtt != null) {
						var val = tvAtt.Value;
						if (MSBuildToolsVersionExtensions.TryParse (val, out MSBuildToolsVersion tv)) {
							toolsVersion = tv;
							return tv;
						}
					}
				}

				toolsVersion = MSBuildToolsVersion.Unknown;
				return toolsVersion.Value;
			}
		}

		[LoggerMessage (EventId = 0, Level = LogLevel.Warning, Message = "Error loading schema")]
		static partial void LogUnhandledErrorLoadingSchema (ILogger logger, Exception ex);

		[LoggerMessage (EventId = 1, Level = LogLevel.Warning, Message = "Error importing file '{importFile}'")]
		static partial void LogUnhandledErrorImportingFile  (ILogger logger, Exception ex, string importFile);

		[LoggerMessage (EventId = 2, Level = LogLevel.Warning, Message = "Error building document model '{importFile}'")]
		static partial void LogUnhandledErrorBuildingDocumentModel (ILogger logger, Exception ex, string importFile);

		[LoggerMessage (EventId = 3, Level = LogLevel.Warning, Message = "Error resolving tasks files")]
		static partial void LogUnhandledErrorResolvingTasksFiles (ILogger logger, Exception ex);

		[LoggerMessage (EventId = 4, Level = LogLevel.Warning, Message = "Error resolving tasks file '{tasksFile}'")]
		static partial void LogUnhandledErrorResolvingTasksFile (ILogger logger, Exception ex, string tasksFile);

		[LoggerMessage (EventId = 5, Level = LogLevel.Warning, Message = "Error determining target framework")]
		static partial void LogUnhandledErrorDeterminingTargetFramework (ILogger logger, Exception ex);

		[LoggerMessage (EventId = 6, Level = LogLevel.Warning, Message = "Error validating document")]
		static partial void LogUnhandledErrorValidatingDocument (ILogger logger, Exception ex);
	}
}