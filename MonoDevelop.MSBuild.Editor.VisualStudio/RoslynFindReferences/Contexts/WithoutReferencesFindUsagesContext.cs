// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell.FindAllReferences;
using MonoDevelop.MSBuild.Editor.Host;

namespace MonoDevelop.MSBuild.Editor.VisualStudio.FindReferences
{
	internal partial class StreamingFindUsagesPresenter
	{
		/// <summary>
		/// Context to be used for FindImplementations/GoToDef (as opposed to FindReferences).
		/// This context will not group entries by definition, and will instead just create
		/// entries for the definitions themselves.
		/// </summary>
		private class WithoutReferencesFindUsagesContext : AbstractTableDataSourceFindUsagesContext
		{
			public WithoutReferencesFindUsagesContext (
				StreamingFindUsagesPresenter presenter,
				IFindAllReferencesWindow findReferencesWindow,
				string referenceName,
				ImmutableArray<AbstractFindUsagesCustomColumnDefinition> customColumns)
				: base (presenter, findReferencesWindow, referenceName, customColumns)
			{
			}

			// We should never be called in a context where we get references.
			protected override Task OnReferenceFoundWorkerAsync (FoundReference reference)
				=> throw new InvalidOperationException ();

			// Nothing to do on completion.
			protected override Task OnCompletedAsyncWorkerAsync ()
				=> Task.CompletedTask;

			protected override Task OnDefinitionFoundWorkerAsync (FoundReference definition)
			{
				RoslynDefinitionBucket definitionBucket = GetOrCreateDefinitionBucket (definition);

				var entry = new DefinitionItemEntry (this, definitionBucket, definition);

				if (entry != null) {
					lock (Gate) {
						EntriesWhenGroupingByDefinition = EntriesWhenGroupingByDefinition.Add (entry);
						EntriesWhenNotGroupingByDefinition = EntriesWhenNotGroupingByDefinition.Add (entry);
					}

					NotifyChange ();
				}

				return Task.CompletedTask;
			}
		}
	}
}