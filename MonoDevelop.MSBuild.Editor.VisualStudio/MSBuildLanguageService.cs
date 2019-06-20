// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.Package;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TextManager.Interop;
using System;
using System.Runtime.InteropServices;

namespace MonoDevelop.MSBuild.Editor.VisualStudio
{
	[Guid (MSBuildLanguageGuid)]
	public class MSBuildLanguageService : LanguageService
	{
		public const string MSBuildLanguageGuid = "111e2ecb-9e5f-4945-9d21-d4e5368d620b";

		LanguagePreferences preferences;

		public override string Name => "MSBuild";

		public MSBuildLanguageService (object site)
		{
			ThreadHelper.ThrowIfNotOnUIThread ();
			SetSite (site);
		}

		public override AuthoringScope ParseSource (ParseRequest req) => null;

		public override LanguagePreferences GetLanguagePreferences () =>
			preferences ?? (preferences = new LanguagePreferences (Site, GetLanguageServiceGuid(), Name)  {
				IndentSize = 2,
				InsertTabs = false,
				EnableAsyncCompletion = true,
				EnableQuickInfo = true,
				ShowNavigationBar = false,
				LineNumbers = true,
				WordWrap = true,
				WordWrapGlyphs = true
			});

		public override IScanner GetScanner (IVsTextLines buffer) => null;

		public override string GetFormatFilterList ()
			=> "MSBuild targets (*.targets)|*.targets|MSBuild properties (*.props)|*.props";

		public override void Dispose ()
		{
			base.Dispose ();
			preferences?.Dispose ();
			preferences = null;
		}
	}
}