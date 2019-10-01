using System;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.MiniEditor;
using Microsoft.VisualStudio.Threading;
using MonoDevelop.MSBuild.Editor.Completion;
using MonoDevelop.Xml.Editor.Completion;
using MonoDevelop.Xml.Parser;
using MonoDevelop.Xml.Tests.Completion;
using MonoDevelop.Xml.Tests.EditorTestHelpers;

namespace MonoDevelop.MSBuild.Tests
{
	class TestEnvironment
	{
		static bool initialized;

		[Export]
		public static JoinableTaskContext MefJoinableTaskContext = null;

		public static EditorEnvironment EditorEnvironment { get; private set; }
		public static EditorCatalog EditorCatalog { get; private set; }

		public static (EditorEnvironment, EditorCatalog) EnsureInitialized ()
		{
			if (!initialized) {
				initialized = true;
				Initialize ();
			}
			return (EditorEnvironment, EditorCatalog);
		}

		static void Initialize ()
		{
			// Remember to initialize that JoinableTaskContext if you need it
			var mainloop = new MockMainLoop ();
			mainloop.Start ().Wait ();
			MefJoinableTaskContext = mainloop.JoinableTaskContext;
			System.Threading.SynchronizationContext.SetSynchronizationContext (mainloop);

			EditorEnvironment.DefaultAssemblies = new string[2]
			{
				typeof(EditorEnvironment).Assembly.Location, // Microsoft.VisualStudio.MiniEditor
				typeof (Microsoft.VisualStudio.Text.VirtualSnapshotPoint).Assembly.Location, //Microsoft.VisualStudio.Text.Logic
			}.ToImmutableArray ();

			// Create the MEF composition
			// can be awaited instead if your framework supports it
			EditorEnvironment = EditorEnvironment.InitializeAsync (
				typeof (XmlParser).Assembly.Location,
				typeof (XmlCompletionSource).Assembly.Location,
				typeof (MSBuildCompletionSource).Assembly.Location,
				typeof (TestEnvironment).Assembly.Location
			).Result;

			if (EditorEnvironment.CompositionErrors.Length > 0) {
				Console.WriteLine ("Composition Errors:");
				foreach (var error in EditorEnvironment.CompositionErrors)
					Console.WriteLine ("\t" + error);
			}

			// Register your own logging mechanism to print eventual errors
			// in your extensions
			var errorHandler = EditorEnvironment
				.GetEditorHost ()
				.GetService<EditorHostExports.CustomErrorHandler> ();

			errorHandler.ExceptionHandled += (s, e) => Console.WriteLine (e.Exception);

			EditorCatalog = new EditorCatalog (EditorEnvironment);
		}
	}
}
