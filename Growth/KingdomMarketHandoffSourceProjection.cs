using System;
using System.Collections.Generic;
using XRL.World;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	/// <summary>Durable source-side half of a legendary market handoff. The target marker is
	/// deliberately not the sole recovery authority: this receipt survives target destruction.</summary>
	[Serializable]
	public sealed class r_KingdomMarketHandoffSourceProjection : IPart
	{
		public string RealmId = "";
		public string SettlementId = "";
		public string SourceBodyObjectId = "";
		public string TargetBodyObjectId = "";
		public int SourceResidentId;
		public int TargetResidentId;
		public int Tier;
		public string Intent = "";
		public string LifecycleOperationId = "";
		public string LifecyclePlanHash = "";
		public long LifecycleSequence;
		public int LifecycleTerminalClosed;
		public int TargetTerminalDead;

		internal bool Stamp(KingdomSystem System, string Settlement, GameObject Source,
			GameObject Target, int MarketTier, int SourceResident, int TargetResident,
			string FrozenIntent, string OperationId, string PlanHash, long Sequence)
		{
			if (System == null || !GameObject.Validate(Source) || !GameObject.Validate(Target)
				|| SourceResident <= 0 || TargetResident <= 0
				|| MarketTier < KingdomShopStockRules.FirstPhysicalMarketTier
				|| MarketTier > KingdomShopStockRules.MaximumTier) return false;
			bool blank = string.IsNullOrEmpty(RealmId) && string.IsNullOrEmpty(SettlementId)
				&& string.IsNullOrEmpty(SourceBodyObjectId)
				&& string.IsNullOrEmpty(TargetBodyObjectId) && SourceResidentId == 0
				&& TargetResidentId == 0 && Tier == 0 && string.IsNullOrEmpty(Intent)
				&& string.IsNullOrEmpty(LifecycleOperationId)
				&& string.IsNullOrEmpty(LifecyclePlanHash) && LifecycleSequence == 0L
				&& LifecycleTerminalClosed == 0 && TargetTerminalDead == 0;
			bool exact = RealmId == System.RealmId && SettlementId == Settlement
				&& SourceBodyObjectId == Source.IDIfAssigned
				&& TargetBodyObjectId == Target.IDIfAssigned
				&& SourceResidentId == SourceResident && TargetResidentId == TargetResident
				&& Tier == MarketTier && Intent == FrozenIntent
				&& LifecycleOperationId == OperationId && LifecyclePlanHash == PlanHash
				&& LifecycleSequence == Sequence
				&& LifecycleTerminalClosed == 0 && TargetTerminalDead == 0;
			if (!blank && !exact) return false;
			if (Simulation.City.KingdomResidents.IdOf(Source) != SourceResident
				|| Simulation.City.KingdomResidents.IdOf(Target) != TargetResident
				|| System.SettlementIdForOwnedZone(Source.CurrentZone?.ZoneID) != Settlement
				|| System.SettlementIdForOwnedZone(Target.CurrentZone?.ZoneID) != Settlement
				|| !KingdomCitizenship.BelongsTo(System, Target)) return false;
			if (blank && (!KingdomCitizenship.BelongsTo(System, Source)
				|| !KingdomGrowth.TryAuthorizedMarketBody(System, Source.CurrentZone, Source,
					MarketTier, out _))) return false;
			RealmId = System.RealmId; SettlementId = Settlement;
			SourceBodyObjectId = Source.IDIfAssigned;
			TargetBodyObjectId = Target.IDIfAssigned;
			SourceResidentId = SourceResident; TargetResidentId = TargetResident;
			Tier = MarketTier; Intent = FrozenIntent ?? "";
			LifecycleOperationId = OperationId ?? ""; LifecyclePlanHash = PlanHash ?? "";
			LifecycleSequence = Sequence;
			LifecycleTerminalClosed = 0; TargetTerminalDead = 0;
			return Exact(System, Source);
		}

		internal bool Exact(KingdomSystem System, GameObject Source)
		{
			string expected = KingdomShopStockRules.SourceId(RealmId, SettlementId, Tier)
				+ ":handoff:" + SourceBodyObjectId + ":" + TargetBodyObjectId;
			bool valid = GameObject.Validate(Source);
			bool resident = valid && (Simulation.City.KingdomResidents.IdOf(Source)
				== SourceResidentId || !Source.IsAlive && r_KingdomLegendaryMarketProjection.DeadResident(
					System, SettlementId, SourceResidentId));
			return System != null && valid && RealmId == System.RealmId
				&& !string.IsNullOrEmpty(SettlementId)
				&& Source.IDIfAssigned == SourceBodyObjectId && SourceResidentId > 0
				&& TargetResidentId > 0 && Tier >= KingdomShopStockRules.FirstPhysicalMarketTier
				&& Tier <= KingdomShopStockRules.MaximumTier && Intent == expected
				&& !string.IsNullOrEmpty(LifecycleOperationId)
				&& !string.IsNullOrEmpty(LifecyclePlanHash)
				&& LifecycleSequence > 0L
				&& (LifecycleTerminalClosed == 0 || LifecycleTerminalClosed == 1)
				&& (TargetTerminalDead == 0 || TargetTerminalDead == 1)
				&& (TargetTerminalDead == 0 || LifecycleTerminalClosed == 1)
				&& resident
				&& System.SettlementIdForOwnedZone(Source.CurrentZone?.ZoneID) == SettlementId;
		}

		internal bool ExactLive(KingdomSystem System, GameObject Source)
		{
			if (!Exact(System, Source) || !Source.IsAlive || Source.IsPlayer()
				|| !KingdomCitizenship.BelongsTo(System, Source)) return false;
			List<KingdomCityBook> books = System.OwnedCityBooks(); int matches = 0;
			KingdomResidentRow found = default(KingdomResidentRow);
			for (int i = 0; books != null && i < books.Count; i++)
				if (books[i]?.SettlementId == SettlementId && KingdomResidents.TryResident(
					books[i], SourceResidentId, out KingdomResidentRow row))
					{ found = row; matches++; }
			return matches == 1 && KingdomResidentRules.OnTheRoll(found);
		}

		internal bool ExactTerminal(KingdomSystem System, GameObject Source)
		{
			string expected = KingdomShopStockRules.SourceId(RealmId, SettlementId, Tier)
				+ ":handoff:" + SourceBodyObjectId + ":" + TargetBodyObjectId;
			return System != null && GameObject.Validate(Source) && !Source.IsAlive
				&& !Source.IsPlayer() && !string.IsNullOrEmpty(SourceBodyObjectId)
				&& !string.IsNullOrEmpty(TargetBodyObjectId)
				&& Source.IDIfAssigned == SourceBodyObjectId && RealmId == System.RealmId
				&& !string.IsNullOrEmpty(SettlementId) && SourceResidentId > 0
				&& TargetResidentId > 0
				&& Tier >= KingdomShopStockRules.FirstPhysicalMarketTier
				&& Tier <= KingdomShopStockRules.MaximumTier
				&& (LifecycleTerminalClosed == 0 || LifecycleTerminalClosed == 1)
				&& (TargetTerminalDead == 0 || TargetTerminalDead == 1)
				&& (TargetTerminalDead == 0 || LifecycleTerminalClosed == 1)
				&& r_KingdomLegendaryMarketProjection.DeadResident(System, SettlementId,
					SourceResidentId) && Intent == expected && LifecycleSequence > 0L
				&& !string.IsNullOrEmpty(LifecycleOperationId)
				&& !string.IsNullOrEmpty(LifecyclePlanHash);
		}

		public override void FinalizeCopy(GameObject Source, bool CopyEffects, bool CopyID,
			Func<GameObject, GameObject> MapInv)
		{
			base.FinalizeCopy(Source, CopyEffects, CopyID, MapInv);
			ParentObject?.RemovePart(this);
		}
	}
}
