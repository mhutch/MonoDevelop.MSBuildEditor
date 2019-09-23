// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

using Microsoft.VisualStudio.Shell.TableControl;
using Microsoft.VisualStudio.Shell.TableManager;

using MonoDevelop.MSBuild.Editor.Host;
using MonoDevelop.MSBuild.Language;

namespace MonoDevelop.MSBuild.Editor.VisualStudio.FindReferences
{
	internal partial class StreamingFindUsagesPresenter
	{
		private class FoundReferenceEntry : Entry
		{
			private readonly TableDataSourceFindUsagesContext _context;

			protected FoundReference Reference { get;  }

			public FoundReferenceEntry (
				TableDataSourceFindUsagesContext context,
				FoundReference reference)
			{
				_context = context;
				Reference = reference;
			}

			protected StreamingFindUsagesPresenter Presenter => _context.Presenter;

			protected override object GetValueWorker (string keyName)
			{
				switch (keyName) {
				case FindUsagesValueUsageInfoColumnDefinition.ColumnName:
					return Reference.Usage.ToString ();
				case StandardTableKeyNames.DocumentName:
					return Reference.FilePath;
				case StandardTableKeyNames.Line:
					return Reference.StartLine;
				case StandardTableKeyNames.Column:
					return Reference.StartCol;
				case StandardTableKeyNames.Text:
					return Reference.ClassifiedSpans.JoinText ().Trim ();
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

			protected IList<System.Windows.Documents.Inline> CreateLineTextInlines ()
			{
				var propertyId = Reference.Usage == ReferenceUsage.Declaration
					? Xml.Editor.Tags.DefinitionHighlightTag.TagId
					: Reference.Usage == ReferenceUsage.Write
						? Xml.Editor.Tags.WrittenReferenceHighlightTag.TagId
						: Xml.Editor.Tags.ReferenceHighlightTag.TagId;

				var properties = Presenter.FormatMapService
										  .GetEditorFormatMap ("text")
										  .GetProperties (propertyId);
				var highlightBrush = properties["Background"] as Brush;

				var inlines = Reference.ClassifiedSpans.ToInlines (
					Presenter.ClassificationFormatMap,
					Presenter.ClassificationTypeRegistry,
					runCallback: (run, classifiedText, position) => {
						if (highlightBrush != null) {
							if (position == Reference.Highlight.Start) {
								run.SetValue (
									System.Windows.Documents.TextElement.BackgroundProperty,
									highlightBrush);
							}
						}
					});

				return inlines;
			}
		}
	}
}