// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;

namespace MonoDevelop.MSBuildEditor.Analysis
{
	abstract class MSBuildRefactoringProvider
	{
		public abstract Task<MSBuildAction> GetRefactorings (MSBuildRefactoringContext context);
	}
}