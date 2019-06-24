// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.Core.Imaging;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text.Adornments;

using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Schema;

namespace MonoDevelop.MSBuild.Editor
{
	static class DisplayElementFactory
	{
		public static async Task<object> GetInfoTooltipElement (MSBuildRootDocument doc, BaseInfo info, MSBuildResolveResult rr, CancellationToken token)
		{
			object nameElement = GetNameElement (info);
			if (nameElement == null) {
				return null;
			}

			var imageElement = GetImageElement (info);
			if (imageElement != null) {
				nameElement = new ContainerElement (
					ContainerElementStyle.Wrapped | ContainerElementStyle.VerticalPadding,
					imageElement, nameElement
				);
			}

			var elements = new List<object> { nameElement };

			var desc = info.Description;
			var descEl = desc.DisplayElement;

			if (descEl != null) {
				if (descEl is ISymbol symbol) {
					descEl = await GetSymbolDescriptionElement (symbol, token);
				}
				if (descEl != null) {
					elements.Add (descEl);
				}
			}

			if (descEl == null) {
				var descStr = DescriptionFormatter.GetDescription (info, doc, rr);
				if (!string.IsNullOrEmpty (descStr)) {
					elements.Add (new ClassifiedTextElement (new ClassifiedTextRun (PredefinedClassificationTypeNames.NaturalLanguage, desc.Text)));
				}
			}

			var seenIn = GetSeenInElement (info, doc);
			if (seenIn != null) {
				elements.Add (seenIn);
			}

			return elements.Count == 1
				? elements[0]
				: new ContainerElement (ContainerElementStyle.Stacked | ContainerElementStyle.VerticalPadding, elements);
		}

		public static ClassifiedTextElement GetNameElement (BaseInfo info)
		{
			var label = DescriptionFormatter.GetTitle (info);
			if (label.kind == null) {
				return null;
			}

			var runs = new List<ClassifiedTextRun> ();

			runs.Add (new ClassifiedTextRun (PredefinedClassificationTypeNames.Keyword, label.kind));
			runs.Add (new ClassifiedTextRun (PredefinedClassificationTypeNames.WhiteSpace, " "));
			runs.Add (new ClassifiedTextRun (PredefinedClassificationTypeNames.Identifier, label.name));

			string typeInfo = null;
			if (info is ValueInfo vi) {
				var tdesc = vi.GetTypeDescription ();
				if (tdesc.Count > 0) {
					typeInfo = string.Join (" ", tdesc);
				}
			}

			if (info is FunctionInfo fi) {
				typeInfo = fi.ReturnTypeString;
				if (!fi.IsProperty) {
					runs.Add (new ClassifiedTextRun (PredefinedClassificationTypeNames.Other, "("));

					bool first = true;
					foreach (var p in fi.Parameters) {
						if (first) {
							first = false;
						} else {
							runs.Add (new ClassifiedTextRun (PredefinedClassificationTypeNames.Other, ", "));
						}

						runs.Add (new ClassifiedTextRun (PredefinedClassificationTypeNames.SymbolReference, p.Name));
						runs.Add (new ClassifiedTextRun (PredefinedClassificationTypeNames.Other, " : "));
						runs.Add (new ClassifiedTextRun (PredefinedClassificationTypeNames.Type, p.Type));
					}
					runs.Add (new ClassifiedTextRun (PredefinedClassificationTypeNames.Other, ")"));
				}
			}

			if (typeInfo != null) {
				runs.Add (new ClassifiedTextRun (PredefinedClassificationTypeNames.Other, " : "));
				runs.Add (new ClassifiedTextRun (PredefinedClassificationTypeNames.Type, typeInfo));
			}

			return new ClassifiedTextElement (runs);
		}

		public static ContainerElement GetSeenInElement (BaseInfo info, MSBuildRootDocument doc)
		{
			var seenIn = doc.GetFilesSeenIn (info).ToList ();
			if (seenIn.Count == 0) {
				return null;
			}

			Func<string, (string prefix, string remaining)?> shorten = null;

			var elements = new List<ClassifiedTextElement> ();

			int count = 0;
			foreach (var s in seenIn) {
				if (count == 5) {
					elements.Add (new ClassifiedTextElement (new ClassifiedTextRun (PredefinedClassificationTypeNames.Other, "[more in Find References]")));
					break;
				}
				count++;

				//factor out some common prefixes into variables
				//we do this instead of using the original string, as the result is simpler
				//and easier to understand
				shorten = shorten ?? CreateFilenameShortener (doc.RuntimeInformation);
				var replacement = shorten (s);
				if (!replacement.HasValue) {
					elements.Add (new ClassifiedTextElement (new ClassifiedTextRun (PredefinedClassificationTypeNames.Other, s)));
					continue;
				}

				elements.Add (new ClassifiedTextElement (
					new ClassifiedTextRun (PredefinedClassificationTypeNames.SymbolReference, replacement.Value.prefix),
					new ClassifiedTextRun (PredefinedClassificationTypeNames.Other, replacement.Value.remaining)
				));
			}

			if (elements.Count == 0) {
				return null;
			}

			elements.Insert (0, new ClassifiedTextElement (new ClassifiedTextRun (PredefinedClassificationTypeNames.Other, "Seen in:")));
			return new ContainerElement (ContainerElementStyle.Stacked, elements);
		}

