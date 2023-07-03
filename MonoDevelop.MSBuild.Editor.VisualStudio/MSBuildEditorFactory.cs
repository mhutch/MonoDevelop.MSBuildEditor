// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices;

using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.LanguageServices.Implementation;

namespace MonoDevelop.MSBuild.Editor.VisualStudio
{
	[ComVisible (true)]
	[Guid (PackageConsts.EditorFactoryGuid)]
	class MSBuildEditorFactory : AbstractEditorFactory
	{
		public MSBuildEditorFactory (IComponentModel componentModel) : base (componentModel) { }

		protected override string ContentTypeName => MSBuildContentType.Name;

		protected override string LanguageName => PackageConsts.LanguageServiceName;
	}
}