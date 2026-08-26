using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using XRL;
using XRL.Core;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	public sealed partial class KingdomSeal : IPlayerSystem
	{
		private void TryReconcileProfile(string Reason)
		{
			if (ReconcileInProgress || !AuthorityEnabled || !SealEnabled())
			{
				return;
			}
			ReconcileInProgress = true;
			try
			{
				KingdomSealStore store = GetStore();
				int refusedReceipts;
				List<KingdomSealReceipt> receipts = store.ReadReceipts(out refusedReceipts);
				if (refusedReceipts > 0)
				{
					ReportFailure(Reason,
						"one or more receipt files could not be validated; reconciliation was refused");
					return;
				}
				int refusedStages;
				List<string> origins = store.StagedOrigins(out refusedStages);
				if (refusedStages > 0)
				{
					ReportFailure(Reason,
						"one or more stage filenames or the bounded stage scan could not be validated");
					return;
				}
				for (int i = 0; i < origins.Count; i++)
				{
					KingdomSealRecord stage = store.ReadStage(origins[i]);
					if (stage == null)
					{
						ReportFailure(Reason, "the stage journal for " + origins[i]
							+ " could not be validated");
						return;
					}
					if (stage.Status != KingdomSealStatus.Terminal)
					{
						continue;
					}
					string scoreFailure;
					bool exactScore;
					if (!TryExactScore(stage.OriginGameId, out exactScore, out scoreFailure))
					{
						ReportFailure("score proof for " + stage.OriginGameId, scoreFailure);
						continue;
					}
					string primaryFailure;
					KingdomSealPrimaryState primary = ExactPrimaryState(stage.OriginGameId,
						out primaryFailure);
					if (primary == KingdomSealPrimaryState.Unknown)
					{
						ReportFailure("primary proof for " + stage.OriginGameId, primaryFailure);
						continue;
					}
					if (!KingdomSealEngineRules.MayPromote(stage.Status, exactScore, primary))
					{
						continue;
					}
					KingdomSealRecord promoted = KingdomSealRules.Promote(stage,
						KingdomSealEligibility.Ended);
					string failure;
					if (!store.TryWriteLegacy(promoted, out failure))
					{
						ReportFailure("legacy promotion for " + stage.LegacyId, failure);
					}
				}

				Dictionary<string, KingdomSealPrimaryState> primaryByTarget =
					new Dictionary<string, KingdomSealPrimaryState>();
				for (int i = 0; i < receipts.Count; i++)
				{
					KingdomSealReceipt receipt = receipts[i];
					if (receipt.State != KingdomSealReceiptState.Reserved)
					{
						continue;
					}
					KingdomSealPrimaryState primary;
					if (!primaryByTarget.TryGetValue(receipt.TargetGameId, out primary))
					{
						string primaryFailure;
						primary = ExactPrimaryState(receipt.TargetGameId, out primaryFailure);
						primaryByTarget[receipt.TargetGameId] = primary;
						if (primary == KingdomSealPrimaryState.Unknown)
						{
							ReportFailure("receipt primary proof for " + receipt.TargetGameId,
								primaryFailure);
						}
					}
					if (primary == KingdomSealPrimaryState.Absent)
					{
						bool released;
						string releaseFailure;
						if (!store.TryReleaseAbandonedReservation(receipt, out released,
							out releaseFailure))
						{
							ReportFailure("abandoned reservation for " + receipt.LegacyId,
								releaseFailure);
						}
						else if (released)
						{
							KingdomLog.Log("seal: released interrupted import " + receipt.LegacyId
								+ " -> " + receipt.TargetGameId);
						}
						continue;
					}
					// A primary save proves only that the target exists. The inheritance owner
					// commits after Applied/AlreadyApplied and exact marker/object proof.
				}
			}
			catch (Exception ex)
			{
				ReportFailure(Reason, ex.Message, ex);
			}
			finally
			{
				ReconcileInProgress = false;
			}
		}

		private KingdomSealStore GetStore()
		{
			if (!AuthorityEnabled)
			{
				throw new InvalidOperationException(
					"The saved seal coordinator is disabled for this save.");
			}
			if (Store == null)
			{
				Store = new KingdomSealStore(ProfileRootPath());
			}
			return Store;
		}

		private bool SameCurrentIdentity(KingdomSealRecord Record)
		{
			return Record != null && Record.LineageId == LineageId
				&& Record.LegacyId == LegacyId && Record.OriginGameId == OriginGameId
				&& Record.Generation == Generation;
		}

		private bool HasCompleteIdentity => KingdomSealReceipt.ValidId(LineageId)
			&& KingdomSealReceipt.ValidId(LegacyId)
			&& KingdomSealReceipt.ValidId(OriginGameId)
			&& Generation >= 0 && Generation <= 1024 && Revision >= 0;

		private void AdoptIdentity(KingdomSealRecord Record)
		{
			LineageId = Record.LineageId;
			LegacyId = Record.LegacyId;
			OriginGameId = Record.OriginGameId;
			Generation = Record.Generation;
			Revision = Record.Revision;
		}

	}
}
