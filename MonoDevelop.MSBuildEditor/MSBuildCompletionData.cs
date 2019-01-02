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
		readonly int priorityGroup = 0;

		public MSBuildCompletionData (BaseInfo info, MSBuildRootDocument doc, MSBuildResolveResult rr, DataType type)
			: base (info.Name, null, type)
		{
			switch (info) {
			case FunctionInfo fi: {
					foreach (var overload in fi.Overloads) {
						AddOverload (new MSBuildCompletionData (overload, doc, rr, type));
					}
					Icon = Stock.Method;
					break;
				}
			case PropertyInfo _:
				Icon = Stock.Property;
				break;
			case FileOrFolderInfo f:
				Icon = f.IsFolder ? Stock.ClosedFolder : Stock.GenericFile;
				break;
			case ClassInfo _:
				Icon = Stock.Class;
				break;
			}

			this.info = info;
			this.doc = doc;
			this.rr = rr;


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