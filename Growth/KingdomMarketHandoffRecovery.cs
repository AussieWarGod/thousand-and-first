using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomGrowth
	{
		private sealed class SourceAbortRow
		{
			internal GameObject Item;
			internal string MarketTarget;
			internal string StockTarget;
			internal bool Retire;
		}

		/// <summary>Recovers only an exact source receipt whose frozen target resident is
		/// recorded Dead. Missing objects alone are never terminal evidence.</summary>
		private static bool TryRecoverAbsentHandoffTargets(KingdomSystem System,
			KingdomSurvey Survey, string Settlement, out string Failure)
		{
			Failure = null;
			if (!KingdomMarketHandoffGlobalIndex.TryLoaded(out IList<GameObject> loaded))
				{ Failure = "market handoff recovery lacks the bounded global index"; return false; }
			if (!KingdomMarketHandoffGraphAuthority.TryPreflight(System, loaded,
				Settlement, out Failure)) return false;
			for (int i = 0; i < loaded.Count; i++)
			{
				GameObject source = loaded[i];
				r_KingdomMarketHandoffSourceProjection marker =
					source?.GetPart<r_KingdomMarketHandoffSourceProjection>();
				if (marker == null || marker.SettlementId != Settlement) continue;
				if (!marker.Exact(System, source) && !marker.ExactTerminal(System, source))
					{ Failure = "market handoff source receipt is divergent"; return false; }
				if (!KingdomMarketHandoffGraphAuthority.TryUnique(loaded,
					marker.TargetBodyObjectId, out GameObject target))
					{ Failure = "market handoff target identity is ambiguous"; return false; }
				if (GameObject.Validate(target) && target.IsAlive)
				{
					KingdomLifecycleLodgeTerminalReceipt liveReceipt = KingdomGuestLifecycle.Open(
						System, KingdomLifecycleLane.NotableGuest)?.LodgeTerminal;
					r_KingdomLegendaryMarketProjection liveLegend =
						target.GetPart<r_KingdomLegendaryMarketProjection>();
					bool dormantSourceDead = liveReceipt?.MarketSourcePrepared
						== KingdomLifecycleLodgeTerminalReceipt.MarketSourceDead
						&& liveLegend?.HandoffPrepared == 0
						&& !target.HasIntProperty("VillageMerchant")
						&& !target.HasStringProperty(KingdomGuestbook.MarketHandoffIntentProperty)
						&& !target.HasStringProperty(KingdomGuestbook.MarketHandoffPriorProperty);
					if (dormantSourceDead && !TryFinalizeLiveSourceDeadHandoff(System,
						loaded, source, marker, target, out Failure)) return false;
					continue;
				}
				if (!r_KingdomLegendaryMarketProjection.DeadResident(System, Settlement,
					marker.TargetResidentId))
					{ Failure = "missing handoff target lacks exact Dead resident proof"; return false; }
				if (marker.LifecycleTerminalClosed != 1
					|| !ExactLifecycleRelease(System, marker))
					{ Failure = "dead handoff target awaits exact lifecycle terminal closure"; return false; }
				r_KingdomLegendaryMarketProjection targetMarker =
					target?.GetPart<r_KingdomLegendaryMarketProjection>();
				if (targetMarker?.HandoffPrepared == 1)
				{
					if (!TryAbortPreparedLegendaryHandoff(System,
						Survey, target, targetMarker, out Failure)) return false;
				}
				else if (targetMarker != null)
				{
					if (!TryRetireCompletedDeadHandoffTarget(System, loaded, target,
						marker, targetMarker, out Failure)) return false;
				}
				else if (GameObject.Validate(target) && marker.TargetTerminalDead != 1
					&& !TryClearMarkerlessDeadHandoffTarget(System, loaded, target,
						marker, out Failure)) return false;
				if (GameObject.Validate(target) && (target.GetPart<r_KingdomLegendaryMarketProjection>()
					!= null || target.HasIntProperty("VillageMerchant")
					|| target.HasStringProperty(KingdomGuestbook.MarketHandoffIntentProperty)
					|| target.HasStringProperty(KingdomGuestbook.MarketHandoffPriorProperty)))
					{ Failure = "terminal handoff target still carries civic authority"; return false; }
				if (marker.TargetTerminalDead == 1)
				{
					if (MatchingReleasedSlot(System, marker)) continue;
					if (!TryFinalizeTerminalSource(loaded, source, marker, out Failure)) return false;
					continue;
				}
				if (!TryReleaseAbsentTarget(System, loaded, source, marker, out Failure))
					return false;
			}
			return true;
		}

		private static bool TryFinalizeTerminalSource(IList<GameObject> Loaded,
			GameObject Source, r_KingdomMarketHandoffSourceProjection Marker,
			out string Failure)
		{
			Failure = null;
			if (!KingdomMarketHandoffGraphAuthority.TryUnique(Loaded,
				Marker.TargetBodyObjectId, out GameObject target))
				{ Failure = "terminal market handoff target identity is ambiguous"; return false; }
			for (int i = 0; i < Loaded.Count; i++)
				if (Loaded[i]?.GetStringProperty(KingdomGuestbook.MarketTransferTargetProperty)
					== Marker.TargetBodyObjectId || Loaded[i]?.GetStringProperty(
						KingdomShopStockRules.StockTransferTargetProperty)
						== Marker.TargetBodyObjectId
					|| ReferenceEquals(Loaded[i]?.InInventory, target)
						&& KingdomMarketStockProtection.HasProjection(Loaded[i]))
					{ Failure = "terminal market handoff still owns a transfer intent"; return false; }
			Source.RemovePart(Marker);
			if (Source.GetPart<r_KingdomMarketHandoffSourceProjection>() == null) return true;
			Failure = "terminal market handoff source receipt did not retire"; return false;
		}

		private static bool ExactLifecycleRelease(KingdomSystem System,
			r_KingdomMarketHandoffSourceProjection Marker)
		{
			KingdomLifecycleBook book = System?.LifecycleBook;
			if (book == null
				|| book.SettlementId != Marker.SettlementId
				|| book.NotableGuestRetiredThrough < Marker.LifecycleSequence) return false;
			if (Marker.TargetTerminalDead == 1 && book.NotableGuest == null) return true;
			if (book.RecentProofs == null || !MatchingReleasedSlot(System, Marker)) return false;
			int matches = 0;
			for (int i = 0; i < book.RecentProofs.Count; i++)
			{
				KingdomLifecycleProof proof = book.RecentProofs[i];
				if (proof.Sequence == Marker.LifecycleSequence
					&& proof.Id == Marker.LifecycleOperationId
					&& proof.PlanHash == Marker.LifecyclePlanHash
					&& proof.Lane == KingdomLifecycleLane.NotableGuest
					&& proof.Action == KingdomLifecycleAction.Lodge) matches++;
			}
			return matches == 1;
		}

		private static bool MatchingReleasedSlot(KingdomSystem System,
			r_KingdomMarketHandoffSourceProjection Marker)
		{
			KingdomLifecycleOperation op = System?.LifecycleBook?.NotableGuest;
			return op != null && op.Sequence == Marker.LifecycleSequence
				&& op.Id == Marker.LifecycleOperationId && op.PlanHash == Marker.LifecyclePlanHash
				&& op.Action == KingdomLifecycleAction.Lodge
				&& op.Lane == KingdomLifecycleLane.NotableGuest && op.ObjectId == Marker.TargetBodyObjectId
				&& op.LodgeTerminal?.State == KingdomLifecycleLodgeTerminalState.AuthorityReleased;
		}

		private static bool TryReleaseAbsentTarget(KingdomSystem System,
			IList<GameObject> Loaded, GameObject Source,
			r_KingdomMarketHandoffSourceProjection Marker, out string Failure)
		{
			Failure = null; List<SourceAbortRow> rows = new List<SourceAbortRow>();
			List<GameObject> retire = new List<GameObject>();
			HashSet<string> receipts = new HashSet<string>();
			for (int i = 0; i < Loaded.Count; i++)
			{
				GameObject item = Loaded[i];
				if (!GameObject.Validate(item)) continue;
				string marketTarget = item.GetStringProperty(
					KingdomGuestbook.MarketTransferTargetProperty);
				string stockTarget = item.GetStringProperty(
					KingdomShopStockRules.StockTransferTargetProperty);
				if (marketTarget != Marker.TargetBodyObjectId
					&& stockTarget != Marker.TargetBodyObjectId) continue;
				KingdomMarketHandoffIntentState pair = KingdomMarketHandoffIntentRules.Classify(
					marketTarget, Marker.TargetBodyObjectId,
					stockTarget, Marker.TargetBodyObjectId);
				if (pair == KingdomMarketHandoffIntentState.Divergent)
					{ Failure = "absent-target handoff has a cross-target intent"; return false; }
				if (rows.Count >= KingdomShopStockRules.MaximumCustodyRows)
					{ Failure = "absent-target custody roster is torn or exceeds its bound"; return false; }
				bool projection = KingdomMarketStockProtection.HasProjection(item);
				bool atSource = ReferenceEquals(item.InInventory, Source);
				if (!projection && (pair == KingdomMarketHandoffIntentState.SecondOnly
					|| pair == KingdomMarketHandoffIntentState.Paired))
					{ Failure = "absent-target stock intent lost its receipt"; return false; }
				bool liveSource = Marker.ExactLive(System, Source);
				bool shouldRetire = projection && (!atSource || !liveSource);
				string receipt = item.GetStringProperty(KingdomShopStockRules.StockReceiptProperty);
				if (projection && (!KingdomMarketStockCustody.OurMarker(System,
					Marker.SettlementId, item) || item.GetIntProperty("_stock") != 1
					|| receipt != KingdomShopStockRules.StockReceiptId(System.RealmId,
						Marker.SettlementId, item.IDIfAssigned)
					|| string.IsNullOrEmpty(receipt) || !receipts.Add(receipt)))
					{ Failure = "absent-target stock receipt is foreign or divergent"; return false; }
				if (projection && atSource && liveSource
					&& !KingdomMarketStockCustody.ExactHeld(System,
					Marker.SettlementId, Source, item))
					{ Failure = "living source stock custody is not exact"; return false; }
				if (shouldRetire) retire.Add(item);
				rows.Add(new SourceAbortRow { Item = item, MarketTarget = marketTarget,
					StockTarget = stockTarget, Retire = shouldRetire });
			}
			if (!KingdomMarketRemoval.TryPrepareTransaction(System, retire,
				new List<GameObject>(), out KingdomMarketRemovalTransaction tx, out Failure)
				|| !KingdomMarketRemoval.TryCommitTransaction(System, tx, out Failure)) return false;
			try
			{
				for (int i = 0; i < rows.Count; i++)
				{
					if (!rows[i].Retire) rows[i].Item.SetStringProperty(
						KingdomShopStockRules.StockTransferTargetProperty, null, RemoveIfNull: true);
					rows[i].Item.SetStringProperty(KingdomGuestbook.MarketTransferTargetProperty,
						null, RemoveIfNull: true);
				}
				for (int i = 0; i < rows.Count; i++)
					if (!rows[i].Retire && KingdomMarketStockProtection.HasProjection(rows[i].Item)
						&& !KingdomMarketStockCustody.TryRebindPhysical(System, Marker.SettlementId,
							Source, rows[i].Item, out Failure))
						return RollbackAbsentTarget(rows, tx, Failure, out Failure);
			}
			catch (System.Exception error)
			{
				return RollbackAbsentTarget(rows, tx, "absent-target intent cleanup threw "
					+ error.GetType().Name, out Failure);
			}
			Marker.TargetTerminalDead = 1;
			if (Source.GetPart<r_KingdomMarketHandoffSourceProjection>() == Marker) return true;
			Failure = "terminal source checkpoint did not persist exactly"; return false;
		}

		private static bool RollbackAbsentTarget(List<SourceAbortRow> Rows,
			KingdomMarketRemovalTransaction Transaction, string Cause, out string Failure)
		{
			bool restored = true;
			for (int i = Rows.Count - 1; i >= 0; i--)
				try
				{
					Rows[i].Item.SetStringProperty(KingdomGuestbook.MarketTransferTargetProperty,
						Rows[i].MarketTarget, RemoveIfNull: true);
					Rows[i].Item.SetStringProperty(KingdomShopStockRules.StockTransferTargetProperty,
						Rows[i].StockTarget, RemoveIfNull: true);
				}
				catch (System.Exception) { restored = false; }
			if (!KingdomMarketRemoval.TryRollback(Transaction, out string rollback)) restored = false;
			Failure = restored ? Cause : (Cause ?? "absent-target recovery failed")
				+ "; rollback did not restore exact custody";
			return false;
		}
	}
}
