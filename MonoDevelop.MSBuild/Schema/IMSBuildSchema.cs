// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

using MonoDevelop.MSBuild.Language.Typesystem;

namespace MonoDevelop.MSBuild.Schema
{
	interface IMSBuildSchema
	{
		Dictionary<string, PropertyInfo> Properties { get; }
		Dictionary<string, ItemInfo> Items { get; }
		Dictionary<string, TaskInfo> Tasks { get; }
		Dictionary<string, TargetInfo> Targets { get; }

		bool IsPrivate (string name);
	}
}