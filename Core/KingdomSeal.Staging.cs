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
		private bool AuthorityEnabled => KingdomSealEngineRules.SealAuthorityEnabled(
			LoadFailed, SealDisabled);

		private bool TryRequireAuthority(out string Failure)
		{
			Failure = "";
			if (AuthorityEnabled)
			{
				return true;
			}
			Failure = "the saved seal coordinator is disabled for this save";
			return false;
		}

		private void NeutralizeDisabledState()
		{
			SealDisabled = true;
			LineageId = "";
			LegacyId = "";
			OriginGameId = "";
			Generation = 0;
			Revision = 0;
			LastPollTick = 0L;
			SealedLegacyId = "";
			LastAccessionToken = "";
			PendingAccessionToken = "";
			Store = null;
			Dirty = false;
			DirtyReason = null;
			FlushInProgress = false;
			ReconcileInProgress = false;
			LastFailureKey = null;
		}

		private bool IsGenerationSealed => AuthorityEnabled && !string.IsNullOrEmpty(LegacyId)
			&& string.Equals(SealedLegacyId, LegacyId, StringComparison.Ordinal);

		private void MarkDirty(string Reason)
		{
			if (!AuthorityEnabled || IsGenerationSealed)
			{
				return;
			}
			Dirty = true;
			DirtyReason = KingdomSealRules.SanitizeText(Reason, 160);
		}

		private bool TryFlushLiving(string Reason, bool ProbeEvenIfClean, out string Failure)
		{
			Failure = "";
			if (FlushInProgress)
			{
				Failure = "a seal flush is already in progress";
				return false;
			}
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
			if (!Dirty && !ProbeEvenIfClean && string.IsNullOrEmpty(PendingAccessionToken))
			{
				return true;
			}
			FlushInProgress = true;
			try
			{
				if (!EnsureIdentity(kingdom, out Failure))
				{
					return false;
				}
				if (!string.IsNullOrEmpty(PendingAccessionToken)
					&& !TryCompletePendingGeneration(kingdom, out Failure))
				{
					return false;
				}
				if (IsGenerationSealed)
				{
					Dirty = false;
					DirtyReason = null;
					return true;
				}

				KingdomSealStore store = GetStore();
				KingdomSealRecord existing = store.ReadStage(OriginGameId);
				if (existing != null && existing.Status == KingdomSealStatus.Retired
					&& SameCurrentIdentity(existing))
				{
					return TryCompleteRetirement(existing, out Failure);
				}
				int baseRevision = Revision;
				if (existing != null && SameCurrentIdentity(existing) && existing.Revision > baseRevision)
				{
					baseRevision = existing.Revision;
				}
				if (existing != null && !SameCurrentIdentity(existing))
				{
					Failure = "the origin journal names a different current generation";
					return false;
				}

				KingdomSealRecord probe;
				if (!TryCapture(kingdom, LegacyId, Generation, baseRevision,
					SafeTick(game.TimeTicks), out probe, out Failure))
				{
					return false;
				}
				if (existing != null && existing.Status == KingdomSealStatus.Living
					&& KingdomSealEngineRules.SameLivingSnapshot(existing, probe))
				{
					Revision = existing.Revision;
					Dirty = false;
					DirtyReason = null;
					LastFailureKey = null;
					return true;
				}
				int nextRevision;
				if (!KingdomSealEngineRules.TryNextRevision(baseRevision, out nextRevision))
				{
					Failure = "the seal revision is exhausted";
					return false;
				}
				KingdomSealRecord next;
				if (!TryCapture(kingdom, LegacyId, Generation, nextRevision,
					SafeTick(game.TimeTicks), out next, out Failure))
				{
					return false;
				}
				if (!store.TryStage(next, out Failure))
				{
					return false;
				}
				Revision = nextRevision;
				LastPollTick = SafeTick(game.TimeTicks);
				Dirty = false;
				DirtyReason = null;
				LastFailureKey = null;
				return true;
			}
			catch (Exception ex)
			{
				Failure = ex.Message;
				return false;
			}
			finally
			{
				FlushInProgress = false;
			}
		}

		private bool TryWriteTerminal(string CauseText, string CauseKind, long CauseTurn,
			out string Failure)
		{
			Failure = "";
			if (!TryRequireAuthority(out Failure))
			{
				return false;
			}
			KingdomSystem kingdom = The.Game?.GetSystem<KingdomSystem>();
			if (kingdom == null || !kingdom.Founded)
			{
				return true;
			}
			if (!EnsureIdentity(kingdom, out Failure))
			{
				return false;
			}
			if (IsGenerationSealed)
			{
				return true;
			}
			KingdomSealStore store = GetStore();
			KingdomSealRecord existing = store.ReadStage(OriginGameId);
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
			KingdomSealRecord terminal;
			try
			{
				terminal = KingdomSealRules.WithTerminalCause(living, CauseText, CauseKind,
					SafeTick(CauseTurn));
			}
			catch (Exception ex)
			{
				Failure = ex.Message;
				return false;
			}
			if (!store.TryStage(terminal, out Failure))
			{
				return false;
			}
			Revision = terminal.Revision;
			Dirty = false;
			DirtyReason = null;
			return true;
		}

	}
}
