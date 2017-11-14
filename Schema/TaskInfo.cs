// Copyright (c) 2016 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using MonoDevelop.MSBuildEditor.Language;

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

		public override MSBuildKind Kind => MSBuildKind.Task;

	}

	class TaskParameterInfo : BaseInfo
	{
		public TaskParameterInfo (string name, string description) : base (name, description)
		{
		}

		public override MSBuildKind Kind => MSBuildKind.TaskParameter;
	}
}