using System;
using System.Text;
using XRL;

namespace ThousandAndFirst.Harness
{
	// Reads the actual automatically staged empty-camp record; never triggers a seal write.
	// The founding flush stages revision 1 (profile schema 1, foundation body pool) before any
	// heart work exists. On the stripped roadless testground every later flush returns typed
	// Pending (#135) and stages nothing, so that founding stage is the only record production
	// can show, while the live polity ledger has since committed unresolved authority that
	// cannot be sealed until a witnessed street reaches the zone edge. A schema-2 native stage
	// proof therefore needs a roaded testground and is not claimed here.
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
			string before = coordinator.NativePendingStageEvidence();
			var store = new KingdomSealStore(DataManager.SyncedPath("ThousandAndFirst"));
			KingdomSealRecord staged = store.ReadStage(game.GameID); prove();
			Require(staged != null && staged.Status == KingdomSealStatus.Living
				&& staged.OriginGameId == game.GameID && staged.LegacyId == legacy && staged.LineageId == lineage
				&& staged.Generation == coordinator.CurrentGeneration
				&& staged.RealmId == system.CurrentRealmId && staged.SettlementId == system.CurrentSettlementId
				&& staged.Population == 0 && staged.Revision == 1 && staged.WrittenTick == foundedTick && staged.WrittenTick <= tick,
				"automatic founding stage is missing or is not the untouched revision-1 empty-camp record");
			KingdomPolityProfileRevision profile = CurrentRealmProfile(system.PolityLedger);
			Require(profile != null && KingdomPolityProfileRules.IsUnresolvedBodyPool(profile.BodyKeys),
				"live polity ledger did not commit unresolved authority during the empty-camp warmup");
			Require(coordinator.NativeSpatialCaptureWaits(out string failure),
				"roadless capture did not return typed pending: " + failure);
			Require(coordinator.NativePendingStageEvidence() == before, "pending capture changed revision or staged record");
			string wire = staged.Compose();
			Require(KingdomSealRecord.TryParse(wire, out var roundtrip, out _, out failure)
				&& roundtrip.Compose() == wire, "actual empty-camp seal roundtrip refused: " + failure);
			prove(); Require(GameOwnerUnchanged(game, coordinator, tick, legacy, lineage), "seal observation changed coordinator or clock");
			evidence.Append("\nactual-empty-camp-stage=founding stage-revision=").Append(staged.Revision)
				.Append(" stage-schema=").Append(staged.ProfileSchema).Append(" tick=").Append(staged.WrittenTick)
				.Append(" population=0 stage-bodies=").Append(string.Join("+", staged.CanonicalBodyKeys))
				.Append(" ledger-bodies=unresolved spatial=pending restage=none technology=")
				.Append(staged.TechnologyBand).Append(" source=").Append(staged.SourceProfileDigest)
				.Append("; canonical-roundtrip=true; read-only=true");
		}
		private static KingdomPolityProfileRevision CurrentRealmProfile(KingdomPolityLedger ledger)
		{
			KingdomPolityRecord polity = ledger?.Polities.Find(p => p.Source == KingdomPolitySource.CurrentRealm);
			return polity == null ? null
				: ledger.Profiles.Find(p => p.ProfileId == polity.ProfileId && p.Revision == polity.ProfileRevision);
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
