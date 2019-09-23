// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Microsoft.VisualStudio.Text.Classification;
using MonoDevelop.MSBuild.Editor.Host;
using Roslyn.Utilities;

namespace MonoDevelop.MSBuild.Editor.VisualStudio.FindReferences
{
	internal static partial class WpfClassificationExtensions
	{
		public static Run ToRun (this ClassifiedText part, IClassificationFormatMap formatMap, IClassificationTypeRegistryService typeMap)
		{
			var run = new Run (part.Text);

			var classificationType = typeMap.GetClassificationType (part.ClassificationType);

			var format = formatMap.GetTextProperties (classificationType);
			run.SetTextProperties (format);

			return run;
		}

		public static IList<Inline> ToInlines (
		   this IEnumerable<ClassifiedText> parts,
		   IClassificationFormatMap formatMap,
		   IClassificationTypeRegistryService typeMap,
		   Action<Run, ClassifiedText, int> runCallback = null)
		{
			var inlines = new List<Inline> ();

			var position = 0;
			foreach (var part in parts) {
				var run = part.ToRun (formatMap, typeMap);
				runCallback?.Invoke (run, part, position);
				inlines.Add (run);

				position += part.Text.Length;
			}

			return inlines;
		}

		public static TextBlock ToTextBlock (
			this IEnumerable<ClassifiedText> parts,
			IClassificationFormatMap formatMap,
			IClassificationTypeRegistryService typeMap)
		{
			var inlines = parts.ToInlines (formatMap, typeMap);
			return inlines.ToTextBlock (formatMap);
		}

		public static TextBlock ToTextBlock (
			this IEnumerable<Inline> inlines,
			IClassificationFormatMap formatMap,
			bool wrap = true)
		{
			var textBlock = new TextBlock {
				TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
				TextTrimming = wrap ? TextTrimming.None : TextTrimming.CharacterEllipsis
			};

			textBlock.SetDefaultTextProperties (formatMap);
			textBlock.Inlines.AddRange (inlines);

			return textBlock;
		}
	}
}