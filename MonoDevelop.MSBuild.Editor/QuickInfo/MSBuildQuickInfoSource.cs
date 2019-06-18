// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Language.Intellisense;

namespace MonoDevelop.MSBuild.Editor.QuickInfo
{
	class MSBuildQuickInfoSource : IAsyncQuickInfoSource
	{
		public Task<QuickInfoItem> GetQuickInfoItemAsync (IAsyncQuickInfoSession session, CancellationToken cancellationToken)
		{
			throw new System.NotImplementedException ();
		}

		public void Dispose ()
		{
			throw new System.NotImplementedException ();
		}
	}
}