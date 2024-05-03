// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace MonoDevelop.MSBuild.Language;

class WellKnownTaskFactory
{
	const string RoslynCodeTaskFactoryAssemblyProperty = "$(RoslynCodeTaskFactory)";

	public const string CodeTaskFactory = "CodeTaskFactory";
	public const string RoslynCodeTaskFactory = "RoslynCodeTaskFactory";

	public static string? TryGet (string taskFactoryAttributeValue, string? assemblyNameAttributeValue)
	{
		if (string.IsNullOrEmpty (taskFactoryAttributeValue)) {
			throw new ArgumentException ($"'{nameof (taskFactoryAttributeValue)}' cannot be null or empty.", nameof (taskFactoryAttributeValue));
		}

		if (string.Equals (taskFactoryAttributeValue, CodeTaskFactory, StringComparison.OrdinalIgnoreCase)) {
			if (string.Equals (assemblyNameAttributeValue, RoslynCodeTaskFactoryAssemblyProperty, StringComparison.OrdinalIgnoreCase)) {
				return RoslynCodeTaskFactory;
			}
			return CodeTaskFactory;
		}

		if (string.Equals (taskFactoryAttributeValue, RoslynCodeTaskFactory, StringComparison.OrdinalIgnoreCase)) {
			return RoslynCodeTaskFactory;
		}

		return null;
	}
}