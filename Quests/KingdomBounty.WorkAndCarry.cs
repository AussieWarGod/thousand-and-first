using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomBounty
	{
		private static void Work(KingdomSystem System, Zone Z, KingdomSurvey Survey, GameObject Notice, r_KingdomNotice Data)
		{
			BountyTask task = (BountyTask)Data.TaskCode;
			long now = The.Game.TimeTicks;
			switch (task)
			{
			case BountyTask.Clearance:
			{
				KingdomMaterials.ClearanceAssessment assessment = KingdomMaterials.Assess(System, Z, Data.X1, Data.Y1, Data.X2, Data.Y2);
				if (!assessment.Valid || assessment.Standing > 0)
				{
					return;
				}
				Finish(System, Z, Survey, Notice, Data, null);
				return;
			}
			case BountyTask.Manning:
			{
				ManOneWork(System, Survey);
				if (now < Data.DueTick)
				{
					return;
				}
				Finish(System, Z, Survey, Notice, Data, null);
				return;
			}
			case BountyTask.Fetch:
			{
				if (now < Data.DueTick)
				{
					return;
				}
				Carry(System, Z, Notice, Data, Survey);
				return;
			}
			case BountyTask.Scouting:
			{
				if (now < Data.DueTick)
				{
					return;
				}
				Scout(System, Z, Survey, Notice, Data);
				return;
			}
			default:
				Finish(System, Z, Survey, Notice, Data, null);
				return;
			}
		}

		private static void Carry(KingdomSystem System, Zone Z, GameObject Notice, r_KingdomNotice Data, KingdomSurvey Survey)
		{
			if ((BountyTransferPhase)Data.TransferPhase == BountyTransferPhase.Quarantined)
			{
				TellQuarantine(System, Data);
				return;
			}
			if ((BountyTransferPhase)Data.TransferPhase != BountyTransferPhase.None
				&& !ContinueTransfer(Z, Notice, Data))
			{
				TellQuarantine(System, Data);
				return;
			}
			GameObject pile = FindPile(Z, Notice, Data);
			if (pile == null && (BountyTransferPhase)Data.TransferPhase == BountyTransferPhase.None)
			{
				Announce(System, Data, BountyBlock.PileEmpty);
				return;
			}
			if (Data.HaulPhase == 1)
			{
				Quarantine(Data, "A porter handoff returned through an uninspectable callback seam.");
				TellQuarantine(System, Data);
				return;
			}
			if (Data.HaulPhase == 3)
			{
				return;
			}
			if (HaulHook != null && Data.HaulPhase == 0)
			{
				Data.HaulPhase = 1;
				if (HaulHook(System, pile, Data.WorkerName, Data.TakenTick))
				{
					Data.HaulPhase = 3;
					return;
				}
				Data.HaulPhase = 2;
			}
			KingdomMaterials.MaterialStock stock = KingdomMaterials.Stock(Z);
			GameObject container = null;
			for (int i = 0; i < stock.Stockpiles.Count; i++)
			{
				if (stock.Stockpiles[i].Inventory != null && stock.Stockpiles[i] != pile)
				{
					container = stock.Stockpiles[i];
					break;
				}
			}
			if (container == null)
			{
				Announce(System, Data, BountyBlock.NowhereToCarry);
				return;
			}
			if (pile == null || pile.Inventory == null)
			{
				Announce(System, Data, BountyBlock.PileEmpty);
				return;
			}
			int guard = pile.Inventory.Objects.Count + 1;
			while (guard-- > 0)
			{
				if ((BountyTransferPhase)Data.TransferPhase == BountyTransferPhase.None)
				{
					GameObject next = null;
					for (int i = 0; i < pile.Inventory.Objects.Count; i++)
					{
						GameObject candidate = pile.Inventory.Objects[i];
						if (GameObject.Validate(candidate) && KingdomMaterials.TryMaterialOf(candidate, out _))
						{
							next = candidate;
							break;
						}
					}
					if (next == null) break;
					Data.TransferItemId = next.ID;
					Data.TransferSourceId = pile.ID;
					Data.TransferDestinationId = container.ID;
					Data.TransferUnits = next.Count;
					Data.TransferTotalBefore = Data.TransferredUnits;
					Data.TransferPhase = (int)BountyTransferPhase.Bound;
				}
				if (!ContinueTransfer(Z, Notice, Data))
				{
					TellQuarantine(System, Data);
					return;
				}
			}
			if ((BountyTransferPhase)Data.TransferPhase != BountyTransferPhase.None)
			{
				return;
			}
			if (Data.TransferredUnits <= 0)
			{
				Announce(System, Data, BountyBlock.PileEmpty);
				return;
			}
			Announce(System, Data, BountyBlock.None);
			int moved = Data.TransferredUnits;
			Finish(System, Z, Survey, Notice, Data, moved
				+ ((moved == 1) ? " load was carried in" : " loads were carried in"));
		}

	}
}
