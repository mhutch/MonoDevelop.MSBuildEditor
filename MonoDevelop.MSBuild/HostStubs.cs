// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

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
	}
}