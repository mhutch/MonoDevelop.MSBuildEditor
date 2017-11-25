// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MonoDevelop.Ide.CodeCompletion;
using MonoDevelop.Ide.Gui;
using System;

namespace MonoDevelop.MSBuildEditor
{
	public class GenerateGuidCompletionData : CompletionData
	{
		public GenerateGuidCompletionData ()
			: base ("(new GUID)", Stock.Add, "Generate a new GUID")
		{
		}

		public override string CompletionText {
			get => Guid.NewGuid().ToString ("B").ToUpper ();
			set => base.CompletionText = value;
		}
    }
}
