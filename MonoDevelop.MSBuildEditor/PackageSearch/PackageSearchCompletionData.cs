// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MonoDevelop.Ide.CodeCompletion;
using MonoDevelop.Ide.Gui;
using ProjectFileTools.NuGetSearch.Contracts;
using ProjectFileTools.NuGetSearch.Search;
using System;

namespace MonoDevelop.MSBuildEditor.PackageSearch
{
	class PackageSearchCompletionData : CompletionData
	{
		IPackageSearchManager manager;
		string packageId, packageVersion, tfm;

		public PackageSearchCompletionData (
			IPackageSearchManager manager, string name,
			string packageId, string packageVersion, string tfm)
			: base (name, Stock.Reference)
		{
			this.manager = manager;
			this.packageId = packageId;
			this.packageVersion = packageVersion;
			this.tfm = tfm;
		}

		public override Task<TooltipInformation> CreateTooltipInformation (bool smartWrap, CancellationToken cancelToken)
		{
			return manager.SearchPackageInfo (packageId, packageVersion, tfm, cancelToken).ContinueWith (
				t => PackageSearchHelpers.CreateTooltipInformation (t.Result),
				cancelToken, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
		}
	}
}
