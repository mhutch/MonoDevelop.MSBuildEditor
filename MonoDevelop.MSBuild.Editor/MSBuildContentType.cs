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
		internal static readonly ContentTypeDefinition MSBuildContentTypeDefinition;

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
		[FileExtension (".user")]
		[ContentType (Name)]
		internal static FileExtensionToContentTypeDefinition MSBuildUserFileExtensionDefinition;
	}
}
