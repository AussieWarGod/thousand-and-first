using System;
using System.Collections.Generic;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public sealed partial class KingdomWaterDebit
	{
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
				SynchronizeCachedRows();
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
					SynchronizeCachedRows();
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
				if (State == KingdomWaterDebitState.Failed) ReconcilePhysicalRows();
				Operating = false;
			}
		}

	}
}
