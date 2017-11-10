// Copyright (c) 2016 Xamarin Inc.
// Copyright (c) Microsoft. ALl rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MonoDevelop.MSBuildEditor.Language
{
	enum MSBuildToolsVersion
	{
		Unknown,
		V2_0,
		V3_5,
		V4_0,
		//VS2013
		V12_0,
		//VS2015
		V14_0,
		//VS2017
		V15_0,
	}

	static class MSBuildToolsVersionExtensions
	{
		const MSBuildToolsVersion DEFAULT = MSBuildToolsVersion.V15_0;

		public static string ToVersionString (this MSBuildToolsVersion version)
		{
			switch (version) {
			case MSBuildToolsVersion.V2_0:
				return "2.0";
			case MSBuildToolsVersion.V3_5:
				return "3.5";
			case MSBuildToolsVersion.V4_0:
				return "4.0";
			case MSBuildToolsVersion.V12_0:
				return "12.0";
			case MSBuildToolsVersion.V14_0:
				return "14.0";
			case MSBuildToolsVersion.V15_0:
				return "15.0";
			default:
				return ToVersionString (DEFAULT);
			}
		}

		public static bool IsAtLeast (this MSBuildToolsVersion version, MSBuildToolsVersion other)
		{
			if (version == MSBuildToolsVersion.Unknown) {
				version = DEFAULT;
			}

			return (int)other >= (int)version;
		}
	}
}
