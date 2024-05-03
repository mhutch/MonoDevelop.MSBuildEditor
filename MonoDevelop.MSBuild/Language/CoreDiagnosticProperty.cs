// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MonoDevelop.MSBuild.Language
{
	/// <summary>
	/// The names of properties used by various attached to core diagnostics.
	/// </summary>
	public static class CoreDiagnosticProperty
	{
		/// <summary>
		/// A potentially misspelled name or value
		/// </summary>
		public const string MisspelledNameOrValue = nameof (MisspelledNameOrValue);

		/// <summary>
		/// For misspelled metadata, the name of the item
		/// </summary>
		public const string MisspelledMetadataItemName = nameof (MisspelledMetadataItemName);

		/// <summary>
		/// If a misspelled name has multiple spans to fix, the affected spans
		/// </summary>
		public const string MisspelledNameSpans = nameof (MisspelledNameSpans);

		/// <summary>
		/// For a potentially misspelled value, the symbol representing the expected type of the value
		/// </summary>
		public const string MisspelledValueExpectedType = nameof (MisspelledValueExpectedType);

		/// <summary>
		/// The symbol affected by the diagnostic
		/// </summary>
		public const string Symbol = nameof (Symbol);
	}
}