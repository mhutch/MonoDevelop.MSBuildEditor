using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;

using MonoDevelop.MSBuild.Editor.Completion;
using MonoDevelop.Xml.Logging;

using NuGet.VisualStudio;

using ProjectFileTools.NuGetSearch.Contracts;

namespace MonoDevelop.MSBuild.Editor.VisualStudio
{
	[Export (typeof (IPackageFeedRegistryProvider))]
	[Name ("Visual Studio Package Feed Registry Provider")]
	partial class PackageFeedRegistryProvider : IPackageFeedRegistryProvider
	{
		readonly object gate = new ();
		readonly JoinableTaskContext joinableTaskContext;
		readonly IVsPackageSourceProvider provider;
		readonly ILogger logger;
		Task<IReadOnlyList<string>> packageSourcesTask;

		[ImportingConstructor]
		public PackageFeedRegistryProvider (JoinableTaskContext joinableTaskContext, IVsPackageSourceProvider provider, MSBuildEnvironmentLogger logger)
		{
			this.joinableTaskContext = joinableTaskContext;
			this.provider = provider;
			this.logger = logger.Logger;

			provider.SourcesChanged += OnSourcesChanged;
			OnSourcesChanged (this, EventArgs.Empty);
		}

		void OnSourcesChanged (object sender, EventArgs e)
		{
			lock(gate) {
				packageSourcesTask = Task.Run (() => GetPackageSourcesAsync ()).WithTaskExceptionLogger (logger);
			}
		}

		async Task<IReadOnlyList<string>> GetPackageSourcesAsync ()
		{
			// docs for IVsPackageSourceProvider are wrong, in VS 17.3 and later it's no longer thread safe
			await joinableTaskContext.Factory.SwitchToMainThreadAsync ();

			List<string> sources;
			try {
				 sources = provider.GetSources (true, false).Select (s => s.Value).ToList ();
			} catch (Exception ex) {
				LogFailedToGetConfiguredSources (logger, ex);
				sources = [ "https://api.nuget.org/v3/index.json" ];
			}

			// make sure we always have the installed package cache as a source
			if (!sources.Any (x => x.IndexOf ("\\.nuget", StringComparison.OrdinalIgnoreCase) > -1)) {
				sources.Add (Environment.ExpandEnvironmentVariables ("%USERPROFILE%\\.nuget\\packages"));
			}

			return sources;
		}

		// TODO: on base interface, make this a "try" method and add the "changed" event
		public IReadOnlyList<string> ConfiguredFeeds {
			get {
				if (packageSourcesTask is not null && packageSourcesTask.Status == TaskStatus.RanToCompletion) {
// https://github.com/microsoft/vs-threading/issues/301
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
					return packageSourcesTask.Result;
#pragma warning restore VSTHRD002 // Avoid problematic synchronous waits
				}
				return Array.Empty<string> ();
			}
		}

		[LoggerMessage (EventId = 0, Level = LogLevel.Error, Message = "Failed to get configured NuGet sources")]
		static partial void LogFailedToGetConfiguredSources (ILogger logger, Exception ex);
	}
}
