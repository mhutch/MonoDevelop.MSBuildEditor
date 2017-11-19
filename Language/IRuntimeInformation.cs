// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace MonoDevelop.MSBuildEditor.Language
{
	public interface IRuntimeInformation
	{
		string GetBinPath ();
		string GetToolsPath ();
		IEnumerable<string> GetExtensionsPaths ();
		string GetSdksPath ();
	}
}
