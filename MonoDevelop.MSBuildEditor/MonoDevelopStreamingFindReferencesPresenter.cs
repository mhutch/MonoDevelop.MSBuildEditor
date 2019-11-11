// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel.Composition;
using MonoDevelop.MSBuild.Editor.Host;

namespace MonoDevelop.MSBuildEditor
{
	[Export (typeof (IStreamingFindReferencesPresenter))]
	class MonoDevelopStreamingFindReferencesPresenter : IStreamingFindReferencesPresenter
	{
		public void ClearAll ()
		{
			throw new NotImplementedException ();
		}

		public FindReferencesContext StartSearch (string title, string referenceName, bool showUsage)
		{
			throw new NotImplementedException ();
		}
	}
}
