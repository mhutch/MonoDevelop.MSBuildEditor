// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

// this is based on Roslyn's Microsoft.CodeAnalysis.FindUsages.FindUsagesContext

using System.Threading;
using System.Threading.Tasks;

namespace MonoDevelop.MSBuild.Editor.Host
{
	public abstract class FindReferencesContext
	{
		public virtual CancellationToken CancellationToken { get; }

		protected FindReferencesContext ()
		{
		}

		public virtual Task ReportMessageAsync (string message) => Task.CompletedTask;

		public virtual Task SetSearchTitleAsync (string title) => Task.CompletedTask;

		public virtual Task OnCompletedAsync () => Task.CompletedTask;

		public virtual Task OnReferenceFoundAsync (FoundReference reference) => Task.CompletedTask;

		public virtual Task ReportProgressAsync (int current, int maximum) => Task.CompletedTask;
	}
}