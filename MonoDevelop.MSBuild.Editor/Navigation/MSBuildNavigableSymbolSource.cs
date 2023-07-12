// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;

using MonoDevelop.Xml.Editor.Logging;
using MonoDevelop.Xml.Logging;

namespace MonoDevelop.MSBuild.Editor.Navigation
{
	class MSBuildNavigableSymbolSource : INavigableSymbolSource
	{
		readonly ITextBuffer buffer;
		readonly MSBuildNavigableSymbolSourceProvider provider;

		public MSBuildNavigableSymbolSource (ITextBuffer buffer, MSBuildNavigableSymbolSourceProvider provider)
		{
			this.buffer = buffer;
			this.provider = provider;
		}

		public Task<INavigableSymbol> GetNavigableSymbolAsync (SnapshotSpan triggerSpan, CancellationToken token)
			=> provider.LoggerFactory.GetLogger<MSBuildNavigableSymbolSource> (buffer).InvokeAndLogExceptions (() => GetNavigableSymbolAsyncInternal (triggerSpan, token));

		public Task<INavigableSymbol> GetNavigableSymbolAsyncInternal (SnapshotSpan triggerSpan, CancellationToken token)
		{
			var point = triggerSpan.Start;
			var navResult = provider.NavigationService.GetNavigationResult (buffer, point);
			if (navResult != null) {
				return Task.FromResult<INavigableSymbol> (new MSBuildNavigableSymbol (provider.NavigationService, navResult, point.Snapshot));
			}
			return Task.FromResult<INavigableSymbol> (null);
		}

		public void Dispose ()
		{
		}
	}
}
