// Copyright (c) 2014 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;
using MonoDevelop.Ide.CodeCompletion;
using MonoDevelop.Ide.Gui;
using MonoDevelop.MSBuildEditor.Language;
using MonoDevelop.MSBuildEditor.Schema;
using MonoDevelop.Xml.Completion;

namespace MonoDevelop.MSBuildEditor
{
	class MSBuildCompletionData : XmlCompletionData
	{
		readonly MSBuildRootDocument doc;
		readonly MSBuildResolveResult rr;
		readonly BaseInfo info;

		int priorityGroup = 0;

		public MSBuildCompletionData (BaseInfo info, MSBuildRootDocument doc, MSBuildResolveResult rr, DataType type)
			: base (info.Name, null, type)
		{
			this.info = info;
			this.doc = doc;
			this.rr = rr;

			if (info is FileOrFolderInfo f) {
				Icon = f.IsFolder? Stock.ClosedFolder : Stock.GenericFile;
			}

			// deprioritize private values
			if (info.Name[0]=='_') {
				priorityGroup = -int.MaxValue;
			}
		}

		public override int PriorityGroup => priorityGroup;

		public override Task<TooltipInformation> CreateTooltipInformation (bool smartWrap, CancellationToken cancelToken)
		{
			return Task.FromResult (MSBuildTooltipProvider.CreateTooltipInformation (doc, info, rr));
		}
    }
}