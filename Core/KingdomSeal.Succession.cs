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
		private bool TryRetire(out string Failure)
		{
			Failure = "";
			if (!TryRequireAuthority(out Failure))
			{
				return false;
			}
			KingdomSystem kingdom = The.Game?.GetSystem<KingdomSystem>();
			if (kingdom == null || !kingdom.Founded)
			{
				Failure = "no founded realm can be retired";
				return false;
			}
			if (!EnsureIdentity(kingdom, out Failure))
			{
				return false;
			}
			KingdomSealStore store = GetStore();
			KingdomSealRecord existing = store.ReadStage(OriginGameId);
			if (IsGenerationSealed)
			{
				return HasExactLegacy(store, LegacyId, out Failure);
			}
			if (existing != null && existing.Status == KingdomSealStatus.Retired
				&& SameCurrentIdentity(existing))
			{
				return TryCompleteRetirement(existing, out Failure);
			}
			if (existing != null && !SameCurrentIdentity(existing))
			{
				Failure = "the origin journal names a different current generation";
				return false;
			}
			int baseRevision = existing == null ? Revision : Math.Max(Revision, existing.Revision);
			KingdomSealRecord living;
			if (!TryCapture(kingdom, LegacyId, Generation, baseRevision,
				SafeTick(The.Game.TimeTicks), out living, out Failure))
			{
				return false;
			}
			KingdomSealRecord retired;
			try
			{
				retired = KingdomSealRules.WithRetirement(living);
			}
			catch (Exception ex)
			{
				Failure = ex.Message;
				return false;
			}
			if (!store.TryStage(retired, out Failure))
			{
				return false;
			}
			return TryCompleteRetirement(retired, out Failure);
		}

		private bool TryCompleteRetirement(KingdomSealRecord Retired, out string Failure)
		{
			Failure = "";
			if (!TryRequireAuthority(out Failure))
			{
				return false;
			}
			if (Retired == null || Retired.Status != KingdomSealStatus.Retired
				|| Retired.OriginGameId != The.Game?.GameID)
			{
				Failure = "the retirement stage does not belong to this game";
				return false;
			}
			KingdomSealRecord promoted;
			try
			{
				promoted = KingdomSealRules.PromoteRetirement(Retired);
			}
			catch (Exception ex)
			{
				Failure = ex.Message;
				return false;
			}
			if (!GetStore().TryWriteLegacy(promoted, out Failure))
			{
				return false;
			}
			AdoptIdentity(Retired);
			SealedLegacyId = Retired.LegacyId;
			Dirty = false;
			DirtyReason = null;
			LastFailureKey = null;
			return true;
		}

		private bool TryAdvanceGeneration(string AccessionToken, out string Failure)
		{
			Failure = "";
			if (!TryRequireAuthority(out Failure))
			{
				return false;
			}
			int tokenOrdinal;
			long tokenTick;
			if (!KingdomSuccessionRules.TryReadDeathToken(AccessionToken,
				out tokenOrdinal, out tokenTick))
			{
				Failure = "succession supplied no canonical accession identity";
				return false;
			}
			if (string.Equals(LastAccessionToken, AccessionToken, StringComparison.Ordinal))
			{
				if (tokenOrdinal == Generation) return true;
				Failure = "the completed accession token names the wrong generation";
				return false;
			}
			if (!string.IsNullOrEmpty(PendingAccessionToken))
			{
				if (!string.Equals(PendingAccessionToken, AccessionToken, StringComparison.Ordinal))
				{
					Failure = "a different accession is still waiting for its stage handoff";
					return false;
				}
				if (tokenOrdinal != Generation)
				{
					Failure = "the pending accession token names the wrong generation";
					return false;
				}
				return TryCompletePendingGeneration(The.Game?.GetSystem<KingdomSystem>(), out Failure);
			}
			if (Generation == int.MaxValue || tokenOrdinal != Generation + 1)
			{
				Failure = "the accession token does not name the exact next generation";
				return false;
			}
			XRLGame game = The.Game;
			KingdomSystem kingdom = game?.GetSystem<KingdomSystem>();
			if (game == null || kingdom == null || !kingdom.Founded || !IsKingdomMode(game))
			{
				Failure = "a successor generation requires a founded Kingdom-mode realm";
				return false;
			}
			if (!EnsureIdentity(kingdom, out Failure))
			{
				return false;
			}
			KingdomSealStore store = GetStore();
			KingdomSealRecord previous = store.ReadStage(OriginGameId);
			if (previous == null)
			{
				Dirty = true;
				if (!TryFlushLiving("accession preflight", ProbeEvenIfClean: true, out Failure))
				{
					return false;
				}
				previous = store.ReadStage(OriginGameId);
			}
			if (previous == null || !SameCurrentIdentity(previous)
				|| (previous.Status != KingdomSealStatus.Living
					&& previous.Status != KingdomSealStatus.Retired))
			{
				Failure = "the current generation has no exact living or retired stage to hand off";
				return false;
			}
			int nextGeneration;
			int nextRevision;
			if (!KingdomSealEngineRules.TryNextGeneration(previous.Generation, out nextGeneration)
				|| !KingdomSealEngineRules.TryNextRevision(previous.Revision, out nextRevision))
			{
				Failure = "the lineage has exhausted its generation or revision bound";
				return false;
			}
			string nextLegacy = MintId();
			KingdomSealRecord successor;
			if (!TryCapture(kingdom, nextLegacy, nextGeneration, nextRevision,
				SafeTick(game.TimeTicks), out successor, out Failure))
			{
				return false;
			}
			if (!KingdomSealEngineRules.MayAdvanceGeneration(previous, successor))
			{
				Failure = "the successor snapshot is not the exact adjacent living generation";
				return false;
			}

			// Publish intended save state before the external handoff. If disk I/O fails or tears,
			// BeforeSave/load retries the exact identity tuple; Store keeps the durable successor slot
			// canonical and finishes its other slot from that copy.
			LegacyId = successor.LegacyId;
			Generation = successor.Generation;
			Revision = successor.Revision;
			SealedLegacyId = "";
			PendingAccessionToken = AccessionToken;
			Dirty = true;
			if (!store.TryAdvanceGeneration(previous, successor, out Failure))
			{
				return false;
			}
			LastAccessionToken = AccessionToken;
			PendingAccessionToken = "";
			Dirty = false;
			DirtyReason = null;
			return true;
		}

	}
}
