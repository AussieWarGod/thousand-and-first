using System;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>
	/// One synchronous Charter selection. Services mark their successful durable publication;
	/// the scope charges exactly once after the service has returned, then the Charter unwinds.
	/// Reading, cancellation, validation failure, and bookkeeping never mark the scope.
	/// </summary>
	public sealed class KingdomGovernanceScope : IDisposable
	{
		[ThreadStatic]
		private static KingdomGovernanceScope Active;

		private readonly GameObject Actor;

		private bool Disposed;

		private string Verb;

		public bool Committed { get; private set; }

		private KingdomGovernanceScope(GameObject Actor)
		{
			if (Active != null)
			{
				throw new InvalidOperationException("a governance action is already open");
			}
			this.Actor = Actor;
			Active = this;
		}

		public static KingdomGovernanceScope Begin(GameObject Actor)
		{
			return new KingdomGovernanceScope(Actor);
		}

		/// <summary>Marks the current Charter selection after its durable mutation succeeded.
		/// Returns false outside a Charter scope or after an earlier commit.</summary>
		public static bool Commit(string Verb)
		{
			KingdomGovernanceScope scope = Active;
			if (scope == null || scope.Disposed)
			{
				return false;
			}
			if (scope.Committed)
			{
				KingdomLog.Log("governance: refused a second commit in one Charter selection ("
					+ KingdomGovernanceRules.EnergyReason(Verb) + ")");
				return false;
			}
			scope.Committed = true;
			scope.Verb = Verb;
			return true;
		}

		/// <summary>Lets a nested menu unwind immediately after its first successful commit.</summary>
		public static bool HasCommitted
		{
			get { return Active != null && Active.Committed; }
		}

		public void Dispose()
		{
			if (Disposed)
			{
				return;
			}
			Disposed = true;
			if (Active == this)
			{
				Active = null;
			}
			else
			{
				KingdomLog.Log("governance: action scope lost its ownership before disposal");
			}
			if (!Committed)
			{
				return;
			}
			if (Actor == null || !GameObject.Validate(Actor))
			{
				KingdomLog.Log("governance: committed action had no valid actor to charge");
				return;
			}
			Actor.UseEnergy(KingdomGovernanceRules.NominalEnergyCost,
				KingdomGovernanceRules.EnergyReason(Verb));
		}
	}
}
