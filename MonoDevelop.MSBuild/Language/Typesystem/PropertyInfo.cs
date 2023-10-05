// Copyright (c) 2016 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MonoDevelop.MSBuild.Language.Typesystem
{
	class PropertyInfo : VariableInfo
	{
		public bool IsReserved { get; }
		public bool IsReadOnly { get; }

		public PropertyInfo (
			string name, DisplayText description,
			MSBuildValueKind valueKind = MSBuildValueKind.Unknown,
			CustomTypeInfo customType = null, string defaultValue = null,
			bool isDeprecated = false, string deprecationMessage = null)
			: base (name, description, valueKind, customType, defaultValue, isDeprecated, deprecationMessage)
		{
		}

		public PropertyInfo (
			string name, DisplayText description,
			bool isReserved, bool isReadOnly,
			MSBuildValueKind valueKind = MSBuildValueKind.Unknown,
			CustomTypeInfo customType = null, string defaultValue = null,
			bool isDeprecated = false, string deprecationMessage = null)
			: base (name, description, valueKind, customType, defaultValue, isDeprecated, deprecationMessage)
		{
			IsReserved = isReserved;
			IsReadOnly = isReadOnly;
		}
	}
}