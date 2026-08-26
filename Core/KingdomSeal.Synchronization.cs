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
		private bool TryCompletePendingGeneration(KingdomSystem Kingdom, out string Failure)
		{
			Failure = "";
			if (!TryRequireAuthority(out Failure))
			{
				return false;
			}
			if (string.IsNullOrEmpty(PendingAccessionToken))
			{
				return true;
			}
			if (!KingdomSealEngineRules.AccessionTokenIsOrdinal(PendingAccessionToken,
				Generation))
			{
				Failure = "the pending accession token is not canonical for this generation";
				return false;
			}
			if (Kingdom == null || !Kingdom.Founded)
			{
				Failure = "the pending accession has no founded realm to capture";
				return false;
			}
			KingdomSealStore store = GetStore();
			KingdomSealRecord existing = store.ReadStage(OriginGameId);
			if (existing != null && SameCurrentIdentity(existing)
				&& existing.Status == KingdomSealStatus.Living)
			{
				if (!store.TryCompleteGenerationAdvance(existing, out Failure))
				{
					return false;
				}
				Revision = existing.Revision;
				LastAccessionToken = PendingAccessionToken;
				PendingAccessionToken = "";
				Dirty = true;
				return true;
			}
			if (existing == null || existing.LineageId != LineageId
				|| existing.OriginGameId != OriginGameId
				|| existing.Generation + 1 != Generation
				|| (existing.Status != KingdomSealStatus.Living
					&& existing.Status != KingdomSealStatus.Retired))
			{
				Failure = "the pending accession cannot find its exact prior generation";
				return false;
			}
			KingdomSealRecord successor;
			if (!TryCapture(Kingdom, LegacyId, Generation, Revision,
				SafeTick(The.Game.TimeTicks), out successor, out Failure))
			{
				return false;
			}
			if (!store.TryAdvanceGeneration(existing, successor, out Failure))
			{
				return false;
			}
			LastAccessionToken = PendingAccessionToken;
			PendingAccessionToken = "";
			Dirty = true;
			return true;
		}

		private bool TrySynchronizeLoadedWorld(out string Failure)
		{
			Failure = "";
			if (!TryRequireAuthority(out Failure))
			{
				return false;
			}
			XRLGame game = The.Game;
			KingdomSystem kingdom = game?.GetSystem<KingdomSystem>();
			if (game == null || kingdom == null || !kingdom.Founded)
			{
				return true;
			}
			if (!EnsureIdentity(kingdom, out Failure))
			{
				return false;
			}
			if (!string.IsNullOrEmpty(PendingAccessionToken))
			{
				return TryCompletePendingGeneration(kingdom, out Failure);
			}
			KingdomSealStore store = GetStore();
			KingdomSealRecord stage = store.ReadStage(OriginGameId);
			if (IsGenerationSealed)
			{
				if (stage != null && stage.Status == KingdomSealStatus.Retired
					&& SameCurrentIdentity(stage))
				{
					return TryCompleteRetirement(stage, out Failure);
				}
				string retirementPrimaryFailure;
				if (ExactPrimaryState(OriginGameId, out retirementPrimaryFailure)
					!= KingdomSealPrimaryState.Present)
				{
					Failure = "the saved retirement differs from its journal, but its exact primary could not be proved standing"
						+ (string.IsNullOrEmpty(retirementPrimaryFailure) ? ""
							: ": " + retirementPrimaryFailure);
					return false;
				}
				if (!store.TryRestoreRetiredGeneration(new KingdomSealLineage(LineageId,
					LegacyId, OriginGameId, Generation, Revision), out Failure))
				{
					return false;
				}
				stage = store.ReadStage(OriginGameId);
				if (stage == null || stage.Status != KingdomSealStatus.Retired
					|| !SameCurrentIdentity(stage))
				{
					Failure = "the saved retirement did not restore one exact retired journal";
					return false;
				}
				return TryCompleteRetirement(stage, out Failure);
			}
			if (stage == null)
			{
				Dirty = true;
				return TryFlushLiving("loaded world", ProbeEvenIfClean: true, out Failure);
			}
			if (stage.Status == KingdomSealStatus.Retired && SameCurrentIdentity(stage))
			{
				return TryCompleteRetirement(stage, out Failure);
			}

			KingdomSealRecord saved;
			if (!TryCapture(kingdom, LegacyId, Generation, Revision,
				SafeTick(game.TimeTicks), out saved, out Failure))
			{
				return false;
			}
			if (!KingdomSealEngineRules.MayRestoreLoadedPrimary(stage, saved))
			{
				Failure = "the loaded primary is not the same as, or older than, a living or terminal stage";
				return false;
			}
			string primaryFailure;
			if (ExactPrimaryState(OriginGameId, out primaryFailure) != KingdomSealPrimaryState.Present)
			{
				Failure = "the loaded generation differs from its stage, but its exact primary could not be proved standing"
					+ (string.IsNullOrEmpty(primaryFailure) ? "" : ": " + primaryFailure);
				return false;
			}
			if (stage.Status == KingdomSealStatus.Terminal)
			{
				int canceledRevision;
				if (!KingdomSealEngineRules.TryNextRevision(stage.Revision,
					out canceledRevision))
				{
					Failure = "the terminal attempt revision is exhausted";
					return false;
				}
				KingdomSealRecord canceled;
				if (!TryCapture(kingdom, stage.LegacyId, stage.Generation,
					canceledRevision, SafeTick(game.TimeTicks), out canceled, out Failure)
					|| !store.TryStage(canceled, out Failure))
				{
					return false;
				}
				stage = canceled;
			}
			if (!store.TryRestoreLivingGeneration(saved, out Failure))
			{
				// One living write may sit beside the terminal it canceled. A higher living
				// revision replaces that remaining slot; Store then accepts the exact primary.
				int scrubRevision;
				if (!KingdomSealEngineRules.TryNextRevision(stage.Revision, out scrubRevision))
				{
					Failure = "the recoverable living journal revision is exhausted";
					return false;
				}
				KingdomSealRecord scrub;
				if (!TryCapture(kingdom, stage.LegacyId, stage.Generation,
					scrubRevision, SafeTick(game.TimeTicks), out scrub, out Failure)
					|| !store.TryStage(scrub, out Failure)
					|| !store.TryRestoreLivingGeneration(saved, out Failure))
				{
					return false;
				}
			}
			Revision = saved.Revision;
			Dirty = false;
			DirtyReason = null;
			return true;
		}

		private bool EnsureIdentity(KingdomSystem Kingdom, out string Failure)
		{
			Failure = "";
			if (!TryRequireAuthority(out Failure))
			{
				return false;
			}
			XRLGame game = The.Game;
			if (game == null || Kingdom == null || !Kingdom.Founded
				|| !KingdomSealReceipt.ValidId(game.GameID))
			{
				Failure = "the founded realm or its game has no valid identity";
				return false;
			}
			if (HasCompleteIdentity)
			{
				if (OriginGameId != game.GameID)
				{
					Failure = "the saved seal identity belongs to another game";
					return false;
				}
				return true;
			}
			if (!string.IsNullOrEmpty(LineageId) || !string.IsNullOrEmpty(LegacyId)
				|| !string.IsNullOrEmpty(OriginGameId))
			{
				Failure = "the saved seal identity is incomplete";
				return false;
			}

			KingdomSealRecord staged = GetStore().ReadStage(game.GameID);
			if (staged != null && staged.OriginGameId == game.GameID
				&& KingdomSealReceipt.ValidId(staged.LineageId)
				&& KingdomSealReceipt.ValidId(staged.LegacyId))
			{
				AdoptIdentity(staged);
				if (staged.Status == KingdomSealStatus.Retired)
				{
					SealedLegacyId = staged.LegacyId;
				}
				return true;
			}
			LineageId = MintId();
			LegacyId = MintId();
			OriginGameId = game.GameID;
			Generation = 0;
			Revision = 0;
			SealedLegacyId = "";
			Dirty = true;
			return true;
		}

	}
}
