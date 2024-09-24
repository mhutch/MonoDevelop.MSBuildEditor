// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MonoDevelop.MSBuild.Editor.CodeActions
{
	class MSBuildChangeAnnotation (string label)
	{
		public string Label => label;

		public bool NeedsConfirmation { get; set; }

		public string? Description { get; set; }
	}
}