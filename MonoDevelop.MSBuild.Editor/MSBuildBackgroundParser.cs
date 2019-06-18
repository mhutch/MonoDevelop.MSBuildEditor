// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Text;
using MonoDevelop.Xml.Editor.IntelliSense;
using MonoDevelop.Xml.Parser;

namespace MonoDevelop.MSBuild.Editor.Completion
{
	public class MSBuildBackgroundParser : XmlBackgroundParser<MSBuildParseResult>
	{
		protected override Task<MSBuildParseResult> StartParseAsync (
			ITextSnapshot2 snapshot, MSBuildParseResult previousParse,
			ITextSnapshot2 previousSnapshot, CancellationToken token)
		{
			var parser = new XmlParser (StateMachine, true);
			return Task.Run (() => {
				var length = snapshot.Length;
				for (int i = 0; i < length; i++) {
					parser.Push (snapshot[i]);
				}
				return new MSBuildParseResult (parser.Nodes.GetRoot (), parser.Diagnostics);
			});
		}
	}
}