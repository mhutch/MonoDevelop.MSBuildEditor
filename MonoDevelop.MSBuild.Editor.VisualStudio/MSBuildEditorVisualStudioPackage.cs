// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices;
using System.Threading;

using Microsoft.VisualStudio.Shell;

using Task = System.Threading.Tasks.Task;

namespace MonoDevelop.MSBuild.Editor.VisualStudio
{

	[InstalledProductRegistration(Consts.PackageName, Consts.PackageDescription, Consts.PackageVersion)]
	[PackageRegistration (UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
	[Guid (Consts.PackageGuid)]

	[ProvideLanguageService (
		typeof (MSBuildLanguageService),
		Consts.LanguageServiceName, 101,
		ShowDropDownOptions = false,
		RequestStockColors = true,
		DefaultToInsertSpaces = true,
		ShowSmartIndent = true
		)]

	// dunno why this isn't part of ProvideLanguageService 🤷‍
	[SetRegistrationOption (Consts.LanguageServiceKey, "ShowBraceCompletion", 1)]

	[ProvideLanguageExtension (typeof (MSBuildLanguageService), MSBuildFileExtension.targets)]
	[ProvideLanguageExtension (typeof (MSBuildLanguageService), MSBuildFileExtension.props)]
	[ProvideLanguageExtension (typeof (MSBuildLanguageService), MSBuildFileExtension.tasks)]
	[ProvideLanguageExtension (typeof (MSBuildLanguageService), MSBuildFileExtension.overridetasks)]
	[ProvideLanguageExtension (typeof (MSBuildLanguageService), MSBuildFileExtension.csproj)]
	[ProvideLanguageExtension (typeof (MSBuildLanguageService), MSBuildFileExtension.vbproj)]
	[ProvideLanguageExtension (typeof (MSBuildLanguageService), MSBuildFileExtension.fsproj)]
	[ProvideLanguageExtension (typeof (MSBuildLanguageService), MSBuildFileExtension.xproj)]
	[ProvideLanguageExtension (typeof (MSBuildLanguageService), MSBuildFileExtension.vcxproj)]
	[ProvideLanguageExtension (typeof (MSBuildLanguageService), MSBuildFileExtension.proj)]
	[ProvideLanguageExtension (typeof (MSBuildLanguageService), MSBuildFileExtension.user)]

	public sealed class MSBuildEditorVisualStudioPackage : AsyncPackage
	{

		protected override Task InitializeAsync (CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
		{
			return base.InitializeAsync (cancellationToken, progress);
		}
	}
}
