using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.World;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomPlots
	{
		private static bool TryBuildGrowthPlan(Zone Z, GameObject Predecessor,
			GameObject Successor, string SuccessorKey, string PlotId,
			KingdomPlotRules.PlotRect Old, KingdomPlotRules.PlotRect Grown,
			KingdomPlotRules.RoofState Roof, int HeartX, int HeartY, bool KeepInner,
			string Wall, out GrowthPlan Plan)
		{
			Plan = null;
			if (Z == null || !BoundedGrowthIdentity(Predecessor?.IDIfAssigned)
				|| !BoundedGrowthIdentity(Successor?.IDIfAssigned) || string.IsNullOrEmpty(SuccessorKey)
				|| !BoundedGrowthText(SuccessorKey, 256) || string.IsNullOrEmpty(PlotId)
				|| !BoundedGrowthText(PlotId, 128) || !BoundedGrowthText(Wall, 256)
				|| (KingdomPlotRules.RaisesWalls(Roof) && string.IsNullOrEmpty(Wall))) return false;
			List<GrowthRow> rows = new List<GrowthRow>();
			HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
			if (!KeepInner)
			{
				for (int y = Old.Y1; y <= Old.Y2; y++)
					for (int x = Old.X1; x <= Old.X2; x++)
					{
						if (Grown.Contains(x, y) && Grown.IsBorder(x, y)) continue;
						Cell cell = Z.GetCell(x, y);
						if (cell == null) return false;
						foreach (GameObject item in cell.GetObjects())
						{
							if (!GameObject.Validate(item)
								|| item.GetIntProperty(PlotPartProperty) != 1
								|| item.GetStringProperty(PlotIdProperty) != PlotId
								|| item.GetIntProperty(KingdomYards.YardWorkProperty) == 1
								|| !(item.IsWall() || item.IsDoor()
									|| item.Blueprint == FrameBlueprint)) continue;
							if (!BoundedGrowthIdentity(item.IDIfAssigned)
								|| !BoundedGrowthText(item.Blueprint, 256) || !ids.Add(item.IDIfAssigned)
								|| rows.Count >= MaxGrowthRows) return false;
							rows.Add(new GrowthRow { Kind = 1, X = x, Y = y,
								Blueprint = item.Blueprint, Id = item.IDIfAssigned, State = 0 });
						}
					}
			}
			if (KingdomPlotRules.Encloses(Roof))
			{
				bool hasDoor = KingdomPlotRules.TryDoor(Grown, HeartX, HeartY,
					out var doorX, out var doorY);
				for (int y = Grown.Y1; y <= Grown.Y2; y++)
					for (int x = Grown.X1; x <= Grown.X2; x++)
					{
						Cell cell = Z.GetCell(x, y);
						if (cell == null) return false;
						if (BlockedForPlot(cell)) continue;
						bool border = Grown.IsBorder(x, y);
						string blueprint = border
							? (hasDoor && x == doorX && y == doorY ? DoorBlueprint
								: (KingdomPlotRules.RaisesWalls(Roof) ? Wall : null))
							: FloorBlueprint;
						if (string.IsNullOrEmpty(blueprint)) continue;
						GameObject existing = null;
						int sameBlueprint = 0;
						foreach (GameObject item in cell.GetObjects())
							if (GameObject.Validate(item) && item.Blueprint == blueprint)
							{
								sameBlueprint++;
								if (item.GetIntProperty(PlotPartProperty) == 1
									&& item.GetStringProperty(PlotIdProperty) == PlotId)
									existing = item;
							}
						if (sameBlueprint > 0 && (sameBlueprint != 1 || existing == null)) return false;
						string outputId = existing?.IDIfAssigned;
						int state = existing == null ? 0 : 2;
						if (existing == null)
						{
							do { outputId = Guid.NewGuid().ToString("N"); }
							while (ids.Contains(outputId));
							if (KingdomConstruction.FindExactId(Z, outputId, out _)
								!= KingdomPhysicalLookupState.Absent) return false;
						}
						if (!BoundedGrowthIdentity(outputId) || !ids.Add(outputId)
							|| rows.Count >= MaxGrowthRows) return false;
						rows.Add(new GrowthRow { Kind = 2, X = x, Y = y,
							Blueprint = blueprint, Id = outputId, State = state });
					}
			}
			rows.Sort(delegate(GrowthRow A, GrowthRow B)
			{
				return CompareGrowthRows(A, B);
			});
			Plan = new GrowthPlan { PredecessorId = Predecessor.IDIfAssigned,
				SuccessorId = Successor.IDIfAssigned, SuccessorKey = SuccessorKey, PlotId = PlotId,
				Old = Old, Grown = Grown, Roof = Roof, HeartX = HeartX, HeartY = HeartY,
				KeepInner = KeepInner, Wall = Wall ?? "", Done = false, Rows = rows };
			return true;
		}

		private static bool ApplyGrowthPlan(Zone Z, GameObject Predecessor,
			GameObject Successor, GrowthPlan Plan)
		{
			if (!ValidateGrowthWorld(Z, Predecessor, Successor, Plan, true)) return false;
			for (int i = 0; i < Plan.Rows.Count; i++)
			{
				GrowthRow row = Plan.Rows[i];
				if (row.Kind == 1)
				{
					if (row.State == 2) continue;
					if (row.State != 0) return false;
					GameObject exact;
					if (KingdomConstruction.FindExactId(Z, row.Id, out exact)
						!= KingdomPhysicalLookupState.Exact
						|| !ExactGrowthRemoval(exact, Z, row, Plan.PlotId)) return false;
					row.State = 1;
					if (!PublishGrowthPlan(Predecessor, Plan)) return false;
					bool removed;
					try { removed = exact.Destroy(null, Silent: true); }
					catch
					{
						KingdomSurvey.ObserveCurrentTopologyInActive(Z, exact);
						return false;
					}
					if (removed && !GameObject.Validate(exact))
						KingdomSurvey.ObserveRemovedFromActive(Z, exact);
					if (!removed || GameObject.Validate(exact)
						|| KingdomConstruction.FindExactId(Z, row.Id, out _)
							!= KingdomPhysicalLookupState.Absent
						|| !ExactGrowthEndpoints(Predecessor, Successor,
							Z.GetCell(Plan.Old.CenterX, Plan.Old.CenterY), Plan)) return false;
					row.State = 2;
					if (!PublishGrowthPlan(Predecessor, Plan)
						|| !ValidateGrowthWorld(Z, Predecessor, Successor, Plan, true)) return false;
					continue;
				}
				if (row.State == 2)
				{
					if (!RetireSettledGrowthRoot(Z, Predecessor, row, Plan.PlotId, null))
						return false;
					continue;
				}
				if (row.State == 1)
				{
					GameObject rooted;
					if (!TryGrowthRoot(Predecessor, row, out rooted)
						|| !ExactGrowthOutput(rooted, Z, row, Plan.PlotId)) return false;
					row.State = 2;
					if (!PublishGrowthPlan(Predecessor, Plan)
						|| !RetireSettledGrowthRoot(Z, Predecessor, row, Plan.PlotId, rooted))
						return false;
					continue;
				}
				if (row.State != 0 || !GrowthTargetEmpty(Z, row)) return false;
				GameObject placed;
				try { placed = GameObject.Create(row.Blueprint); }
				catch { return false; }
				if (!ExactGrowthEndpoints(Predecessor, Successor,
					Z.GetCell(Plan.Old.CenterX, Plan.Old.CenterY), Plan)
					|| !ValidateGrowthWorld(Z, Predecessor, Successor, Plan, true)
					|| !GameObject.Validate(placed) || placed.Blueprint != row.Blueprint) return false;
				placed.IDIfAssigned = row.Id;
				placed.SetIntProperty(PlotPartProperty, 1);
				placed.SetStringProperty(PlotIdProperty, Plan.PlotId);
				if (!RootGrowthOutput(Predecessor, row, placed)) return false;
				row.State = 1;
				if (!PublishGrowthPlan(Predecessor, Plan)) return false;
				GameObject accepted = null;
				try
				{
					accepted = Z.GetCell(row.X, row.Y).AddObject(placed);
				}
				catch
				{
					if (!TrySettleGrowthAddAfterCallback(Z, Predecessor, Successor,
						Plan, row, placed)) return false;
					continue;
				}
				finally
				{
					KingdomSurvey.ObserveAddResultInActive(Z, placed, accepted);
				}
				if (!ReferenceEquals(accepted, placed)
					|| !TrySettleGrowthAddAfterCallback(Z, Predecessor, Successor,
						Plan, row, placed)) return false;
			}
			return true;
		}

		private static bool TrySettleGrowthAddAfterCallback(Zone Z, GameObject Predecessor,
			GameObject Successor, GrowthPlan Plan, GrowthRow Row, GameObject Expected)
		{
			GameObject rooted;
			if (Row.State != 1 || !TryGrowthRoot(Predecessor, Row, out rooted)
				|| !ReferenceEquals(rooted, Expected)
				|| !ExactGrowthEndpoints(Predecessor, Successor,
					Z.GetCell(Plan.Old.CenterX, Plan.Old.CenterY), Plan)
				|| !ExactGrowthOutput(Expected, Z, Row, Plan.PlotId)) return false;
			Row.State = 2;
			return PublishGrowthPlan(Predecessor, Plan)
				&& RetireSettledGrowthRoot(Z, Predecessor, Row, Plan.PlotId, Expected)
				&& ValidateGrowthWorld(Z, Predecessor, Successor, Plan, true);
		}

		private static bool ValidateGrowthWorld(Zone Z, GameObject Predecessor,
			GameObject Successor, GrowthPlan Plan, bool AllowPending)
		{
			if (Plan == null || Plan.Rows == null || Plan.Rows.Count > MaxGrowthRows
				|| !ExactGrowthEndpoints(Predecessor, Successor,
					Z?.GetCell(Plan.Old.CenterX, Plan.Old.CenterY), Plan)) return false;
			for (int i = 0; i < Plan.Rows.Count; i++)
			{
				GrowthRow row = Plan.Rows[i];
				if (row == null || row.State < 0 || row.State > 2) return false;
				GameObject exact;
				KingdomPhysicalLookupState state = KingdomConstruction.FindExactId(
					Z, row.Id, out exact);
				if (row.Kind == 1)
				{
					if (row.State == 2)
					{
						if (state != KingdomPhysicalLookupState.Absent) return false;
					}
					else if (row.State == 0)
					{
						if (state != KingdomPhysicalLookupState.Exact
							|| !ExactGrowthRemoval(exact, Z, row, Plan.PlotId)) return false;
					}
					else if (!AllowPending) return false;
				}
				else if (row.Kind == 2)
				{
					if (row.State == 2)
					{
						if (state != KingdomPhysicalLookupState.Exact
							|| !ExactGrowthOutput(exact, Z, row, Plan.PlotId)) return false;
					}
					else if (row.State == 0)
					{
						if (state != KingdomPhysicalLookupState.Absent
							|| !GrowthTargetEmpty(Z, row)) return false;
					}
					else
					{
						GameObject rooted;
						if (!AllowPending || !TryGrowthRoot(Predecessor, row, out rooted)
							|| (state == KingdomPhysicalLookupState.Exact
								&& (!ReferenceEquals(exact, rooted)
									|| !ExactGrowthOutput(rooted, Z, row, Plan.PlotId)))
							|| state == KingdomPhysicalLookupState.Ambiguous) return false;
					}
				}
				else return false;
			}
			return !Plan.Done || AllGrowthRowsSettled(Plan);
		}

	}
}
