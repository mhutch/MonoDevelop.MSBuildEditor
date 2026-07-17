// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System;
using System.Diagnostics;
using System.IO;

using Microsoft.VisualStudio.Text;

namespace MonoDevelop.MSBuild.Editor.Classification
{
	/// <summary>
	/// Determines whether the Visual Studio TextMate colorization service is expected to work in the host process.
	/// In VS 2026 (18.x) the TextMate asset service used by <see cref="MSBuildTextMateTagger"/> no longer
	/// produces a working classification tagger, so classification falls back to <see cref="MSBuildClassificationTagger"/>.
	/// </summary>
	static class TextMateSupport
	{
		static readonly Lazy<bool> availability = new (ComputeAvailability);

		/// <summary>
		/// Whether the host is expected to support the legacy TextMate asset service (VS 17.x). Computed once per process.
		/// </summary>
		public static bool IsAvailable => availability.Value;

		/// <summary>
		/// Describes the host version probe result, for logging purposes.
		/// </summary>
		public static string HostDescription { get; private set; } = "unknown host";

		/// <summary>
		/// Computes whether the host is expected to support the legacy TextMate asset service.
		/// </summary>
		/// <returns>False if the host is known to be VS 18.0 (VS 2026) or later, true otherwise.</returns>
		static bool ComputeAvailability ()
		{
			// primary probe: the version of the host process (devenv.exe -> 17.x for VS 2022, 18.x for VS 2026)
			try {
				string? mainModulePath = Process.GetCurrentProcess ().MainModule?.FileName;
				if (mainModulePath is not null && string.Equals (Path.GetFileNameWithoutExtension (mainModulePath), "devenv", StringComparison.OrdinalIgnoreCase)) {
					int productMajorVersion = FileVersionInfo.GetVersionInfo (mainModulePath).ProductMajorPart;
					if (productMajorVersion > 0) {
						HostDescription = $"devenv version {productMajorVersion}";
						return productMajorVersion < 18;
					}
				}
			} catch (Exception) {
				// ignore, fall through to the editor assembly version probe
			}

			// fallback probe: the version of the loaded editor assemblies
			try {
				Version? editorAssemblyVersion = typeof (ITextBuffer).Assembly.GetName ().Version;
				if (editorAssemblyVersion is not null && editorAssemblyVersion.Major > 0) {
					HostDescription = $"editor assembly version {editorAssemblyVersion}";
					return editorAssemblyVersion.Major < 18;
				}
			} catch (Exception) {
				// ignore, assume TextMate is available; the null-result fallback in
				// MSBuildTextMateTagger.CreateTagger still protects against a missing tagger
			}

			return true;
		}
	}
}
