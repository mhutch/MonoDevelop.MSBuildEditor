// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System;
using System.Collections.Generic;

using MonoDevelop.Xml.Logging;

namespace MonoDevelop.MSBuild.Editor.VisualStudio.Logging;

class UserIdentifiableValueSanitizer
{
	readonly UserIdentifiableValueHasher hasher = new ();

	public object? Sanitize (object? value)  => value switch {
		null => null,
		IUserIdentifiableValue userVal => Sanitize (userVal),
		_ => value
	};

	public object? Sanitize<T> (T value) => value switch {
		null => null,
		IUserIdentifiableValue userVal => Sanitize (userVal),
		_ => value
	};

	public object? Sanitize (IUserIdentifiableValue value) => value switch {
		null => null,
		UserIdentifiableFileName userFilename => Sanitize (userFilename),
		UserIdentifiable<Type> userType => Sanitize (userType),
		_ => HashUserValue (value.Value)
	};

	public string Sanitize (UserIdentifiableFileName filename) => IsNonUserFilePath (filename.Value)? filename.Value : HashUserString (filename.Value);

	public object Sanitize (UserIdentifiable<Type> type) => IsNonUserType (type.Value) ? type.Value : HashUserString (type.Value.ToString ());

	string HashUserValue (object? value) => value?.ToString () is string stringVal? HashUserString (stringVal) : "[null]";

	public string HashUserString (string value)
	{
		lock (hasher) {
			var hash = hasher.Hash (value);
			// only take the first half of the hash to make it easier to deal with in logs
			// collisions don't matter too much, we just need something non-reversible
			value = Convert.ToBase64String (hash, 0, 16);
		}
		return value;
	}

	public bool IsUserIdentifiableValue (object value) => value is IUserIdentifiableValue userVal && IsUserIdentifiableValue (userVal);

	public bool IsUserIdentifiableValue (IUserIdentifiableValue value)
		=> value switch {
			UserIdentifiableFileName filename => !IsNonUserFilePath (filename.Value),
			UserIdentifiable<Type> type => !IsNonUserType (type.Value),
			_ => true
		};

	public virtual bool IsNonUserType (Type type) => false;
	public virtual bool IsNonUserFilePath (string filePath) => false;

	public string? TryGetSanitizedLogMessage<TState> (TState state, out bool isError)
	{
		if (state is null) {
			isError = false;
			return null;
		}

		Type stateType = typeof (TState);
		if (stateType.FullName.StartsWith ("Microsoft.Extensions.Logging.LoggerMessage+LogValues", StringComparison.Ordinal) == false) {
			isError = true;
			return $"SANITIZATION ERROR: Unhandled log message type ${stateType.FullName}";
		}

		if (state is not IReadOnlyList<KeyValuePair<string, object?>> list) {
			isError = true;
			return $"SANITIZATION ERROR: Log message type ${stateType.FullName} is not a list";
		}

		bool needsSanitization = false;
		foreach (var val in list) {
			if (val.Value is object obj && IsUserIdentifiableValue (val.Value)) {
				needsSanitization = true;
				break;
			}
		}
		if (!needsSanitization) {
			isError = false;
			return null;
		}

		var instanceBindingFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;

		var formatterField = stateType.GetField ("_formatter", instanceBindingFlags);

		if (formatterField is null || formatterField?.GetValue (state) is not object formatterFieldVal) {
			isError = true;
			return $"SANITIZATION ERROR: Did not find formatter on log message type ${stateType.FullName}";
		}

		var formatterFieldType = formatterField.FieldType;
		var formatMessageMethod = formatterFieldType.GetMethod ("FormatWithOverwrite", instanceBindingFlags);
		if (formatMessageMethod is null) {
			isError = true;
			return $"SANITIZATION ERROR: Did not find FormatWithOverwrite method on log message formatter type ${formatterFieldType}";
		}

		//last item in list is the original format string so omit it
		var valuesArr = new object?[list.Count-1];
		for (int i = 0;i < valuesArr.Length; i++) {
			valuesArr[i] = list[i].Value is object val? Sanitize (val) : null;
		}

		try {
			var result = (string)formatMessageMethod.Invoke (formatterFieldVal, new object[] { valuesArr });
			isError = false;
			return result;
		} catch {
			isError = true;
			return $"SANITIZATION ERROR: Could not invoke formatter for log message formatter type ${formatterFieldType}";
		}
	}
}
