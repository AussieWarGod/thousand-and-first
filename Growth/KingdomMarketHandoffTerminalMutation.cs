using System;
using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomGrowth
	{
		internal static bool TrySealCompletedDeadHandoffOutcome(KingdomSystem System,
			KingdomLifecycleOperation Open, GameObject SourceBody,
			r_KingdomMarketHandoffSourceProjection Source)
		{
			KingdomLifecycleLodgeTerminalReceipt receipt = Open?.LodgeTerminal;
			if (receipt?.MarketSourcePrepared
				!= KingdomLifecycleLodgeTerminalReceipt.MarketPrepared) return true;
			if (!KingdomMarketHandoffGlobalIndex.TryLoaded(out IList<GameObject> loaded)
				|| !KingdomMarketHandoffGraphAuthority.TryPreflight(System, loaded,
					Open.SettlementId, out _)
				|| !KingdomMarketHandoffGraphAuthority.TryUnique(loaded, Open.ObjectId,
					out GameObject target)) return false;
			r_KingdomLegendaryMarketProjection legend =
				target?.GetPart<r_KingdomLegendaryMarketProjection>();
			if (legend == null || legend.HandoffPrepared == 1) return true;
			if (!ReferenceEquals(Source?.ParentObject, SourceBody)
				|| !TryExactTerminalReceipt(System, target, Source, out KingdomLifecycleOperation exact,
					out _)
				|| !ReferenceEquals(exact, Open)
				|| !ExactDeadHandoffTarget(System, target, Source, Open)
				|| !ExactCompletedTargetLegend(System, target, Source, legend)
				|| !TryCollectCompletedTargetRows(System, loaded, target, Source,
					out _, out _, out _)) return false;
			return KingdomLifecycleRules.TryCommitLodgeMarketSource(System.LifecycleBook, Open,
				Source.SourceBodyObjectId, Source.SourceResidentId, Source.Tier,
				Source.Intent, false);
		}

		private static bool TryRetireCompletedDeadHandoffTarget(KingdomSystem System,
			IList<GameObject> Loaded, GameObject Target,
			r_KingdomMarketHandoffSourceProjection Source,
			r_KingdomLegendaryMarketProjection Legend, out string Failure)
		{
			Failure = null;
			if (!TryExactTerminalReceipt(System, Target, Source,
				out KingdomLifecycleOperation open,
				out KingdomLifecycleLodgeTerminalReceipt receipt)
				|| !ExactDeadHandoffTarget(System, Target, Source, open)
				|| !ExactCompletedTargetLegend(System, Target, Source, Legend)
				|| !TryCollectCompletedTargetRows(System, Loaded, Target, Source,
					out List<SourceAbortRow> rows, out List<GameObject> retire, out Failure))
			{
				Failure = Failure ?? "completed terminal target lacks exact authority"; return false;
			}
			if (receipt.MarketSourcePrepared == KingdomLifecycleLodgeTerminalReceipt.MarketPrepared
				&& !KingdomLifecycleRules.TryCommitLodgeMarketSource(System.LifecycleBook, open,
					Source.SourceBodyObjectId, Source.SourceResidentId, Source.Tier,
					Source.Intent, false))
				{ Failure = "completed terminal target could not seal its outcome"; return false; }
			if (!KingdomMarketRemoval.TryPrepareTransaction(System, retire,
				new List<GameObject>(), out KingdomMarketRemovalTransaction transaction,
				out Failure) || !KingdomMarketRemoval.TryCommitTransaction(
					System, transaction, out Failure)) return false;
			bool hadVillage = Target.HasIntProperty("VillageMerchant");
			int village = Target.GetIntProperty("VillageMerchant");
			string bodyIntent = Target.GetStringProperty(
				KingdomGuestbook.MarketHandoffIntentProperty);
			string bodyPrior = Target.GetStringProperty(
				KingdomGuestbook.MarketHandoffPriorProperty);
			try
			{
				ClearTerminalRows(rows);
				Target.SetStringProperty(KingdomGuestbook.MarketHandoffPriorProperty,
					null, RemoveIfNull: true);
				Target.SetStringProperty(KingdomGuestbook.MarketHandoffIntentProperty,
					null, RemoveIfNull: true);
				Target.RemoveIntProperty("VillageMerchant");
				Target.RemovePart(Legend);
				if (Target.GetPart<r_KingdomLegendaryMarketProjection>() == null
					&& !Target.HasIntProperty("VillageMerchant")
					&& !Target.HasStringProperty(KingdomGuestbook.MarketHandoffIntentProperty)
					&& !Target.HasStringProperty(KingdomGuestbook.MarketHandoffPriorProperty)
					&& Target.IsMerchant() && NoTargetHandoffResidue(Loaded, Target)) return true;
				Failure = "completed terminal target did not read back";
			}
			catch (Exception error)
				{ Failure = "completed terminal cleanup threw " + error.GetType().Name; }
			return RollbackCompletedTarget(rows, transaction, Target, Legend, hadVillage,
				village, bodyIntent, bodyPrior, Failure, out Failure);
		}

		private static bool TryClearMarkerlessDeadHandoffTarget(KingdomSystem System,
			IList<GameObject> Loaded, GameObject Target,
			r_KingdomMarketHandoffSourceProjection Source, out string Failure)
		{
			Failure = null;
			if (!TryExactTerminalReceipt(System, Target, Source,
				out KingdomLifecycleOperation open,
				out KingdomLifecycleLodgeTerminalReceipt receipt)
					|| Source.LifecycleTerminalClosed != 1
					|| !ExactDeadHandoffTarget(System, Target, Source, open)
					|| Target.GetPart<r_KingdomLegendaryMarketProjection>() != null
					|| Target.HasIntProperty("VillageMerchant") || !Target.IsMerchant()
					|| !Target.HasIntProperty("Merchant")
					|| Target.GetIntProperty("Merchant") != 1
					|| Target.GetIntProperty("InventoryTier") != Source.Tier
					|| !SealedFiniteRestocker(
						Target.GetPart<XRL.World.Parts.GenericInventoryRestocker>())
				|| !KingdomMarketHandoffIntentRules.ExactOrRecoverable(Target.GetStringProperty(
					KingdomGuestbook.MarketHandoffIntentProperty), Source.Intent,
					Target.GetStringProperty(KingdomGuestbook.MarketHandoffPriorProperty),
					Source.SourceBodyObjectId))
				{ Failure = "markerless terminal target lacks exact source-and-body authority"; return false; }
			List<SourceAbortRow> rows = new List<SourceAbortRow>();
			for (int i = 0; i < Loaded.Count; i++)
			{
				GameObject item = Loaded[i];
				if (!GameObject.Validate(item)) continue;
				string market = item.GetStringProperty(KingdomGuestbook.MarketTransferTargetProperty);
				string stock = item.GetStringProperty(KingdomShopStockRules.StockTransferTargetProperty);
				bool heldProjection = ReferenceEquals(item.InInventory, Target)
					&& KingdomMarketStockProtection.HasProjection(item);
				if (market != Target.IDIfAssigned && stock != Target.IDIfAssigned && !heldProjection)
					continue;
				KingdomMarketHandoffIntentState pair = KingdomMarketHandoffIntentRules.Classify(
					market, Target.IDIfAssigned, stock, Target.IDIfAssigned);
				bool cleanupResidue = receipt.MarketSourcePrepared
					!= KingdomLifecycleLodgeTerminalReceipt.MarketPrepared
					&& !heldProjection && !KingdomMarketStockProtection.HasProjection(item)
					&& pair == KingdomMarketHandoffIntentState.FirstOnly;
				if (!cleanupResidue)
					{ Failure = "markerless target has unauthenticated stock state"; return false; }
				rows.Add(new SourceAbortRow { Item = item, MarketTarget = market,
					StockTarget = stock });
			}
			string bodyIntent = Target.GetStringProperty(
				KingdomGuestbook.MarketHandoffIntentProperty);
			string bodyPrior = Target.GetStringProperty(
				KingdomGuestbook.MarketHandoffPriorProperty);
			try
			{
				ClearTerminalRows(rows);
				Target.SetStringProperty(KingdomGuestbook.MarketHandoffPriorProperty,
					null, RemoveIfNull: true);
				Target.SetStringProperty(KingdomGuestbook.MarketHandoffIntentProperty,
					null, RemoveIfNull: true);
				if (!Target.HasStringProperty(KingdomGuestbook.MarketHandoffPriorProperty)
					&& !Target.HasStringProperty(KingdomGuestbook.MarketHandoffIntentProperty)
					&& NoTargetHandoffResidue(Loaded, Target)) return true;
				Failure = "markerless terminal cleanup did not read back";
			}
			catch (Exception error)
				{ Failure = "markerless terminal cleanup threw " + error.GetType().Name; }
			RestoreTerminalRows(rows);
			Target.SetStringProperty(KingdomGuestbook.MarketHandoffIntentProperty,
				bodyIntent, RemoveIfNull: true);
			Target.SetStringProperty(KingdomGuestbook.MarketHandoffPriorProperty,
				bodyPrior, RemoveIfNull: true);
			return false;
		}

		private static bool TryFinalizeLiveSourceDeadHandoff(KingdomSystem System,
			IList<GameObject> Loaded, GameObject SourceBody,
			r_KingdomMarketHandoffSourceProjection Source, GameObject Target,
			out string Failure)
		{
			Failure = null;
			if (!TryExactTerminalReceipt(System, Target, Source, out _,
				out KingdomLifecycleLodgeTerminalReceipt receipt)
				|| Source.LifecycleTerminalClosed != 0 || Source.TargetTerminalDead != 0
				|| !ExactLiveDormantSourceDead(System, Loaded, Target, Source, receipt))
				{ Failure = "live SourceDead residue is not an exact dormant handoff"; return false; }
			SourceBody.RemovePart(Source);
			if (SourceBody.GetPart<r_KingdomMarketHandoffSourceProjection>() == null) return true;
			Failure = "live SourceDead source receipt did not retire"; return false;
		}

		private static void ClearTerminalRows(List<SourceAbortRow> Rows)
		{
			for (int i = 0; i < Rows.Count; i++)
			{
				Rows[i].Item.SetStringProperty(KingdomShopStockRules.StockTransferTargetProperty,
					null, RemoveIfNull: true);
				Rows[i].Item.SetStringProperty(KingdomGuestbook.MarketTransferTargetProperty,
					null, RemoveIfNull: true);
			}
		}

		private static void RestoreTerminalRows(List<SourceAbortRow> Rows)
		{
			for (int i = Rows.Count - 1; i >= 0; i--)
			{
				Rows[i].Item.SetStringProperty(KingdomGuestbook.MarketTransferTargetProperty,
					Rows[i].MarketTarget, RemoveIfNull: true);
				Rows[i].Item.SetStringProperty(KingdomShopStockRules.StockTransferTargetProperty,
					Rows[i].StockTarget, RemoveIfNull: true);
			}
		}

		private static bool RollbackCompletedTarget(List<SourceAbortRow> Rows,
			KingdomMarketRemovalTransaction Transaction, GameObject Target,
			r_KingdomLegendaryMarketProjection Legend, bool HadVillage, int Village,
			string BodyIntent, string BodyPrior, string Cause, out string Failure)
		{
			bool restored = KingdomMarketRemoval.TryRollback(Transaction, out string rollback);
			try
			{
				RestoreTerminalRows(Rows);
				if (Target.GetPart<r_KingdomLegendaryMarketProjection>() == null) Target.AddPart(Legend);
				if (HadVillage) Target.SetIntProperty("VillageMerchant", Village);
				else Target.RemoveIntProperty("VillageMerchant");
				Target.SetStringProperty(KingdomGuestbook.MarketHandoffIntentProperty,
					BodyIntent, RemoveIfNull: true);
				Target.SetStringProperty(KingdomGuestbook.MarketHandoffPriorProperty,
					BodyPrior, RemoveIfNull: true);
			}
			catch { restored = false; }
			Failure = (Cause ?? "completed terminal cleanup failed")
				+ (restored ? "" : "; rollback incomplete: " + rollback);
			return false;
		}
	}
}
