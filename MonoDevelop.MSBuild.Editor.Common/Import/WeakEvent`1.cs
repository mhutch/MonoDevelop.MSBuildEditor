// IMPORTED from https://raw.githubusercontent.com/dotnet/roslyn/c8fba927cf53c6826e7dcf272abc1ff835a890f8/src/Workspaces/SharedUtilitiesAndExtensions/Compiler/Core/Utilities/WeakEvent%601.cs
// CHANGES:
// - Removed WeakEventHandler<T> delegate as it is already defined in MonoDevelop.Xml.Options

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using MonoDevelop.Xml.Options;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis;

internal readonly struct WeakEvent<TEventArgs>()
{
    /// <summary>
    /// Each registered event handler has the lifetime of an associated owning object. This table ensures the weak
    /// references to the event handlers are not cleaned up while the owning object is still alive.
    /// </summary>
    private readonly EnumerableConditionalWeakTable<object, WeakEventHandler<TEventArgs>> _handlers = new();

    public void AddHandler(object target, WeakEventHandler<TEventArgs> handler)
    {
        lock (_handlers.WriteLock)
        {
            if (_handlers.TryGetValue(target, out var existingHandler))
            {
                _handlers.AddOrUpdate(target, existingHandler + handler);
            }
            else
            {
                _handlers.Add(target, handler);
            }
        }
    }

    public void RemoveHandler(object target, WeakEventHandler<TEventArgs> handler)
    {
        lock (_handlers.WriteLock)
        {
            if (_handlers.TryGetValue(target, out var existingHandler))
            {
                var newHandler = existingHandler - handler;
                if (newHandler != null)
                {
                    _handlers.AddOrUpdate(target, newHandler);
                }
                else
                {
                    _handlers.Remove(target);
                }
            }
        }
    }

    public void RaiseEvent(object sender, TEventArgs e)
    {
        foreach (var (target, handler) in _handlers)
        {
            handler(sender, target, e);
        }
    }
}
