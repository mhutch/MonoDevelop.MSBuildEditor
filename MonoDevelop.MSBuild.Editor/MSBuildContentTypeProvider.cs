// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Utilities;

using MonoDevelop.MSBuild.Workspace;

namespace MonoDevelop.MSBuild.Editor
{
	[Export(typeof(IFilePathToContentTypeProvider))]
	[Name("MSBuild Content Type Provider")]
	[FileExtension("*")]
	class MSBuildContentTypeProvider : IFilePathToContentTypeProvider
	{
		[ImportingConstructor]
		public MSBuildContentTypeProvider (IContentTypeRegistryService contentTypeRegistryService)
		{
			ContentTypeRegistryService = contentTypeRegistryService;
		}

		public IContentTypeRegistryService ContentTypeRegistryService { get; }

		public bool TryGetContentTypeForFilePath (string filePath, out IContentType contentType)
		{
			if (MSBuildFileKindExtensions.GetFileKind (filePath) != MSBuildFileKind.Unknown) {
				contentType = ContentTypeRegistryService.GetContentType (MSBuildContentType.Name);
				return true;
			}

			contentType = null;
			return false;
		}
	}
}
