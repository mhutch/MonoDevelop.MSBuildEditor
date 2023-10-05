// Copyright (c) 2016 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace MonoDevelop.MSBuild.Language.Typesystem
{
	public sealed class CustomTypeInfo
	{
		public CustomTypeInfo (IReadOnlyList<CustomTypeValue> values, string name = null, DisplayText description = default, bool allowUnknownValues = false, MSBuildValueKind baseKind = MSBuildValueKind.Unknown, bool caseSensitive = false)
        {
			Values = values ?? throw new ArgumentNullException (nameof (values));
			Name = name;
			Description = description;
			AllowUnknownValues = allowUnknownValues;
			BaseKind = baseKind;
			CaseSensitive = caseSensitive;

			foreach (var v in values) {
				v.SetParent (this);
			}
		}

		public string Name { get; }
		public DisplayText Description { get; }
		public bool AllowUnknownValues { get; }
		public IReadOnlyList<CustomTypeValue> Values { get; }
		public MSBuildValueKind BaseKind { get;}
		public bool CaseSensitive { get; }
	}
}