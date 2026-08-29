using XRL;
using XRL.Messages;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomRelocation
	{
		/// <summary>Resolves at most the current move. Every state change is CAS-published.</summary>
		public static void OnZoneActivated(KingdomSystem System, Zone Zone, KingdomSurvey Survey)
		{
			if (Zone == null || !HasActive(Zone)) return;
			if (!TryRead(Zone, out KingdomRelocationReceipt receipt,
				out string expected, out string failure)) return;
			if (receipt.Phase == KingdomRelocationPhase.Quarantined) return;
			if (receipt.CurrentMove >= 0 && receipt.CurrentMove < receipt.Moves.Count
				&& (receipt.Moves[receipt.CurrentMove].Phase
						== KingdomRelocationMovePhase.RollingBack
					|| receipt.Moves[receipt.CurrentMove].Phase
						== KingdomRelocationMovePhase.RolledBack))
			{
				RollbackAndQuarantine(Zone, expected, receipt, receipt.Failure
					?? "The interrupted ring-call rollback is being reconciled.");
				return;
			}
			if (System == null || !System.Founded || System.RealmId != receipt.RealmId
				|| !System.ClaimedZones.Contains(Zone.ZoneID))
			{
				RollbackAndQuarantine(Zone, expected, receipt,
					"The ring-call ground is no longer held by its exact realm.");
				return;
			}
			if (receipt.Phase == KingdomRelocationPhase.Complete)
			{
				CleanCompletedArtifacts(Zone, receipt);
				TryRetire(Zone, expected, receipt, out _); return;
			}
			if (!ReconcileCurrent(Zone, ref expected, receipt, out failure)
				|| !TryRead(Zone, out receipt, out expected, out failure)) return;
			CleanCompletedArtifacts(Zone, receipt);
			if (!EnsureFrames(Zone, receipt, out GameObject frame, out failure))
			{
				Quarantine(Zone, expected, receipt, failure); return;
			}
			long now = The.Game == null ? receipt.Moves[receipt.CurrentMove].LastTick
				: The.Game.TimeTicks;
			KingdomRelocationMove move = receipt.Moves[receipt.CurrentMove];
			// A completed frame may wait on a creature, a callback, a save/load boundary, or
			// any individual row publication. Resume that exact handover before considering time.
			if (move.Phase == KingdomRelocationMovePhase.Handover)
			{
				if (!TryHandOver(System, Zone, ref expected, receipt, out failure)
					&& !string.IsNullOrEmpty(failure))
					KingdomLog.Log("relocation handover waits: " + failure);
				return;
			}
			if (move.LastTick <= System.MasterOptionTick)
			{
				PauseClock(Zone, expected, receipt, move, now); return;
			}
			if (!KingdomUpgrade.Enabled || !KingdomMaster.AutomaticWorkAllowed(System)
				|| receipt.Held)
			{
				PauseClock(Zone, expected, receipt, move, now); return;
			}
			int effectiveness = KingdomConstructionPresence.EffectivenessOf(frame, System,
				out _, out _);
			ArchitectureLabourProgress progress = KingdomArchitectureRules.AdvanceLabour(
				move.LastTick, now, move.RemainingTicks, effectiveness, 100);
			bool changed = move.Phase == KingdomRelocationMovePhase.Waiting
				|| progress.NextTick != move.LastTick
				|| progress.RemainingTicks != move.RemainingTicks;
			if (!changed) return;
			move.Phase = progress.Complete ? KingdomRelocationMovePhase.Handover
				: KingdomRelocationMovePhase.Working;
			move.LastTick = progress.NextTick; move.RemainingTicks = progress.RemainingTicks;
			if (progress.Complete) move.CompletionTick = progress.CompletionTick;
			if (!TryPublish(Zone, expected, receipt, out expected, out failure)) return;
			if (!progress.Complete) return;
			System.Ledger.Note("{{W|The receiving frame for " + (move.DisplayName ?? move.BuildKey)
				+ " is complete. The same whole lot is crossing now.}}");
			if (!TryHandOver(System, Zone, ref expected, receipt, out failure)
				&& !string.IsNullOrEmpty(failure))
				KingdomLog.Log("relocation handover waits: " + failure);
		}

		private static void PauseClock(Zone Zone, string Expected,
			KingdomRelocationReceipt Receipt, KingdomRelocationMove Move, long Now)
		{
			if (Now <= Move.LastTick) return;
			Move.LastTick = Now;
			TryPublish(Zone, Expected, Receipt, out _, out _);
		}

		public static void ReconcileZone(Zone Zone)
		{
			if (Zone == null || !HasActive(Zone)) return;
			KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
			KingdomSurvey survey = KingdomSurvey.ActiveFor(Zone) ?? KingdomSurvey.Take(Zone, system);
			OnZoneActivated(system, Zone, survey);
		}
	}
}
