// Copyright (c) 2015 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;

using MonoDevelop.Ide.TypeSystem;

namespace MonoDevelop.MSBuildEditor
{
	class MSBuildDocumentParser : TypeSystemParser
	{
		public override Task<ParsedDocument> Parse (ParseOptions options, CancellationToken cancellationToken = default (CancellationToken))
		{
			return Task.Run (() => MSBuildParsedDocument.ParseInternal (options, cancellationToken), cancellationToken);
		}
	}
}
