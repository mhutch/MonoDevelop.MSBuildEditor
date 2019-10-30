// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;

namespace MonoDevelop.MSBuild.Analysis
{
	abstract class MSBuildRefactoringProvider
	{
		public abstract Task RegisterRefactoringsAsync (MSBuildRefactoringContext context);
	}
}