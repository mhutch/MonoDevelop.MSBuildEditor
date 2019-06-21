// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text.Adornments;

using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Schema;

namespace MonoDevelop.MSBuild.Editor
{
	static class DisplayElementFactory
	{
		public static object GetInfoTooltipElement (MSBuildRootDocument doc, BaseInfo info, MSBuildResolveResult rr)
		{
			var nameElement = GetNameElement (info);
			if (nameElement == null) {
				return null;
			}

			var elements = new List<object> ();
			elements.Add (nameElement);

			var desc = GetDescriptionElement (info, doc, rr);
			if (desc != null) {
				elements.Add (desc);
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
					runs.Add (new ClassifiedTextRun (PredefinedClassificationTypeNames.Other, ")"));

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

			Func<string, (string prefix, string remaining)?> shorten  = null;

			var elements = new List<ClassifiedTextElement> ();

			int count = 0;
			foreach (var s in seenIn) {
				if (count == 5) {
					elements.Add (new ClassifiedTextElement (new ClassifiedTextRun (PredefinedClassificationTypeNames.Other, "[more in Find References]")));
					break;
				}

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

		public static object GetDescriptionElement (BaseInfo info, MSBuildRootDocument doc, MSBuildResolveResult rr)
		{
			var desc = DescriptionFormatter.GetDescription (info, doc, rr);
			if (desc.DisplayElement != null) {
				return desc.DisplayElement;
			} else if (!desc.IsEmpty) {
				return new ClassifiedTextElement (new ClassifiedTextRun (PredefinedClassificationTypeNames.NaturalLanguage, desc.Text));
			}
			return null;
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
	}

}
