using System;
using System.Collections.Generic;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	/// <summary>
	/// An all-or-nothing debit bound to the exact dedicated vessels measured by one
	/// <see cref="KingdomSurvey"/>. Reservation is read-only. Commit validates every vessel before
	/// changing the first, measures every drain, and restores every original snapshot if any drain
	/// or engine callback fails. Rollback returns water to those same vessels; it never pours an
	/// equivalent amount into whatever storage happens to be available later.
	/// </summary>
	public sealed class KingdomWaterDebit
	{
		private sealed class Entry
		{
			internal LiquidVolume Vessel;
			internal GameObject Owner;
			internal Zone OriginalZone;
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

		internal static KingdomWaterDebit Reserve(KingdomSurvey Survey, int Amount)
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
					dedicated[i] = OwnsVessel(owner, vessel) && owner.GetIntProperty("KingdomStores") == 1;
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
					if (!OwnsVessel(owner, vessel) || owner.GetIntProperty("KingdomStores") != 1 ||
						vessel.Volume != volumes[i] || !KingdomLiquids.HasFreshWater(vessel) || vessel.MaxVolume < 0)
					{
						return debit.FailReservation(KingdomWaterDebitFault.VesselChanged,
							"A vessel changed while its exact allocation was being recorded.");
					}
					debit.Entries.Add(new Entry
					{
						Vessel = vessel,
						Owner = owner,
						OriginalZone = owner.CurrentZone,
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

		/// <summary>
		/// Drains every reserved allocation or restores every original vessel. Calling Commit again
		/// after success is an idempotent success; failed and rolled-back receipts never mutate.
		/// </summary>
		public bool Commit()
		{
			KingdomWaterDebitAction action = KingdomWaterDebitRules.CommitAction(State);
			if (action == KingdomWaterDebitAction.SucceedWithoutMutation)
			{
				return true;
			}
			if (action != KingdomWaterDebitAction.Drain)
			{
				return false;
			}
			if (Operating)
			{
				Fault = KingdomWaterDebitFault.Busy;
				return false;
			}

			Operating = true;
			int oldStored = Survey.StoredWater;
			int oldSpace = Survey.StorageSpace;
			bool drainStarted = false;
			try
			{
				if (!AllStillReserved())
				{
					return Fail(KingdomWaterDebitFault.VesselChanged,
						"A reserved vessel changed before the debit began.", false);
				}
				int newStored;
				int newSpace;
				if (!KingdomWaterDebitRules.TryCountersAfterCommit(
					Survey.StoredWater, Survey.StorageSpace, Amount, out newStored, out newSpace))
				{
					return Fail(KingdomWaterDebitFault.SurveyChanged,
						"The survey counters changed before the debit began.", false);
				}

				for (int i = 0; i < Entries.Count; i++)
				{
					Entry entry = Entries[i];
					int before = entry.Vessel.Volume;
					drainStarted = true;
					entry.DrainAttempted = true;
					int removed;
					try
					{
						removed = KingdomLiquids.Drain(entry.Vessel, entry.Allocation);
					}
					catch
					{
						if (!BindingMatches(entry) ||
							(!StateMatches(entry, entry.OriginalVolume) &&
							 !StateMatches(entry, entry.OriginalVolume - entry.Allocation)))
						{
							entry.ObservationUncertain = true;
						}
						throw;
					}
					bool transitionExact = KingdomWaterDebitRules.DrainTransitionExact(
						before, entry.Vessel == null ? -1 : entry.Vessel.Volume,
						entry.Allocation, removed,
						StateMatches(entry, entry.OriginalVolume - entry.Allocation),
						BindingMatches(entry)) && DrainProgressMatches(i);
					if (!transitionExact)
					{
						entry.ObservationUncertain = true;
						bool notificationClean;
						bool exact = RestoreAll(out notificationClean);
						if (exact)
						{
							SetCleanClaim();
						}
						else
						{
							MeasureClaim();
						}
						return Fail(!exact ? KingdomWaterDebitFault.RestoreFailed :
							(notificationClean ? KingdomWaterDebitFault.DrainMismatch : KingdomWaterDebitFault.Exception),
							"A vessel did not yield its exact allocation." +
							(notificationClean ? "" : " Restoration notification also failed."), exact);
					}
					entry.DrainProved = true;
					entry.MeasuredRemoved = SaturatingAdd(entry.MeasuredRemoved, entry.Allocation);
				}

				if (!AllStillCommitted() || Survey.StoredWater != oldStored || Survey.StorageSpace != oldSpace)
				{
					bool notificationClean;
					bool exact = RestoreAll(out notificationClean);
					if (exact) SetCleanClaim(); else MeasureClaim();
					return Fail(exact ? KingdomWaterDebitFault.SurveyChanged : KingdomWaterDebitFault.RestoreFailed,
						"The exact physical debit changed before survey accounting." +
						(notificationClean ? "" : " Restoration notification also failed."), exact);
				}
				Survey.StoredWater = newStored;
				Survey.StorageSpace = newSpace;
				State = KingdomWaterDebitState.Committed;
				SetCommittedClaim();
				Fault = KingdomWaterDebitFault.None;
				Failure = null;
				RestorationExact = false;
				return true;
			}
			catch (Exception ex)
			{
				bool notificationClean = true;
				bool exact = !drainStarted || RestoreAll(out notificationClean);
				if (exact)
				{
					SetCleanClaim();
				}
				else
				{
					MeasureClaim();
				}
				return Fail(exact ? KingdomWaterDebitFault.Exception : KingdomWaterDebitFault.RestoreFailed,
					Describe(ex) + (notificationClean ? "" : " Restoration notification also failed."), exact);
			}
			finally
			{
				Operating = false;
			}
		}

		/// <summary>
		/// Cancels an unused reservation or compensates a committed one into the same vessel
		/// instances and original volumes. Later edits to any bound vessel are never overwritten.
		/// </summary>
		public bool Rollback()
		{
			KingdomWaterDebitAction action = KingdomWaterDebitRules.RollbackAction(State);
			if (action == KingdomWaterDebitAction.SucceedWithoutMutation)
			{
				return true;
			}
			if (action == KingdomWaterDebitAction.CancelReservation)
			{
				State = KingdomWaterDebitState.RolledBack;
				Fault = KingdomWaterDebitFault.None;
				Failure = null;
				RestorationExact = true;
				SetCleanClaim();
				return true;
			}
			if (action != KingdomWaterDebitAction.Restore)
			{
				return false;
			}
			if (Operating)
			{
				Fault = KingdomWaterDebitFault.Busy;
				return false;
			}

			Operating = true;
			bool countersProved = false;
			bool restorationStarted = false;
			int oldStored = Survey.StoredWater;
			int oldSpace = Survey.StorageSpace;
			int newStored = 0;
			int newSpace = 0;
			try
			{
				if (!AllStillCommitted())
				{
					return Fail(KingdomWaterDebitFault.VesselChanged,
						"A committed vessel changed before rollback.", false);
				}
				if (!KingdomWaterDebitRules.TryCountersAfterRollback(
					Survey.StoredWater, Survey.StorageSpace, Amount, out newStored, out newSpace))
				{
					return Fail(KingdomWaterDebitFault.SurveyChanged,
						"The survey counters changed before rollback.", false);
				}
				countersProved = true;

				restorationStarted = true;
				bool notificationClean;
				bool exact = RestoreAll(out notificationClean);
				if (!exact)
				{
					MeasureClaim();
					return Fail(KingdomWaterDebitFault.RestoreFailed,
						"An original vessel could not be restored exactly.", false);
				}
				if (Survey.StoredWater != oldStored || Survey.StorageSpace != oldSpace)
				{
					SetCleanClaim();
					return Fail(KingdomWaterDebitFault.SurveyChanged,
						"The survey counters changed during exact vessel restoration.", true);
				}
				Survey.StoredWater = newStored;
				Survey.StorageSpace = newSpace;
				SetCleanClaim();
				if (!notificationClean)
				{
					// Update is presentation/notification after the authoritative liquid snapshots.
					// Once both the vessels and survey counters are exact, the compensation succeeded;
					// callers must not report lost water or try to compensate a second time.
					State = KingdomWaterDebitState.RolledBack;
					Fault = KingdomWaterDebitFault.Exception;
					Failure = "The vessels were restored exactly, but an engine refresh callback failed.";
					RestorationExact = true;
					KingdomLog.Log("water debit: " + Failure);
					return true;
				}
				State = KingdomWaterDebitState.RolledBack;
				Fault = KingdomWaterDebitFault.None;
				Failure = null;
				RestorationExact = true;
				return true;
			}
			catch (Exception ex)
			{
				if (!restorationStarted)
				{
					return Fail(KingdomWaterDebitFault.Exception, Describe(ex), false);
				}
				bool notificationClean = false;
				bool exact = AllSnapshotsMatch();
				if (exact && countersProved &&
					Survey.StoredWater == oldStored && Survey.StorageSpace == oldSpace)
				{
					Survey.StoredWater = newStored;
					Survey.StorageSpace = newSpace;
				}
				if (exact)
				{
					SetCleanClaim();
				}
				else
				{
					MeasureClaim();
				}
				return Fail(exact ? KingdomWaterDebitFault.Exception : KingdomWaterDebitFault.RestoreFailed,
					Describe(ex) + (notificationClean ? "" : " Restoration notification also failed."), exact);
			}
			finally
			{
				Operating = false;
			}
		}

		private bool AllStillReserved()
		{
			for (int i = 0; i < Entries.Count; i++)
			{
				Entry entry = Entries[i];
				if (!KingdomWaterDebitRules.EntryStillReserved(
					entry.OriginalVolume,
					entry.Vessel == null ? -1 : entry.Vessel.Volume,
					entry.Allocation,
					KingdomLiquids.HasFreshWater(entry.Vessel),
					IsDedicated(entry),
					OwnsVessel(entry.Owner, entry.Vessel),
					entry.Vessel != null && entry.Vessel.MaxVolume == entry.OriginalMaxVolume,
					entry.Owner != null && ReferenceEquals(entry.Owner.CurrentZone, entry.OriginalZone),
					entry.Vessel != null && ReferenceEquals(entry.Vessel.ComponentLiquids,
						entry.ComponentIdentity) && ComponentsMatch(entry.Vessel.ComponentLiquids,
						entry.OriginalComponents)))
				{
					return false;
				}
			}
			return true;
		}

		private bool AllStillCommitted()
		{
			for (int i = 0; i < Entries.Count; i++)
			{
				Entry entry = Entries[i];
				int volume = (entry.Vessel == null) ? -1 : entry.Vessel.Volume;
				bool emptyOrPure = entry.Vessel != null &&
					(volume == 0 ? entry.Vessel.ComponentLiquids.Count == 0 : KingdomLiquids.HasFreshWater(entry.Vessel));
				if (!KingdomWaterDebitRules.EntryStillCommitted(
					entry.OriginalVolume,
					volume,
					entry.Allocation,
					emptyOrPure,
					IsDedicated(entry),
					OwnsVessel(entry.Owner, entry.Vessel),
					entry.Vessel != null && entry.Vessel.MaxVolume == entry.OriginalMaxVolume,
					entry.Owner != null && ReferenceEquals(entry.Owner.CurrentZone, entry.OriginalZone),
					entry.Vessel != null && ReferenceEquals(entry.Vessel.ComponentLiquids,
						entry.ComponentIdentity) && CompositionMatches(entry, volume)))
				{
					return false;
				}
			}
			return true;
		}

		private bool RestoreAll(out bool NotificationClean)
		{
			NotificationClean = true;
			bool exact = true;
			for (int i = 0; i < Entries.Count; i++)
			{
				Entry entry = Entries[i];
				if (SnapshotMatches(entry))
				{
					continue;
				}
				if (!BindingMatches(entry) || !entry.DrainAttempted ||
					!StateMatches(entry, entry.OriginalVolume - entry.Allocation) ||
					!TryAssignSnapshot(entry))
				{
					entry.ObservationUncertain = true;
					NotificationClean = false;
					exact = false;
					continue;
				}
				try
				{
					entry.Vessel.Update();
				}
				catch
				{
					NotificationClean = false;
				}
				if (!SnapshotMatches(entry))
				{
					entry.ObservationUncertain = true;
					exact = false;
				}
				if (!RestoreProgressMatches(i)) exact = false;
			}
			return exact && AllSnapshotsMatch();
		}

		private bool AllSnapshotsMatch()
		{
			for (int i = 0; i < Entries.Count; i++)
			{
				if (!SnapshotMatches(Entries[i])) return false;
			}
			return true;
		}

		private bool DrainProgressMatches(int LastDrained)
		{
			bool exact = true;
			for (int i = 0; i < Entries.Count; i++)
			{
				Entry entry = Entries[i];
				int expected = i <= LastDrained
					? entry.OriginalVolume - entry.Allocation
					: entry.OriginalVolume;
				if (!StateMatches(entry, expected))
				{
					entry.ObservationUncertain = true;
					exact = false;
				}
			}
			return exact;
		}

		private bool RestoreProgressMatches(int RestoredThrough)
		{
			bool exact = true;
			for (int i = 0; i < Entries.Count; i++)
			{
				Entry entry = Entries[i];
				bool matches = SnapshotMatches(entry);
				if (!matches && i > RestoredThrough && entry.DrainAttempted)
				{
					matches = StateMatches(entry, entry.OriginalVolume - entry.Allocation);
				}
				if (!matches)
				{
					entry.ObservationUncertain = true;
					exact = false;
				}
			}
			return exact;
		}

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
				return Entry != null && OwnsVessel(Entry.Owner, Entry.Vessel) &&
					ReferenceEquals(Entry.Owner.CurrentZone, Entry.OriginalZone) &&
					Entry.Owner.GetIntProperty("KingdomStores") == 1 &&
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
			return Entry != null && Entry.Owner != null && Entry.Owner.GetIntProperty("KingdomStores") == 1;
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
		}
	}
}
