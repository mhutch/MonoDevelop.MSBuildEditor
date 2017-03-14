//
// MSBuildParsedDocument.cs
//
// Author:
//       Mikayla Hutchinson <m.j.hutchinson@gmail.com>
//
// Copyright (c) 2016 Xamarin Inc.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.



namespace MonoDevelop.MSBuildEditor
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
