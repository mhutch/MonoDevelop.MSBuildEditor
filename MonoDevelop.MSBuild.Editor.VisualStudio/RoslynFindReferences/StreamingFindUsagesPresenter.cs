// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.FindAllReferences;
using Microsoft.VisualStudio.Shell.TableControl;
using Microsoft.VisualStudio.Text.Classification;
using MonoDevelop.MSBuild.Editor.Host;

namespace MonoDevelop.MSBuild.Editor.VisualStudio.FindReferences
{
	[Export (typeof (IStreamingFindReferencesPresenter)), Shared]
	internal partial class StreamingFindUsagesPresenter : IStreamingFindReferencesPresenter
	{
		public const string RoslynFindUsagesTableDataSourceIdentifier =
			nameof (RoslynFindUsagesTableDataSourceIdentifier);

		public const string RoslynFindUsagesTableDataSourceSourceTypeIdentifier =
			nameof (RoslynFindUsagesTableDataSourceSourceTypeIdentifier);

		private readonly IServiceProvider _serviceProvider;

		public readonly IEditorFormatMapService FormatMapService;
		public readonly IClassificationFormatMap ClassificationFormatMap;
		public readonly IClassificationTypeRegistryService ClassificationTypeRegistry;

		private readonly HashSet<TableDataSourceFindUsagesContext> _currentContexts =
			new HashSet<TableDataSourceFindUsagesContext> ();
		private readonly ImmutableArray<AbstractFindUsagesCustomColumnDefinition> _customColumns;

		public IMSBuildEditorHost Host { get; private set; }

		[ImportingConstructor]
		public StreamingFindUsagesPresenter (
			IMSBuildEditorHost host,
			SVsServiceProvider serviceProvider,
			IEditorFormatMapService formatMapService,
			IClassificationFormatMapService classificationFormatMapService,
			IClassificationTypeRegistryService classificationTypeRegistry,
			[ImportMany]IEnumerable<Lazy<ITableColumnDefinition, NameMetadata>> columns)
			: this (
				  host,
				   serviceProvider,
				   formatMapService,
				   classificationFormatMapService,
				   classificationTypeRegistry,
				   columns.Where (c => c.Metadata.Name == FindUsagesValueUsageInfoColumnDefinition.ColumnName).Select (c => c.Value))
		{
		}

		// Test only
		public StreamingFindUsagesPresenter (
			ExportProvider exportProvider)
			: this (
				  exportProvider.GetExportedValue<IMSBuildEditorHost> (),
				  exportProvider.GetExportedValue<SVsServiceProvider> (),
				  exportProvider.GetExportedValue<IEditorFormatMapService> (),
				  exportProvider.GetExportedValue<IClassificationFormatMapService> (),
				  exportProvider.GetExportedValue<IClassificationTypeRegistryService> (),
				  exportProvider.GetExportedValues<ITableColumnDefinition> ())
		{
		}

		private StreamingFindUsagesPresenter (
			IMSBuildEditorHost host,
			Microsoft.VisualStudio.Shell.SVsServiceProvider serviceProvider,
			IEditorFormatMapService formatMapService,
			IClassificationFormatMapService classificationFormatMapService,
			IClassificationTypeRegistryService classificationTypeRegistry,
			IEnumerable<ITableColumnDefinition> columns)
		{
			Host = host;
			_serviceProvider = serviceProvider;
			FormatMapService = formatMapService;
			ClassificationFormatMap = classificationFormatMapService.GetClassificationFormatMap ("tooltip");
			ClassificationTypeRegistry = classificationTypeRegistry;

			_customColumns = columns.OfType<AbstractFindUsagesCustomColumnDefinition> ().ToImmutableArray ();
		}

		public void ClearAll ()
		{
			ThreadHelper.ThrowIfNotOnUIThread ();

			foreach (var context in _currentContexts) {
				context.Clear ();
			}
		}

		public FindReferencesContext StartSearch (string title, string referenceName, bool supportsReferences)
		{
			ThreadHelper.ThrowIfNotOnUIThread ();

			var context = StartSearchWorker (title, referenceName, supportsReferences);

			// Keep track of this context object as long as it is being displayed in the UI.
			// That way we can Clear it out if requested by a client.  When the context is
			// no longer being displayed, VS will dispose it and it will remove itself from
			// this set.
			_currentContexts.Add (context);
			return context;
		}

		private TableDataSourceFindUsagesContext StartSearchWorker (string title, string referenceName, bool showUsage)
		{
			ThreadHelper.ThrowIfNotOnUIThread ();

			var vsFindAllReferencesService = (IFindAllReferencesService)_serviceProvider.GetService (typeof (SVsFindAllReferences));

			// Get the appropriate window for FAR results to go into.
			var window = vsFindAllReferencesService.StartSearch (title);

			// Keep track of the users preference for grouping by definition if we don't already know it.
			// We need this because we disable the Definition column when we're not showing references
			// (i.e. GoToImplementation/GoToDef).  However, we want to restore the user's choice if they
			// then do another FindAllReferences.
			var desiredGroupingPriority = MSBuildOptions.DefinitionGroupingPriority;
			if (desiredGroupingPriority < 0) {
				StoreCurrentGroupingPriority (window);
			}

			return StartSearchWithoutReferences (window, referenceName, showUsage);
		}

		private TableDataSourceFindUsagesContext StartSearchWithoutReferences (IFindAllReferencesWindow window, string referenceName, bool showUsage)
		{
			ThreadHelper.ThrowIfNotOnUIThread ();

			// If we're not showing references, then disable grouping by definition, as that will
			// just lead to a poor experience.  i.e. we'll have the definition entry buckets, 
			// with the same items showing underneath them.
			SetDefinitionGroupingPriority (window, 0);
			return new TableDataSourceFindUsagesContext (this, window, referenceName, showUsage? _customColumns : ImmutableArray<AbstractFindUsagesCustomColumnDefinition>.Empty);
		}

		private void StoreCurrentGroupingPriority (IFindAllReferencesWindow window)
		{
			var definitionColumn = window.GetDefinitionColumn ();
			MSBuildOptions.DefinitionGroupingPriority = definitionColumn.GroupingPriority;
		}

		private void SetDefinitionGroupingPriority (IFindAllReferencesWindow window, int priority)
		{
			ThreadHelper.ThrowIfNotOnUIThread ();

			var newColumns = new List<ColumnState> ();
			var tableControl = (IWpfTableControl2)window.TableControl;

			foreach (var columnState in window.TableControl.ColumnStates) {
				var columnState2 = columnState as ColumnState2;
				if (columnState?.Name == StandardTableColumnDefinitions2.Definition) {
					newColumns.Add (new ColumnState2 (
						columnState2.Name,
						isVisible: false,
						width: columnState2.Width,
						sortPriority: columnState2.SortPriority,
						descendingSort: columnState2.DescendingSort,
						groupingPriority: priority));
				} else {
					newColumns.Add (columnState);
				}
			}

			tableControl.SetColumnStates (newColumns);
		}
	}
}