		public static object GetResolvedPathElement (List<NavigationAnnotation> navs)
		{
			if (navs.Count == 1) {
				return new ClassifiedTextElement (
					new ClassifiedTextRun (PredefinedClassificationTypeNames.Other, "Resolved Path:"),
					new ClassifiedTextRun (PredefinedClassificationTypeNames.WhiteSpace, " "),
					new ClassifiedTextRun (PredefinedClassificationTypeNames.Literal, navs[0].Path)
				);
			}

			var elements = new List<ClassifiedTextElement> ();
			elements.Add (new ClassifiedTextElement (new ClassifiedTextRun (PredefinedClassificationTypeNames.Other, "Resolved Paths:")));

			int i = 0;
			foreach (var location in navs) {
				elements.Add (new ClassifiedTextElement (new ClassifiedTextRun (PredefinedClassificationTypeNames.Literal, location.Path)));
				if (i == 5) {
					elements.Add (new ClassifiedTextElement (new ClassifiedTextRun (PredefinedClassificationTypeNames.Other, "[more in Go to Definition]")));
					break;
				}
			}

			return new ContainerElement (ContainerElementStyle.Stacked, elements);
		}

		/// <summary>
		/// Shortens filenames by extracting common prefixes into MSBuild properties. Returns null if the name could not be shortened in this way.
		/// </summary>
		public static Func<string, (string prefix, string remaining)?> CreateFilenameShortener (IRuntimeInformation runtimeInfo)
		{
			var prefixes = GetPrefixes (runtimeInfo);
			return s => GetLongestReplacement (s, prefixes);
		}

		static List<(string prefix, string subst)> GetPrefixes (IRuntimeInformation runtimeInfo)
		{
			var list = new List<(string prefix, string subst)> {
				(runtimeInfo.BinPath, "$(MSBuildBinPath)"),
				(runtimeInfo.ToolsPath, "$(MSBuildToolsPath)")
			};
			foreach (var extPath in runtimeInfo.SearchPaths["MSBuildExtensionsPath"]) {
				list.Add ((extPath, "$(MSBuildExtensionsPath)"));
			}
			var sdksPath = runtimeInfo.SdksPath;
			if (sdksPath != null) {
				list.Add ((sdksPath, "$(MSBuildSDKsPath)"));
			}
			return list;
		}

		static (string prefix, string remaining)? GetLongestReplacement (string val, List<(string prefix, string subst)> replacements)
		{
			(string prefix, string subst)? longestReplacement = null;
			foreach (var replacement in replacements) {
				if (val.StartsWith (replacement.prefix, System.StringComparison.OrdinalIgnoreCase)) {
					if (!longestReplacement.HasValue || longestReplacement.Value.prefix.Length < replacement.prefix.Length) {
						longestReplacement = replacement;
					}
				}
			}

			if (longestReplacement.HasValue) {
				return (longestReplacement.Value.subst, val.Substring (longestReplacement.Value.prefix.Length));
			}

			return null;
		}

		public static ImageElement GetImageElement (BaseInfo info)
		{
			var id = GetKnownImageIdForInfo (info, false);
			return id.HasValue ? new ImageElement (id.Value.ToImageId ()) : null;
		}

		public static ImageElement GetImageElement (KnownImages image) => new ImageElement (image.ToImageId ());

		static KnownImages? GetKnownImageIdForInfo (BaseInfo info, bool isPrivate)
		{
			switch (info) {
			case MSBuildLanguageElement el:
				if (!el.IsAbstract)
					return KnownImages.IntellisenseKeyword;
				break;
			case MSBuildLanguageAttribute att:
				if (!att.IsAbstract) {
					return KnownImages.IntellisenseKeyword;
				}
				break;
			case ItemInfo item:
				return isPrivate ? KnownImages.MSBuildItemPrivate : KnownImages.MSBuildItem;
			case PropertyInfo prop:
				return isPrivate ? KnownImages.MSBuildPropertyPrivate : KnownImages.MSBuildProperty;
			case TargetInfo prop:
				return isPrivate ? KnownImages.MSBuildTargetPrivate : KnownImages.MSBuildTarget;
			case MetadataInfo meta:
				return isPrivate ? KnownImages.MSBuildMetadata : KnownImages.MSBuildMetadataPrivate;
			case TaskInfo task:
				return KnownImages.MSBuildTask;
			case ConstantInfo value:
				return KnownImages.MSBuildConstant;
			case FileOrFolderInfo value:
				return value.IsFolder ? KnownImages.FolderClosed : KnownImages.GenericFile;
			case FrameworkInfo fxi:
				return KnownImages.MSBuildFrameworkId;
			case TaskParameterInfo tpi:
				return KnownImages.MSBuildTaskParameter;
			case FunctionInfo fi:
				if (fi.IsProperty) {
					//FIXME: can we resolve the msbuild / .net property terminology overloading?
					return KnownImages.Property;
				}
				return KnownImages.Method;
			case ClassInfo ci:
				return KnownImages.Class;
			}
			return null;
		}

