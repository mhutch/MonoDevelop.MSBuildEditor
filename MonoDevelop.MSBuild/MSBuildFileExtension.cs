// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MonoDevelop.MSBuild
{
	/// <summary>
	/// Known extensions of MSBuild files
	/// </summary>
	class MSBuildFileExtension
	{
		// NOTE: keep in sync with:
		// * XmlChooserFactory registration in MonoDevelop.MSBuild.Editor.VisualStudio\languages.pkgdef
		// * ProvideLanguageExtension/ProvideEditorExtension in MonoDevelop.MSBuild.Editor.VisualStudio\MSBuildEditorVisualStudioPackage.cs
		// * ContentTypeDefinitions in MonoDevelop.MSBuild.Editor\MSBuildContentType.cs
		// * MonoDevelop.MSBuildEditor\Properties\Manifest.addin.xml

		public const string targets = ".targets";
		public const string props = ".props";
		public const string tasks = ".tasks";
		public const string overridetasks = ".overridetasks";
		public const string csproj = ".csproj";
		public const string vbproj = ".vbproj";
		public const string fsproj = ".fsproj";
		public const string xproj = ".xproj";
		public const string vcxproj = ".vcxproj";
		public const string proj = ".proj";
		public const string user = ".user";
	}
}
