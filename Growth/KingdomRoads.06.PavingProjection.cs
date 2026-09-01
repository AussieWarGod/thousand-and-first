using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomRoads
	{
		private sealed class RoadReceipt
		{
			public string TallyBefore, TallyAfter, FullBefore, FullAfter;
			public int State;
			public List<RoadRow> Rows = new List<RoadRow>();
		}

		private sealed class RoadRow
		{
			public int X, Y;
			public string OldId, OldBlueprint, NewId;
			public bool Settled;
		}

		private static bool RoadTerminalExact(Zone Z, string Blueprint,
			IList<KingdomConstructionCell> Cells, KingdomConstructionJob Job)
		{
			if (Z == null || string.IsNullOrEmpty(Blueprint) || Cells == null || Job == null
				|| !TryDecodeRoadReceipt(Job.PhysicalReceipt, out var receipt)
				|| receipt.State != 2 || receipt.Rows.Count != Cells.Count
				|| Job.PhysicalIndex != receipt.Rows.Count
				|| (Z.GetZoneProperty(TallyProperty, null) ?? "") != receipt.TallyAfter
				|| (Z.GetZoneProperty(FullSaidProperty, null) ?? "") != receipt.FullAfter) return false;
			for (int i = 0; i < receipt.Rows.Count; i++)
			{
				RoadRow row = receipt.Rows[i];
				if (!row.Settled || row.X != Cells[i].X || row.Y != Cells[i].Y
					|| !ExactRoadFloor(Z, row, Blueprint, Job, true)) return false;
			}
			return true;
		}

		private static bool ProjectPaving(Zone Z, string Blueprint,
			IList<KingdomConstructionCell> Cells, KingdomConstructionJob Job,
			out KingdomConstructionJob Updated, out int NewlyLaid, out string Failure)
		{
			Updated = Job;
			NewlyLaid = 0;
			Failure = null;
			if (Z == null || string.IsNullOrEmpty(Blueprint) || Cells == null || Cells.Count == 0
				|| !CurrentRoadOwner(Z, Job))
				return false;
			if (KingdomConstructionRules.IsTerminal(Updated.Phase))
				return Updated.Phase == KingdomConstructionPhase.Complete
					&& Updated.PhysicalPhase == KingdomPhysicalPhase.RoadTallySettled;
			RoadReceipt receipt;
			if (Updated.PhysicalPhase == KingdomPhysicalPhase.None)
			{
				if (Updated.Phase != KingdomConstructionPhase.ProjectionPending
					&& !KingdomConstruction.BeginProjection(ref Updated, out Failure)) return false;
				if (!FreezeRoadReceipt(Z, Cells, out receipt))
				{
					Failure = "The exact old road-floor identities or tally could not be frozen.";
					KingdomConstruction.Quarantine(ref Updated, Failure);
					return false;
				}
				if (!KingdomConstruction.UpdatePhysical(ref Updated,
					KingdomPhysicalPhase.RoadPlanFrozen, 0, 0, 0, null, null,
					EncodeRoadReceipt(receipt))) return false;
			}
			if (!TryDecodeRoadReceipt(Updated.PhysicalReceipt, out receipt)
				|| receipt.Rows.Count != Cells.Count || Updated.PhysicalIndex > receipt.Rows.Count)
			{
				Failure = "The frozen road receipt is malformed.";
				KingdomConstruction.Quarantine(ref Updated, Failure);
				return false;
			}
			for (int i = 0; i < receipt.Rows.Count; i++)
			{
				RoadRow row = receipt.Rows[i];
				if (row.X != Cells[i].X || row.Y != Cells[i].Y)
				{
					Failure = "Road receipt coordinates no longer match the frozen route.";
					KingdomConstruction.Quarantine(ref Updated, Failure);
					return false;
				}
				if (row.Settled)
				{
					if (!ExactRoadFloor(Z, row, Blueprint, Updated, true))
					{
						Failure = "A settled paved floor moved, changed, or was replaced.";
						KingdomConstruction.Quarantine(ref Updated, Failure);
						return false;
					}
					continue;
				}
				Cell cell = Z.GetCell(row.X, row.Y);
				GameObject old;
				GameObject floor;
				KingdomPhysicalLookupState oldState = FindRoadId(Z, row.OldId, out old);
				KingdomPhysicalLookupState floorState = FindRoadId(Z, row.NewId, out floor);
				if (oldState == KingdomPhysicalLookupState.Ambiguous
					|| floorState == KingdomPhysicalLookupState.Ambiguous)
				{
					Failure = "A road receipt ID resolves to more than one loaded physical object.";
					KingdomConstruction.Quarantine(ref Updated, Failure);
					return false;
				}
				if (Updated.PhysicalPhase == KingdomPhysicalPhase.RoadOutputPending
					&& Updated.PhysicalIndex == i)
				{
					if (!ExactRoadOld(old, cell, row) || !ExactRoadFloor(Z, row,
						Blueprint, Updated, false))
					{
						Failure = "Road AddObject was interrupted without exact old/new proof.";
						KingdomConstruction.Quarantine(ref Updated, Failure);
						return false;
					}
					if (!KingdomConstruction.UpdatePhysical(ref Updated,
						KingdomPhysicalPhase.RoadOutputSettled, i, 0, 0,
						row.OldId, row.NewId, EncodeRoadReceipt(receipt))) return false;
				}
				else if (Updated.PhysicalPhase == KingdomPhysicalPhase.RoadRemovalPending
					&& Updated.PhysicalIndex == i)
				{
					Failure = "Road predecessor removal was interrupted before callback-success proof.";
					KingdomConstruction.Quarantine(ref Updated, Failure);
					return false;
				}
				else if (Updated.PhysicalPhase == KingdomPhysicalPhase.RoadPlanFrozen)
				{
					if (!ExactRoadOld(old, cell, row)
						|| floorState != KingdomPhysicalLookupState.Absent)
					{
						Failure = "A frozen old road floor changed before paving.";
						KingdomConstruction.Quarantine(ref Updated, Failure);
						return false;
					}
					if (string.IsNullOrEmpty(row.NewId))
					{
						do { row.NewId = System.Guid.NewGuid().ToString("N"); }
						while (FindRoadId(Z, row.NewId, out _)
							== KingdomPhysicalLookupState.Exact);
						if (FindRoadId(Z, row.NewId, out _)
							!= KingdomPhysicalLookupState.Absent
							|| !KingdomConstruction.UpdatePhysical(ref Updated,
								KingdomPhysicalPhase.RoadPlanFrozen, i, 0, 0,
								row.OldId, row.NewId, EncodeRoadReceipt(receipt))) return false;
						floorState = FindRoadId(Z, row.NewId, out floor);
					}
					if (floorState != KingdomPhysicalLookupState.Absent)
					{
						Failure = "The frozen road output ID is absent, duplicated, or already occupied.";
						KingdomConstruction.Quarantine(ref Updated, Failure);
						return false;
					}
					try { floor = GameObject.Create(Blueprint); }
					catch (System.Exception ex)
					{
						Failure = "Road floor creation threw: " + ex.Message;
						KingdomConstruction.Quarantine(ref Updated, Failure);
						return false;
					}
					if (!GameObject.Validate(floor))
					{
						Failure = "Road floor blueprint created no exact output.";
						KingdomConstruction.Quarantine(ref Updated, Failure);
						return false;
					}
					if (!CurrentRoadOwner(Z, Updated) || !ExactRoadOld(old, cell, row)
						|| FindRoadId(Z, row.NewId, out _) != KingdomPhysicalLookupState.Absent)
					{
						RemoveRoadObject(floor, Z);
						Failure = "Road endpoints changed during output creation.";
						KingdomConstruction.Quarantine(ref Updated, Failure);
						return false;
					}
					floor.ID = row.NewId;
					floor.SetIntProperty(PathStateProperty,
						(int)KingdomRoadRules.WearState.Paved);
					CopyRoadSemantic(old, floor);
					KingdomConstruction.Bind(floor, Updated);
					if (!KingdomConstruction.UpdatePhysical(ref Updated,
						KingdomPhysicalPhase.RoadOutputPending, i, 0, 0,
						row.OldId, row.NewId, EncodeRoadReceipt(receipt)))
					{
						RemoveRoadObject(floor, Z);
						return false;
					}
					GameObject accepted;
					try
					{
						accepted = cell.AddObject(floor);
						KingdomSurvey.ObserveAddResultInActive(Z, floor, accepted);
					}
					catch (System.Exception ex)
					{
						bool cleaned = RemoveRoadObject(floor, Z);
						Failure = (cleaned ? "Road AddObject threw after output publication: "
							: "Road AddObject threw and cleanup failed: ") + ex.Message;
						KingdomConstruction.Quarantine(ref Updated, Failure);
						return false;
					}
					if (!ReferenceEquals(accepted, floor) || !CurrentRoadOwner(Z, Updated)
						|| !ExactRoadOld(old, cell, row)
						|| !ExactRoadFloor(Z, row, Blueprint, Updated, false))
					{
						Failure = "Road endpoints changed during AddObject.";
						KingdomConstruction.Quarantine(ref Updated, Failure);
						return false;
					}
					if (!KingdomConstruction.UpdatePhysical(ref Updated,
						KingdomPhysicalPhase.RoadOutputSettled, i, 0, 0,
						row.OldId, row.NewId, EncodeRoadReceipt(receipt))) return false;
				}
				if (Updated.PhysicalPhase != KingdomPhysicalPhase.RoadOutputSettled
					|| !KingdomConstruction.UpdatePhysical(ref Updated,
						KingdomPhysicalPhase.RoadRemovalPending, i, 0, 0,
						row.OldId, row.NewId, EncodeRoadReceipt(receipt))) return false;
				bool removed;
				try { removed = old.Obliterate(null, Silent: true); }
				catch (System.Exception ex)
				{
					KingdomSurvey.ObserveCurrentTopologyInActive(Z, old);
					Failure = "Road predecessor removal threw: " + ex.Message;
					KingdomConstruction.Quarantine(ref Updated, Failure);
					return false;
				}
				if (removed && !GameObject.Validate(old))
					KingdomSurvey.ObserveRemovedFromActive(Z, old);
				KingdomPhysicalLookupState oldAfter = FindRoadId(Z, row.OldId, out var oldReplacement);
				if (!removed || GameObject.Validate(old)
					|| oldAfter != KingdomPhysicalLookupState.Absent
					|| GameObject.Validate(oldReplacement) || !CurrentRoadOwner(Z, Updated)
					|| !ExactRoadFloor(Z, row, Blueprint, Updated, false))
				{
					Failure = "Road predecessor removal was vetoed, moved, or replaced.";
					KingdomConstruction.Quarantine(ref Updated, Failure);
					return false;
				}
				row.Settled = true;
				NewlyLaid++;
				if (!KingdomConstruction.UpdatePhysical(ref Updated,
					KingdomPhysicalPhase.RoadPlanFrozen, i + 1, 0, 0,
					row.OldId, row.NewId, EncodeRoadReceipt(receipt))) return false;
			}
			if (receipt.State == 0)
			{
				receipt.State = 1;
				if (!KingdomConstruction.UpdatePhysical(ref Updated,
					KingdomPhysicalPhase.RoadTallyPending, receipt.Rows.Count, 0, 0,
					null, null, EncodeRoadReceipt(receipt))) return false;
			}
			if (receipt.State != 1 || Updated.PhysicalPhase != KingdomPhysicalPhase.RoadTallyPending)
			{
				Failure = "Road tally receipt carries an impossible state.";
				KingdomConstruction.Quarantine(ref Updated, Failure);
				return false;
			}
			string tally = Z.GetZoneProperty(TallyProperty, null) ?? "";
			if (tally == receipt.TallyBefore)
			{
				Z.SetZoneProperty(TallyProperty, receipt.TallyAfter);
				if (!CurrentRoadOwner(Z, Updated)) return false;
			}
			else if (tally != receipt.TallyAfter)
			{
				Failure = "Road tally changed outside its frozen before/after values.";
				KingdomConstruction.Quarantine(ref Updated, Failure);
				return false;
			}
			string full = Z.GetZoneProperty(FullSaidProperty, null) ?? "";
			if (full == receipt.FullBefore)
			{
				Z.SetZoneProperty(FullSaidProperty, receipt.FullAfter);
				if (!CurrentRoadOwner(Z, Updated)) return false;
			}
			else if (full != receipt.FullAfter)
			{
				Failure = "Road full-tally notice changed outside its frozen before/after values.";
				KingdomConstruction.Quarantine(ref Updated, Failure);
				return false;
			}
			if ((Z.GetZoneProperty(TallyProperty, null) ?? "") != receipt.TallyAfter
				|| (Z.GetZoneProperty(FullSaidProperty, null) ?? "") != receipt.FullAfter)
				return false;
			receipt.State = 2;
			if (!KingdomConstruction.UpdatePhysical(ref Updated,
				KingdomPhysicalPhase.RoadTallySettled, receipt.Rows.Count, 0, 0,
				null, null, EncodeRoadReceipt(receipt))) return false;
			return KingdomConstruction.Complete(ref Updated);
		}

	}
}
