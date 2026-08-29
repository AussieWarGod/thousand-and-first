using System;
using System.Collections.Generic;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public sealed partial class KingdomWaterDebit
	{
		private void SetCommittedClaim()
		{
			Spent = Amount;
			Outstanding = 0;
			Lost = Amount;
			MeasurementExact = true;
		}

		private void SetCleanClaim()
		{
			for (int i = 0; i < Entries.Count; i++)
			{
				Entries[i].MeasuredRemoved = 0;
			}
			Spent = 0;
			Outstanding = Amount;
			Lost = 0;
			MeasurementExact = true;
		}

		private void MeasureClaim()
		{
			int count = Entries.Count;
			int[] original = new int[count];
			int[] current = new int[count];
			int[] removed = new int[count];
			bool[] same = new bool[count];
			bool[] water = new bool[count];
			for (int i = 0; i < count; i++)
			{
				Entry entry = Entries[i];
				original[i] = entry.OriginalVolume;
				removed[i] = entry.MeasuredRemoved;
				TryObserveClaimRow(entry, out current[i], out same[i], out water[i]);
				if (entry.ObservationUncertain) same[i] = false;
			}
			if (!KingdomWaterDebitRules.TryClassifyClaim(Amount, original, current, removed,
				same, water, out int spent, out int outstanding, out int lost, out bool exact))
			{
				Spent = 0;
				Outstanding = Amount;
				Lost = 0;
				MeasurementExact = false;
				return;
			}
			Spent = spent;
			Outstanding = outstanding;
			Lost = lost;
			MeasurementExact = exact;
		}

		/// <summary>
		/// Failure accounting runs from catch blocks, so observing a damaged engine object must
		/// itself be total. An unreadable row is uncertain; no stale-reference Drain return is
		/// credited as payment.
		/// </summary>
		private static void TryObserveClaimRow(Entry Entry, out int CurrentVolume,
			out bool SameVessel, out bool EmptyOrPureWater)
		{
			CurrentVolume = -1;
			SameVessel = false;
			EmptyOrPureWater = false;
			try
			{
				if (!BindingMatches(Entry))
				{
					return;
				}
				SameVessel = true;
				CurrentVolume = Entry.Vessel.Volume;
				EmptyOrPureWater = CurrentVolume == 0
					? Entry.Vessel.ComponentLiquids != null && Entry.Vessel.ComponentLiquids.Count == 0
					: KingdomLiquids.HasFreshWater(Entry.Vessel);
			}
			catch
			{
				CurrentVolume = -1;
				SameVessel = false;
				EmptyOrPureWater = false;
			}
		}

		private static int SaturatingAdd(int Left, int Right)
		{
			long sum = (long)Left + Right;
			return (sum > int.MaxValue) ? int.MaxValue : (int)sum;
		}

		private static bool TryAssignSnapshot(Entry Entry)
		{
			try
			{
				if (!BindingMatches(Entry) ||
					!StateMatches(Entry, Entry.OriginalVolume - Entry.Allocation))
				{
					return false;
				}
				Entry.ComponentIdentity.Clear();
				foreach (KeyValuePair<string, int> component in Entry.OriginalComponents)
				{
					Entry.ComponentIdentity.Add(component.Key, component.Value);
				}
				Entry.Vessel.Volume = Entry.OriginalVolume;
				return SnapshotMatches(Entry);
			}
			catch
			{
				return false;
			}
		}

		private static bool SnapshotMatches(Entry Entry)
		{
			try
			{
				return BindingMatches(Entry) && StateMatches(Entry, Entry.OriginalVolume);
			}
			catch
			{
				return false;
			}
		}

		private static bool OwnsVessel(GameObject Owner, LiquidVolume Vessel)
		{
			return GameObject.Validate(Owner) && Vessel != null && ReferenceEquals(Vessel.ParentObject, Owner) &&
				ReferenceEquals(Owner.GetPart<LiquidVolume>(), Vessel);
		}

		private static bool BindingMatches(Entry Entry)
		{
			try
			{
				return Entry != null && OwnsVessel(Entry.Owner, Entry.Vessel)
					&& LocationMatches(Entry) && IsDedicated(Entry) &&
					Entry.Vessel.MaxVolume == Entry.OriginalMaxVolume &&
					Entry.ComponentIdentity != null &&
					ReferenceEquals(Entry.Vessel.ComponentLiquids, Entry.ComponentIdentity);
			}
			catch
			{
				return false;
			}
		}

		private static bool StateMatches(Entry Entry, int ExpectedVolume)
		{
			return BindingMatches(Entry) && Entry.Vessel.Volume == ExpectedVolume &&
				CompositionMatches(Entry, ExpectedVolume);
		}

		private static bool CompositionMatches(Entry Entry, int Volume)
		{
			if (Entry == null || Entry.Vessel == null || Entry.Vessel.ComponentLiquids == null)
			{
				return false;
			}
			return Volume == 0
				? Entry.Vessel.ComponentLiquids.Count == 0
				: ComponentsMatch(Entry.Vessel.ComponentLiquids, Entry.OriginalComponents);
		}

		private static bool ComponentsMatch(Dictionary<string, int> Current,
			Dictionary<string, int> Expected)
		{
			if (Current == null || Expected == null || Current.Count != Expected.Count) return false;
			foreach (KeyValuePair<string, int> component in Expected)
			{
				int value;
				if (!Current.TryGetValue(component.Key, out value) || value != component.Value) return false;
			}
			return true;
		}

		private static bool IsDedicated(Entry Entry)
		{
			return Entry != null && Entry.Owner != null && (Entry.Dedicated
				? Entry.Owner.GetIntProperty("KingdomStores") == 1
				: DirectlyCarried(Entry.Carrier, Entry.Owner)
					&& Entry.Vessel != null && !Entry.Vessel.Sealed);
		}

		private static bool LocationMatches(Entry entry)
		{
			return entry != null && (entry.Dedicated
				? entry.Owner != null && ReferenceEquals(entry.Owner.CurrentZone, entry.OriginalZone)
				: GameObject.Validate(entry.Carrier)
					&& ReferenceEquals(entry.Carrier.CurrentZone, entry.OriginalZone)
					&& DirectlyCarried(entry.Carrier, entry.Owner));
		}

		private static bool DirectlyCarried(GameObject carrier, GameObject item)
		{
			return GameObject.Validate(carrier) && GameObject.Validate(item)
				&& carrier.Inventory != null && carrier.Inventory.Objects != null
				&& ReferenceEquals(item.InInventory, carrier)
				&& carrier.Inventory.Objects.Contains(item);
		}

		private static bool SeenEarlier(LiquidVolume[] Vessels, int Count, LiquidVolume Candidate)
		{
			for (int i = 0; i < Count; i++)
			{
				if (ReferenceEquals(Vessels[i], Candidate))
				{
					return true;
				}
			}
			return false;
		}

		private KingdomWaterDebit FailReservation(KingdomWaterDebitFault Fault, string Failure)
		{
			State = KingdomWaterDebitState.Failed;
			this.Fault = Fault;
			this.Failure = Failure;
			Entries.Clear();
			return this;
		}

		private bool Fail(KingdomWaterDebitFault Fault, string Failure, bool RestorationExact)
		{
			State = KingdomWaterDebitState.Failed;
			this.Fault = Fault;
			this.Failure = Failure;
			this.RestorationExact = RestorationExact;
			return false;
		}

		private static string Describe(Exception Exception)
		{
			if (Exception == null)
			{
				return "An unknown engine exception interrupted the exact debit.";
			}
			return Exception.GetType().Name + ": " + Exception.Message;
		}	}
}
