using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomTrade
	{
		private static bool PrepareCharterDelivery(KingdomSystem System,
			KingdomTradeBook Book, KingdomTradeCharter Charter, Zone Z,
			KingdomSurvey Survey, long Now)
		{
			if (Charter == null || Charter.Quarantined || Book.OpenOperation != null) return false;
			if (!KingdomData.TryGetDeal(Charter.DealKey, out KingdomRules.DealEntry deal)
				|| deal.IntervalTicks <= 0L || deal.IncomeDrams < 0)
			{
				Charter.Quarantined = true;
				Charter.Fault = "Charter content cannot be frozen for delivery.";
				return false;
			}
			int cycles = KingdomRules.BankedCycles(Now, Charter.NextTick, deal.IntervalTicks);
			if (cycles <= 0) return false;
			int goodsHouseholds = KingdomYardGoods.ExactStandingHouseholds(Survey);
			int incomePerCycle = KingdomYardGoodsRules.IncomePerCycle(
				deal.IncomeDrams, goodsHouseholds);
			int water = KingdomTradeRules.SaturatingMultiply(incomePerCycle, cycles);
			if (water > KingdomTradeRules.MaxOperationWater) return false;
			KingdomTradeOperation operation = KingdomTradeRules.NewOperation(Book,
				KingdomTradeOperationKind.CharterDelivery, Now);
			if (operation == null) return false;
			if (!BindOperationSettlement(System, Book, operation, Z))
			{
				Quarantine(operation, "The charter delivery could not bind its exact settlement and zone.");
				return true;
			}
			operation.CharterId = Charter.Id;
			operation.DealKey = Charter.DealKey;
			operation.DealDisplayName = deal.DisplayName;
			operation.Faction = Charter.Faction;
			operation.Cycles = cycles;
			// Frozen adjusted income is durable authority. Later release, registry merge, or reload
			// cannot change this already-open exchange or apply its household goods twice.
			operation.IncomePerCycle = incomePerCycle;
			operation.IntervalTicks = deal.IntervalTicks;
			operation.DueBefore = Charter.NextTick;
			operation.DueAfter = KingdomTradeRules.SaturatingAdd(Now, deal.IntervalTicks);
			operation.CaravanBlueprint = deal.CaravanBlueprint;
			operation.WaterDirection = KingdomTradeWaterDirection.Credit;
			operation.RequestedWater = water;
			operation.ProjectionId = KingdomTradeRules.ProjectionId(operation.Id);
			for (int i = 0; i < Book.Projections.Count; i++)
			{
				KingdomTradeProjectionRow collision = Book.Projections[i];
				if (collision == null || !string.Equals(collision.ProjectionId,
					operation.ProjectionId, StringComparison.Ordinal)) continue;
				collision.Quarantined = true;
				collision.Fault = AppendFault(collision.Fault,
					"new projection identity collided with preserved authority");
				Quarantine(operation, "Projection identity collided before physical mutation.");
				return true;
			}
			KingdomTradeProjectionRow prior;
			if (!TryProjectionRow(Book, operation.SettlementId, out prior))
			{
				Quarantine(operation,
					"Per-city caravan projection authority collided before delivery.");
				return true;
			}
			if (prior != null)
			{
				operation.PriorProjectionId = prior.ProjectionId;
				operation.PriorProjectionObjectId = prior.ObjectId;
				operation.PriorProjectionZoneId = prior.ZoneId;
			}
			FreezeMaterials(operation, KingdomMaterials.DealMaterialsFor(deal.Key).Scaled(cycles * 100));
			int before = System.GetRegardForRealm(Charter.Faction);
			long after = (long)before + KingdomRules.DealTrickleStanding;
			if (after < int.MinValue || after > int.MaxValue)
			{
				Quarantine(operation, "Standing CAS would overflow.");
				return true;
			}
			operation.Standing = new KingdomTradeStandingCas
			{
				Faction = Charter.Faction,
				Before = before,
				Delta = KingdomRules.DealTrickleStanding,
				After = (int)after,
				State = KingdomTradePhysicalState.Prepared
			};
			operation.Pattern = KingdomCeremony.FreezePatternBook(System,
				operation.SettlementId, operation.Sequence);
			if (!KingdomTradePatternRules.Valid(operation.Pattern))
			{
				Quarantine(operation,
					"The charter's pattern-book offer could not be frozen within its bounds.");
			}
			return true;
		}

		private static bool TryProjectionRow(KingdomTradeBook Book, string SettlementId,
			out KingdomTradeProjectionRow Projection)
		{
			Projection = null;
			if (Book?.Projections == null || !KingdomTradeRules.ValidId(SettlementId))
				return false;
			for (int i = 0; i < Book.Projections.Count; i++)
			{
				KingdomTradeProjectionRow row = Book.Projections[i];
				if (row == null) return false;
				if (!string.Equals(row.SettlementId, SettlementId, StringComparison.Ordinal)) continue;
				if (row.Quarantined) return false;
				if (Projection != null) return false;
				Projection = row;
			}
			return true;
		}

		private static void PrepareManifestDelivery(KingdomSystem System, KingdomTradeBook Book,
			KingdomTradeManifestState Manifest, Zone Z, long Now)
		{
			KingdomTradeOperation operation = KingdomTradeRules.NewOperation(Book,
				KingdomTradeOperationKind.ManifestDelivery, Now);
			if (operation == null) return;
			if (!BindOperationSettlement(System, Book, operation, Z))
			{
				Quarantine(operation, "The manifest arrival could not bind its exact settlement and zone.");
				return;
			}
			CopyManifest(operation, Manifest);
			operation.WaterDirection = KingdomTradeWaterDirection.Credit;
			operation.RequestedWater = Manifest.EscrowDrams;
			operation.ManifestEscrowBefore = Manifest.EscrowDrams;
			operation.ManifestEscrowDebit = 0;
			operation.ManifestEscrowAfter = Manifest.EscrowDrams;
			operation.ManifestEscrowState = KingdomTradePhysicalState.Prepared;
		}

		private static void PrepareManifestClockOperation(KingdomSystem System,
			KingdomTradeBook Book, KingdomTradeManifestState Manifest, Zone Z, long Now)
		{
			KingdomTradeOperation operation = KingdomTradeRules.NewOperation(Book,
				Manifest.TurnedBack ? KingdomTradeOperationKind.ManifestLapse
					: KingdomTradeOperationKind.ManifestTurnback, Now);
			if (operation == null) return;
			if (!BindOperationSettlement(System, Book, operation, Z))
			{
				Quarantine(operation, "The manifest clock could not bind its exact settlement and zone.");
				return;
			}
			CopyManifest(operation, Manifest);
			if (operation.Kind == KingdomTradeOperationKind.ManifestLapse)
			{
				operation.RetainedBefore = Book.RetainedEscrowDrams;
				operation.RetainedDelta = Manifest.EscrowDrams;
				operation.RetainedAfter = KingdomTradeRules.SaturatingAdd(
					operation.RetainedBefore, operation.RetainedDelta);
				operation.RetainedState = KingdomTradePhysicalState.Prepared;
			}
			operation.ManifestLoadedTick = Now;
			operation.ManifestDeadlineTick = KingdomTradeRules.SaturatingAdd(Now,
				KingdomManifestRules.ManifestWindowTicks);
		}

		private static void CopyManifest(KingdomTradeOperation Operation,
			KingdomTradeManifestState Manifest)
		{
			Operation.ManifestId = Manifest.Id;
			Operation.OriginId = Manifest.OriginId;
			Operation.OriginName = Manifest.OriginName;
			Operation.DestinationId = Manifest.DestinationId;
			Operation.DestinationName = Manifest.DestinationName;
			Operation.ManifestLoadedTick = Manifest.LoadedTick;
			Operation.ManifestDeadlineTick = Manifest.DeadlineTick;
			Operation.RequestedWater = Manifest.EscrowDrams;
		}

	}
}
