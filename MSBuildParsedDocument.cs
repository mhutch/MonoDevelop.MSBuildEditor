//
// MSBuildParsedDocument.cs
//
// Author:
//       Mikayla Hutchinson <m.j.hutchinson@gmail.com>
//
// Copyright (c) 2016 Xamarin Inc.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using MonoDevelop.Core;
using MonoDevelop.Core.Assemblies;
using MonoDevelop.Core.Text;
using MonoDevelop.Ide.Editor;
using MonoDevelop.Ide.TypeSystem;
using MonoDevelop.Xml.Dom;
using MonoDevelop.Xml.Editor;
using MonoDevelop.Xml.Parser;

namespace MonoDevelop.MSBuildEditor
{
	class MSBuildParsedDocument : XmlParsedDocument
	{
		MSBuildToolsVersion? toolsVersion;

		public MSBuildResolveContext Context { get; internal set; }

		public MSBuildParsedDocument (string filename) : base (filename)
		{
		}

		public MSBuildToolsVersion ToolsVersion {
			get {
				if (toolsVersion.HasValue) {
					return toolsVersion.Value;
				}

				if (XDocument.RootElement != null) {
					var sdkAtt = XDocument.RootElement.Attributes [new XName ("Sdk")];
					if (sdkAtt != null) {
						toolsVersion = MSBuildToolsVersion.V15_0;
						return toolsVersion.Value;
					}

					var tvAtt = XDocument.RootElement.Attributes [new XName ("ToolsVersion")];
					if (tvAtt != null) {
						var val = tvAtt.Value;
						MSBuildToolsVersion tv;
						if (Enum.TryParse (val, out tv)) {
							toolsVersion = tv;
							return tv;
						}
					}
				}

				toolsVersion = MSBuildToolsVersion.Unknown;
				return toolsVersion.Value;
			}
		}

		public IReadOnlyList<TargetFrameworkMoniker> Frameworks { get; private set; }

		public MSBuildSdkResolver SdkResolver { get; private set; }
		public ITextSource Text { get; private set; }

		internal static ParsedDocument ParseInternal (ParseOptions options, CancellationToken token)
		{
			var doc = new MSBuildParsedDocument (options.FileName);
			doc.Flags |= ParsedDocumentFlags.NonSerializable;

			var xmlParser = new XmlParser (new XmlRootState (), true);
			try {
				xmlParser.Parse (options.Content.CreateReader ());
			} catch (Exception ex) {
				LoggingService.LogError ("Unhandled error parsing xml document", ex);
			}

			doc.XDocument = xmlParser.Nodes.GetRoot ();

			doc.Text = options.Content;

			doc.AddRange (xmlParser.Errors);

			if (doc.XDocument != null && doc.XDocument.RootElement != null) {
				if (!doc.XDocument.RootElement.IsEnded)
					doc.XDocument.RootElement.End (xmlParser.Location);
			}

			var oldDoc = (MSBuildParsedDocument)options.OldParsedDocument;

			//FIXME: unfortunately the XML parser's regions only have line+col locations, not offsets
			//so we need to create an ITextDocument to extract tag bodies
			//we should fix this by changing the parser to use offsets for the tag locations
			var textDoc = TextEditorFactory.CreateNewDocument (options.Content, options.FileName, MSBuildTextEditorExtension.MSBuildMimeType);

			doc.SdkResolver = oldDoc?.SdkResolver ?? new MSBuildSdkResolver (Runtime.SystemAssemblyService.CurrentRuntime);

			var propVals = new PropertyValueCollector (true);

			string projectPath = options.FileName;
			doc.Context = MSBuildResolveContext.Create (
				options.FileName,
				true,
				doc.XDocument,
				textDoc,
				doc.SdkResolver,
				propVals,
				(ctx, imp, props) => doc.ResolveImport (oldDoc, projectPath, options.FileName, imp, props, token)
			);

			doc.AddRange (doc.Context.Errors);

			doc.Frameworks = propVals.GetFrameworks ();

			return doc;
		}

		Import ParseImport (Import import, string projectPath, PropertyValueCollector propVals, CancellationToken token)
		{
			token.ThrowIfCancellationRequested ();

			var xmlParser = new XmlParser (new XmlRootState (), true);
			ITextDocument textDoc;
			try {
				textDoc = TextEditorFactory.CreateNewDocument (import.Filename, MSBuildTextEditorExtension.MSBuildMimeType);
				xmlParser.Parse (textDoc.CreateReader ());
			} catch (Exception ex) {
				LoggingService.LogError ("Unhandled error parsing xml document", ex);
				return import;
			}

			var doc = xmlParser.Nodes.GetRoot ();

			import.ResolveContext = MSBuildResolveContext.Create (
				import.Filename,
				false,
				doc,
				textDoc,
				SdkResolver,
				propVals,
				(ctx, imp, props) => ResolveImport (null, projectPath, import.Filename, imp, props, token)
			);

			return import;
		}

		IEnumerable<Import> ResolveImport (MSBuildParsedDocument oldDoc, string projectPath, string thisFilePath, string import, PropertyValueCollector propVals, CancellationToken token)
		{
			//TODO: re-use these contexts instead of recreating them
			var importEvalCtx = MSBuildEvaluationContext.Create (
				ToolsVersion, Runtime.SystemAssemblyService.DefaultRuntime, SdkResolver, projectPath, thisFilePath
			);

			bool foundAny = false;

			//the ToList is necessary because nested parses can alter the list between this yielding values 
			foreach (var filename in importEvalCtx.EvaluatePathWithPermutation (import, Path.GetDirectoryName (thisFilePath), propVals).ToList ()) {
				if (string.IsNullOrEmpty (filename)) {
					continue;
				}

				var fi = new FileInfo (filename);
				if (!fi.Exists) {
					continue;
				}

				foundAny = true;

				if (oldDoc != null && oldDoc.Context.Imports.TryGetValue (filename, out Import oldImport) && oldImport.TimeStampUtc == fi.LastWriteTimeUtc) {
					//TODO: check mtimes of descendent imports too
					yield return oldImport;
				} else {
					//TODO: guard against cyclic imports
					yield return ParseImport (new Import (filename, fi.LastWriteTimeUtc), projectPath, propVals, token);
				}
			}

			if (!foundAny) {
				if (oldDoc == null && failedImports.Add (import)) {
					LoggingService.LogDebug ($"Could not resolve MSBuild import '{import}'");
				}
				yield return new Import (import, DateTime.MinValue);
			}
		}

		static readonly HashSet<string> failedImports = new HashSet<string> ();
	}
}
