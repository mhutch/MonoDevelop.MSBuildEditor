// Copyright (c).NET Foundation and Contributors
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ProjectFileTools.NuGetSearch.Contracts;

namespace ProjectFileTools.NuGetSearch.Search;

internal class PackageFeedSearchJob<T> : IPackageFeedSearchJob<T>
{
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly ConcurrentDictionary<Task<IReadOnlyList<T>>, string> _taskMap = new ConcurrentDictionary<Task<IReadOnlyList<T>>, string>();
    private readonly object _updateSync = new object();
    private readonly object _cancellationSync = new object();
    private bool _isInitializing;

    public event EventHandler Updated;

    public PackageFeedSearchJob(IReadOnlyList<Tuple<string, Task<IReadOnlyList<T>>>> searchTasks, CancellationTokenSource cancellationTokenSource)
    {
        _cancellationTokenSource = cancellationTokenSource;
        List<string> searchingIn = new List<string>(searchTasks.Count);
        _isInitializing = true;
        Results = new List<T>();

        foreach (Tuple<string, Task<IReadOnlyList<T>>> taskInfo in searchTasks)
        {
            _taskMap[taskInfo.Item2] = taskInfo.Item1;
            taskInfo.Item2.ContinueWith(HandleCompletion);
            searchingIn.Add(taskInfo.Item1);
        }

        RemainingFeeds = searchingIn;
        SearchingIn = searchingIn;

        _isInitializing = false;
        foreach (Tuple<string, Task<IReadOnlyList<T>>> taskInfo in searchTasks)
        {
            if (taskInfo.Item2.IsCompleted)
            {
                HandleCompletion(taskInfo.Item2);
            }
        }
    }

    private void HandleCompletion(Task<IReadOnlyList<T>> task)
    {
        if (_isInitializing)
        {
            return;
        }

        if (_taskMap.TryRemove(task, out string name))
        {
            lock (_updateSync)
            {
                List<string> remaining = RemainingFeeds.ToList();
                remaining.Remove(name);
                RemainingFeeds = remaining;

                if (task.Result != null)
                {
                    List<T> results = Results.ToList();
                    results.AddRange(task.Result.Where(x => !Equals(x, default(T))));
                    Results = results;
                }
            }

            Updated?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Cancel()
    {
        if(RemainingFeeds.Count == 0)
        {
            return;
        }

        if (!_cancellationTokenSource.IsCancellationRequested)
        {
            lock (_cancellationSync)
            {
                if (!_cancellationTokenSource.IsCancellationRequested)
                {
                    _cancellationTokenSource.Cancel();
                    IsCancelled = true;
                }
            }
        }
    }

    public IReadOnlyList<string> RemainingFeeds { get; private set; }

    public IReadOnlyList<T> Results { get; private set; }

    public IReadOnlyList<string> SearchingIn { get; }

    public bool IsCancelled { get; private set; }
}