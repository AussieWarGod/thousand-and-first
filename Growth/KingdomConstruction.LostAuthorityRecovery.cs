using System.Collections.Generic;

using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomConstruction
	{
		private const int MaxLostAuthorityReceiptsPerPass = 8;
		internal const string LostAuthorityCursorKey =
			"$ThousandAndFirst_ConstructionInputLostAuthorityCursor";

		internal static void OnLostAuthorityAttendedPass(KingdomSystem system,
			Zone zone, KingdomSurvey survey)
		{
			if (Resolving || system == null || !system.Founded || zone == null || survey == null
				|| !ReferenceEquals(KingdomSurvey.ActiveFor(zone), survey)
				|| !KingdomMaster.AutomaticWorkAllowed(system)) return;
			if (!TryRead(out List<KingdomConstructionJob> jobs, out string _)) return;
			string owner = OwnerOf(system);
			if (string.IsNullOrEmpty(owner) || !TryReadLostAuthorityCursor(out string cursor)) return;
			int start = 0;
			for (int i = 0; i < jobs.Count; i++)
				if (jobs[i]?.Id == cursor) { start = (i + 1) % jobs.Count; break; }
			Resolving = true;
			try
			{
				int attended = 0;
				for (int offset = 0; offset < jobs.Count
					&& attended < MaxLostAuthorityReceiptsPerPass; offset++)
				{
					int i = (start + offset) % jobs.Count;
					KingdomConstructionJob job = jobs[i];
					if (job == null || job.OwnerKey != owner || job.Compacted
						|| string.IsNullOrEmpty(job.InputReceipt)
						|| !KingdomConstructionRules.TryGetInputReceipt(job,
							out KingdomConstructionInputReceipt receipt)
						|| receipt.ConstructionJobId != job.Id
						|| KingdomConstructionInputRules.IsTerminal(receipt)
						|| !InputReceiptTouchesZone(receipt, zone.ZoneID)) continue;
					bool cancellation = receipt.TxPhase == KingdomConstructionInputTxPhase.CancellationPending
						|| receipt.TxPhase == KingdomConstructionInputTxPhase.RollbackPending
						|| receipt.TxPhase == KingdomConstructionInputTxPhase.CompensationPending;
					bool commitWon = receipt.TxPhase == KingdomConstructionInputTxPhase.Closing
						|| InputCommitBoundaryWon(receipt);
					if (!cancellation && !commitWon)
					{
						if (!PublishLostAuthorityCursor(job.Id)) return;
						attended++;
						if (!Cancel(ref job,
							"Claim authority ended while exact input custody remained open.")) continue;
						if (!TryFind(job.Id, out job)
							|| !KingdomConstructionRules.TryGetInputReceipt(job, out receipt)) continue;
					}
					if (!LostAuthorityPartitionActionable(system, job, receipt, zone.ZoneID))
						continue;
					if (!PublishLostAuthorityCursor(job.Id)) return;
					attended++;
					DriveRoutedInput(system, zone, ref job, out _);
				}
			}
			finally { Resolving = false; }
		}

		private static bool TryReadLostAuthorityCursor(out string cursor)
		{
			cursor = null;
			if (The.Game?.ObjectGameState == null) return false;
			if (!The.Game.ObjectGameState.TryGetValue(LostAuthorityCursorKey,
				out object value)) return true;
			cursor = value as string;
			return !string.IsNullOrEmpty(cursor);
		}

		private static bool PublishLostAuthorityCursor(string jobId)
		{
			if (string.IsNullOrEmpty(jobId) || The.Game?.ObjectGameState == null) return false;
			The.Game.SetObjectGameState(LostAuthorityCursorKey, jobId);
			return The.Game.ObjectGameState.TryGetValue(LostAuthorityCursorKey,
				out object value) && value as string == jobId;
		}

		private static bool InputCommitBoundaryWon(KingdomConstructionInputReceipt receipt)
		{
			for (int i = 0; receipt != null && i < receipt.SourceCount; i++)
			{
				KingdomConstructionInputSourceLine source = receipt.SourceAt(i);
				KingdomConstructionInputCargoLine cargo = receipt.CargoAt(source.CargoOrdinal);
				if (source.Phase == KingdomConstructionInputSourcePhase.Spent
					|| cargo.Phase == KingdomConstructionInputCargoPhase.DebitIntent
					|| cargo.Phase == KingdomConstructionInputCargoPhase.Spent) return true;
			}
			return false;
		}

		private static bool LostAuthorityPartitionActionable(KingdomSystem system,
			KingdomConstructionJob job, KingdomConstructionInputReceipt receipt, string zoneId)
		{
			if (receipt == null || string.IsNullOrEmpty(zoneId)) return false;
			bool cancellation = receipt.TxPhase == KingdomConstructionInputTxPhase.CancellationPending
				|| receipt.TxPhase == KingdomConstructionInputTxPhase.RollbackPending
				|| receipt.TxPhase == KingdomConstructionInputTxPhase.CompensationPending;
			if (!cancellation) return zoneId == receipt.TargetZoneId;
			if (CancellationTargetPartitionRequired(system, job, receipt, out _, out _))
				return zoneId == receipt.TargetZoneId;
			int sourceOrdinal = NextCancellationSourceOrdinal(receipt);
			if (sourceOrdinal >= 0)
				return zoneId == receipt.SourceAt(sourceOrdinal).SourceZoneId;
			for (int i = 0; i < receipt.ChildCount; i++)
			{
				KingdomConstructionInputChild child = receipt.ChildAt(i);
				if (Simulation.City.KingdomCentralLogistics
					.ConstructionInputCarrierCustodyExists(system, job.Id, child.TripId))
					return zoneId == child.SourceZoneId;
			}
			return zoneId == receipt.TargetZoneId;
		}

		internal static bool HasNonterminalRoutedInputAuthority(KingdomSystem system,
			out string failure)
		{
			failure = null;
			if (!TryRead(out List<KingdomConstructionJob> jobs, out failure)) return true;
			for (int i = 0; i < jobs.Count; i++)
			{
				KingdomConstructionJob job = jobs[i];
				if (job == null || string.IsNullOrEmpty(job.InputReceipt)) continue;
				if (!KingdomConstructionRules.TryGetInputReceipt(job,
					out KingdomConstructionInputReceipt receipt))
				{ failure = "A routed-input receipt cannot be authenticated."; return true; }
				if (!KingdomConstructionInputRules.IsTerminal(receipt)
					|| receipt.TxPhase == KingdomConstructionInputTxPhase.Quarantined
					|| Simulation.City.KingdomCentralLogistics
						.ConstructionInputOwnerAuthorityExists(system, job.Id, receipt))
				{ failure = "Attended routed-input custody is still open."; return true; }
			}
			if (Simulation.City.KingdomCentralLogistics
				.AnyConstructionInputAuthorityExists(system))
			{ failure = "Orphan routed-input custody still exists."; return true; }
			return false;
		}
	}
}
