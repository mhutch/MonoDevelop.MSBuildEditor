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
	[SetRegistrationOptionAttribute (LanguageServiceKey, "ShowBraceCompletion", 1)]
	public sealed class MSBuildEditorVisualStudioPackage : AsyncPackage
	{
		public const string PackageGuidString = "6c7bd60d-5321-4fb0-8684-9736003d64ad";
		public const string LanguageServiceName = MSBuildContentType.Name;
		public const string LanguageServiceKey = @"Languages\Language Services\" + LanguageServiceName;

		protected override Task InitializeAsync (CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
			=> Task.CompletedTask;
	}
}
