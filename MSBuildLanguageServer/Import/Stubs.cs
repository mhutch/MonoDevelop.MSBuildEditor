// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// stubs to help imported files work w/o bringing in too many dependencies

using System.Composition;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Host
{
	public abstract class HostServices
	{
		protected internal abstract HostWorkspaceServices CreateWorkspaceServices (Workspace workspace);
	}
}

namespace Microsoft.CodeAnalysis.Host.Mef
{
	class MefWorkspaceServices : HostWorkspaceServices
	{
		public MefWorkspaceServices (IMefHostExportProvider host, Workspace workspace)
		{
		}
	}
}

namespace Microsoft.CodeAnalysis.Host
{
	public abstract class HostWorkspaceServices
	{
	}
}

namespace Microsoft.CodeAnalysis
{
	public class Workspace
	{
	}
}


namespace Microsoft.CodeAnalysis.LanguageServer
{
	enum WellKnownLspServerKinds
	{
		MSBuild,
		Any,

		// some imported classes use this, alias it to our MSBuild value
		CSharpVisualBasicLspServer = MSBuild,

		// used by AbstractLanguageServerProtocolTests
		AlwaysActiveVSLspServer
	}

	static class WellKnownLspServerKindExtensions
	{
		public static string ToTelemetryString(this WellKnownLspServerKinds serverKind)
			=> serverKind switch
			{
				WellKnownLspServerKinds.MSBuild => "MSBuild",
				_ => throw ExceptionUtilities.UnexpectedValue(serverKind),
			};
	}
}

// Logger.cs has a Using for this namespace but doesn't actually use classes from it
namespace Microsoft.CodeAnalysis.Options {
}

namespace Microsoft.CodeAnalysis.LanguageServer
{
	interface ExperimentalCapabilitiesProvider : ICapabilitiesProvider {}
}

namespace Microsoft.CodeAnalysis.LanguageServer.StarredSuggestions
{
	class StarredCompletionAssemblyHelper
	{
		// not called unless serverConfiguration.StarredCompletionsPath is non-null
		public static string GetStarredCompletionAssemblyPath(string starredCompletionsPath)
			=> throw new NotSupportedException ();
	}

}

