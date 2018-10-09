// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Reflection;
using Microsoft.CodeAnalysis;
using BF = System.Reflection.BindingFlags;

namespace MonoDevelop.MSBuildEditor.Schema
{
	static class RoslynHelpers
	{
		static MethodInfo getReferenceMethod;
		static object metadataService;

		static RoslynHelpers ()
		{
			var metadataServiceType = typeof (Workspace).Assembly.GetType ("Microsoft.CodeAnalysis.Host.IMetadataService");
			getReferenceMethod = metadataServiceType.GetMethod ("GetReference", BF.Instance | BF.NonPublic | BF.Public);
			var services = Ide.TypeSystem.TypeSystemService.Workspace.Services;
			var getServiceMeth = services.GetType ().GetMethod ("GetService", BF.Instance | BF.NonPublic | BF.Public);
			getServiceMeth = getServiceMeth.MakeGenericMethod (metadataServiceType);
			metadataService = getServiceMeth.Invoke (services, null);
		}

		public static PortableExecutableReference GetReference (string resolvedPath, MetadataReferenceProperties properties = default)
		{
			//HACK: for some reason the cache fails on 7.3 but works on 7.4
			if (BuildInfo.Version.StartsWith ("7.3", StringComparison.Ordinal)) {
				return MetadataReference.CreateFromFile (resolvedPath);
			}
			return (PortableExecutableReference)getReferenceMethod.Invoke (metadataService, new object [] { resolvedPath, properties });
		}

		public static string GetFullName (this ITypeSymbol symbol)
		{
			var sb = new System.Text.StringBuilder ();
			var ns = symbol.ContainingNamespace;
			while (ns != null && !string.IsNullOrEmpty (ns.Name)) {
				sb.Insert (0, '.');
				sb.Insert (0, ns.Name);
				ns = ns.ContainingNamespace;
			}
			sb.Append (symbol.Name);
			return sb.ToString ();
		}
	}
}
