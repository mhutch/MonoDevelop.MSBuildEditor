// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MonoDevelop.MSBuildEditor.Language;

namespace MonoDevelop.MSBuildEditor.Schema
{
	class ValueInfo : BaseInfo
	{
		public ValueInfo (string name, string description) : base (name, description)
		{
		}

		public override MSBuildKind Kind => MSBuildKind.Expression;
	}
}