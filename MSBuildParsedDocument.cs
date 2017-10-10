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
using MonoDevelop.MSBuildEditor.ExpressionParser;
using System.Linq;
using MonoDevelop.Core.Assemblies;
using Microsoft.Build.Framework;

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

		public MSBuildSdkResolver SdkResolver { get; private set; }

		internal static ParsedDocument ParseInternal (Ide.TypeSystem.ParseOptions options, CancellationToken token)
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

			//FIXME: unfortunately the XML parser's regions only have line+col locations, not offsets
			//so we need to create an ITextDocument to extract tag bodies
			//we should fix this by changing the parser to use offsets for the tag locations
			var textDoc = TextEditorFactory.CreateNewDocument (options.Content, options.FileName, MSBuildTextEditorExtension.MSBuildMimeType);

			doc.SdkResolver = oldDoc?.SdkResolver ?? new MSBuildSdkResolver (Runtime.SystemAssemblyService.CurrentRuntime);

			string projectPath = options.FileName;
			doc.Context = MSBuildResolveContext.Create (
				options.FileName,
				doc.XDocument,
				textDoc,
				doc.SdkResolver,
				(ctx, imp, sdk, reg, sreg, props) => doc.ResolveToplevelImport (oldDoc, projectPath, ctx, imp, sdk, reg, sreg, props, token)
			);

			return doc;
		}

		static IEnumerable<string> EvaluateImport (MSBuildResolveContext resolveCtx, string import, MSBuildEvaluationContext importEvalCtx, Dictionary<string, List<string>> properties)
		{
			//TODO: support wildcards
			if (import.IndexOf ('*') != -1) {
				yield break;
			}

			//fast path for imports without properties, will generally be true for SDK imports
			if (import.IndexOf ('$') < 0) {
				yield return EvaluateImport (resolveCtx, import, null);
				yield break;
			}

			if (properties == null) {
				yield return EvaluateImport (resolveCtx, import, importEvalCtx);
				yield break;
			}

			//ensure each of the properties is fully evaluated
			foreach (var p in properties) {
				if (p.Value != null) {
					for (int i = 0; i < p.Value.Count; i++) {
						var val = p.Value [i];
						int recDepth = 0;
						while (val.IndexOf ('$') > -1 && (recDepth++ < 10)) {
							val = importEvalCtx.Evaluate (val);
						}
						p.Value [i] = val;
					}
				}
			}

			//set the property values on the context
			foreach (var p in properties) {
				if (p.Value != null) {
					importEvalCtx.SetPropertyValue (p.Key, p.Value [0]);
				}
			}

			//permute on properties for which we have multiple values
			var expr = new Expression ();
			expr.Parse (import, ExpressionParser.ParseOptions.None);
			var propsToPermute = new List<Tuple<string,List<string>>> ();
			foreach (var prop in expr.Collection.OfType<PropertyReference> ()) {
				if (properties.TryGetValue (prop.Name, out List<string> values) && values != null) {
					if (values.Count > 1) {
						propsToPermute.Add (Tuple.Create (prop.Name, values));
					}
				}
			}

			if (propsToPermute.Count == 0) {
				yield return EvaluateImport (resolveCtx, import, importEvalCtx);
			} else {
				foreach (var ctx in PermuteProperties (importEvalCtx, propsToPermute)) {
					yield return EvaluateImport (resolveCtx, import, importEvalCtx);
				}
			}
		}

		//TODO: guard against excessive permutation
		static IEnumerable<MSBuildEvaluationContext> PermuteProperties (MSBuildEvaluationContext evalCtx, List<Tuple<string, List<string>>> multivaluedProperties, int idx = 0)
		{
			var prop = multivaluedProperties[idx];
			var name = prop.Item1;
			foreach (var val in prop.Item2) {
				evalCtx.SetPropertyValue (name, val);
				if (idx + 1 == multivaluedProperties.Count) {
					yield return evalCtx;
				} else {
					foreach (var permutation in PermuteProperties (evalCtx, multivaluedProperties, idx + 1)) {
						yield return permutation;
					}
				}
			}
		}

		static string EvaluateImport (MSBuildResolveContext ctx, string import, MSBuildEvaluationContext importEvalCtx)
		{
			string filename = importEvalCtx != null ? importEvalCtx.Evaluate (import) : import;
			return MSBuildProjectService.FromMSBuildPath (Path.GetDirectoryName (ctx.Filename), filename);
		}

		Import ParseImport (Import import, string projectPath, CancellationToken token)
		{
			token.ThrowIfCancellationRequested ();

			var xmlParser = new XmlParser (new XmlRootState (), true);
			string text;
			try {
				text = Core.Text.TextFileUtility.ReadAllText (import.Filename);
				xmlParser.Parse (new StringReader (text));
			} catch (Exception ex) {
				LoggingService.LogError ("Unhandled error parsing xml document", ex);
				return import;
			}

			var doc = xmlParser.Nodes.GetRoot ();

			var textDoc = TextEditorFactory.CreateNewDocument (projectPath, MSBuildTextEditorExtension.MSBuildMimeType);
			textDoc.Text = text;

			import.ResolveContext = MSBuildResolveContext.Create (
				import.Filename,
				doc,
				textDoc,
				SdkResolver,
				(ctx, imp, sdk, reg, sreg, props) => ResolveNestedImport (projectPath, ctx, imp, sdk, reg, sreg, props, token)
			);

			return import;
		}

		IEnumerable<Import> ResolveToplevelImport (MSBuildParsedDocument oldDoc, string projectPath, MSBuildResolveContext ctx, string import, string sdk, DocumentRegion region, DocumentRegion sdkRegion, Dictionary<string, List<string>> properties, CancellationToken token)
		{
			if (string.IsNullOrWhiteSpace (import)) {
				Add (new Error (ErrorType.Warning, "Empty value", region));
				yield break;
			}

			if (sdk != null) {
				if (!SdkReference.TryParse (sdk, out SdkReference sdkRef)) {
					Add (new Error (ErrorType.Warning, "Invalid SDK reference", region));
					yield break;
				}

				var sdkPath = SdkResolver.GetSdkPath (sdkRef, projectPath, null);
				if (sdkPath == null) {
					Add (new Error (ErrorType.Warning, $"Could not resolve SDK '{sdk}'", sdkRegion));
					yield break;
				}

				import = sdkPath + "\\" + import;
			}

			//TODO: re-use these contexts instead of recreating them
			var importEvalCtx = ctx.CreateImportEvalCtx (ToolsVersion, projectPath);

			bool foundAny = false;

			foreach (var filename in EvaluateImport (ctx, import, importEvalCtx, properties)) {
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
					yield return ParseImport (new Import (filename, fi.LastWriteTimeUtc), projectPath, token);
				}
			}

			if (!foundAny) {
				Add (new Error (ErrorType.Warning, "Could not resolve import", region));
				yield return new Import (import, DateTime.MinValue);
			}
		}

		IEnumerable<Import> ResolveNestedImport (string projectPath, MSBuildResolveContext ctx, string import, string sdk, DocumentRegion region, DocumentRegion sdkRegion, Dictionary<string, List<string>> properties, CancellationToken token)
		{
			if (string.IsNullOrWhiteSpace (import)) {
				yield break;
			}

			if (sdk != null) {
				if (!SdkReference.TryParse (sdk, out SdkReference sdkRef)) {
					yield break;
				}

				var sdkPath = SdkResolver.GetSdkPath (sdkRef, projectPath, null);
				if (sdkPath == null) {
					yield break;
				}

				import = sdkPath + "\\" + import;
			}

			//TODO: re-use these contexts instead of recreating them
			var importEvalCtx = ctx.CreateImportEvalCtx (ToolsVersion, projectPath);

			bool foundAny = false;

			foreach (var filename in EvaluateImport (ctx, import, importEvalCtx, properties)) {
				if (string.IsNullOrEmpty (filename)) {
					continue;
				}

				var fi = new FileInfo (filename);
				if (!fi.Exists) {
					continue;
				}

				foundAny = true;

				//TODO: guard against cyclic imports
				yield return ParseImport (new Import (filename, fi.LastWriteTimeUtc), projectPath, token);
			}

			if (!foundAny) {
				LoggingService.LogWarning ($"Could not resolve MSBuild import '{import}'");
			}
		}
	}
}
