// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Windows.Documents;
using Microsoft.CodeAnalysis.Editor.Shared.Extensions;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Text;
using MonoDevelop.MSBuild.Editor.Host;

namespace MonoDevelop.MSBuild.Editor.VisualStudio.FindReferences
{
	internal partial class StreamingFindUsagesPresenter
	{
		/// <summary>
		/// Shows a DefinitionItem as a Row in the FindReferencesWindow.  Only used for
		/// GoToDefinition/FindImplementations.  In these operations, we don't want to 
		/// create a DefinitionBucket.  So we instead just so the symbol as a normal row.
		/// </summary>
		private class DefinitionItemEntry : AbstractDocumentSpanEntry
		{
			public DefinitionItemEntry (
				AbstractTableDataSourceFindUsagesContext context,
				RoslynDefinitionBucket definitionBucket,
				SourceLocation span)
				: base (context, definitionBucket, span)
			{
			}

			protected override IList<Inline> CreateLineTextInlines ()
				=> DefinitionBucket.DefinitionItem.DisplayParts.ToInlines (Presenter.ClassificationFormatMap);
		}
	}
}