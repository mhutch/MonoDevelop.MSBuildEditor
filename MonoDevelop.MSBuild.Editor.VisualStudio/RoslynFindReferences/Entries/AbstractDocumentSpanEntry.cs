// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Windows;
using System.Windows.Documents;

using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Shell.TableControl;
using Microsoft.VisualStudio.Shell.TableManager;

using MonoDevelop.MSBuild.Editor.Host;

namespace MonoDevelop.MSBuild.Editor.VisualStudio.FindReferences
{
	internal partial class StreamingFindUsagesPresenter
	{
		/// <summary>
		/// Base type of all <see cref="Entry"/>s that represent some source location in 
		/// a <see cref="CodeAnalysis.Document"/>.  Navigation to that location is provided by this type.
		/// Subclasses can be used to provide customized line text to display in the entry.
		/// </summary>
		private abstract class AbstractDocumentSpanEntry : Entry
		{
			private readonly AbstractTableDataSourceFindUsagesContext _context;

			protected SourceLocation Span { get;  }

			protected AbstractDocumentSpanEntry (
				AbstractTableDataSourceFindUsagesContext context,
				RoslynDefinitionBucket definitionBucket,
				SourceLocation span)
				: base (definitionBucket)
			{
				_context = context;
				Span = span;
			}

			protected StreamingFindUsagesPresenter Presenter => _context.Presenter;

			protected override object GetValueWorker (string keyName)
			{
				switch (keyName) {
				case StandardTableKeyNames.DocumentName:
					return Span.FilePath;
				case StandardTableKeyNames.Line:
					return Span.StartLine;
				case StandardTableKeyNames.Column:
					return Span.StartCol;
				case StandardTableKeyNames.Text:
					return Span.LineText.ToString ().Trim ();
				}

				return null;
			}

			public override bool TryCreateColumnContent (string columnName, out FrameworkElement content)
			{
				if (columnName == StandardTableColumnDefinitions2.LineText) {
					var inlines = CreateLineTextInlines ();
					var textBlock = inlines.ToTextBlock (Presenter.ClassificationFormatMap, wrap: false);

					content = textBlock;
					return true;
				}

				content = null;
				return false;
			}

			protected abstract IList<Inline> CreateLineTextInlines ();

			public static SourceText GetLineContainingPosition (SourceText text, int position)
			{
				var line = text.Lines.GetLineFromPosition (position);

				return text.GetSubText (line.Span);
			}
		}
	}
}