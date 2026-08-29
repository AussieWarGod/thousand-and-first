using System;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>One synchronous right to mark an active governance scope after durable CAS.</summary>
	internal sealed class KingdomGovernanceReservation : IDisposable,
		IKingdomVocationServicePublication
	{
		private KingdomGovernanceScope Owner;
		internal readonly string Verb;

		internal KingdomGovernanceReservation(KingdomGovernanceScope owner, string verb)
		{
			Owner = owner; Verb = verb;
		}

		bool IKingdomVocationServicePublication.TryPublish(Func<bool> publish) =>
			Owner != null && Owner.PublishReserved(this, publish);

		public void Dispose()
		{
			KingdomGovernanceScope owner = Owner;
			if (owner != null) owner.CancelReserved(this);
		}

		internal void Finish()
		{
			Owner = null;
		}
	}

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

		private KingdomGovernanceReservation Reserved;

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
			if (scope == null || scope.Disposed || scope.Reserved != null)
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

		/// <summary>Reserves the only commit slot; disposal rolls back unless durable work commits it.</summary>
		internal static bool TryReserve(string verb,
			out KingdomGovernanceReservation reservation)
		{
			reservation = null;
			KingdomGovernanceScope scope = Active;
			if (scope == null || scope.Disposed || scope.Committed || scope.Reserved != null)
				return false;
			reservation = new KingdomGovernanceReservation(scope, verb);
			scope.Reserved = reservation;
			return true;
		}

		/// <summary>Runs a callback under the one governance token. False or exception invokes
		/// exact compensation before the reservation is released; only an uncompensated true return
		/// can mark the action committed.</summary>
		internal static bool TryPublish(string verb, Func<bool> publish,
			Func<bool> compensate)
		{
			if (publish == null || compensate == null || !TryReserve(verb,
				out KingdomGovernanceReservation reservation)) return false;
			using (reservation)
			{
				return ((IKingdomVocationServicePublication)reservation).TryPublish(delegate
				{
					bool published = false;
					try { published = publish(); }
					catch (Exception ex)
					{
						KingdomLog.Log("governance: " +
							KingdomGovernanceRules.EnergyReason(verb) +
							" publication threw (" + ex.Message + ")");
					}
					if (published) return true;
					try
					{
						if (compensate()) return false;
						KingdomLog.Log("governance: " +
							KingdomGovernanceRules.EnergyReason(verb) +
							" compensation was not exact");
					}
					catch (Exception ex)
					{
						KingdomLog.Log("governance: " +
							KingdomGovernanceRules.EnergyReason(verb) +
							" compensation threw (" + ex.Message + ")");
					}
					return false;
				});
			}
		}

		internal bool PublishReserved(KingdomGovernanceReservation reservation,
			Func<bool> publish)
		{
			if (Disposed || Committed || Active != this ||
				!ReferenceEquals(Reserved, reservation) || publish == null) return false;
			// Publication callback is synchronous and non-yielding. The public helper wraps it with
			// compensation, so only true reaches this scope mark.
			if (!publish()) return false;
			Reserved = null; Committed = true; Verb = reservation.Verb;
			reservation.Finish(); return true;
		}

		internal void CancelReserved(KingdomGovernanceReservation reservation)
		{
			if (ReferenceEquals(Reserved, reservation)) Reserved = null;
			reservation.Finish();
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
			if (Reserved != null)
			{
				KingdomGovernanceReservation reserved = Reserved;
				Reserved = null; reserved.Finish();
			}
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
