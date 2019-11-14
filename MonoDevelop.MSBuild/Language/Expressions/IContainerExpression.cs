// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace MonoDevelop.MSBuild.Language.Expressions
{
	/// <summary>
	/// Implemented by expression nodes that contain an indeterminate list of other expressions.
	/// </summary>
	public interface IContainerExpression
	{
		IReadOnlyList<ExpressionNode> Nodes { get; }
	}
}
