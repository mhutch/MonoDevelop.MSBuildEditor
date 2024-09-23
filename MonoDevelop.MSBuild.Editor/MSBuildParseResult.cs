// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

using Microsoft.VisualStudio.Text;

using MonoDevelop.MSBuild.Analysis;
using MonoDevelop.MSBuild.Language;

namespace MonoDevelop.MSBuild.Editor.Completion
{
	class MSBuildParseResult
	{
		public MSBuildParseResult (MSBuildRootDocument msbuildDocument, ITextSnapshot snapshot)
		{
			MSBuildDocument = msbuildDocument;
			Snapshot = snapshot;
		}

		public MSBuildRootDocument MSBuildDocument { get; }

		public ITextSnapshot Snapshot { get; set; }
	}
}