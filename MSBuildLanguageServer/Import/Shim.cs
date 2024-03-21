// stubs to help imported files work w/o bringing in too many dependencies

using System.Composition;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Host
{
	struct HostServices
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
		CSharpVisualBasicLspServer = MSBuild
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

namespace Microsoft.CodeAnalysis.LanguageServer
{
	[Export(typeof(HostServicesProvider)), Shared]
	class HostServicesProvider
	{
		public Host.HostServices HostServices => new ();
	}
}