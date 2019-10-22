// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuild.Analysis
{
	static class MSBuildDiagnosticExtensions
	{
		public static void Add (
			this ICollection<MSBuildDiagnostic> list,
			MSBuildDiagnosticDescriptor descriptor,
			TextSpan span
			)
			=> list.Add (new MSBuildDiagnostic (descriptor, span));

		public static void Add (
			this ICollection<MSBuildDiagnostic> list,
			MSBuildDiagnosticDescriptor descriptor,
			TextSpan span,
			params object[] messageArgs
			)
			=> list.Add (new MSBuildDiagnostic (descriptor, span, messageArgs));

		public static void Add (this ICollection<MSBuildDiagnostic> list,
			MSBuildDiagnosticDescriptor descriptor,
			TextSpan span,
			ImmutableDictionary<string, object> properties = null,
			object[] messageArgs = null
			)
			=> list.Add (new MSBuildDiagnostic (descriptor, span, properties, messageArgs));
	}
}