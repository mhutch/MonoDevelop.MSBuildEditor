// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace MonoDevelop.Projects.MSBuild.Conditions
{
	interface IPropertyCollector
	{
		void AddPropertyValues (List<string> combinedProperty, List<string> combinedValue);
	}
}