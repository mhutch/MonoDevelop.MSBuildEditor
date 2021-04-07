// Copyright (c) 2016 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MonoDevelop.MSBuild.Language.Typesystem
{
	class PropertyInfo : VariableInfo
	{
		public bool Reserved { get; }

		public PropertyInfo (
			string name, DisplayText description, bool reserved = false,
			MSBuildValueKind valueKind = MSBuildValueKind.Unknown,
			CustomTypeInfo customType = null, string defaultValue = null,
			bool isDeprecated = false, string deprecationMessage = null)
			: base (name, description, valueKind, customType, defaultValue, isDeprecated, deprecationMessage)
		{
			Reserved = reserved;
		}
    }
}