// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Utilities;
using MonoDevelop.Xml.Editor;

namespace MonoDevelop.MSBuild.Editor
{
	public static class MSBuildContentType
	{
		public const string Name = "MSBuild";

		[Export]
		[Name (Name)]
		[BaseDefinition (XmlContentTypeNames.XmlCore)]
		internal static readonly ContentTypeDefinition XmlContentTypeDefinition;

		[Export]
		[FileExtension (".targets")]
		[ContentType (Name)]
		internal static FileExtensionToContentTypeDefinition MSBuildTargetsFileExtensionDefinition;

		[Export]
		[FileExtension (".props")]
		[ContentType (Name)]
		internal static FileExtensionToContentTypeDefinition MSBuildPropsFileExtensionDefinition;

		[Export]
		[FileExtension (".tasks")]
		[ContentType (Name)]
		internal static FileExtensionToContentTypeDefinition MSBuildTasksFileExtensionDefinition;

		[Export]
		[FileExtension (".overridetasks")]
		[ContentType (Name)]
		internal static FileExtensionToContentTypeDefinition MSBuildOverrideTasksFileExtensionDefinition;

		[Export]
		[FileExtension (".csproj")]
		[ContentType (Name)]
		internal static FileExtensionToContentTypeDefinition MSBuildCSProjFileExtensionDefinition;

		[Export]
		[FileExtension (".vbproj")]
		[ContentType (Name)]
		internal static FileExtensionToContentTypeDefinition MSBuildVBProjFileExtensionDefinition;

		[Export]
		[FileExtension (".fsproj")]
		[ContentType (Name)]
		internal static FileExtensionToContentTypeDefinition MSBuildFSProjFileExtensionDefinition;

		[Export]
		[FileExtension (".xproj")]
		[ContentType (Name)]
		internal static FileExtensionToContentTypeDefinition MSBuildXProjFileExtensionDefinition;

		[Export]
		[FileExtension (".vcxproj")]
		[ContentType (Name)]
		internal static FileExtensionToContentTypeDefinition MSBuildVCXProjFileExtensionDefinition;

		[Export]
		[FileExtension (".proj")]
		[ContentType (Name)]
		internal static FileExtensionToContentTypeDefinition MSBuildProjFileExtensionDefinition;

		[Export]
		[FileExtension (".user")]
		[ContentType (Name)]
		internal static FileExtensionToContentTypeDefinition MSBuildUserFileExtensionDefinition;
	}
}
