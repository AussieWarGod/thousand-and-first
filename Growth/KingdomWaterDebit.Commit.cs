using System;
using System.Collections.Generic;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public sealed partial class KingdomWaterDebit
	{
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
				if (!CurrentLeaseAuthorityAllowsDebit() || !AllStillReserved())
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
					string leaseFailure;
					if (entry.Dedicated && !KingdomConstructionInputLeaseAuthority
						.TryObjectAvailableForLocalDebit(entry.Owner, out leaseFailure))
					{
						bool notificationClean = true;
						bool exact = !drainStarted || RestoreAll(out notificationClean);
						if (exact) SetCleanClaim(); else MeasureClaim();
						return Fail(exact ? KingdomWaterDebitFault.VesselChanged
							: KingdomWaterDebitFault.RestoreFailed,
							(leaseFailure ?? "A routed-input lease claimed the next vessel.")
							+ (notificationClean ? "" : " Restoration notification also failed."),
							exact);
					}
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
				SynchronizeCachedRows();
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
				if (State == KingdomWaterDebitState.Failed) ReconcilePhysicalRows();
				Operating = false;
			}
		}

	}
}
