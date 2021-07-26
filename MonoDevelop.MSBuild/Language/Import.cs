// Copyright (c) 2016 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;

using MonoDevelop.MSBuild.SdkResolution;

namespace MonoDevelop.MSBuild.Language
{
	[DebuggerDisplay ("{OriginalImport} (Resolved = {IsResolved})")]
	class Import
	{
		public string Filename { get; private set; }
		public string OriginalImport { get; private set; }
		public string Sdk { get; }
		public SdkInfo ResolvedSdk { get; }
		public DateTime TimeStampUtc { get; }
		public MSBuildDocument Document { get; set; }
		public bool IsResolved { get { return Document != null; } }

		public Import (string importExpr, string sdk, string resolvedFilename, SdkInfo resolvedSdk, DateTime timeStampUtc)
		{
			OriginalImport = importExpr;
			Filename = resolvedFilename;
			Sdk = sdk;
			TimeStampUtc = timeStampUtc;
			ResolvedSdk = resolvedSdk;
		}
	}
}