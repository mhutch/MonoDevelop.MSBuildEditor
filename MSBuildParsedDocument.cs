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
using System.Threading;
using System.Threading.Tasks;

using MonoDevelop.Core;
using MonoDevelop.Ide.TypeSystem;
using MonoDevelop.Projects.Formats.MSBuild;
using MonoDevelop.Xml.Dom;
using MonoDevelop.Xml.Editor;
using MonoDevelop.Xml.Parser;
using MonoDevelop.Ide.Editor;
using MonoDevelop.Projects.MSBuild;

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

			doc.AddRange (xmlParser.Errors);

			if (doc.XDocument != null && doc.XDocument.RootElement != null) {
				if (!doc.XDocument.RootElement.IsEnded)
					doc.XDocument.RootElement.End (xmlParser.Location);
			}

			var oldDoc = (MSBuildParsedDocument)options.OldParsedDocument;

			string projectPath = options.FileName;
			doc.Context = MSBuildResolveContext.Create (options.FileName, doc.XDocument, (ctx, imp, reg) => doc.ResolveToplevelImport (oldDoc, projectPath, ctx, imp, reg, token));

			return doc;
		}

		static string EvaluateImport (MSBuildResolveContext ctx, string import, MSBuildEvaluationContext importEvalCtx)
		{
			string filename = importEvalCtx.Evaluate (import);

			//TODO: support wildcards
			if (filename.IndexOf ('*') != -1)
				return null;

			var basePath = Path.GetDirectoryName (ctx.Filename);

			filename = MSBuildProjectService.FromMSBuildPath (basePath, filename);

			return filename;
		}

		Import ResolveToplevelImport (MSBuildParsedDocument oldDoc, string projectPath, MSBuildResolveContext ctx, string import, DocumentRegion region, CancellationToken token)
		{
			if (string.IsNullOrWhiteSpace (import)) {
				Add (new Error (ErrorType.Warning, "Empty value", region));
				return null;
			}

			//TODO: use property values when resolving imports
			//TODO: add MSBuildThisFileDirectory etc 
			var importEvalCtx = ctx.CreateImportEvalCtx (ToolsVersion, projectPath);
			string filename = EvaluateImport (ctx, import, importEvalCtx);
			if (filename == null) {
				return null;
			}

			var fi = new FileInfo (filename);

			if (!fi.Exists) {
				if (oldDoc != null)
					Add (new Error (ErrorType.Warning, "Could not resolve import", region));
				return new Import (filename, DateTime.MinValue);
			}

			Import oldImport;
			if (oldDoc != null && oldDoc.Context.Imports.TryGetValue (filename, out oldImport) && oldImport.TimeStampUtc == fi.LastWriteTimeUtc) {
				//TODO: check mtimes of descendent imports too
				return oldImport;
			}

			return ParseImport (new Import (filename, fi.LastWriteTimeUtc), projectPath, token);
		}

		Import ParseImport (Import import, string projectPath, CancellationToken token)
		{
			token.ThrowIfCancellationRequested ();
			
			var xmlParser = new XmlParser (new XmlRootState (), true);
			try {
				string text = Core.Text.TextFileUtility.ReadAllText (import.Filename);
				xmlParser.Parse (new StringReader (text));
			} catch (Exception ex) {
				LoggingService.LogError ("Unhandled error parsing xml document", ex);
			}

			var doc = xmlParser.Nodes.GetRoot ();

			import.ResolveContext = MSBuildResolveContext.Create (import.Filename, doc, (ctx, imp, reg) => ResolveNestedImport (projectPath, ctx, imp, reg, token));

			return import;
		}

		Import ResolveNestedImport (string projectPath, MSBuildResolveContext ctx, string import, DocumentRegion reg, CancellationToken token)
		{
			if (string.IsNullOrWhiteSpace (import)) {
				return null;
			}

			var importEvalCtx = ctx.CreateImportEvalCtx (ToolsVersion, projectPath);
			string filename = EvaluateImport (ctx, import, importEvalCtx);
			if (string.IsNullOrEmpty (filename)) {
				LoggingService.LogWarning ($"Could not resolve MSBuild import '{import}'");
				return null;
			}

			var fi = new FileInfo (filename);
			if (!fi.Exists) {
				return new Import (filename, DateTime.MinValue);
			}

			//TODO: guard against infinite recursion
			return ParseImport (new Import (filename, fi.LastWriteTimeUtc), projectPath, token);
		}
	}
}
