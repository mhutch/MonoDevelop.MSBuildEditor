// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MonoDevelop.Xml.Analysis;

namespace MonoDevelop.MSBuild.Schema;

public class MSBuildSchemaLoadError
{
	public MSBuildSchemaLoadError (string message, XmlDiagnosticSeverity severity, string origin, (int line, int col)? position, string path)
	{
		Message = message;
		JsonPath = path;
		FilePosition = position;
		Severity = severity;
		Origin = origin;
	}

	public string Message { get; }
	public string JsonPath { get; }
	public (int line, int col)? FilePosition { get; }
	public XmlDiagnosticSeverity Severity { get; }

	/// <summary>
	/// Label describing the file or resource in which the error was located
	/// </summary>
	public string Origin { get; }

	public override string ToString ()
	{
		return $"{JsonPath}: {Message}";
	}
}
