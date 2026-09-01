using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomLifecycleWireCodec
	{
		private static KingdomRaidLedger UpgradeRaidLedgerV1(KingdomRaidLedger x)
		{
			x.Version = 2;
			for (int i = 0; i < x.Incidents.Count; i++)
			{
				KingdomRaidIncident q = x.Incidents[i];
				long lead = q.DueTick > q.DeliveredTick ? q.DueTick - q.DeliveredTick : 1L;
				q.DemandLeadTicks = lead;
				q.DemandChannelId = KingdomLifecycleRules.ChildId(q.Id, "demand-channel", 0);
				if (q.State == KingdomRaidIncidentState.Queued)
				{
					q.DeliveredTick = 0L; q.DueTick = 0L; q.RemainingLeadTicks = lead;
					q.ChannelState = KingdomRaidChannelState.None;
				}
				else if (q.State == KingdomRaidIncidentState.Warned
					|| q.State == KingdomRaidIncidentState.ConfrontationReady)
				{
					q.DueTick = 0L; q.RemainingLeadTicks = q.State == KingdomRaidIncidentState.Warned
						? lead : 0L;
					q.ChannelState = KingdomRaidChannelState.RedeliveryQueued;
				}
				else q.ChannelState = KingdomRaidChannelState.Closed;
				if (q.ObjectiveObjectId != null && q.AttackOperationId == null)
					q.AttackOperationId = KingdomLifecycleRules.ChildId(q.Id, "legacy-attack", 0);
				if (q.State == KingdomRaidIncidentState.Resolved
					&& q.Resolution == KingdomRaidResolution.StoresPlundered)
				{
					q.RecoveryState = KingdomRaidRecoveryState.LegacyUnavailable;
					q.RecoveryResolvedTick = q.ResolvedTick;
					q.RecoveryNotice = "This raid predates the recovery contract; no recovery was fabricated.";
				}
			}
			return UpgradeRaidLedgerV2(x);
		}

		private static KingdomRaidLedger UpgradeRaidLedgerV2(KingdomRaidLedger x)
		{
			if (x == null || x.Version != 2) return x;
			x.Version = KingdomRaidLedger.CurrentVersion;
			for (int i = 0; x.Incidents != null && i < x.Incidents.Count; i++)
			{
				KingdomRaidIncident q = x.Incidents[i];
				if (q == null) continue;
				q.DefenceReservationVersion = 0;
				q.DefenceReservations = new List<KingdomRaidDefenceReservation>();
				if (q.State == KingdomRaidIncidentState.Fortified)
				{
					// V2 froze only object-id=score text. It cannot prove the resident rows,
					// bodies or posts that supplied that score, so reopen every answer without
					// silently reserving substitute people on load.
					q.Response = KingdomRaidResponse.None;
					q.State = KingdomRaidIncidentState.ConfrontationReady;
					q.DueTick = 0L;
					q.RemainingLeadTicks = 0L;
					q.DefenceEstimate = 0;
					q.DefenceCommitment = null;
					q.FortifyOrderedTick = 0L;
					q.LastNotice = "This older muster named works but not exact resident-row crews. "
						+ "Every answer is open again; nothing was spent or inferred.";
				}
				else if (q.Response == KingdomRaidResponse.Fortify
					&& (q.State == KingdomRaidIncidentState.Active
						|| q.State == KingdomRaidIncidentState.Resolved
						|| q.State == KingdomRaidIncidentState.Cancelled
						|| q.State == KingdomRaidIncidentState.Quarantined))
				{
					// The physical raid/result already owns causality. Retain no actionable
					// unproved commitment in the upgraded incident.
					q.DefenceEstimate = 0;
					q.DefenceCommitment = null;
				}
			}
			return x;
		}

		/// <summary>Archive v3-v7 reflected the exact unframed raid-v1 object graph. Keep that
		/// historical archive surface byte-frozen, then migrate it through the same decoder-owned
		/// upgrade used by the lifecycle wire. No caller may reinterpret a partial v2 graph.</summary>
		internal static bool UpgradeArchivedRaidLedgerV1(KingdomLifecycleBook book)
		{
			if (book == null || book.RaidLedger == null) return false;
			if (book.FormatVersion == KingdomLifecycleRules.RaidLedgerLifecycleFormatVersion
				|| book.FormatVersion == KingdomLifecycleRules.DefenceReservationLifecycleFormatVersion
				|| book.FormatVersion == KingdomLifecycleRules.LodgeTerminalLifecycleFormatVersion)
				book.FormatVersion = KingdomLifecycleRules.CurrentFormatVersion;
			else if (book.FormatVersion != KingdomLifecycleRules.CurrentFormatVersion) return false;
			if (book.RaidLedger.Version == KingdomRaidLedger.CurrentVersion)
				return KingdomRaidIncidentRules.ValidLedger(book.RaidLedger);
			if (book.RaidLedger.Version == 1)
				book.RaidLedger = UpgradeRaidLedgerV1(book.RaidLedger);
			else if (book.RaidLedger.Version == 2)
				book.RaidLedger = UpgradeRaidLedgerV2(book.RaidLedger);
			else return false;
			return KingdomRaidIncidentRules.ValidLedger(book.RaidLedger);
		}

	}
}
