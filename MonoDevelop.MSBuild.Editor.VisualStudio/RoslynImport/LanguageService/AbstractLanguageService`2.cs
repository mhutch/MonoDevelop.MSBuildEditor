// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using System;

using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.TextManager.Interop;

namespace Microsoft.VisualStudio.LanguageServices.Implementation.LanguageService
{
	internal abstract partial class AbstractLanguageService<TPackage, TLanguageService> : AbstractLanguageService
        where TPackage : AbstractPackage<TPackage, TLanguageService>
        where TLanguageService : AbstractLanguageService<TPackage, TLanguageService>
    {
        internal TPackage Package { get; }

        // DevDiv 753309:
        // We've redefined some VS interfaces that had incorrect PIAs. When 
        // we interop with native parts of VS, they always QI, so everything
        // works. However, Razor is now managed, but assumes that the C#
        // language service is native. When setting breakpoints, they
        // get the language service from its GUID and cast it to IVsLanguageDebugInfo.
        // This would QI the native lang service. Since we're managed and
        // we've redefined IVsLanguageDebugInfo, the cast
        // fails. To work around this, we put the LS inside a ComAggregate object,
        // which always force a QueryInterface and allow their cast to succeed.
        // 
        // This also fixes 752331, which is a similar problem with the 
        // exception assistant.
        internal object ComAggregate { get; private set; }

        // Note: The lifetime for state in this class is carefully managed.  For every bit of state
        // we set up, there is a corresponding tear down phase which deconstructs the state in the
        // reverse order it was created in.
        internal IVsEditorAdaptersFactoryService EditorAdaptersFactoryService { get; private set; }

        /// <summary>
        /// Whether or not we have been set up. This is set once everything is wired up and cleared once tear down has begun.
        /// </summary>
        /// <remarks>
        /// We don't set this until we've completed setup. If something goes sideways during it, we will never register
        /// with the shell and thus have a floating thing around that can't be safely shut down either. We're in a bad
        /// state but trying to proceed will only make things worse.
        /// </remarks>
        private bool _isSetUp;

        protected AbstractLanguageService(TPackage package)
        {
            Package = package;
        }

        public override IServiceProvider SystemServiceProvider
            => Package;

        /// <summary>
        /// Setup and TearDown go in reverse order.
        /// </summary>
        internal void Setup()
        {
            Shell.ThreadHelper.ThrowIfNotOnUIThread ();

            this.ComAggregate = CreateComAggregate();

            // First, acquire any services we need throughout our lifetime.
            this.GetServices();

            // TODO: Is the below access to component model required or can be removed?
            _ = this.Package.ComponentModel;

            // Start off a background task to prime some components we'll need for editing
			/*
            VsTaskLibraryHelper.CreateAndStartTask(VsTaskLibraryHelper.ServiceInstance, VsTaskRunContext.BackgroundThread,
                () => PrimeLanguageServiceComponentsOnBackground());
			*/

            // Finally, once our connections are established, set up any initial state that we need.
            // Note: we may be instantiated at any time (including when the IDE is already
            // debugging).  We must not assume anything about our initial state and must instead
            // query for all the information we need at this point.
            this.Initialize();

            _isSetUp = true;
        }

        private object CreateComAggregate()
		{
            Shell.ThreadHelper.ThrowIfNotOnUIThread ();
            return Interop.ComAggregate.CreateAggregatedObject(this);
        }

        internal void TearDown()
        {
            if (!_isSetUp)
            {
                throw new InvalidOperationException();
            }

            _isSetUp = false;
            GC.SuppressFinalize(this);

            this.Uninitialize();
            this.RemoveServices();
        }

        ~AbstractLanguageService()
        {
            if (!Environment.HasShutdownStarted && _isSetUp)
            {
                throw new InvalidOperationException("TearDown not called!");
            }
        }

        protected virtual void GetServices()
        {
            // This method should only contain calls to acquire services off of the component model
            // or service providers.  Anything else which is more complicated should go in Initialize
            // instead.
            this.EditorAdaptersFactoryService = this.Package.ComponentModel.GetService<IVsEditorAdaptersFactoryService>();
        }

        protected virtual void RemoveServices()
        {
            this.EditorAdaptersFactoryService = null;
        }

        /// <summary>
        /// Called right after we instantiate the language service.  Used to set up any internal
        /// state we need.
        /// 
        /// Try to keep this method fairly clean.  Any complicated logic should go in methods called
        /// from this one.  Initialize and Uninitialize go in reverse order 
        /// </summary>
        protected virtual void Initialize()
        {
			/*
            InitializeLanguageDebugInfo();
			*/
		}

		protected virtual void Uninitialize()
        {
			/*
            UninitializeLanguageDebugInfo();
			*/
		}

		protected abstract string ContentTypeName { get; }
        protected abstract string LanguageName { get; }

        protected virtual void SetupNewTextView(IVsTextView textView)
        {
			/*
            Contract.ThrowIfNull(textView);

            var wpfTextView = EditorAdaptersFactoryService.GetWpfTextView(textView);
            Contract.ThrowIfNull(wpfTextView, "Could not get IWpfTextView for IVsTextView");

            Debug.Assert(!wpfTextView.Properties.ContainsProperty(typeof(AbstractVsTextViewFilter)));

            var workspace = Package.ComponentModel.GetService<VisualStudioWorkspace>();

            // The lifetime of CommandFilter is married to the view
            wpfTextView.GetOrCreateAutoClosingProperty(v =>
                new StandaloneCommandFilter(
                    v, Package.ComponentModel).AttachToVsTextView());
			*/
        }

		/*
        private void InitializeLanguageDebugInfo()
            => this.LanguageDebugInfo = this.CreateLanguageDebugInfo();

        protected abstract Guid DebuggerLanguageId { get; }

        private VsLanguageDebugInfo CreateLanguageDebugInfo()
        {
            var workspace = this.Workspace;
            var languageServices = workspace.Services.GetLanguageServices(RoslynLanguageName);

            return new VsLanguageDebugInfo(
                this.DebuggerLanguageId,
                (TLanguageService)this,
                languageServices,
                this.Package.ComponentModel.GetService<IThreadingContext>(),
                this.Package.ComponentModel.GetService<IUIThreadOperationExecutor>());
        }

        private void UninitializeLanguageDebugInfo()
            => this.LanguageDebugInfo = null;*/
    }
}