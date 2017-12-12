// Copyright (c) 2016 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace MonoDevelop.MSBuildEditor.Schema
{
	class TaskInfo : BaseInfo
	{
		public Dictionary<string, TaskParameterInfo> Parameters { get; internal set; }

		public TaskInfo (string name, string description)
			: base (name, description)
		{
			Parameters = new Dictionary<string, TaskParameterInfo> ();
		}
	}

	class TaskParameterInfo : ValueInfo
	{
		public TaskParameterUsage Usage { get; }

		public TaskParameterInfo (string name, string description, TaskParameterUsage usage, MSBuildValueKind kind) : base (name, description, kind)
		{
			Usage = usage;
		}
	}

	enum TaskParameterUsage
	{
		Unknown,
		Input,
		RequiredInput,
		Output
	}
}