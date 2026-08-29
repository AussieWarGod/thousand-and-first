using System;
using System.Collections.Generic;

using XRL;

namespace ThousandAndFirst
{
	public static partial class KingdomConstruction
	{
		private const int MaxGlobalInputReceiptsPerTurn = 8;

		/// <summary>Bounded realm-global semantic audit. It may sweep neutral rows or publish
		/// cancellation, but never resolves ground, renders a body, or performs physical recovery.</summary>
		public static void OnGlobalRecoveryPass(KingdomSystem system)
		{
			if (Resolving || system == null || !system.Founded || The.Game == null
				|| !KingdomMaster.AutomaticWorkAllowed(system)) return;
			string owner = OwnerOf(system);
			if (string.IsNullOrEmpty(owner)) return;
			List<KingdomConstructionJob> jobs;
			string failure;
			if (!TryRead(out jobs, out failure))
			{
				KingdomLog.Log("construction recovery: " + failure);
				return;
			}
			List<string> adoptedOwners = new List<string>();
			for (int i = 0; i < jobs.Count; i++)
				if (jobs[i] != null && !string.IsNullOrEmpty(jobs[i].InputReceipt))
					adoptedOwners.Add(jobs[i].Id);
			Simulation.City.KingdomCityFault orphanFault;
			if (!Simulation.City.KingdomCentralLogistics
				.TrySweepOrphanedConstructionInputReservations(system, adoptedOwners,
					out orphanFault))
			{
				KingdomLog.Log("construction recovery: neutral route sweep refused ("
					+ orphanFault + ").");
				return;
			}
			int attended = 0;
			Resolving = true;
			try
			{
				for (int i = 0; i < jobs.Count
					&& attended < MaxGlobalInputReceiptsPerTurn; i++)
				{
					KingdomConstructionJob job = jobs[i];
					KingdomConstructionInputReceipt receipt;
					if (job == null || job.OwnerKey != owner
						|| string.IsNullOrEmpty(job.InputReceipt)
						|| job.Compacted
						|| !KingdomConstructionRules.TryGetInputReceipt(job, out receipt)) continue;
					bool authorized = system.Founded
						&& !string.IsNullOrEmpty(owner) && job.OwnerKey == owner
						&& RealmClaims(system, job.ZoneId)
						&& SourcesRemainClaimed(system, receipt);

					if (KingdomConstructionInputRules.IsTerminal(receipt))
					{
						for (int child = 0; child < receipt.ChildCount; child++)
							Simulation.City.KingdomCentralLogistics
								.TryClearConstructionInputRetirement(system, job.Id, receipt,
									receipt.ChildAt(child).TripId);
						if (!authorized
							&& receipt.TxPhase == KingdomConstructionInputTxPhase.Committed
							&& !KingdomConstructionRules.IsTerminal(job.Phase))
						{
							if (Quarantine(ref job,
								"Routed input committed before realm authority ended; projection is held for inspection."))
								attended++;
						}
						continue;
					}

					if (!authorized
						&& receipt.TxPhase != KingdomConstructionInputTxPhase.CancellationPending
						&& receipt.TxPhase != KingdomConstructionInputTxPhase.RollbackPending
						&& receipt.TxPhase != KingdomConstructionInputTxPhase.CompensationPending)
					{
						// Refusal means a debit intent/commit boundary already won. Continue exact
						// recovery; the terminal committed row is held above on the next pass.
						if (Cancel(ref job,
							"Realm or claim authority ended while routed input was in custody.")) attended++;
						if (!TryFind(job.Id, out job)
							|| !KingdomConstructionRules.TryGetInputReceipt(job, out receipt)) continue;
					}

					// Authorized receipts wait for attended source/target passes. Cancellation
					// likewise waits for exact active custody; global time authors no ground access.
				}
			}
			finally
			{
				Resolving = false;
			}
		}

		private static bool SourcesRemainClaimed(KingdomSystem system,
			KingdomConstructionInputReceipt receipt)
		{
			for (int i = 0; i < receipt.SourceCount; i++)
				if (!RealmClaims(system, receipt.SourceAt(i).SourceZoneId)) return false;
			return true;
		}

		private static bool RealmClaims(KingdomSystem system, string zoneId)
		{
			return system != null && system.OwnedZone(zoneId);
		}
	}
}
