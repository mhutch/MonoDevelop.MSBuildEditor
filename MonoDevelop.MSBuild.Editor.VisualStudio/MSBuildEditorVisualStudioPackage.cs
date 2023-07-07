// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using System.Threading;

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

using Microsoft.VisualStudio.LanguageServices.Implementation.LanguageService;

using MonoDevelop.MSBuild.Editor.VisualStudio.Logging;
using MonoDevelop.MSBuild.Editor.VisualStudio.Options;

using Task = System.Threading.Tasks.Task;
using System.Collections.Generic;

namespace MonoDevelop.MSBuild.Editor.VisualStudio
{
	[InstalledProductRegistration(PackageResxId.PackageNameStr, PackageResxId.PackageDescriptionStr, PackageConsts.PackageVersion)]
	[PackageRegistration (UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
	[Guid (PackageConsts.PackageGuid)]

	[ProvideLanguageService (typeof (MSBuildLanguageService), PackageConsts.LanguageServiceName, PackageResxId.LanguageName,
		ShowDropDownOptions = false,
		RequestStockColors = true,
		DefaultToInsertSpaces = true,
		ShowSmartIndent = true,
		MatchBraces = true,
		EnableAsyncCompletion = true,
		ShowCompletion = true
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

	[ProvideEditorFactory (typeof (MSBuildEditorFactory), PackageResxId.EditorName, deferUntilIntellisenseIsReady: false, CommonPhysicalViewAttributes = (int)__VSPHYSICALVIEWATTRIBUTES.PVA_SupportsPreview)]
	[ProvideEditorLogicalView (typeof (MSBuildEditorFactory), VSConstants.LOGVIEWID.TextView_string)]
	[ProvideEditorLogicalView (typeof (MSBuildEditorFactory), VSConstants.LOGVIEWID.Code_string)]
	[ProvideEditorLogicalView (typeof (MSBuildEditorFactory), VSConstants.LOGVIEWID.Debugging_string)]

	[ProvideEditorExtension (typeof (MSBuildEditorFactory), MSBuildFileExtension.targets, 65535)]
	[ProvideEditorExtension (typeof (MSBuildEditorFactory), MSBuildFileExtension.props, 65535)]
	[ProvideEditorExtension (typeof (MSBuildEditorFactory), MSBuildFileExtension.tasks, 65535)]
	[ProvideEditorExtension (typeof (MSBuildEditorFactory), MSBuildFileExtension.overridetasks, 65535)]
	[ProvideEditorExtension (typeof (MSBuildEditorFactory), MSBuildFileExtension.csproj, 65535)]
	[ProvideEditorExtension (typeof (MSBuildEditorFactory), MSBuildFileExtension.vbproj, 65535)]
	[ProvideEditorExtension (typeof (MSBuildEditorFactory), MSBuildFileExtension.fsproj, 65535)]
	[ProvideEditorExtension (typeof (MSBuildEditorFactory), MSBuildFileExtension.xproj, 65535)]
	[ProvideEditorExtension (typeof (MSBuildEditorFactory), MSBuildFileExtension.vcxproj, 65535)]
	[ProvideEditorExtension (typeof (MSBuildEditorFactory), MSBuildFileExtension.proj, 65535)]
	[ProvideEditorExtension (typeof (MSBuildEditorFactory), MSBuildFileExtension.user, 65535)]

	[ProvideOptionPage(typeof(MSBuildTelemetryOptionsPage), "MSBuild Editor", "Telemetry", PackageResxId.EditorName, PackageResxId.TelemetryOptionsPageName, false, PackageResxId.TelemetryOptionsPageKeywords)]
	[ProvideProfile(typeof(MSBuildTelemetryOptionsPage), "MSBuild Editor", "Telemetry", PackageResxId.EditorName, PackageResxId.TelemetryOptionsPageName, false)]

	sealed class MSBuildEditorVisualStudioPackage : AbstractPackage<MSBuildEditorVisualStudioPackage, MSBuildLanguageService>
	{
		MSBuildExtensionLogger logger;

		protected override IEnumerable<IVsEditorFactory> CreateEditorFactories ()
		{
			yield return new MSBuildEditorFactory (ComponentModel);
		}

		protected override MSBuildLanguageService CreateLanguageService () => new (this);

		protected override async Task InitializeAsync (CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
		{
			var settingsStorage = new MSBuildEditorSettingsStorage ();
			var telemetryOptions = await MSBuildTelemetryOptions.GetLiveInstanceAsync ();

			logger = new MSBuildExtensionLogger (settingsStorage, telemetryOptions);
			MSBuildTelemetryOptions.Saved += logger.UpdateTelemetryOptions;

			((IServiceContainer)this).AddService (typeof (MSBuildExtensionLogger), logger, true);

			await base.InitializeAsync (cancellationToken, progress);
		}

		protected override int QueryClose (out bool canClose)
		{
			logger.ShutdownTelemetry ();
			return base.QueryClose (out canClose);
		}
	}
}
