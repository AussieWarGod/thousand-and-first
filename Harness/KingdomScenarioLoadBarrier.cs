using System;
using System.Threading.Tasks;

namespace ThousandAndFirst.Harness
{
	// The owned runner terminates this terminal fixture; no worker outcome releases dispatch.
	internal sealed class KingdomScenarioLoadBarrier<T>
	{
		private readonly object Gate = new object();
		private readonly TaskCompletionSource<T> Parked = new TaskCompletionSource<T>();
		private bool IsClaimed;
		private Task StartedWork;

		internal bool Claimed
		{
			get { lock (Gate) return IsClaimed; }
		}

		internal Task<T> Pending { get { return Parked.Task; } }

		internal Task Work
		{
			get { lock (Gate) return StartedWork; }
		}

		internal bool TryClaim()
		{
			lock (Gate)
			{
				if (IsClaimed) return false;
				IsClaimed = true;
				return true;
			}
		}

		internal Task<T> Start(Func<Task> work)
		{
			if (work == null) throw new ArgumentNullException(nameof(work));
			lock (Gate)
			{
				if (!IsClaimed) throw new InvalidOperationException("load barrier is not claimed");
				if (StartedWork == null)
				{
					StartedWork = Task.Run(async delegate
					{
						Task running = work();
						if (running == null)
							throw new InvalidOperationException("load worker returned no task");
						await running.ConfigureAwait(false);
					});
				}
				return Parked.Task;
			}
		}
	}
}
