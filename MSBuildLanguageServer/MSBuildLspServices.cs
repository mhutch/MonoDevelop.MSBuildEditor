// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using Microsoft.CommonLanguageServerProtocol.Framework;
using Microsoft.VisualStudio.Composition;

class MSBuildLspServices (ExportProvider exportProvider) : ILspServices
{
	private readonly ExportProvider exportProvider = exportProvider;

	public void Dispose () {}

	public ImmutableArray<Type> GetRegisteredServices () => throw new NotSupportedException ();

	public bool SupportsGetRegisteredServices () => false;

	public T GetRequiredService<T> () where T : notnull => exportProvider.GetExportedValue<T> ();

	public IEnumerable<T> GetRequiredServices<T> () => exportProvider.GetExportedValues<T> ();

	public object? TryGetService (Type type) => exportProvider.GetExportedValues (type, null).FirstOrDefault ();
}
