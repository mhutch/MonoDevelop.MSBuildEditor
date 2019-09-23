// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Windows;

namespace MonoDevelop.MSBuild.Editor.VisualStudio.FindReferences
{
	internal partial class StreamingFindUsagesPresenter
	{
		/// <summary>
		/// Represents a single entry (i.e. row) in the ungrouped FAR table.
		/// </summary>
		private abstract class Entry
		{
			protected Entry ()
			{
			}

			public bool TryGetValue (string keyName, out object content)
			{
				content = GetValueWorker (keyName);
				return content != null;
			}

			protected abstract object GetValueWorker (string keyName);

			public virtual bool TryCreateColumnContent (string columnName, out FrameworkElement content)
			{
				content = null;
				return false;
			}
		}
	}
}