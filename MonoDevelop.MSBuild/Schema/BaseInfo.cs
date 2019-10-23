// Copyright (c) 2016 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace MonoDevelop.MSBuild.Schema
{
	public abstract class BaseInfo
	{
		readonly DisplayText description;

		public string Name { get; }
		public virtual DisplayText Description => description;

		protected BaseInfo (string name, DisplayText description)
		{
			Name = name;
			this.description = description;
		}

		public override bool Equals (object obj)
		{
			var other = obj as BaseInfo;
			return other != null && string.Equals (Name, other.Name, StringComparison.OrdinalIgnoreCase);
		}

		public override int GetHashCode ()
		{
			return StringComparer.OrdinalIgnoreCase.GetHashCode (Name);
		}
	}

	public abstract class ValueInfo : BaseInfo
	{
		protected ValueInfo (
			string name, DisplayText description, MSBuildValueKind valueKind = MSBuildValueKind.Unknown,
			CustomTypeInfo customType = null, string defaultValue = null, bool isDeprecated = false, string deprecationMessage = null)
			: base (name, description)
		{
			if (valueKind.IsCustomType () && customType == null) {
				throw new ArgumentException ($"When {nameof(valueKind)} is {nameof(MSBuildValueKind.CustomType)}, {nameof (customType)} cannot be null");
			}

			if (customType != null && !valueKind.IsCustomType ()) {
				throw new ArgumentException ($"When {nameof(customType)} is provided, {nameof(valueKind)} must be {nameof(MSBuildValueKind.CustomType)}");
			}

			CustomType = customType;
			DefaultValue = defaultValue;
			IsDeprecated = isDeprecated || !string.IsNullOrEmpty (deprecationMessage);
			DeprecationMessage = deprecationMessage;
			ValueKind = valueKind;
		}

		public MSBuildValueKind ValueKind { get; }
		public CustomTypeInfo CustomType { get; }
		public string DefaultValue { get; }
		public bool IsDeprecated { get; }
		public string DeprecationMessage { get; }
	}

	public class CustomTypeInfo
	{
		public CustomTypeInfo (List<ConstantInfo> values, string name = null, bool allowUnknownValues = false)
        {
			Values = values ?? throw new ArgumentNullException (nameof (values));
			Name = name;
			AllowUnknownValues = allowUnknownValues;
		}

		public string Name { get; }
		public bool AllowUnknownValues { get; }
		public List<ConstantInfo> Values { get; }
	}
}