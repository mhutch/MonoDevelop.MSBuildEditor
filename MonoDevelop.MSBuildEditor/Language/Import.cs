// Copyright (c) 2016 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace MonoDevelop.MSBuildEditor.Language
{
	class Import
	{
		public string Filename { get; private set; }
		public string Sdk { get; }
		public DateTime TimeStampUtc { get; }
		public MSBuildResolveContext ResolveContext { get; set; }
		public bool IsResolved { get { return ResolveContext != null; } }

		public Import (string filename, string sdk, DateTime timeStampUtc)
		{
			Filename = filename;
			Sdk = sdk;
			TimeStampUtc = timeStampUtc;
		}
	}
}