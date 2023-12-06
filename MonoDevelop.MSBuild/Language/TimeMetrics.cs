// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;

namespace MonoDevelop.MSBuild.Language;

class TimeMetrics
{
	public int Count;
	public TimeSpan Total { get; private set; }
	public TimeSpan Max { get; private set; }
	public TimeSpan Min { get; private set; }
	public TimeSpan Average => TimeSpan.FromTicks (Total.Ticks / Count);

	void LogTimeMetric (TimeSpan timeSpan)
	{
		Total += timeSpan;
		Count++;
		if (timeSpan > Max) {
			Max = timeSpan;
		}
		if (timeSpan < Min || Min == TimeSpan.Zero) {
			Min = timeSpan;
		}
	}

	public IDisposable CreateTimer () => new TimeMetricsTimer (this);

	struct TimeMetricsTimer : IDisposable
	{
		int disposed;
		readonly DateTime start;
		readonly TimeMetrics parent;

		public TimeMetricsTimer (TimeMetrics parent)
		{
			this.parent = parent;
			start = DateTime.UtcNow;
		}

		public void Dispose ()
		{
			if (Interlocked.Exchange (ref disposed, 1) == 0) {
				var elapsed = DateTime.UtcNow - start;
				parent.LogTimeMetric (elapsed);

			}
		}
	}
}