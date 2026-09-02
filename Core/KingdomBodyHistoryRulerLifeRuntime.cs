#if !TAF_TESTS
using System;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
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
