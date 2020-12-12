// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MonoDevelop.MSBuild.Evaluation;
using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.MSBuild.Language.Typesystem;

namespace MonoDevelop.MSBuild.Schema
{
	interface ITaskMetadataBuilder
	{
		TaskInfo CreateTaskInfo (
			string typeName, string assemblyName, ExpressionNode assemblyFile, string assemblyFileStr,
			string declaredInFile, int declaredAtOffset,
			IMSBuildEvaluationContext evaluationContext);
	}
}