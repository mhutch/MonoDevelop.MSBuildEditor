// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading;

using Microsoft.VisualStudio.Text;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuild.Analysis
{
	class EditTextActionOperation : MSBuildActionOperation
	{
		readonly List<Edit> edits = new List<Edit> ();

		public EditTextActionOperation ()
		{
		}

		public sealed override void Apply (ITextBuffer document, CancellationToken cancellationToken)
		{
			using var edit = document.CreateEdit ();
			foreach (var change in edits) {
				switch (change.Kind) {
				case Kind.Insert:
					edit.Insert (change.Offset, change.Text);
					break;
				case Kind.Replace:
					edit.Replace (change.Offset, change.Length, change.Text);
					break;
				case Kind.Delete:
					edit.Delete (change.Offset, change.Length);
					break;
				}
			}
			edit.Apply ();
		}
		EditTextActionOperation WithEdit (Edit e)
		{
			edits.Add (e);
			return this;
		}

		public EditTextActionOperation Insert (int offset, string text) => WithEdit (new Edit (Kind.Insert, offset, 0, text));
		public EditTextActionOperation Replace (int offset, int length, string text) => WithEdit (new Edit (Kind.Replace, offset, length, text));
		public EditTextActionOperation Replace (TextSpan span, string text) => WithEdit (new Edit (Kind.Replace, span.Start, span.Length, text));
		public EditTextActionOperation Delete (int offset, int length) => WithEdit (new Edit (Kind.Delete, offset, length, null));
		public EditTextActionOperation DeleteBetween (int start, int end) => WithEdit (new Edit (Kind.Delete, start, end - start, null));

		readonly struct Edit
		{
			public readonly Kind Kind;
			public readonly int Offset, Length;
			public readonly string Text;

			public Edit (Kind kind, int offset, int length, string text)
			{
				Kind = kind;
				Offset = offset;
				Length = length;
				Text = text;
			}
		}

		enum Kind
		{
			Insert,
			Replace,
			Delete,
		}

		public EditTextActionOperation RenameElement (XElement element, string newName)
			=> Replace (element.NameSpan, newName)
				.Replace (element.ClosingTag.Span.Start + 2, element.Name.Length, newName);
	}
}