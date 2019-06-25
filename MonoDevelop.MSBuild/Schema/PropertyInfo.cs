// Copyright (c) 2016 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace MonoDevelop.MSBuild.Schema
{
	class PropertyInfo : ValueInfo
	{
		public bool Reserved { get; }

		public PropertyInfo (
			string name, DisplayText description, bool reserved = false,
			MSBuildValueKind valueKind = MSBuildValueKind.Unknown,
			List<ConstantInfo> values = null, string defaultValue = null)
			: base (name, description, valueKind, values, defaultValue)
		{
			Reserved = reserved;
		}
    }
}