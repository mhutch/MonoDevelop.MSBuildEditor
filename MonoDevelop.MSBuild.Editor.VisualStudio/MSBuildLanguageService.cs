// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices;
using System.Text;

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.LanguageServices.Implementation.LanguageService;
using Microsoft.VisualStudio.TextManager.Interop;

using MonoDevelop.MSBuild.Workspace;

namespace MonoDevelop.MSBuild.Editor.VisualStudio;

[ComVisible (true)]
[Guid (PackageConsts.LanguageServiceGuid)]
class MSBuildLanguageService : AbstractLanguageService<MSBuildEditorVisualStudioPackage, MSBuildLanguageService>, IVsFormatFilterProvider
{
	public MSBuildLanguageService (MSBuildEditorVisualStudioPackage package) : base (package) { }

	protected override string LanguageName => PackageConsts.LanguageServiceName;

	protected override string ContentTypeName => MSBuildContentType.Name;

	public override Guid LanguageServiceId => PackageGuids.LanguageService;

	FormatFilter[] formatFilters = new[] {
		new FormatFilter ("MSBuild File", MSBuildFileExtension.All)
	};

	readonly record struct FormatFilter (string name, params string[] extensions)
	{
	}

	int IVsFormatFilterProvider.GetFormatFilterList (out string pbstrFilterList)
	{
		var sb = new StringBuilder ();
		foreach (var filter in formatFilters) {
			sb.Append (filter.name);
			sb.Append ("' (");
			bool first = true;
			foreach (var ext in filter.extensions) {
				if (!first) {
					sb.Append (", ");
				}
				first = false;
				sb.Append ("*");
				sb.Append (ext);
			}
			sb.Append (")\n");

			first = true;
			foreach (var ext in filter.extensions) {
				if (!first) {
					sb.Append (";");
				}
				first = false;
				sb.Append ("*");
				sb.Append (ext);
			}
			sb.Append ("\n");
		}

		pbstrFilterList = sb.ToString ();

		return VSConstants.S_OK;
	}

	int IVsFormatFilterProvider.CurFileExtensionFormat (string bstrFileName, out uint pdwExtnIndex)
	{
		for (uint filterIndex = 0; filterIndex < formatFilters.Length; filterIndex++) {
			var filter = formatFilters[filterIndex];
			foreach (var ext in filter.extensions) {
				if (bstrFileName.EndsWith (ext, StringComparison.OrdinalIgnoreCase)) {
					pdwExtnIndex = filterIndex;
					return VSConstants.S_OK;
				}
			}
		}

		pdwExtnIndex = 0;
		return VSConstants.E_FAIL;
	}

	int IVsFormatFilterProvider.QueryInvalidEncoding (uint format, out string pbstrMessage)
	{
		pbstrMessage = null;
		return VSConstants.S_FALSE;
	}
}