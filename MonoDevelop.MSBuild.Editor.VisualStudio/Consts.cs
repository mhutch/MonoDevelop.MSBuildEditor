// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MonoDevelop.MSBuild.Editor.VisualStudio
{
	class Consts
	{
		public const string PackageGuid = "6c7bd60d-5321-4fb0-8684-9736003d64ad";

		public const string PackageName = "MSBuild Editor";
		public const string PackageDescription = "Language service for MSBuild projects and targets";
		public const string PackageVersion = ThisAssembly.AssemblyInformationalVersion;

		public const string LanguageServiceGuid = "111e2ecb-9e5f-4945-9d21-d4e5368d620b";

		public const string LanguageServiceName = "MSBuild";
		public const string LanguageServiceKey = @"Languages\Language Services\" + LanguageServiceName;
	}
}
