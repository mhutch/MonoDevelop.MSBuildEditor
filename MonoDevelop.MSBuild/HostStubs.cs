// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.CodeAnalysis;
using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Schema;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo ("MonoDevelop.MSBuild.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo ("MonoDevelop.MSBuild.Editor")]

namespace MonoDevelop.MSBuild
{
	static class LoggingService
	{
		public static void LogDebug (string message) => Console.WriteLine (message);
		public static void LogError (string message, Exception ex) => LogError ($"{message}: {ex}");
		public static void LogError (string message) => Console.Error.WriteLine (message);
		internal static void LogWarning (string v) => Console.WriteLine (v);
	}

	static class Markup
	{
		public static string EscapeText (string text) => throw new NotImplementedException ();
	}

	static class TextEditorFactory
	{
		public static ITextSource CreateNewDocument (string filename) => new StringTextSource (File.ReadAllText (filename), filename);
		public static ITextSource CreateNewDocument (string content, string filename) => new StringTextSource (content, filename);

		class StringTextSource : ITextSource
		{
			string content;

			public StringTextSource (string content, string filename)
			{
				this.content = content;
				FileName = filename;
			}

			public string FileName { get; set; }
			public int Length => content.Length;
			public TextReader CreateReader () => new StringReader (content);
			public char GetCharAt (int offset) => content[offset];
			public string GetTextBetween (int begin, int end) => content.Substring (begin, end - begin);
		}
	}

	static class MSBuildHost
	{
		public static class Options
		{
			public static bool ShowPrivateSymbols => false;
		}

		public static IMSBuildEvaluationContext CreateEvaluationContext (IRuntimeInformation runtimeInformation, string projectPath, string thisFilePath) => new NoopMSBuildEvaluationContext ();

		public static ITaskMetadataBuilder CreateTaskMetadataBuilder (MSBuildRootDocument doc) => new NoopTaskMetadataBuilder ();

		public static IFunctionTypeProvider GetFunctionTypeProvider () => FunctionTypeProvider;

		// hack for tests
		public static IFunctionTypeProvider FunctionTypeProvider { get; set; }

		//FIXME
		class NoopTaskMetadataBuilder : ITaskMetadataBuilder
		{
			public TaskInfo CreateTaskInfo (string typeName, string assemblyName, string assemblyFile, string declaredInFile, int declaredAtOffset, PropertyValueCollector propVals)
			{
				return null;
			}
		}

		//FIXME
		class NoopMSBuildEvaluationContext : IMSBuildEvaluationContext
		{
			public IEnumerable<string> EvaluatePathWithPermutation (string pathExpression, string baseDirectory, PropertyValueCollector propVals)
			{
				yield break;
			}
		}
	}

	interface IMSBuildEvaluationContext
	{
		IEnumerable<string> EvaluatePathWithPermutation (string pathExpression, string baseDirectory, PropertyValueCollector propVals);
	}
}