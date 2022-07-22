// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices;

using Microsoft.VisualStudio.Package;
using Microsoft.VisualStudio.TextManager.Interop;

namespace MonoDevelop.MSBuild.Editor.VisualStudio
{
	[Guid (Consts.LanguageServiceGuid)]
	public class MSBuildLanguageService : LanguageService
	{
		public override string Name => MSBuildContentType.Name;

		public MSBuildLanguageService (object site)
		{
			Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread ();
			SetSite (site);
		}

		LanguagePreferences preferences;

		public override LanguagePreferences GetLanguagePreferences ()
		{
			if (preferences == null) {
				preferences = new LanguagePreferences (Site, GetLanguageServiceGuid (), Name);
				if (preferences != null) {
					preferences.Init ();

					preferences.EnableMatchBraces = true;
					preferences.EnableShowMatchingBrace = true;
					preferences.EnableMatchBracesAtCaret = true;
					preferences.HighlightMatchingBraceFlags = _HighlightMatchingBraceFlags.HMB_USERECTANGLEBRACES;

					preferences.EnableCodeSense = true;
					preferences.EnableAsyncCompletion = true;
					preferences.EnableQuickInfo = true;

					preferences.AutoOutlining = true;
					preferences.ShowNavigationBar = false;

					preferences.IndentSize = 2;
					preferences.IndentStyle = IndentingStyle.Smart;
					preferences.TabSize = 2;
					preferences.InsertTabs = false;

					preferences.WordWrap = false;
					preferences.WordWrapGlyphs = true;
				}
			}
			return preferences;
		}

		public override IScanner GetScanner (IVsTextLines buffer) => null;

		public override AuthoringScope ParseSource (ParseRequest req) => null;

		public override string GetFormatFilterList ()
			=> "MSBuild targets (*.targets)|*.targets|MSBuild properties (*.props)|*.props";

		public override void Dispose ()
		{
			preferences?.Dispose ();
			preferences = null;

			base.Dispose ();
		}
	}
}