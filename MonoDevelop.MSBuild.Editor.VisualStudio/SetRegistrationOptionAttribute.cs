// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.VisualStudio.Shell;

namespace MonoDevelop.MSBuild.Editor.VisualStudio
{
	[AttributeUsage (AttributeTargets.Class, AllowMultiple = true)]
	class SetRegistrationOptionAttribute : RegistrationAttribute
	{
		readonly string keyName, valueName;
		readonly object value;

		public SetRegistrationOptionAttribute (string keyName, string valueName, object value)
		{
			this.keyName = keyName;
			this.valueName = valueName;
			this.value = value;
		}

		public override void Register (RegistrationContext context)
		{
			using (Key key = context.CreateKey (keyName)) key.SetValue (valueName, value);
		}

		public override void Unregister (RegistrationContext context) => context.RemoveKey (keyName);
	}
}
