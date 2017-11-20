// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using MonoDevelop.Core;
using MonoDevelop.MSBuildEditor.Schema;

namespace MonoDevelop.MSBuildEditor.Language
{
	class MonoDevelopMSBuildSchemaProvider : MSBuildSchemaProvider
	{
		public override MSBuildSchema GetSchema (Import import)
		{
			try {
				return base.GetSchema (import);
			} catch (Exception ex) {
				LoggingService.LogError ($"Failed to load schema for '${import.Filename}'", ex);
			}
			return null;
		}
	}
}