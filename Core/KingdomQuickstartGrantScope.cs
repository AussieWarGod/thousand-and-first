using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>Owns only references returned by this attempt's factories until exact verification.</summary>
	internal sealed class KingdomQuickstartGrantScope<T> where T : class
	{
		private readonly List<T> Allocations = new List<T>();
		private bool Closed;
		private bool FactoryInterrupted;

		private KingdomQuickstartGrantScope() { }

		public T Create(Func<T> Factory)
		{
			if (Closed) throw new InvalidOperationException("The fresh grant scope is closed.");
			if (Factory == null) throw new ArgumentNullException("Factory");
			T item;
			try { item = Factory(); }
			catch { FactoryInterrupted = true; throw; }
			if (item != null && !Owns(item)) Allocations.Add(item);
			return item;
		}

		private bool Owns(T Item)
		{
			for (int i = 0; i < Allocations.Count; i++)
				if (ReferenceEquals(Allocations[i], Item)) return true;
			return false;
		}

		public static bool TryExecute(Func<KingdomQuickstartGrantScope<T>, T> PrepareAndPlace,
			Func<T, bool> Verify, Func<T, bool> RemoveExact, Func<bool> Quarantined,
			Action Quarantine, Action ReleaseQuarantine, out T Grant)
		{
			Grant = null;
			if (PrepareAndPlace == null || Verify == null || RemoveExact == null
				|| Quarantined == null || Quarantine == null || ReleaseQuarantine == null)
				throw new ArgumentNullException("A fresh grant callback is missing.");
			if (Quarantined()) return false;
			KingdomQuickstartGrantScope<T> scope = new KingdomQuickstartGrantScope<T>();
			bool committed = false;
			try
			{
				T candidate = PrepareAndPlace(scope);
				if (candidate == null || !scope.Owns(candidate) || !Verify(candidate)
					|| Quarantined()) return false;
				Grant = candidate;
				committed = true;
				return true;
			}
			finally
			{
				scope.Closed = true;
				if (!committed)
				{
					// Fence before callbacks: a save during partial rollback must forbid replacement.
					Exception fenceFailure = null;
					try { Quarantine(); }
					catch (Exception ex) { fenceFailure = ex; }
					bool removed = !scope.FactoryInterrupted;
					for (int i = scope.Allocations.Count - 1; i >= 0; i--)
					{
						try { if (!RemoveExact(scope.Allocations[i])) removed = false; }
						catch { removed = false; }
					}
					if (fenceFailure != null)
						throw new InvalidOperationException("Fresh grant cleanup lost its fence.", fenceFailure);
					if (removed) ReleaseQuarantine();
				}
			}
		}
	}
}
