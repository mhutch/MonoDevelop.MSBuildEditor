// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// stubs to help imported files work w/o bringing in too many dependencies

using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Test.Utilities;
using Microsoft.VisualStudio.Composition;

namespace Microsoft.CodeAnalysis.Test.Utilities
{
	class EditorTestWorkspace
	{
		TestComposition composition;
		string? workspaceKind;
		WorkspaceConfigurationOptions configurationOptions;
		bool supportsLspMutation;

		public EditorTestWorkspace (TestComposition composition, string? workspaceKind, WorkspaceConfigurationOptions configurationOptions, bool supportsLspMutation)
		{
			this.composition = composition;
			this.workspaceKind = workspaceKind;
			this.configurationOptions = configurationOptions;
			this.supportsLspMutation = supportsLspMutation;
			Services = new (composition.GetHostServices ());
			ExportProvider = composition.ExportProviderFactory.CreateExportProvider ();
		}

		public ExportProvider ExportProvider { get; }
		public WorkspaceServices Services { get; internal set; }

		public record class WorkspaceServices (HostServices HostServices);
	}

	class WorkspaceConfigurationOptions
	{
		private bool enableOpeningSourceGeneratedFiles;

		public WorkspaceConfigurationOptions (bool EnableOpeningSourceGeneratedFiles)
		{
			enableOpeningSourceGeneratedFiles = EnableOpeningSourceGeneratedFiles;
		}
	}
}

namespace Microsoft.CodeAnalysis.Options
{
	interface IGlobalOptionService
	{
	}
}

namespace Microsoft.CodeAnalysis.Remote
{
	class ZZZZZ { }
}

namespace Microsoft.CodeAnalysis.UnitTests.Remote
{
	class ZZZZZ { }
}

namespace Roslyn.Test.Utilities
{
	class TestBase { }
}

namespace Microsoft.CodeAnalysis.UnitTests.Remote
{
	class TestSerializerService
	{
		[Export (typeof (Factory))]
		internal class Factory { }
	}
}

namespace Microsoft.CodeAnalysis.Remote
{
	class BrokeredServiceBase
	{
	}
}

namespace Microsoft.CodeAnalysis.Editor.UnitTests
{
	public static class EditorTestCompositions
	{
		public static TestComposition LanguageServerProtocol { get; } = TestComposition.Empty
			.AddAssemblies (typeof (Microsoft.CodeAnalysis.LanguageServer.MSBuildLanguageServer).Assembly);

		public static TestComposition LanguageServerProtocolEditorFeatures => LanguageServerProtocol;
	}
}