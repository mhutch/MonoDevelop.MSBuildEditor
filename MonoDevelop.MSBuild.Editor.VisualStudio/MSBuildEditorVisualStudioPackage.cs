// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel.Composition;
using System.Runtime.InteropServices;
using System.Threading;

using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Utilities;

using Task = System.Threading.Tasks.Task;

namespace MonoDevelop.MSBuild.Editor.VisualStudio
{
	[PackageRegistration (UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
	[Guid (PackageGuidString)]
	public sealed class MSBuildEditorVisualStudioPackage : AsyncPackage
	{
		public const string PackageGuidString = "6c7bd60d-5321-4fb0-8684-9736003d64ad";
		public const string LanguageServiceName = MSBuildContentType.Name;
		public const string LanguageServiceKey = @"Languages\Language Services\" + LanguageServiceName;

		protected override Task InitializeAsync (CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
			=> Task.CompletedTask;

		[Export]
		[Name ("xmlcore")]
		[BaseDefinition ("xml")]
		internal static readonly ContentTypeDefinition XmlCoreContentTypeDefinition;
	}
}
