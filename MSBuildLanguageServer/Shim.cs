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
		Any
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