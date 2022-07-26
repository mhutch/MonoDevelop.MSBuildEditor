// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices;

using Microsoft.VisualStudio.Package;
using Microsoft.VisualStudio.TextManager.Interop;

using Community.VisualStudio.Toolkit;

namespace MonoDevelop.MSBuild.Editor.VisualStudio
{
	[ComVisible (true)]
	[Guid (PackageConsts.LanguageServiceGuid)]
	public class MSBuildLanguageService : LanguageBase
	{
		public override string Name => PackageConsts.LanguageServiceName;

		public override string[] FileExtensions { get; } = new[] {
			MSBuildFileExtension.targets,
			MSBuildFileExtension.props,
			MSBuildFileExtension.tasks,
			MSBuildFileExtension.overridetasks,
			MSBuildFileExtension.csproj,
			MSBuildFileExtension.vbproj,
			MSBuildFileExtension.fsproj,
			MSBuildFileExtension.xproj,
			MSBuildFileExtension.vcxproj,
			MSBuildFileExtension.proj,
			MSBuildFileExtension.user
		};

		public MSBuildLanguageService (object site) : base (site) { }

		public override void SetDefaultPreferences (LanguagePreferences preferences)
		{
			preferences.EnableMatchBraces = true;
			preferences.EnableShowMatchingBrace = true;
			preferences.EnableMatchBracesAtCaret = true;
			preferences.HighlightMatchingBraceFlags = _HighlightMatchingBraceFlags.HMB_USERECTANGLEBRACES;

			preferences.EnableCodeSense = false;
			preferences.EnableAsyncCompletion = true;
			preferences.EnableQuickInfo = true;

			preferences.AutoOutlining = false;
			preferences.ShowNavigationBar = false;

			preferences.IndentSize = 2;
			preferences.IndentStyle = IndentingStyle.Smart;
			preferences.TabSize = 2;
			preferences.InsertTabs = false;

			preferences.WordWrap = false;
			preferences.WordWrapGlyphs = true;
		}
	}
}