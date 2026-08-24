using System;

namespace ThousandAndFirst
{
	/// <summary>The durable in-memory phase of one exact draw from dedicated water stores.</summary>
	public enum KingdomWaterDebitState
	{
		Failed = 0,
		Reserved = 1,
		Committed = 2,
		RolledBack = 3
	}

	/// <summary>Why an exact water receipt could not reach the requested phase.</summary>
	public enum KingdomWaterDebitFault
	{
		None = 0,
		InvalidSurvey = 1,
		InvalidVessels = 2,
		InsufficientWater = 3,
		VesselChanged = 4,
		SurveyChanged = 5,
		DrainMismatch = 6,
		Exception = 7,
		RestoreFailed = 8,
		Busy = 9
	}

	/// <summary>The engine action a receipt phase permits. Kept pure so every state is tested.</summary>
	public enum KingdomWaterDebitAction
	{
		Reject = 0,
		SucceedWithoutMutation = 1,
		Drain = 2,
		Restore = 3,
		CancelReservation = 4
	}

	/// <summary>
	/// Engine-free laws for exact water debits. Planning only apportions a request; it never
	/// changes the supplied arrays. The live receipt binds each non-zero allocation to the exact
	/// <c>LiquidVolume</c> from which it was measured.
	/// </summary>
	public static class KingdomWaterDebitRules
	{
		/// <summary>
		/// Classifies the physical claim left by a live debit. A still-bound pure/empty vessel
		/// makes its current deficit authoritative. When identity or liquid shape is no longer
		/// provable, no requested spend is credited and <paramref name="MeasurementExact"/> is
		/// false; a durable caller must quarantine the whole claim rather than retry it.
		/// </summary>
		public static bool TryClassifyClaim(int Requested, int[] OriginalVolumes,
			int[] CurrentVolumes, int[] MeasuredRemoved, bool[] SameVessel,
			bool[] EmptyOrPureWater, out int Spent, out int Outstanding, out int Lost,
			out bool MeasurementExact)
		{
			Spent = 0;
			Outstanding = (Requested > 0) ? Requested : 0;
			Lost = 0;
			MeasurementExact = false;
			if (Requested < 0 || OriginalVolumes == null || CurrentVolumes == null ||
				MeasuredRemoved == null || SameVessel == null || EmptyOrPureWater == null ||
				OriginalVolumes.Length != CurrentVolumes.Length ||
				OriginalVolumes.Length != MeasuredRemoved.Length ||
				OriginalVolumes.Length != SameVessel.Length ||
				OriginalVolumes.Length != EmptyOrPureWater.Length)
			{
				return false;
			}
			MeasurementExact = true;
			long lost = 0L;
			for (int i = 0; i < OriginalVolumes.Length; i++)
			{
				int original = OriginalVolumes[i];
				int measured = MeasuredRemoved[i];
				if (original < 0 || measured < 0)
				{
					MeasurementExact = false;
					original = (original > 0) ? original : 0;
					measured = (measured > 0) ? measured : 0;
				}
				int rowLost;
				if (SameVessel[i] && EmptyOrPureWater[i] && CurrentVolumes[i] >= 0)
				{
					rowLost = original - CurrentVolumes[i];
					if (rowLost < 0)
					{
						rowLost = 0;
					}
				}
				else
				{
					// A callback may have replaced or detached the measured part. Drain's return
					// was computed through that stale reference and is not payment proof.
					MeasurementExact = false;
					rowLost = 0;
				}
				lost += rowLost;
				if (lost > int.MaxValue)
				{
					lost = int.MaxValue;
				}
			}
			Lost = (int)lost;
			if (MeasurementExact)
			{
				Spent = (Lost < Requested) ? Lost : Requested;
				Outstanding = Requested - Spent;
			}
			else
			{
				Spent = 0;
				Outstanding = Requested;
			}
			return true;
		}

		/// <summary>Exact observable shape of one completed UseDrams call.</summary>
		public static bool DrainTransitionExact(int BeforeVolume, int AfterVolume,
			int Allocation, int ReturnedRemoved, bool ExactAfterState, bool ExactBinding)
		{
			return BeforeVolume > 0 && Allocation > 0 && Allocation <= BeforeVolume &&
				ReturnedRemoved == Allocation && AfterVolume == BeforeVolume - Allocation &&
				ExactAfterState && ExactBinding;
		}

		/// <summary>
		/// Plans an all-or-nothing draw in survey order. Non-positive requests are total no-ops.
		/// Impure or non-dedicated rows are unavailable, not partly admissible.
		/// </summary>
		public static bool TryPlan(
			int Amount,
			int[] Volumes,
			bool[] PureWater,
			bool[] Dedicated,
			out int[] Allocations,
			out int Total,
			out KingdomWaterDebitFault Fault)
		{
			Allocations = null;
			Total = 0;
			if (Volumes == null || PureWater == null || Dedicated == null ||
				Volumes.Length != PureWater.Length || Volumes.Length != Dedicated.Length)
			{
				Fault = KingdomWaterDebitFault.InvalidVessels;
				return false;
			}

			Allocations = new int[Volumes.Length];
			if (Amount <= 0)
			{
				Fault = KingdomWaterDebitFault.None;
				return true;
			}

			int remaining = Amount;
			for (int i = 0; i < Volumes.Length && remaining > 0; i++)
			{
				if (!PureWater[i] || !Dedicated[i] || Volumes[i] <= 0)
				{
					continue;
				}
				int take = (Volumes[i] < remaining) ? Volumes[i] : remaining;
				Allocations[i] = take;
				remaining -= take;
				Total += take;
			}

			if (remaining != 0)
			{
				Array.Clear(Allocations, 0, Allocations.Length);
				Total = 0;
				Fault = KingdomWaterDebitFault.InsufficientWater;
				return false;
			}
			Fault = KingdomWaterDebitFault.None;
			return true;
		}

