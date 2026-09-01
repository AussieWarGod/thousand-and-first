using System;
using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	internal static partial class KingdomMarketStockCustody
	{
		private static bool MoveTo(Move Move, GameObject Target, out string Failure)
		{
			Failure = null;
			Move.Item.SetStringProperty(KingdomShopStockRules.StockTransferTargetProperty,
				Target.IDIfAssigned);
			GameObject accepted = null;
			try { accepted = Target.Inventory.AddObjectToInventory(Move.Item, null,
				Silent: true, NoStack: true); }
			catch (Exception error)
			{
				Failure = "market stock move failed (" + error.GetType().Name + ")";
			}
			if (ReferenceEquals(accepted, Move.Item) && NativeStock(Target, Move.Item)
				&& !Move.Source.Inventory.Objects.Contains(Move.Item)) return true;
			Failure = Failure ?? "market stock move did not read back exactly"; return false;
		}

		private static bool Rollback(KingdomSystem System, string SettlementId,
			GameObject Target, List<Move> Moves)
		{
			bool exact = true;
			for (int i = Moves.Count - 1; i >= 0; i--)
			{
				Move move = Moves[i];
				if (ReferenceEquals(move.Item.InInventory, Target))
				{
					GameObject accepted = null;
					try { accepted = move.Source.Inventory.AddObjectToInventory(move.Item, null,
						Silent: true, NoStack: true); } catch { }
					if (!ReferenceEquals(accepted, move.Item)
						|| !ReferenceEquals(move.Item.InInventory, move.Source))
						{ exact = false; continue; }
				}
				if (!ReferenceEquals(move.Item.InInventory, move.Source))
					{ exact = false; continue; }
				bool rebound = ExactHeld(System, SettlementId, move.Source, move.Item)
					|| (KingdomMarketStockProtection.HasProjection(move.Item)
						? TryRebindPhysical(System, SettlementId, move.Source,
							move.Item, out _)
						: TryBind(System, SettlementId, move.Source, move.Item,
							false, out _));
				if (!rebound || !ExactHeld(System, SettlementId, move.Source, move.Item))
					exact = false;
			}
			return exact;
		}
	}
}
