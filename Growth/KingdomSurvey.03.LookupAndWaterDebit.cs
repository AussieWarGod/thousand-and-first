using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using XRL;
using XRL.Messages;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public partial class KingdomSurvey
	{
		public GameObject FindCitizen(int ResidentId)
		{
			if (ResidentId <= 0) return null;
			GameObject found = null;
			for (int i = 0; i < CitizenBodies.Count; i++)
			{
				GameObject item = CitizenBodies[i];
				IndexedRow row;
				if (!Rows.TryGetValue(item, out row) || row.ResidentId != ResidentId) continue;
				if (found != null) return null;
				found = item;
			}
			return found;
		}

		/// <summary>Exact resident-body witness from the maintained id index. Duplicate id bodies
		/// fail closed as Missing; publishing a transition from ambiguous physical evidence would
		/// conceal the very duplication the binding invariant exists to expose.</summary>
		internal bool TryWitnessResident(int ResidentId,
			out Simulation.City.KingdomBodyWitness Witness)
		{
			Witness = Simulation.City.KingdomBodyWitness.Missing;
			GameObject found = null;
			for (int i = 0; i < ResidentBodies.Count; i++)
			{
				GameObject item = ResidentBodies[i];
				if (!GameObject.Validate(item)
					|| Simulation.City.KingdomResidents.IdOf(item) != ResidentId) continue;
				if (found != null) return false;
				found = item;
			}
			if (found == null) return true;
			if (found.IsPlayerLed() || found.IsPlayer())
				Witness = Simulation.City.KingdomBodyWitness.Led;
			else Witness = found.IsAlive ? Simulation.City.KingdomBodyWitness.Present
				: Simulation.City.KingdomBodyWitness.Killed;
			return true;
		}

		/// <summary>One exact live body for a persisted engine id, restricted to the binding kind's
		/// already-classified subset. Null means absent or ambiguous.</summary>
		internal GameObject FindBoundBody(string ObjectId,
			Simulation.City.KingdomBindingKind Kind)
		{
			if (string.IsNullOrEmpty(ObjectId)) return null;
			List<GameObject> candidates = Kind == Simulation.City.KingdomBindingKind.Resident
				? ResidentBodies : Transients;
			GameObject found = null;
			for (int i = 0; i < candidates.Count; i++)
			{
				GameObject item = candidates[i];
				if (!GameObject.Validate(item)
					|| !string.Equals(item.IDIfAssigned, ObjectId, StringComparison.Ordinal)) continue;
				if (found != null) return null;
				found = item;
			}
			return found;
		}

		internal GameObject FindTransient(int JobId)
		{
			if (JobId <= 0) return null;
			GameObject found = null;
			for (int i = 0; i < Transients.Count; i++)
			{
				GameObject item = Transients[i];
				if (!GameObject.Validate(item)
					|| item.GetIntProperty(Simulation.City.KingdomResidents.JobIdProperty) != JobId)
					continue;
				if (found != null) return null;
				found = item;
			}
			return found;
		}

		public static bool ObserveAddedToActive(Zone Z, GameObject Item)
		{
			KingdomSurvey survey = ActiveFor(Z);
			return survey != null && survey.ObserveAdded(Item);
		}

		public static bool ObserveChangedInActive(Zone Z, GameObject Item)
		{
			KingdomSurvey survey = ActiveFor(Z);
			return survey != null && survey.ObserveChanged(Item);
		}

		/// <summary>Callback-failure seam: publish what physically exists, not what the callback
		/// returned or threw, into the one bound survey.</summary>
		public static bool ObserveCurrentTopologyInActive(Zone Z, GameObject Item)
		{
			KingdomSurvey survey = ActiveFor(Z);
			return survey != null && survey.ObserveCurrentTopology(Item);
		}

		/// <summary>AddObject may stack into or replace the attempted object. Re-prove both
		/// identities so a landed replacement refreshes instead of remaining stale.</summary>
		public static void ObserveAddResultInActive(Zone Z, GameObject Attempted,
			GameObject Accepted)
		{
			KingdomSurvey survey = ActiveFor(Z);
			if (survey == null) return;
			survey.ObserveCurrentTopology(Attempted);
			if (!ReferenceEquals(Accepted, Attempted))
				survey.ObserveCurrentTopology(Accepted);
		}

		public static bool ObserveRemovedFromActive(Zone Z, GameObject Item)
		{
			KingdomSurvey survey = ActiveFor(Z);
			return survey != null && survey.ObserveRemoved(Item);
		}

		/// <summary>
		/// As <see cref="Take(Zone)"/>, but also folds in the settlement-wide defence bonus its
		/// districts earn. A garrison trains the whole watch, not just the tower standing on it,
		/// so the bonus is read from every claimed zone's district, not only this one.
		/// </summary>
		/// <param name="Z">Zone to survey. Null yields an empty survey.</param>
		/// <param name="System">Kingdom whose claimed-zone districts contribute the bonus.</param>
		public static KingdomSurvey Take(Zone Z, KingdomSystem System)
		{
			KingdomSurvey survey = Take(Z);
			if (System != null)
			{
				survey.DistrictDefenceBonus = KingdomRules.DistrictsDefenceBonus(System.ZoneDistricts.Values);
			}
			return survey;
		}

		/// <summary>Reads current physical defensive shell supply, already scaled by exact
		/// staffing, condition, and affinity, then adds the realm's district bonus.</summary>
		public bool TryDefence(out int Amount, out string Failure)
		{
			Amount = 0; Failure = null;
			if (!TryBenefits(out KingdomBenefitIndex benefits, out Failure)) return false;
			return TryDefence(benefits, out Amount, out Failure);
		}

		/// <summary>Pass-scoped overload for callers that already hold this survey's immutable
		/// benefit observation. Avoids a second full physical scan in one semantic operation.</summary>
		internal bool TryDefence(KingdomBenefitIndex Benefits, out int Amount,
			out string Failure)
		{
			Amount = 0; Failure = null;
			if (Benefits == null)
			{
				Failure = "defence reading has no physical benefit observation"; return false;
			}
			long total = (long)Benefits.Total("defence")
				+ System.Math.Max(0, DistrictDefenceBonus);
			Amount = total >= int.MaxValue ? int.MaxValue : (int)total;
			return true;
		}

		/// <summary>Compatibility value surface. Failure is visible in the log and contributes
		/// zero; catalogue/root defence scalars are never a fallback.</summary>
		public int Defence()
		{
			if (TryDefence(out int amount, out string failure)) return amount;
			KingdomLog.Log("defence: " + (failure ?? "physical observation failed"));
			return 0;
		}

		/// <summary>Draws ordinary-use water from unleased dedicated stores without crossing a
		/// settlement-wide routed-input reserve floor.</summary>
		/// <param name="Drams">Amount requested.</param>
		/// <returns>Amount actually drawn, which may be less than requested.</returns>
		public int Consume(int Drams)
		{
			return ConsumeAvailable(Drams, true);
		}

		/// <summary>Draws the settlement's survival bill. Exact routed cargo remains unavailable,
		/// while the policy floor is spendable because sustaining residents is its purpose.</summary>
		internal int ConsumeUpkeep(int Drams)
		{
			return ConsumeAvailable(Drams, false);
		}

		private int ConsumeAvailable(int Drams, bool PreserveFloor)
		{
			if (Drams <= 0) return 0;
			KingdomConstructionInputLeaseSnapshot leases;
			string failure;
			int available;
			if (!KingdomConstructionInputLeaseAuthority.TryCapture(out leases, out failure)
				|| !KingdomConstructionInputLeaseAuthority.TryWaterAllowance(
					leases, this, PreserveFloor, out available, out failure)) return 0;
			int remaining = Math.Min(Drams, available);
			int budget = remaining;
			HashSet<LiquidVolume> seen = new HashSet<LiquidVolume>();
			for (int i = 0; i < Stores.Count && remaining > 0; i++)
			{
				LiquidVolume store = Stores[i];
				GameObject owner = store == null ? null : store.ParentObject;
				if (store == null || !seen.Add(store) || !GameObject.Validate(owner)
					|| owner.GetIntProperty("KingdomStores") != 1
					|| !ReferenceEquals(owner.GetPart<LiquidVolume>(), store)
					|| KingdomConstructionInputLeaseAuthority.IsLeased(leases, owner)
					|| !KingdomLiquids.HasFreshWater(store))
				{
					continue;
				}
				string leaseFailure;
				if (!KingdomConstructionInputLeaseAuthority.TryObjectAvailableForLocalDebit(
					owner, out leaseFailure)) continue;
				int removed = KingdomLiquids.Drain(store, remaining);
				if (removed > 0)
				{
					remaining -= removed;
					StoredWater -= removed;
					StorageSpace += removed;
					SynchronizeReceiptObject(store.ParentObject);
				}
			}
			return budget - remaining;
		}

		/// <summary>
		/// Reserves an all-or-nothing water debit against the exact dedicated vessels in this
		/// snapshot. Reservation does not remove water. The returned receipt must be
		/// <see cref="KingdomWaterDebit.Commit"/>ted after the caller's other preconditions pass,
		/// and may be <see cref="KingdomWaterDebit.Rollback"/>ed into those same vessels if the
		/// enclosing operation later fails. Use <see cref="Consume"/> instead where a deliberately
		/// partial simulation loss is the rule.
		/// </summary>
		/// <param name="Drams">Exact amount required. Non-positive amounts reserve a total no-op.</param>
		public KingdomWaterDebit ReserveExactWater(int Drams)
		{
			return KingdomWaterDebit.Reserve(this, Drams);
		}

		/// <summary>Try-pattern facade for callers that must not proceed without an exact receipt.</summary>
		public bool TryReserveExactWater(int Drams, out KingdomWaterDebit Debit)
		{
			Debit = ReserveExactWater(Drams);
			return Debit.State == KingdomWaterDebitState.Reserved;
		}
	}
}
