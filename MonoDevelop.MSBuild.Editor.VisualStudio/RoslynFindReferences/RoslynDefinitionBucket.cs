// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Windows;
using System.Windows.Documents;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindUsages;
using Microsoft.VisualStudio.Core.Imaging;
using Microsoft.VisualStudio.Shell.FindAllReferences;
using Microsoft.VisualStudio.Shell.TableControl;
using Microsoft.VisualStudio.Shell.TableManager;
using MonoDevelop.MSBuild.Editor.Host;

namespace MonoDevelop.MSBuild.Editor.VisualStudio.FindReferences
{
	internal partial class StreamingFindUsagesPresenter
	{
		private class RoslynDefinitionBucket : DefinitionBucket, ISupportsNavigation
		{
			private readonly StreamingFindUsagesPresenter _presenter;
			private readonly AbstractTableDataSourceFindUsagesContext _context;

			public readonly FoundReference DefinitionItem;

			public RoslynDefinitionBucket (
				StreamingFindUsagesPresenter presenter,
				AbstractTableDataSourceFindUsagesContext context,
				FoundReference definitionItem)
				: base (name: definitionItem.DisplayParts.JoinText () + " " + definitionItem.GetHashCode (),
					   sourceTypeIdentifier: context.SourceTypeIdentifier,
					   identifier: context.Identifier)
			{
				_presenter = presenter;
				_context = context;
				DefinitionItem = definitionItem;
			}

			public bool TryNavigateTo (bool isPreview)
				=> DefinitionItem.TryNavigateTo (_presenter.Host, isPreview);

			public override bool TryGetValue (string key, out object content)
			{
				content = GetValue (key);
				return content != null;
			}

			private object GetValue (string key)
			{
				switch (key) {
				case StandardTableKeyNames.Text:
				case StandardTableKeyNames.FullText:
					return DefinitionItem.DisplayParts.JoinText ();

				case StandardTableKeyNames2.TextInlines:
					var inlines = new List<Inline> { new Run (" ") };
					inlines.AddRange (DefinitionItem.DisplayParts.ToInlines (_presenter.ClassificationFormatMap));
					foreach (var inline in inlines) {
						inline.SetValue (TextElement.FontWeightProperty, FontWeights.Bold);
					}
					return inlines;

				case StandardTableKeyNames2.DefinitionIcon:
					return DefinitionItem.ImageId.ToImageMoniker ();
				}

				return null;
			}
		}
	}
}