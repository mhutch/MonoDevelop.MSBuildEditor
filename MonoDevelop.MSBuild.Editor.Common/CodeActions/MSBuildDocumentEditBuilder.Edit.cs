// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

using TextSpan = MonoDevelop.Xml.Dom.TextSpan;

namespace MonoDevelop.MSBuild.Editor.CodeActions;

partial class MSBuildDocumentEditBuilder
{
	[DebuggerDisplay ("{Kind}@{Span}:'{Text}'")]
	struct Edit
	{
		public TextSpan Span { get; }
		public string? Text { get; internal set; }
		public TextSpan[]? RelativeSelections { get; }
		public int? BaseIndentDepth { get; }

		public Edit (TextSpan span, string? text = null, TextSpan[]? relativeSelections = null, int? baseIndentDepth = null)
		{
			Span = span;
			Text = text;
			RelativeSelections = relativeSelections;
			BaseIndentDepth = baseIndentDepth;
		}

		public static Edit WithMarkedSelection (TextSpan span, string textWithMarkers, char selectionMarker, int? baseIndentDepth = null)
		{
			(var text, var relativeSelections) = ExtractSpans (textWithMarkers, selectionMarker);

			return new Edit (span, text, relativeSelections.ToArray (), baseIndentDepth);
		}

		static (string text, List<TextSpan> selections) ExtractSpans (string textWithMarkers, char selectionMarker)
		{
			var spans = new List<TextSpan> ();

			int spanStart = -1;
			var cleanTextBuilder = new StringBuilder (textWithMarkers.Length);

			for (int i = 0; i < textWithMarkers.Length; i++) {
				char c = textWithMarkers[i];
				if (c == selectionMarker) {
					if (spanStart < 0) {
						spanStart = cleanTextBuilder.Length;
					} else {
						spans.Add (TextSpan.FromBounds (spanStart, cleanTextBuilder.Length));
						spanStart = -1;
					}
				} else {
					cleanTextBuilder.Append (c);
				}
			}

			if (spanStart > -1) {
				throw new ArgumentException ("Odd number of markers");
			}

			return new (cleanTextBuilder.ToString (), spans);
		}
	}
}
