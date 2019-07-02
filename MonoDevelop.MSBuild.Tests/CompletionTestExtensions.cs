// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using NUnit.Framework;

namespace MonoDevelop.MSBuild.Tests
{
	public static class CompletionTestExtensions
	{
		public static void AssertContains (this CompletionContext context, string name)
		{
			var item = context.Items.FirstOrDefault (i => i.DisplayText == name);
			Assert.NotNull (item, "Completion result is missing item '{0}'", name);
		}

		public static void AssertNonEmpty (this CompletionContext context)
			=> Assert.NotZero (context.Items.Length);

		public static void AssertItemCount (this CompletionContext context, int expectedCount)
			=> Assert.AreEqual (expectedCount, context.Items.Length);

		public static void AssertDoesNotContain (this CompletionContext context, string name)
		{
			var item = context.Items.FirstOrDefault (i => i.DisplayText == name);
			Assert.IsNull (item, "Completion result has unexpected item '{0}'", name);
		}
	}
}
