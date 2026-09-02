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
		private static void ContinuePost(KingdomSystem System, Zone Z, GameObject Notice,
			r_KingdomNotice Data)
		{
			BountyPostPhase phase = (BountyPostPhase)Data.PostPhase;
			if (phase == BountyPostPhase.None || phase == BountyPostPhase.Complete) return;
			Cell expectedCell = BoundCell(Z, Data.PostZoneId, Data.PostCellX, Data.PostCellY);
			if (!NoticeBindingExact(Notice, Data, Z, expectedCell))
			{
				Quarantine(Data, "The posted notice no longer has its exact object, part, cell, and zone binding.");
				return;
			}
			if (!string.IsNullOrEmpty(Data.PileId))
			{
				Cell pileCell = BoundCell(Z, Data.PostZoneId,
					Data.PostPileCellX, Data.PostPileCellY);
				GameObject pile = (Z == null) ? null : Z.FindObjectByID(Data.PileId);
				if (!PileBindingExact(pile, Z, pileCell)
					|| pile.GetStringProperty(FetchMarkProperty) != Notice.IDIfAssigned)
				{
					Quarantine(Data, "The posted fetch notice no longer owns its exact pile mark.");
					return;
				}
			}
			if (phase == BountyPostPhase.Bound)
			{
				if (!KingdomChronicle.RecordOnce(System, EventId(Data, "posted"),
					Data.PostChronicleLine)) return;
				Data.PostPhase = (int)BountyPostPhase.ChronicleDone;
				phase = BountyPostPhase.ChronicleDone;
			}
			if (phase == BountyPostPhase.ChronicleDone)
			{
				if (!DeliverMessage(ref Data.PostMessageState, Data.PostMessageLine)) return;
				Data.PostPhase = (int)BountyPostPhase.MessageSettled;
				phase = BountyPostPhase.MessageSettled;
			}
			if (phase == BountyPostPhase.MessageSettled)
			{
				Data.PostPhase = (int)BountyPostPhase.Complete;
			}
		}

		private static Cell BoundCell(Zone Z, string ZoneId, int X, int Y)
		{
			if (Z == null || string.IsNullOrEmpty(ZoneId) || Z.ZoneID != ZoneId
				|| X < 0 || Y < 0) return null;
			return Z.GetCell(X, Y);
		}

		private static bool NoticeBindingExact(GameObject Notice, r_KingdomNotice Data,
			Zone Z, Cell Cell)
		{
			return GameObject.Validate(Notice) && Data != null && Z != null && Cell != null
				&& Notice.CurrentZone == Z && Notice.CurrentCell == Cell && Cell.ParentZone == Z
				&& ReferenceEquals(Data.ParentObject, Notice)
				&& ReferenceEquals(Notice.GetPart<r_KingdomNotice>(), Data);
		}

		private static bool PileBindingExact(GameObject Pile, Zone Z, Cell Cell)
		{
			return GameObject.Validate(Pile) && Z != null && Cell != null
				&& Pile.CurrentZone == Z && Pile.CurrentCell == Cell && Cell.ParentZone == Z;
		}

		private static bool ClearBoundFetchMark(Zone Z, GameObject Notice,
			r_KingdomNotice Data, string PileId)
		{
			Cell noticeCell = BoundCell(Z, Data.WithdrawZoneId,
				Data.WithdrawCellX, Data.WithdrawCellY);
			if (!NoticeBindingExact(Notice, Data, Z, noticeCell)) return false;
			if (string.IsNullOrEmpty(PileId))
			{
				Data.PileId = null;
				return true;
			}
			Cell pileCell = BoundCell(Z, Data.WithdrawZoneId,
				Data.WithdrawPileCellX, Data.WithdrawPileCellY);
			GameObject pile = (Z == null) ? null : Z.FindObjectByID(PileId);
			if (!PileBindingExact(pile, Z, pileCell)
				|| pile.GetStringProperty(FetchMarkProperty) != Notice.IDIfAssigned) return false;
			pile.RemoveStringProperty(FetchMarkProperty);
			if (!PileBindingExact(pile, Z, pileCell)
				|| !string.IsNullOrEmpty(pile.GetStringProperty(FetchMarkProperty))
				|| !NoticeBindingExact(Notice, Data, Z, noticeCell)) return false;
			Data.PileId = null;
			return true;
		}
	}
}
