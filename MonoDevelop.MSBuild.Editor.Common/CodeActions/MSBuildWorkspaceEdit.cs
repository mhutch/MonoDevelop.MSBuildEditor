// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace MonoDevelop.MSBuild.Editor.CodeActions
{
	class MSBuildWorkspaceEdit (IEnumerable<MSBuildWorkspaceEditOperation>? operations = null)
	{
		public IList<MSBuildWorkspaceEditOperation> Operations => new List<MSBuildWorkspaceEditOperation> (operations ?? []);

		public string? FocusFile { get; set; }
	}
}