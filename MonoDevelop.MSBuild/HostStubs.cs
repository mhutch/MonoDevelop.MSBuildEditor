// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using MonoDevelop.MSBuild.Evaluation;
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
		internal static void LogWarning (string message) => Console.WriteLine (message);
		internal static void LogInfo (string message) => Console.WriteLine (message);
	}

	static class MSBuildHost
	{
		public static class Options
		{
			public static bool ShowPrivateSymbols => false;
		}

		public static ITaskMetadataBuilder CreateTaskMetadataBuilder (MSBuildRootDocument doc) => new NoopTaskMetadataBuilder ();

		//FIXME
		class NoopTaskMetadataBuilder : ITaskMetadataBuilder
		{
			public TaskInfo CreateTaskInfo (string typeName, string assemblyName, string assemblyFile, string declaredInFile, int declaredAtOffset, PropertyValueCollector propVals)
			{
				return null;
			}
		}
	}
}