// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Utilities;

using MonoDevelop.MSBuild.Workspace;
using MonoDevelop.Xml.Editor;

namespace MonoDevelop.MSBuild.Editor
{
	// NOTE: all the extensions ending in "proj" are handled by MSBuildContentTypeProvider
	public static class MSBuildContentType
	{
		public const string Name = "MSBuild";

		[Export]
		[Name (Name)]
		[BaseDefinition (XmlContentTypeNames.XmlCore)]
		internal static readonly ContentTypeDefinition MSBuildContentTypeDefinition;

		[Export]
		[FileExtension (MSBuildFileExtension.targets)]
		[ContentType (Name)]
		internal static FileExtensionToContentTypeDefinition MSBuildTargetsFileExtensionDefinition;

		[Export]
		[FileExtension (MSBuildFileExtension.props)]
		[ContentType (Name)]
		internal static FileExtensionToContentTypeDefinition MSBuildPropsFileExtensionDefinition;

		[Export]
		[FileExtension (MSBuildFileExtension.tasks)]
		[ContentType (Name)]
		internal static FileExtensionToContentTypeDefinition MSBuildTasksFileExtensionDefinition;

		[Export]
		[FileExtension (MSBuildFileExtension.overridetasks)]
		[ContentType (Name)]
		internal static FileExtensionToContentTypeDefinition MSBuildOverrideTasksFileExtensionDefinition;

		[Export]
		[FileExtension (MSBuildFileExtension.user)]
		[ContentType (Name)]
		internal static FileExtensionToContentTypeDefinition MSBuildUserFileExtensionDefinition;
	}
}
