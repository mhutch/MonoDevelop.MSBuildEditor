// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Utilities;

namespace MonoDevelop.MSBuild.Editor
{
	[Export(typeof(IFilePathToContentTypeProvider))]
	[Name("MSBuild Content Type Provider")]
	[FileExtension("*")]
	class MSBuildContentTypeProvider : IFilePathToContentTypeProvider
	{
		[Import]
		private IContentTypeRegistryService ContentTypeRegistryService { get; set; }

		public bool TryGetContentTypeForFilePath (string filePath, out IContentType contentType)
		{
			if (filePath.EndsWith ("proj", StringComparison.OrdinalIgnoreCase)) {
				contentType = ContentTypeRegistryService.GetContentType (MSBuildContentType.Name);
				return true;
			} else {
				contentType = null;
				return false;
			}
		}
	}
}