		static Task<object> GetSymbolDescriptionElement (ISymbol symbol, CancellationToken token)
		{
			return Task.Run (() => {
				try {
					var docs = symbol.GetDocumentationCommentXml (expandIncludes: true, cancellationToken: token);
					if (!string.IsNullOrEmpty (docs)) {
						return (object)GetDocsXmlSummaryElement (docs);
					}
				} catch (Exception ex) {
					LoggingService.LogError ("Error loading docs summary", ex);
				}
				return null;
			}, token);
		}

		// roslyn's IDocumentationCommentFormattingService seems to be basically unusable
		// without internals access, so it some basic formatting ourselves
		static object GetDocsXmlSummaryElement (string docs)
		{
			var docsXml = XDocument.Parse (docs);
			var summaryEl = docsXml.Root?.Element ("summary");
			var runs = new List<ClassifiedTextRun> ();
			foreach (var node in summaryEl.Nodes ()) {
				switch (node) {
				case XText text:
					runs.Add (new ClassifiedTextRun (PredefinedClassificationTypeNames.NaturalLanguage, text.Value));
					break;
				case XElement el:
					if (el.Name == "see") {
						var cref = (string)el.Attribute ("cref");
						if (cref != null) {
							var colonIdx = cref.IndexOf (':');
							if (colonIdx > -1) {
								cref = cref.Substring (colonIdx + 1);
							}
							if (!string.IsNullOrEmpty (cref)) {
								runs.Add (new ClassifiedTextRun (PredefinedClassificationTypeNames.Type, cref));
							}
						} else {
							LoggingService.LogDebug ("Docs 'see' element is missing cref attribute");
						}
						break;
					}
					LoggingService.LogDebug ($"Docs summary has unexpected '{el.Name}' element");
					goto default;
				default:
					LoggingService.LogDebug ($"Docs summary has unexpected '{node.NodeType}' node");
					break;
				}
			}
			return runs == null ? null : (object)new ClassifiedTextElement (runs);
		}
	}

	static class ImageExtensions
	{
		public static ImageId ToImageId (this KnownImages id) => new ImageId (KnownImagesGuid, (int)id);
		static readonly Guid KnownImagesGuid = Guid.Parse ("{ae27a6b0-e345-4288-96df-5eaf394ee369}");
	}

	enum KnownImages
	{
		// these mirror the values from Microsoft.VisualStudio.Imaging.KnownImageIds
		Property = 2429,
		PropertyPrivate = 2434,
		Method = 1874,
		MethodPrivate = 1874,
		Reference = 2521,
		Add = 28,
		NuGet = 3150,
		PackageReference = 3574,
		FolderClosed = 1294,
		BinaryFile = 272,
		Class = 463,
		ClassPrivate = 471,
		Field = 1217,
		FieldPrivate = 1220,
		Enumeration = 1120,
		EnumerationPrivate = 1129,
		Constant = 616,
		ConstantPrivate = 618,
		XMLAttribute = 3564,
		XMLCDataTag = 3567,
		XMLCommentTag = 3568,
		XMLElement = 3573,
		IntellisenseKeyword = 1589,
		Assembly = 196,
		Action = 13,
		DotNETFrameworkDependency = 1019,
		Parameter = 2242,

		// this defines the mapping from the MSBuild usage to the icons we're re-using
		MSBuildProperty = Property,
		MSBuildPropertyPrivate = PropertyPrivate,
		MSBuildItem = Class,
		MSBuildItemPrivate = ClassPrivate,
		MSBuildMetadata = Field,
		MSBuildMetadataPrivate = FieldPrivate,
		MSBuildConstant = Constant,
		MSBuildTarget = Method,
		MSBuildTargetPrivate = MethodPrivate,
		MSBuildTask = Action,
		MSBuildTaskParameter = Parameter,
		MSBuildFrameworkId = DotNETFrameworkDependency,
		GenericFile = BinaryFile
	}
}
