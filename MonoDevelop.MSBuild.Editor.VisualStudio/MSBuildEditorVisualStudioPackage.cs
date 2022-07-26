// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using System.Threading;

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

using Task = System.Threading.Tasks.Task;

namespace MonoDevelop.MSBuild.Editor.VisualStudio
{
	[InstalledProductRegistration(PackageResxId.PackageNameStr, PackageResxId.PackageDescriptionStr, PackageConsts.PackageVersion)]
	[PackageRegistration (UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
	[Guid (PackageConsts.PackageGuid)]

	[ProvideLanguageService (
		typeof (MSBuildLanguageService),
		PackageConsts.LanguageServiceName,
		PackageResxId.EditorName,
		ShowDropDownOptions = false,
		RequestStockColors = true,
		DefaultToInsertSpaces = true,
		ShowSmartIndent = true
		)]

	// dunno why this isn't part of ProvideLanguageService 🤷‍
	[SetRegistrationOption (PackageConsts.LanguageServiceKey, "ShowBraceCompletion", 1)]

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

	[ProvideEditorFactory (typeof (MSBuildLanguageService), PackageResxId.EditorName, CommonPhysicalViewAttributes = (int)__VSPHYSICALVIEWATTRIBUTES.PVA_SupportsPreview)]
	[ProvideEditorLogicalView (typeof (MSBuildLanguageService), VSConstants.LOGVIEWID.TextView_string)]
	[ProvideEditorLogicalView (typeof (MSBuildLanguageService), VSConstants.LOGVIEWID.Code_string)]
	[ProvideEditorLogicalView (typeof (MSBuildLanguageService), VSConstants.LOGVIEWID.Debugging_string)]

	[ProvideEditorExtension (typeof (MSBuildLanguageService), MSBuildFileExtension.targets, 65535)]
	[ProvideEditorExtension (typeof (MSBuildLanguageService), MSBuildFileExtension.props, 65535)]
	[ProvideEditorExtension (typeof (MSBuildLanguageService), MSBuildFileExtension.tasks, 65535)]
	[ProvideEditorExtension (typeof (MSBuildLanguageService), MSBuildFileExtension.overridetasks, 65535)]
	[ProvideEditorExtension (typeof (MSBuildLanguageService), MSBuildFileExtension.csproj, 65535)]
	[ProvideEditorExtension (typeof (MSBuildLanguageService), MSBuildFileExtension.vbproj, 65535)]
	[ProvideEditorExtension (typeof (MSBuildLanguageService), MSBuildFileExtension.fsproj, 65535)]
	[ProvideEditorExtension (typeof (MSBuildLanguageService), MSBuildFileExtension.xproj, 65535)]
	[ProvideEditorExtension (typeof (MSBuildLanguageService), MSBuildFileExtension.vcxproj, 65535)]
	[ProvideEditorExtension (typeof (MSBuildLanguageService), MSBuildFileExtension.proj, 65535)]
	[ProvideEditorExtension (typeof (MSBuildLanguageService), MSBuildFileExtension.user, 65535)]

	public sealed class MSBuildEditorVisualStudioPackage : AsyncPackage
	{
		protected override Task InitializeAsync (CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
		{
			var language = new MSBuildLanguageService (this);
			RegisterEditorFactory (language);
			((IServiceContainer)this).AddService (typeof (MSBuildLanguageService), language, true);

			return base.InitializeAsync (cancellationToken, progress);
		}
	}
}