		/// <summary>Every precondition that must still hold before the first live vessel changes.</summary>
		public static bool EntryStillReserved(
			int OriginalVolume,
			int CurrentVolume,
			int Allocation,
			bool PureWater,
			bool Dedicated,
			bool SameVessel,
			bool SameCapacity)
		{
			return EntryStillReserved(OriginalVolume, CurrentVolume, Allocation, PureWater,
				Dedicated, SameVessel, SameCapacity, true, true);
		}

		public static bool EntryStillReserved(
			int OriginalVolume,
			int CurrentVolume,
			int Allocation,
			bool PureWater,
			bool Dedicated,
			bool SameVessel,
			bool SameCapacity,
			bool SameZone,
			bool SameComponentIdentity)
		{
			return OriginalVolume > 0 && CurrentVolume == OriginalVolume &&
				Allocation > 0 && Allocation <= OriginalVolume && PureWater && Dedicated &&
				SameVessel && SameCapacity && SameZone && SameComponentIdentity;
		}

		/// <summary>Expected post-commit contents, used to keep compensation from overwriting later use.</summary>
		public static bool EntryStillCommitted(
			int OriginalVolume,
			int CurrentVolume,
			int Allocation,
			bool EmptyOrPureWater,
			bool Dedicated,
			bool SameVessel,
			bool SameCapacity)
		{
			return EntryStillCommitted(OriginalVolume, CurrentVolume, Allocation,
				EmptyOrPureWater, Dedicated, SameVessel, SameCapacity, true, true);
		}

		public static bool EntryStillCommitted(
			int OriginalVolume,
			int CurrentVolume,
			int Allocation,
			bool EmptyOrPureWater,
			bool Dedicated,
			bool SameVessel,
			bool SameCapacity,
			bool SameZone,
			bool SameComponentIdentity)
		{
			return OriginalVolume > 0 && Allocation > 0 && Allocation <= OriginalVolume &&
				CurrentVolume == OriginalVolume - Allocation && EmptyOrPureWater && Dedicated &&
				SameVessel && SameCapacity && SameZone && SameComponentIdentity;
		}

		/// <summary>Computes survey counters before mutation so integer failure cannot strand water.</summary>
		public static bool TryCountersAfterCommit(
			int StoredWater,
			int StorageSpace,
			int Amount,
			out int NewStoredWater,
			out int NewStorageSpace)
		{
			NewStoredWater = StoredWater;
			NewStorageSpace = StorageSpace;
			if (Amount < 0 || StoredWater < Amount)
			{
				return false;
			}
			long space = (long)StorageSpace + Amount;
			if (space > int.MaxValue || space < 0L)
			{
				return false;
			}
			NewStoredWater = StoredWater - Amount;
			NewStorageSpace = (int)space;
			return true;
		}

		/// <summary>Counter compensation for a committed receipt, likewise proved before mutation.</summary>
		public static bool TryCountersAfterRollback(
			int StoredWater,
			int StorageSpace,
			int Amount,
			out int NewStoredWater,
			out int NewStorageSpace)
		{
			NewStoredWater = StoredWater;
			NewStorageSpace = StorageSpace;
			if (Amount < 0 || StorageSpace < Amount)
			{
				return false;
			}
			long stored = (long)StoredWater + Amount;
			if (stored > int.MaxValue || stored < 0L)
			{
				return false;
			}
			NewStoredWater = (int)stored;
			NewStorageSpace = StorageSpace - Amount;
			return true;
		}

		public static KingdomWaterDebitAction CommitAction(KingdomWaterDebitState State)
		{
			switch (State)
			{
			case KingdomWaterDebitState.Reserved:
				return KingdomWaterDebitAction.Drain;
			case KingdomWaterDebitState.Committed:
				return KingdomWaterDebitAction.SucceedWithoutMutation;
			default:
				return KingdomWaterDebitAction.Reject;
			}
		}

		public static KingdomWaterDebitAction RollbackAction(KingdomWaterDebitState State)
		{
			switch (State)
			{
			case KingdomWaterDebitState.Reserved:
				return KingdomWaterDebitAction.CancelReservation;
			case KingdomWaterDebitState.Committed:
				return KingdomWaterDebitAction.Restore;
			case KingdomWaterDebitState.RolledBack:
				return KingdomWaterDebitAction.SucceedWithoutMutation;
			default:
				return KingdomWaterDebitAction.Reject;
			}
		}
	}
}
