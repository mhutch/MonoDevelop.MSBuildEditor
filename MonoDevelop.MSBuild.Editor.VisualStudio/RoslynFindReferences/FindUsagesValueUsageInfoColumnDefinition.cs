// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Linq;

using Microsoft.VisualStudio.Shell.TableControl;
using Microsoft.VisualStudio.Utilities;

using MonoDevelop.MSBuild.Language;

namespace MonoDevelop.MSBuild.Editor.VisualStudio.FindReferences
{
	/// <summary>
	/// Custom column to display the reference kind/usage info for the Find All References window.
	/// </summary>
	[Export (typeof (ITableColumnDefinition))]
	[Name (ColumnName)]
	internal sealed class FindUsagesValueUsageInfoColumnDefinition : AbstractFindUsagesCustomColumnDefinition
	{
		public const string ColumnName = nameof (ReferenceUsage);

		[ImportingConstructor]
		public FindUsagesValueUsageInfoColumnDefinition ()
		{
		}

		// Allow filtering of the column by each allowed SymbolUsageInfo kind.
		public override IEnumerable<string> FilterPresets { get; } = new[] { "Read", "Write", "Definition" };
		public override bool IsFilterable => true;

		public override string Name => ColumnName;
		public override string DisplayName => "Kind";
		public override double DefaultWidth => 100.0;

		public override string GetDisplayStringForColumnValues (ImmutableArray<string> values) => values.First ();
		protected override ImmutableArray<string> SplitColumnDisplayValue (string displayValue) => ImmutableArray.Create (displayValue);
	}
}