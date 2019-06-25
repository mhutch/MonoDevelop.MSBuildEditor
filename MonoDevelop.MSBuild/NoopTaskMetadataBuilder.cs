// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MonoDevelop.MSBuild.Evaluation;
using MonoDevelop.MSBuild.Schema;

namespace MonoDevelop.MSBuild
{
	class NoopTaskMetadataBuilder : ITaskMetadataBuilder
	{
		public TaskInfo CreateTaskInfo (string typeName, string assemblyName, string assemblyFile, string declaredInFile, int declaredAtOffset, IMSBuildEvaluationContext evaluationContext)
		{
			return null;
		}
	}
}