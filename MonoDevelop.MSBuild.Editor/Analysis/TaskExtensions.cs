// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;

namespace MonoDevelop.MSBuild.Editor.Analysis
{
	static class TaskExtensions
	{
		public static T WaitAndGetResult<T> (this Task<T> task, CancellationToken token)
		{
			task.Wait (token);
			return task.Result;
		}
	}
}
