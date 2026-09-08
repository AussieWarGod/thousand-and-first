using System;
using System.Text;
using XRL;

namespace ThousandAndFirst.Harness
{
	// Reads the actual automatically staged empty-camp record; never triggers a seal write.
	internal static class KingdomWaterMaintenanceSealEvidence
	{
		internal static void Verify(KingdomSystem system, XRLGame game, long foundedTick,
			Action prove, StringBuilder evidence)
		{
			prove(); long tick = game.TimeTicks;
			KingdomSeal coordinator = game.GetSystem<KingdomSeal>();
			Require(coordinator != null && system.Population == 0 && system.City.ResidentCount == 0,
				"empty-camp seal observation lacks original empty realm");
			string legacy = coordinator.CurrentLegacyId, lineage = coordinator.CurrentLineageId;
			var store = new KingdomSealStore(DataManager.SyncedPath("ThousandAndFirst"));
			KingdomSealRecord staged = store.ReadStage(game.GameID); prove();
			Require(staged != null && staged.Status == KingdomSealStatus.Living
				&& staged.OriginGameId == game.GameID && staged.LegacyId == legacy && staged.LineageId == lineage
				&& staged.Generation == coordinator.CurrentGeneration
				&& staged.RealmId == system.CurrentRealmId && staged.SettlementId == system.CurrentSettlementId
				&& staged.Population == 0 && staged.Revision > 0 && staged.WrittenTick > foundedTick && staged.WrittenTick <= tick
				&& staged.ProfileSchema == KingdomPolityProfileRules.CommittedUnresolvedLegacyProfileSchema
				&& KingdomPolityProfileRules.IsUnresolvedBodyPool(staged.CanonicalBodyKeys),
				"automatic empty-camp stage did not retain exact committed unresolved authority");
			Require(KingdomSealProfileCaptureRules.StillMatches(system.PolityLedger, system.CurrentRealmId,
				staged, system.PolityLedger.Revision, out string failure), failure);
			string wire = staged.Compose();
			Require(KingdomSealRecord.TryParse(wire, out var roundtrip, out _, out failure)
				&& roundtrip.Compose() == wire, "actual empty-camp seal roundtrip refused: " + failure);
			Require(KingdomSealProfileCaptureRules.StillMatches(system.PolityLedger, system.CurrentRealmId,
				roundtrip, system.PolityLedger.Revision, out failure), failure);
			prove(); Require(GameOwnerUnchanged(game, coordinator, tick, legacy, lineage), "seal observation changed coordinator or clock");
			evidence.Append("\nactual-empty-camp-stage revision=").Append(staged.Revision)
				.Append(" tick=").Append(staged.WrittenTick).Append(" schema=2 population=0 bodies=unresolved technology=")
				.Append(staged.TechnologyBand).Append(" source=").Append(staged.SourceProfileDigest)
				.Append("; canonical-roundtrip=true; read-only=true");
		}
		private static bool GameOwnerUnchanged(XRLGame game, KingdomSeal coordinator, long tick,
			string legacy, string lineage)
		{
			return ReferenceEquals(The.Game, game) && ReferenceEquals(game.GetSystem<KingdomSeal>(), coordinator)
				&& game.TimeTicks == tick && coordinator.CurrentLegacyId == legacy && coordinator.CurrentLineageId == lineage;
		}
		private static void Require(bool value, string failure)
		{ KingdomWaterMaintenanceNativeProvider.Require(value, failure); }
	}
}
