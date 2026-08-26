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

		/// <summary>
		/// The settlement's defence: the sum of its defensive works, counting only those with
		/// the crew to man them, plus any kingdom-wide bonus from garrison districts. A
		/// watchtower with nobody in it defends nothing; a garrison district defends everywhere.
		/// </summary>
		public int Defence()
		{
			int total = 0;
			for (int i = 0; i < Defences.Count; i++)
			{
				GameObject work = Defences[i];
				int need = work.GetIntProperty("KingdomStaffNeeded");
				int effectiveness = (need > 0) ? work.GetIntProperty("KingdomEffectiveness") : 100;
				effectiveness = KingdomCrews.ApplyAffinity(work, effectiveness);
				total += work.GetIntProperty("KingdomDefence") * effectiveness / 100;
			}
			return total + DistrictDefenceBonus;
		}

		/// <summary>Draws water from the dedicated stores, updating the survey's counters.</summary>
		/// <param name="Drams">Amount requested.</param>
		/// <returns>Amount actually drawn, which may be less than requested.</returns>
		public int Consume(int Drams)
		{
			int remaining = Drams;
			for (int i = 0; i < Stores.Count && remaining > 0; i++)
			{
				LiquidVolume store = Stores[i];
				if (!KingdomLiquids.HasFreshWater(store))
				{
					continue;
				}
				int removed = KingdomLiquids.Drain(store, remaining);
				if (removed > 0)
				{
					remaining -= removed;
					StoredWater -= removed;
					StorageSpace += removed;
					SynchronizeReceiptObject(store.ParentObject);
				}
			}
			return Drams - remaining;
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
