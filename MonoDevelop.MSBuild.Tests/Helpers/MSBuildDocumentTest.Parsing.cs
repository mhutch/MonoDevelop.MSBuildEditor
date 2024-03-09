// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading;

using Microsoft.Extensions.Logging;

using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.MSBuild.Tests.Helpers;
using MonoDevelop.Xml.Parser;
using MonoDevelop.Xml.Tests;

namespace MonoDevelop.MSBuild.Tests;

partial class MSBuildDocumentTest
{
	public static MSBuildRootDocument GetParsedDocument (
		string source,
		ILogger logger = null,
		MSBuildSchema schema = null,
		MSBuildRootDocument previousDocument = null,
		CancellationToken cancellationToken = default
		)
	{
		// internal errors should cause test failure
		logger ??= TestLoggerFactory.CreateTestMethodLogger ().RethrowExceptions ();

		const string projectFileName = "FakeProject.csproj";

		var schemas = new TestSchemaProvider ();
		if (schema is not null) {
			schemas.AddTestSchema (projectFileName, null, schema);
		}

		var environment = new NullMSBuildEnvironment ();
		var taskMetadataBuilder = new NoopTaskMetadataBuilder ();

		return MSBuildRootDocument.Parse (
			new StringTextSource (source),
			projectFileName,
			previousDocument,
			schemas,
			environment,
			taskMetadataBuilder,
			logger,
			cancellationToken);
	}
}
