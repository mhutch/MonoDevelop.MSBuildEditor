// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;

using MonoDevelop.MSBuild.Language;

namespace MonoDevelop.MSBuild.Editor.Navigation
{
	class MSBuildNavigableSymbol : INavigableSymbol
	{
		readonly MSBuildNavigationService service;
		readonly MSBuildNavigationResult result;

		public MSBuildNavigableSymbol (MSBuildNavigationService service, MSBuildNavigationResult result, ITextSnapshot snapshot)
		{
			this.service = service;
			this.result = result;
			SymbolSpan = new SnapshotSpan (snapshot, result.Offset, result.Length);
		}

		public SnapshotSpan SymbolSpan { get; }

		public IEnumerable<INavigableRelationship> Relationships { get; } = new[] { PredefinedNavigableRelationships.Definition };

		public void Navigate (INavigableRelationship relationship)
		{
			service.Navigate (result, SymbolSpan.Snapshot.TextBuffer);
		}
	}
}
