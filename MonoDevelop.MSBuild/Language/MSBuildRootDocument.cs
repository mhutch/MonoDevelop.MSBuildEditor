// Copyright (c) 2016 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using MonoDevelop.MSBuild.Editor.Completion;
using MonoDevelop.MSBuild.Evaluation;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.Xml.Dom;
using MonoDevelop.Xml.Parser;
using NuGet.Frameworks;

namespace MonoDevelop.MSBuild.Language
{
	class MSBuildRootDocument : MSBuildDocument, IEnumerable<IMSBuildSchema>
	{
		MSBuildToolsVersion? toolsVersion;

		public IReadOnlyList<NuGetFramework> Frameworks { get; private set; }
		public ITextSource Text { get; private set; }
		public XDocument XDocument { get; internal set; }
		public IRuntimeInformation RuntimeInformation { get; private set; }

		public IMSBuildEvaluationContext FileEvaluationContext { get; private set; }

		public static MSBuildRootDocument Empty { get; } = new MSBuildRootDocument (null) { XDocument = new XDocument (), RuntimeInformation = new NullRuntimeInformation () };

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
			ITextSource textSource, MSBuildRootDocument previous,
			MSBuildSchemaProvider schemaProvider, IRuntimeInformation runtimeInfo,
			CancellationToken token)
		{
			var xmlParser = new XmlParser (new XmlRootState (), true);
			try {
				xmlParser.Parse (textSource.CreateReader ());
			} catch (Exception ex) {
				LoggingService.LogError ("Unhandled error parsing xml document", ex);
			}

			var xdocument = xmlParser.Nodes.GetRoot ();

			if (xdocument != null && xdocument.RootElement != null) {
				if (!xdocument.RootElement.IsEnded)
					xdocument.RootElement.End (xmlParser.Position);
			}

			var propVals = new PropertyValueCollector (true);

			string filepath = textSource.FileName;

			var doc = new MSBuildRootDocument (filepath) {
				XDocument = xdocument,
				Text = textSource,
				RuntimeInformation = runtimeInfo
			};
			doc.Errors.AddRange (xmlParser.Diagnostics);

			var importedFiles = new HashSet<string> (StringComparer.OrdinalIgnoreCase);

			if (filepath != null) {
				try {
					doc.Schema = previous?.Schema ?? schemaProvider.GetSchema (filepath, null);
				} catch (Exception ex) {
					LoggingService.LogError ("Error loading schema", ex);
				}
				importedFiles.Add (filepath);
			}

			var taskBuilder = MSBuildHost.CreateTaskMetadataBuilder (doc);

			var parseContext = new MSBuildParserContext (
				runtimeInfo,
				doc,
				previous,
				importedFiles,
				filepath,
				propVals,
				taskBuilder,
				schemaProvider,
				token);

			if (filepath != null) {
				doc.FileEvaluationContext = new MSBuildFileEvaluationContext (parseContext.RuntimeEvaluationContext, filepath, filepath);
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
						var imp = parseContext.GetCachedOrParse (label, possibleFile, null, fi.LastWriteTimeUtc);
						doc.AddImport (imp);
						return imp;
					}
				} catch (Exception ex) {
					LoggingService.LogError ($"Error importing '{possibleFile}'", ex);
				}
				return null;
			}

			Import TryImportSibling (string ifHasThisExtension, string thenTryThisExtension)
			{
				if (filepath == null) {
					return null;
				}
				var extension = Path.GetExtension (filepath);
				if (string.Equals (ifHasThisExtension, extension, StringComparison.OrdinalIgnoreCase)) {
					var siblingFilename = Path.ChangeExtension (filepath, thenTryThisExtension);
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

				doc.Build (xdocument, textSource, parseContext);

				//if this is a props file, try to import the targets _at the bottom_
				var targetsImport = TryImportSibling (".props", ".targets");

				//and if we didn't load intellisense import already, try to load them from the sibling targets
				if (schema == null && targetsImport?.Document?.Schema != null) {
					TryImportIntellisenseImports (targetsImport.Document.Schema);
				}
			} catch (Exception ex) {
				LoggingService.LogError ($"Error building document '{filepath ?? "[unnamed]"}'", ex);
			}

			try {
				var binpath = parseContext.RuntimeInformation.BinPath;
				foreach (var t in Directory.GetFiles (binpath, "*.tasks")) {
					doc.LoadTasks (parseContext, "(core tasks)", t);
				}
				foreach (var t in Directory.GetFiles (binpath, "*.overridetasks")) {
					doc.LoadTasks (parseContext, "(core overridetasks)", t);
				}
			} catch (Exception ex) {
				LoggingService.LogError ("Error resolving tasks", ex);
			}

			try {
				if (previous != null) {
					// try to recover some values that may have been collected from the imports, as they
					// will not have been re-evaluated
					var fx = previous.Frameworks.FirstOrDefault ();
					if (fx != null) {
						propVals.Collect ("TargetFramework", fx.GetShortFolderName ());
						propVals.Collect ("TargetFrameworkVersion", FrameworkInfoProvider.FormatDisplayVersion (fx.Version));
						propVals.Collect ("TargetFrameworkIdentifier", fx.Framework);
					}
				}
				doc.Frameworks = propVals.GetFrameworks ();
			} catch (Exception ex) {
				LoggingService.LogError ("Error determining project framework", ex);
				doc.Frameworks = new List<NuGetFramework> ();
			}

			try {
				//this has to run in a second pass so that it runs after all the schemas are loaded
				var validator = new MSBuildDocumentValidator ();
				validator.Run (doc.XDocument, textSource, doc);
			} catch (Exception ex) {
				LoggingService.LogError ("Error in validation", ex);
			}

			return doc;
		}

		void LoadTasks (MSBuildParserContext context, string label, string filename)
		{
			try {
				var import = context.GetCachedOrParse (label, filename, null, File.GetLastWriteTimeUtc (filename));
				AddImport (import);
			} catch (Exception ex) {
				LoggingService.LogError ($"Error loading tasks file {filename}", ex);
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
					var sdkAtt = XDocument.RootElement.Attributes[new XName ("Sdk")];
					if (sdkAtt != null) {
						toolsVersion = MSBuildToolsVersion.V15_0;
						return toolsVersion.Value;
					}

					var tvAtt = XDocument.RootElement.Attributes[new XName ("ToolsVersion")];
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
	}
}