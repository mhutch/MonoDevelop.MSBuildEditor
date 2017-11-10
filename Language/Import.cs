// Copyright (c) 2016 Xamarin Inc.
// Copyright (c) Microsoft. ALl rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace MonoDevelop.MSBuildEditor.Language
{
	class Import
	{
		public DateTime TimeStampUtc { get; }
		public string Filename { get; private set; }
		public MSBuildResolveContext ResolveContext { get; set; }
		public bool IsResolved { get { return ResolveContext != null; } }

		public Import (string filename, DateTime timeStampUtc)
		{
			Filename = filename;
			TimeStampUtc = timeStampUtc;
		}
	}
}