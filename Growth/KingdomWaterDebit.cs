using System;
using System.Collections.Generic;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	/// <summary>Read-only identity and CAS range exported by an exact reservation before commit.
	/// Callers may persist these facts as their own durable transaction receipt; no live part or
	/// rollback object belongs in save state.</summary>
	internal readonly struct KingdomWaterDebitLeg
	{
		internal readonly GameObject Owner;
		internal readonly int BeforeVolume;
		internal readonly int AfterVolume;
		internal readonly int MaxVolume;

		internal KingdomWaterDebitLeg(GameObject owner, int beforeVolume, int afterVolume,
			int maxVolume)
		{
			Owner = owner;
			BeforeVolume = beforeVolume;
			AfterVolume = afterVolume;
			MaxVolume = maxVolume;
		}
	}

	/// <summary>
	/// An all-or-nothing debit bound to the exact dedicated vessels measured by one
	/// <see cref="KingdomSurvey"/>. Reservation is read-only. Commit validates every vessel before
	/// changing the first, measures every drain, and restores every original snapshot if any drain
	/// or engine callback fails. Rollback returns water to those same vessels; it never pours an
	/// equivalent amount into whatever storage happens to be available later.
	/// </summary>
	public sealed partial class KingdomWaterDebit
	{
		private sealed class Entry
		{
			internal LiquidVolume Vessel;
			internal GameObject Owner;
			internal Zone OriginalZone;
			internal GameObject Carrier;
			internal bool Dedicated;
			internal int OriginalVolume;
			internal int OriginalMaxVolume;
			internal int Allocation;
			internal int MeasuredRemoved;
			internal bool DrainAttempted;
			internal bool DrainProved;
			internal bool ObservationUncertain;
			internal Dictionary<string, int> ComponentIdentity;
			internal Dictionary<string, int> OriginalComponents;
		}

		private readonly KingdomSurvey Survey;
		private readonly List<Entry> Entries = new List<Entry>();
		private bool Operating;

		public int Amount { get; private set; }

		public int VesselCount => Entries.Count;

		/// <summary>Requested drams physically credited to this receipt.</summary>
		public int Spent { get; private set; }

		/// <summary>Requested drams not proved paid. Do not retry when
		/// <see cref="MeasurementExact"/> is false.</summary>
		public int Outstanding { get; private set; }

		/// <summary>Full measured physical deficit, which may exceed the requested credit if a
		/// hostile callback changed more water than it was asked to.</summary>
		public int Lost { get; private set; }

		/// <summary>Whether every deficit was read from the same receipt-bound pure/empty vessel.</summary>
		public bool MeasurementExact { get; private set; }

		public KingdomWaterDebitState State { get; private set; }

		public KingdomWaterDebitFault Fault { get; private set; }

		/// <summary>Short diagnostic only; no exception object is retained in save-bearing state.</summary>
		public string Failure { get; private set; }

		/// <summary>
		/// True when a failed mutation path proved every receipt-bound vessel was put back exactly.
		/// A false return from <see cref="Commit"/> therefore never hides whether physical loss was
		/// compensated.
		/// </summary>
		public bool RestorationExact { get; private set; }

		private KingdomWaterDebit(KingdomSurvey survey, int amount)
		{
			Survey = survey;
			Amount = (amount > 0) ? amount : 0;
			Spent = 0;
			Outstanding = Amount;
			Lost = 0;
			MeasurementExact = true;
			State = KingdomWaterDebitState.Reserved;
			Fault = KingdomWaterDebitFault.None;
		}

		/// <summary>Reserves exact pure water from loose vessels directly carried by one actor.
		/// Nested containers, sealed vessels, stores on the ground, and cached surveys are excluded.</summary>
		internal static KingdomWaterDebit ReserveCarried(GameObject carrier, int amount)
		{
			KingdomSurvey survey = new KingdomSurvey();
			if (!GameObject.Validate(carrier) || carrier.Inventory == null
				|| carrier.Inventory.Objects == null)
				return new KingdomWaterDebit(survey, amount).FailReservation(
					KingdomWaterDebitFault.InvalidSurvey, "The carrier has no exact inventory.");
			for (int i = 0; i < carrier.Inventory.Objects.Count; i++)
			{
				GameObject owner = carrier.Inventory.Objects[i];
				LiquidVolume vessel = owner?.GetPart<LiquidVolume>();
				if (!GameObject.Validate(owner) || vessel == null || vessel.Sealed) continue;
				survey.Stores.Add(vessel);
				survey.StoredWater += vessel.Volume;
				survey.StorageCapacity += Math.Max(0, vessel.MaxVolume);
				survey.StorageSpace += Math.Max(0, vessel.MaxVolume - vessel.Volume);
			}
			return Reserve(survey, amount, carrier);
		}

		/// <summary>Copies exact vessel identities and before/after ranges while reservation is
		/// still pristine. Export is read-only and bounded by requested drams: every retained entry
		/// spends at least one.</summary>
		internal bool TryDescribe(out KingdomWaterDebitLeg[] legs)
		{
			legs = null;
			if (State != KingdomWaterDebitState.Reserved || Amount <= 0 || Entries.Count <= 0
				|| Entries.Count > Amount) return false;
			KingdomWaterDebitLeg[] copy = new KingdomWaterDebitLeg[Entries.Count];
			int total = 0;
			for (int i = 0; i < Entries.Count; i++)
			{
				Entry entry = Entries[i];
				if (!BindingMatches(entry) || entry.Allocation <= 0
					|| entry.Allocation > entry.OriginalVolume) return false;
				copy[i] = new KingdomWaterDebitLeg(entry.Owner, entry.OriginalVolume,
					entry.OriginalVolume - entry.Allocation, entry.OriginalMaxVolume);
				total += entry.Allocation;
			}
			if (total != Amount) return false;
			legs = copy;
			return true;
		}

		internal static KingdomWaterDebit Reserve(KingdomSurvey Survey, int Amount)
		{
			return Reserve(Survey, Amount, null);
		}

		private static KingdomWaterDebit Reserve(KingdomSurvey Survey, int Amount,
			GameObject Carrier)
		{
			KingdomWaterDebit debit = new KingdomWaterDebit(Survey, Amount);
			if (Survey == null)
			{
				return debit.FailReservation(KingdomWaterDebitFault.InvalidSurvey, "The survey is absent.");
			}
			if (Amount <= 0)
			{
				return debit;
			}

			try
			{
				KingdomConstructionInputLeaseSnapshot leases = null;
				if (Carrier == null)
				{
					string leaseFailure;
					int available;
					if (!KingdomConstructionInputLeaseAuthority.TryCapture(
						out leases, out leaseFailure)
						|| !KingdomConstructionInputLeaseAuthority.TryWaterAllowance(
							leases, Survey, true, out available, out leaseFailure))
						return debit.FailReservation(KingdomWaterDebitFault.InvalidSurvey,
							leaseFailure ?? "The durable routed-water leases cannot be read.");
					if (available < Amount)
						return debit.FailReservation(KingdomWaterDebitFault.InsufficientWater,
							"The settlement-wide routed-water reserve leaves too little spendable water.");
				}
				int count = Survey.Stores.Count;
				LiquidVolume[] vessels = new LiquidVolume[count];
				GameObject[] owners = new GameObject[count];
				int[] volumes = new int[count];
				bool[] pure = new bool[count];
				bool[] dedicated = new bool[count];

				for (int i = 0; i < count; i++)
				{
					LiquidVolume vessel = Survey.Stores[i];
					vessels[i] = vessel;
					if (vessel == null || SeenEarlier(vessels, i, vessel))
					{
						continue;
					}
					GameObject owner = vessel.ParentObject;
					owners[i] = owner;
					volumes[i] = vessel.Volume;
					pure[i] = KingdomLiquids.HasFreshWater(vessel);
					dedicated[i] = OwnsVessel(owner, vessel) && (Carrier == null
						? owner.GetIntProperty("KingdomStores") == 1
							&& !KingdomConstructionInputLeaseAuthority.IsLeased(leases, owner)
						: DirectlyCarried(Carrier, owner) && !vessel.Sealed);
				}

				int[] allocations;
				int total;
				KingdomWaterDebitFault fault;
				if (!KingdomWaterDebitRules.TryPlan(Amount, volumes, pure, dedicated,
					out allocations, out total, out fault))
				{
					return debit.FailReservation(fault, "The dedicated vessels cannot cover the exact debit.");
				}

				for (int i = 0; i < allocations.Length; i++)
				{
					if (allocations[i] <= 0)
					{
						continue;
					}
					LiquidVolume vessel = vessels[i];
					GameObject owner = owners[i];
					if (!OwnsVessel(owner, vessel)
						|| (Carrier == null ? owner.GetIntProperty("KingdomStores") != 1
							: !DirectlyCarried(Carrier, owner) || vessel.Sealed) ||
						vessel.Volume != volumes[i] || !KingdomLiquids.HasFreshWater(vessel) || vessel.MaxVolume < 0)
					{
						return debit.FailReservation(KingdomWaterDebitFault.VesselChanged,
							"A vessel changed while its exact allocation was being recorded.");
					}
					debit.Entries.Add(new Entry
					{
						Vessel = vessel,
						Owner = owner,
						OriginalZone = Carrier == null ? owner.CurrentZone : Carrier.CurrentZone,
						Carrier = Carrier, Dedicated = Carrier == null,
						OriginalVolume = vessel.Volume,
						OriginalMaxVolume = vessel.MaxVolume,
						Allocation = allocations[i],
						ComponentIdentity = vessel.ComponentLiquids,
						OriginalComponents = new Dictionary<string, int>(vessel.ComponentLiquids)
					});
				}
				if (total != debit.Amount)
				{
					return debit.FailReservation(KingdomWaterDebitFault.InsufficientWater,
						"The exact allocations do not sum to the requested debit.");
				}
				return debit;
			}
			catch (Exception ex)
			{
				return debit.FailReservation(KingdomWaterDebitFault.Exception, Describe(ex));
			}
		}

	}
}
