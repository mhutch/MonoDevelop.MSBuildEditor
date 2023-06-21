// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Logging;

namespace MonoDevelop.MSBuild.Evaluation
{
	interface IMSBuildEvaluationContext
	{
		/// <summary>Gets the only or most likely value for the property</summary>
		bool TryGetProperty (string name, [NotNullWhen (true)] out EvaluatedValue? value);

		/// <summary>Gets all defined or permuted values for the property</summary>
		/// <param name="isProjectImportStart">Certain properties are multivalued only at the beginning of a project import</param>
		bool TryGetMultivaluedProperty (string name, [NotNullWhen (true)] out OneOrMany<EvaluatedValue>? value, bool isProjectImportStart = false);

		ILogger Logger { get; }
	}
}
