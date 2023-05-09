// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Logging;

using MonoDevelop.MSBuild.Language;

namespace MonoDevelop.MSBuild.Evaluation
{
	/// <summary>
	/// Provides MSBuild property values that have been collected from the targets
	/// </summary>
	class MSBuildCollectedValuesEvaluationContext : IMSBuildEvaluationContext
	{
		readonly PropertyValueCollector collector;
		readonly IMSBuildEvaluationContext fileContext;


		public MSBuildCollectedValuesEvaluationContext (IMSBuildEvaluationContext fileContext, PropertyValueCollector collector)
		{
			this.collector = collector;
			this.fileContext = fileContext;
		}

		public bool TryGetProperty (string name, [NotNullWhen (true)] out EvaluatedValue? value)
		{
			if (fileContext.TryGetProperty (name, out value)) {
				return true;
			}

			if (collector.TryGetValues (name, out var values)) {
				value = values[values.Count - 1];
				return true;
			}

			return false;
		}

		public bool TryGetMultivaluedProperty (string name, [NotNullWhen (true)] out OneOrMany<EvaluatedValue>? value, bool isProjectImportStart = false)
		{
			if (fileContext.TryGetMultivaluedProperty (name, out value, isProjectImportStart)) {
				return true;
			}

			if (collector.TryGetValues (name, out var values)) {
				value = new OneOrMany<EvaluatedValue> (values);
				return true;
			}

			return false;
		}

		public ILogger Logger => fileContext.Logger;
	}
}