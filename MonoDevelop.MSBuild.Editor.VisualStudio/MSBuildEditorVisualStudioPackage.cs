// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices;
using System.Threading;

using Microsoft.VisualStudio.Shell;

using Task = System.Threading.Tasks.Task;

namespace MonoDevelop.MSBuild.Editor.VisualStudio
{
	[PackageRegistration (UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
	[Guid (PackageGuidString)]
	[ProvideLanguageService (
		typeof (MSBuildLanguageService),
		LanguageServiceName, 101,
		ShowDropDownOptions = false,
		RequestStockColors = true,
		DefaultToInsertSpaces = true,
		ShowSmartIndent = true
		)]
	// dunno why this isn't part of ProvideLanguageService 🤷‍
	[SetRegistrationOption (LanguageServiceKey, "ShowBraceCompletion", 1)]
	[ProvideLanguageExtension (typeof (MSBuildLanguageService), ".targets")]
	[ProvideLanguageExtension (typeof (MSBuildLanguageService), ".props")]
	[ProvideLanguageExtension (typeof (MSBuildLanguageService), ".tasks")]
	[ProvideLanguageExtension (typeof (MSBuildLanguageService), ".overridetasks")]
	[ProvideLanguageExtension (typeof (MSBuildLanguageService), ".csproj")]
	[ProvideLanguageExtension (typeof (MSBuildLanguageService), ".vbproj")]
	[ProvideLanguageExtension (typeof (MSBuildLanguageService), ".fsproj")]
	[ProvideLanguageExtension (typeof (MSBuildLanguageService), ".xproj")]
	[ProvideLanguageExtension (typeof (MSBuildLanguageService), ".vcxproj")]
	[ProvideLanguageExtension (typeof (MSBuildLanguageService), ".proj")]
	[ProvideLanguageExtension (typeof (MSBuildLanguageService), ".user")]
	public sealed class MSBuildEditorVisualStudioPackage : AsyncPackage
	{
		public const string PackageGuidString = "6c7bd60d-5321-4fb0-8684-9736003d64ad";
		public const string LanguageServiceName = MSBuildContentType.Name;
		public const string LanguageServiceKey = @"Languages\Language Services\" + LanguageServiceName;

		protected override Task InitializeAsync (CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
			=> Task.CompletedTask;
	}
}
