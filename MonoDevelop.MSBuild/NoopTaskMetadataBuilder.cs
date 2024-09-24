// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MonoDevelop.MSBuild.Evaluation;
using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.MSBuild.Language.Typesystem;
using Microsoft.Extensions.Logging;

namespace MonoDevelop.MSBuild
{
	class NoopTaskMetadataBuilder : ITaskMetadataBuilder
	{
		public TaskInfo CreateTaskInfo (
			string typeName, string assemblyName, ExpressionNode assemblyFile, string assemblyFileStr,
			string declaredInFile, Xml.Dom.TextSpan? declarationSpan, IMSBuildEvaluationContext evaluationContext, ILogger logger)
		{
			return null;
		}
	}
}