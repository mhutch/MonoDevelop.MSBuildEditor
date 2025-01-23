// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Composition;
using System.Collections.Generic;
using System.Collections.Immutable;

using MonoDevelop.Xml.Options;
using System.Threading;

namespace MonoDevelop.MSBuild.Editor.Options;

[Export (typeof (IGlobalOptionService)), Shared]
class GlobalOptionsService : IGlobalOptionService
{
	readonly Microsoft.CodeAnalysis.WeakEvent<OptionChangedEventArgs> optionChanged;

	readonly object locker = new ();
	ImmutableDictionary<IOption, object?> currentValues = ImmutableDictionary.Create<IOption, object?>();

	public void AddOptionChangedHandler (object target, WeakEventHandler<OptionChangedEventArgs> handler)
		=> optionChanged.AddHandler (target, handler);

	public void RemoveOptionChangedHandler (object target, WeakEventHandler<OptionChangedEventArgs> handler)
		=> optionChanged.RemoveHandler (target, handler);

	public T GetOption<T> (Option<T> option)
	{
		currentValues.TryGetValue (option, out var value);
		return (T)value!;
	}

	public ImmutableArray<object?> GetOptions (ImmutableArray<IOption> optionKeys)
	{
		var currentValues = this.currentValues;

		var builder = ImmutableArray.CreateBuilder<object?> (optionKeys.Length);
		foreach (var key in optionKeys) {
			currentValues.TryGetValue (key, out var value);
			builder.Add (value);
		}

		return builder.MoveToImmutable ();
	}

	void RaiseOptionChanged (ImmutableArray<(IOption, object?)> changes)
	{
		optionChanged.RaiseEvent (this, new OptionChangedEventArgs (changes));
	}

	public bool RefreshOption (IOption optionKey, object? newValue)
	{
		lock(locker) {
			if (currentValues.TryGetValue (optionKey, out var currentValue) && Equals (currentValue, newValue)) {
				return false;
			}
			currentValues = currentValues.SetItem (optionKey, newValue);
		}

		// unlike Roslyn we do not yet have "persisters"
		// so just forward this to SetGlobalOption
		SetGlobalOption (optionKey, newValue);

		RaiseOptionChanged ([(optionKey, newValue)]);
		return true;
	}

	public void SetGlobalOption<T> (Option<T> option, T value) => SetGlobalOption (option, (object?)value);

	public void SetGlobalOption (IOption optionKey, object? value)
	{
		lock(locker) {
			if (currentValues.TryGetValue (optionKey, out var currentValue) && Equals (currentValue, value)) {
				return;
			}
			currentValues = currentValues.SetItem (optionKey, value);
		}

		RaiseOptionChanged ([(optionKey, value)]);

		[(optionKey, newValue)]
		/*

		*/
	}

	public bool SetGlobalOptions (ImmutableArray<KeyValuePair<IOption, object?>> options)
	{
		using var _ = ArrayBuilder<(IOption, object?)>.GetInstance(options.Count, out var changedOptions);

		throw new System.NotImplementedException ();
	}

	public bool TryGetOption<T> (Option<T> option, out T? value)
	{
		throw new System.NotImplementedException ();
	}
}