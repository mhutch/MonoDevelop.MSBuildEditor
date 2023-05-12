// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Microsoft.CodeAnalysis.Internal.Log
{

	static class FunctionId
	{
		public static readonly string SpellChecker_ExceptionInCacheRead = nameof (SpellChecker_ExceptionInCacheRead);

		public static string BKTree_ExceptionInCacheRead { get; internal set; }
	}
}

namespace Roslyn.Utilities
{
	static class ExceptionUtilities
	{
		public static Exception UnexpectedValue (ObjectWriter.EncodingKind kind) => throw new Exception ($"Unexpected value of kind {kind}");
		public static Exception UnexpectedValue (Type type) => throw new Exception ($"Unexpected value of type {type}");

		public static Exception UnexpectedValue (byte marker)
		{
			throw new Exception ($"Unexpected value for marker {marker}");
		}
	}

	static class WorkspacesResources
	{
		public static string Deserialization_reader_for_0_read_incorrect_number_of_values
			=> "Deserialization reader for {0} read incorrect number of values";
		public static string The_type_0_is_not_understood_by_the_serialization_binder
			=> "The type {0} is not understood by the serialization binder";
		public static string Cannot_serialize_type_0
			=> "Cannot serialize type {0}";

		public static string Arrays_with_more_than_one_dimension_cannot_be_serialized { get; internal set; }
		public static string Value_too_large_to_be_represented_as_a_30_bit_unsigned_integer { get; internal set; }
	}

	internal static partial class SpecializedCollections
	{
		public static IList<T> EmptyList<T> ()
		{
			return Empty.List<T>.Instance;
		}
	}
}
