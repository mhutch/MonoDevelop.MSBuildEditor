// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;

using MonoDevelop.Xml.Logging;

using Community.VisualStudio.Toolkit;

namespace MonoDevelop.MSBuild.Editor.VisualStudio.Logging;

// VS Community Toolkit's ExceptionExtensions.Log is super buggy if used from background threads
// as although it dispatches messages onto the main thread, it has race conditions that can cause
// many instances of the same log to be created.
// In addition, using the CreateOutputPaneTextWriterAsync is supposed to be more efficient anyways
// it uses an async queue.
// This helper class ensures that we only every create one instance of the output pane and writer
// and that once they are created, we don't have to do any work to obtain them again.
// it's also super defensive, and it it fails for any reason, it will disable itself.
class MSBuildOutputPaneWriter
{
	object lockObj = new ();
	JoinableTask<TextWriter>? outputPaneWriterTask;
	OutputWindowPane? pane;
	TextWriter? outputPaneTextWriter;
	readonly ILogger logger;
	bool isFaulted = false;

	public MSBuildOutputPaneWriter (ILogger logger)
	{
		this.logger = logger;
	}

	public bool WriteMessage (Exception? ex, EventId eventId, LogLevel logLevel, string message)
	{
		if (isFaulted) {
			return false;
		}

		try {
			if (outputPaneTextWriter is not null) {
				WriteFormattedMessage (outputPaneTextWriter, ex, eventId, logLevel, message);
			} else {
				CreatePaneAndWrite ((writer) => WriteFormattedMessage (writer, ex, eventId, logLevel, message)).Forget ();
			}
			return true;
		} catch (Exception internaLEx) {
			logger.LogInternalException (internaLEx);
			isFaulted = true;
			return false;
		}
	}

	void WriteFormattedMessage (TextWriter writer, Exception? ex, EventId eventId, LogLevel logLevel, string message)
	{
		if (ex is null) {
			writer.WriteLine ("{0}: {1}", logLevel, message);
		} else {
			var sb = new StringBuilder ();
			sb.Append (logLevel);
			sb.Append (": ");
			sb.AppendLine (message);
			sb.Append (ex.ToString ());
			sb.Replace ("\r\n", "\r\n  ");
			sb.Length -= 2;

			writer.WriteLine (sb.ToString ());

		}
	}

	async Task CreatePaneAndWrite (Action<TextWriter> value)
	{
		try {
			var writer = await GetOutputPaneWriterTask ();
			value (writer);
		} catch (Exception internaLEx) {
			logger.LogInternalException (internaLEx);
			isFaulted = true;
		}
	}

	JoinableTask<TextWriter> GetOutputPaneWriterTask ()
	{
		if (outputPaneWriterTask is JoinableTask<TextWriter> t) {
			return t;
		}
		lock (lockObj) {
			if (outputPaneWriterTask is JoinableTask<TextWriter> t2) {
				return t2;
			}
			outputPaneWriterTask = ThreadHelper.JoinableTaskFactory.RunAsync (GetOrCreateOutputPaneWriter);
			return outputPaneWriterTask;
		}
	}

	async Task<TextWriter> GetOrCreateOutputPaneWriter ()
	{
		pane = await VS.Windows.CreateOutputWindowPaneAsync ("MSBuild Editor", false);
		return outputPaneTextWriter = await pane.CreateOutputPaneTextWriterAsync ();
	}
}
