// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;

using Task = System.Threading.Tasks.Task;

namespace Microsoft.VisualStudio.LanguageServices.Implementation.LanguageService
{
	internal abstract partial class AbstractPackage<TPackage, TLanguageService> : AbstractPackage
        where TPackage : AbstractPackage<TPackage, TLanguageService>
        where TLanguageService : AbstractLanguageService<TPackage, TLanguageService>
    {
        private TLanguageService _languageService;

        protected AbstractPackage()
        {
        }

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await base.InitializeAsync(cancellationToken, progress).ConfigureAwait(true);

            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var shell = (IVsShell7)await GetServiceAsync(typeof(SVsShell)).ConfigureAwait(true);
            var solution = (IVsSolution)await GetServiceAsync(typeof(SVsSolution)).ConfigureAwait(true);
            cancellationToken.ThrowIfCancellationRequested();
            Assumes.Present(shell);
            Assumes.Present(solution);

            foreach (var editorFactory in CreateEditorFactories())
            {
                RegisterEditorFactory(editorFactory);
            }

            RegisterLanguageService(typeof(TLanguageService), async ct =>
            {
                await JoinableTaskFactory.SwitchToMainThreadAsync(ct);

                // Create the language service, tell it to set itself up, then store it in a field
                // so we can notify it that it's time to clean up.
                _languageService = CreateLanguageService();
                _languageService.Setup();
                return _languageService.ComAggregate;
            });

            LoadComponentsInUIContextOnceSolutionFullyLoadedAsync(cancellationToken).Forget();
        }

        protected override async Task LoadComponentsAsync(CancellationToken cancellationToken)
        {
            // Do the MEF loads and initialization in the BG explicitly.
            await TaskScheduler.Default;
        }

        protected abstract IEnumerable<IVsEditorFactory> CreateEditorFactories();
        protected abstract TLanguageService CreateLanguageService();

        protected void RegisterService<T>(Func<CancellationToken, Task<T>> serviceCreator)
            => AddService(typeof(T), async (container, cancellationToken, type) => await serviceCreator(cancellationToken).ConfigureAwait(true), promote: true);

        // When registering a language service, we need to take its ComAggregate wrapper.
        protected void RegisterLanguageService(Type t, Func<CancellationToken, Task<object>> serviceCreator)
            => AddService(t, async (container, cancellationToken, type) => await serviceCreator(cancellationToken).ConfigureAwait(true), promote: true);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // If we've created the language service then tell it it's time to clean itself up now.
                if (_languageService != null)
                {
                    _languageService.TearDown();
                    _languageService = null;
                }
            }

            base.Dispose(disposing);
        }
    }
}