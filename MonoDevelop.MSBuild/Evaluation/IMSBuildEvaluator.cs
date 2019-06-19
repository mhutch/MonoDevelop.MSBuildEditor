// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MonoDevelop.MSBuild.Evaluation
{
	interface IMSBuildEvaluator
	{
		string Evaluate (string expression);
	}
}