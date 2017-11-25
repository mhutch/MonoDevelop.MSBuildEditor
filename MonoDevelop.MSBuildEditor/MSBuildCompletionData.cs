// Copyright (c) 2014 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;
using MonoDevelop.Ide.CodeCompletion;
using MonoDevelop.MSBuildEditor.Language;
using MonoDevelop.MSBuildEditor.Schema;
using MonoDevelop.Xml.Completion;

namespace MonoDevelop.MSBuildEditor
{
	class MSBuildCompletionData : XmlCompletionData
	{
		readonly MSBuildParsedDocument doc;
		readonly MSBuildResolveResult rr;
		readonly BaseInfo info;

		public MSBuildCompletionData (BaseInfo info, MSBuildParsedDocument doc, MSBuildResolveResult rr, DataType type)
			: base (info.Name, info.Description, type)
		{
			this.info = info;
			this.doc = doc;
			this.rr = rr;
		}

		public override Task<TooltipInformation> CreateTooltipInformation (bool smartWrap, CancellationToken cancelToken)
		{
			return Task.FromResult (MSBuildTooltipProvider.CreateTooltipInformation (doc, info, rr));
		}
    }
}