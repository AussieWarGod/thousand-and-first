using System;
using System.Threading.Tasks;

namespace ThousandAndFirst.Harness
{
	// Terminal fixtures stay parked. An explicit continuation releases only after worker success.
	internal sealed class KingdomScenarioLoadBarrier<T>
	{
		private readonly object Gate = new object();
		private readonly TaskCompletionSource<T> Parked = new TaskCompletionSource<T>();
		private bool IsClaimed;
		private Task StartedWork;
		private bool ResumePrepared;
		private T ResumeValue;

		internal void PrepareResume(T value)
		{
			if ((object)value == null) throw new ArgumentNullException(nameof(value));
			lock (Gate)
			{
				if (!IsClaimed || StartedWork == null || StartedWork.IsCompleted || ResumePrepared)
					throw new InvalidOperationException("load continuation is not available");
				ResumeValue = value;
				ResumePrepared = true;
			}
		}

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
						lock (Gate)
							if (ResumePrepared) Parked.TrySetResult(ResumeValue);
					});
				}
				return Parked.Task;
			}
		}
	}
}
