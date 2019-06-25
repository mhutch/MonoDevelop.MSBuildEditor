// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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

		public bool TryGetProperty (string name, out MSBuildPropertyValue value)
		{
			if (fileContext.TryGetProperty (name, out value)) {
				return true;
			}

			if (collector.TryGetValues (name, out var values)) {
				value = new MSBuildPropertyValue (values);
				return true;
			}

			return false;
		}
	}
}