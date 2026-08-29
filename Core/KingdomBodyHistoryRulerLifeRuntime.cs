#if !TAF_TESTS
using System;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public sealed partial class KingdomSuccession
	{
		/// <summary>Read-only bridge for a commission that must bind one stable reign.</summary>
		internal bool TryReadStableRulerOrdinal(out int Ordinal, out string Failure)
		{
			Ordinal = -1;
			Failure = null;
			if (!KingdomSuccessionRules.SuccessionEnabled(LoadFailed, SuccessionDisabled))
				return Fail("succession authority is disabled or unreadable", out Failure);
			if (!KingdomSuccessionRules.TryValidateSavedState(SuccessionOrdinal,
				PendingDeathToken, CompletedDeathToken, PendingPhase, PendingDueTick,
				PendingRoad, PendingDays, PendingAccessionRepairResidentId != 0,
				PendingSealAccessionToken, out Failure)) return false;
			if (!string.IsNullOrEmpty(PendingDeathToken)
				|| PendingPhase == InterregnumPhase.WordOnTheRoad
				|| PendingPhase == InterregnumPhase.RiteDue
				|| PendingAccessionRepairResidentId != 0)
				return Fail("the ruler life is crossing an interregnum", out Failure);
			if (SuccessionOrdinal == 0)
			{
				if (PendingPhase != InterregnumPhase.None
					|| !string.IsNullOrEmpty(CompletedDeathToken))
					return Fail("the first ruler life is incoherent", out Failure);
			}
			else
			{
				if (PendingPhase != InterregnumPhase.Reigning
					|| !KingdomSuccessionRules.TryReadDeathToken(CompletedDeathToken,
						out int completed, out long _) || completed != SuccessionOrdinal)
					return Fail("the reigning ruler life is incoherent", out Failure);
			}
			Ordinal = SuccessionOrdinal;
			return true;
		}

		private static bool Fail(string Reason, out string Failure)
		{
			Failure = Reason;
			return false;
		}
	}

	/// <summary>
	/// Resolves only the current, loaded player body. Engine sources: The.cs:23 exposes
	/// Player; XRLGame.cs:245-254 exposes gameMode, :286-300 GetSystem, and :1429-1435
	/// GetBooleanGameState. No body, zone, or succession authority is created here.
	/// </summary>
	public static class KingdomBodyHistoryRulerLifeRuntime
	{
		public static bool TryReadCurrent(KingdomSystem System, GameObject Actor,
			out KingdomRulerLifeSnapshot Snapshot, out string Failure)
		{
			Snapshot = null;
			Failure = null;
			XRLGame game = The.Game;
			string objectId = Actor?.IDIfAssigned;
			string realmId = System?.CurrentRealmId;
			if (game == null || System == null || !System.Founded
				|| !ReferenceEquals(The.Player, Actor) || !GameObject.Validate(Actor)
				|| Actor.CurrentZone == null || Actor.CurrentCell == null || Actor.Body == null
				|| string.IsNullOrEmpty(objectId) || !KingdomIdentityRules.IsRealmId(realmId))
				return Fail("the exact current ruler body is unavailable", out Failure);

			KingdomSuccession succession = game.GetSystem<KingdomSuccession>();
			bool kingdomMode = KingdomSuccessionRules.ModeOn(game.gameMode,
				game.GetBooleanGameState(KingdomSuccessionRules.ModeFlagStateKey));
			int ordinal = 0;
			if (kingdomMode && succession == null)
				return Fail("Kingdom Mode has no succession authority", out Failure);
			if (succession != null
				&& !succession.TryReadStableRulerOrdinal(out ordinal, out Failure)) return false;

			KingdomRulerLifeSnapshot candidate = new KingdomRulerLifeSnapshot
			{
				RealmId = realmId,
				SuccessionOrdinal = ordinal,
				BodyObjectId = "taf:object:" + objectId
			};
			candidate.RulerLifeId = KingdomBodyHistoryRulerLifeRules.Identity(
				candidate.RealmId, candidate.SuccessionOrdinal, candidate.BodyObjectId);
			if (!KingdomBodyHistoryRulerLifeRules.Valid(candidate))
				return Fail("the current ruler-life identity is invalid", out Failure);
			Snapshot = candidate;
			return true;
		}

		private static bool Fail(string Reason, out string Failure)
		{
			Failure = Reason;
			return false;
		}
	}
}
#endif
