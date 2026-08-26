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
		private static void PrepareFinalBuilding(GameObject Building,
			KingdomRules.BuildEntry Entry, string Receipt, string PlotId,
			KingdomPlotRules.PlotRect Rect, KingdomPlotRules.PlotRect Footprint,
			KingdomPlotRules.RoofState Roof, string Color, string Detail, string Render,
			string Tile, string DisplayName, long CompleteTick, string PlanQuote,
			bool Heart, bool Yielding, int Defence, int Staff, bool Threshold)
		{
			if (!string.IsNullOrEmpty(Receipt))
				Building.SetStringProperty(KingdomConstruction.ReceiptProperty, Receipt);
			KingdomDesign.ApplyRenderOverrides(Building, Color, Detail, Render, Tile);
			Building.SetIntProperty("KingdomBuilt", 1);
			Building.SetStringProperty(KingdomUpgrade.BuildKeyProperty, Entry.Key);
			Building.SetStringProperty(r_KingdomScaffold.CompletionNameProperty, DisplayName);
			Building.SetStringProperty(r_KingdomScaffold.CompletionTickProperty,
				CompleteTick.ToString(global::System.Globalization.CultureInfo.InvariantCulture));
			if (!string.IsNullOrEmpty(PlanQuote))
				Building.SetStringProperty(r_KingdomScaffold.CompletionPlanProperty, PlanQuote);
			if (Heart) Building.SetIntProperty(HeartPlotProperty, 1);
			if (Yielding)
			{
				Building.SetIntProperty(YieldingProperty, 1);
				Building.RequirePart<r_KingdomYielding>();
			}
			if (!string.IsNullOrEmpty(PlotId)) Building.SetStringProperty(PlotIdProperty, PlotId);
			StampRect(Building, Rect);
			StampFootprint(Building, Footprint, Roof);
			if (Building.GetPart<LiquidVolume>() != null) Building.SetIntProperty("KingdomStores", 1);
			else if (KingdomRules.IsCivicLarderBlueprint(Entry.Blueprint))
				Building.SetIntProperty("KingdomLarder", 1);
			if (Defence > 0) Building.SetIntProperty("KingdomDefence", Defence);
			if (Staff > 0)
			{
				Building.SetIntProperty("KingdomStaffNeeded", Staff);
				if (Threshold) Building.SetIntProperty("KingdomThresholdManning", 1);
				if (Building.GetPart<Capacitor>() != null)
					Building.SetIntProperty("KingdomHandCranked", 1);
			}
		}

		private static bool ExactFinalBuilding(GameObject Building, Zone Z, Cell Cell,
			KingdomRules.BuildEntry Entry, string Receipt, string PlotId,
			KingdomPlotRules.PlotRect Rect, KingdomPlotRules.PlotRect Footprint,
			KingdomPlotRules.RoofState Roof, KingdomArchitectureIntent Architecture,
			bool LegacyArchitecture, KingdomConstructionJob Job)
		{
			if (!GameObject.Validate(Building) || Building.CurrentZone != Z
				|| Building.CurrentCell != Cell || Building.Blueprint != Entry.Blueprint
				|| Building.GetIntProperty("KingdomBuilt") != 1
				|| Building.GetStringProperty(KingdomUpgrade.BuildKeyProperty) != Entry.Key
				|| Building.GetStringProperty(PlotIdProperty) != PlotId
				|| (Job != null && (Building.ID != Job.OutputId
					|| !KingdomConstruction.HasReceipt(Building, Job)
					|| !KingdomConstruction.PaidBuildMatches(Building, Job)
					|| !KingdomConstruction.IsCurrent(Job)))
				|| (!string.IsNullOrEmpty(Receipt)
					&& Building.GetStringProperty(KingdomConstruction.ReceiptProperty) != Receipt)
				|| !TryReadRect(Building, out var observed)
				|| observed.X1 != Rect.X1 || observed.Y1 != Rect.Y1
				|| observed.X2 != Rect.X2 || observed.Y2 != Rect.Y2
				|| !TryReadFootprint(Building, out var foot)
				|| foot.X1 != Footprint.X1 || foot.Y1 != Footprint.Y1
				|| foot.X2 != Footprint.X2 || foot.Y2 != Footprint.Y2
				|| RoofOf(Building) != Roof
				|| !ExpectedArchitectureReceipt(Building, Cell, Entry.Key,
					Architecture, LegacyArchitecture)) return false;
			if (Architecture != null
				&& KingdomArchitectureRules.IsCurrentSnapshotEncoding(
					Architecture.EncodedSnapshot)
				&& !KingdomArchitectureStamper.TryVerifyComplete(Building, Z, out _)) return false;
			GameObject exact;
			if (KingdomConstruction.FindExactId(Z, Building.ID, out exact)
				!= KingdomPhysicalLookupState.Exact || !ReferenceEquals(exact, Building)) return false;
			if (Job == null) return true;
			KingdomSystem system = The.Game == null
				? null : The.Game.RequireSystem<KingdomSystem>();
			return KingdomConstruction.Owns(system, Z, Job);
		}

		/// <summary>
		/// Takes down what stood on the plot, and puts what it was worth into the realm's stock.
		/// Only ever touches cells the survey classified as clearable ground: everything else
		/// refused the plot before it was ever staked, so nothing here has to decide whether a
		/// thing may be destroyed &mdash; that decision was made, once, when the founder chose
		/// this ground.
		/// </summary>
		private static bool ClearGround(r_KingdomPlotWorks Works, Zone Z,
			KingdomPlotRules.PlotRect Plot, KingdomPlotRules.PlotRect Footprint,
			KingdomPlotRules.RoofState Roof, HashSet<int> AuthoredCells = null)
		{
			if (ClearInt(Works, ClearQuarantinedProperty) == 1) return false;
			if (ClearInt(Works, ClearPhaseProperty) != 0)
			{
				if (!ResumeClearPayout(Works, Z)) return false;
			}
			// Carving cuts the room, not the hill. Everything on the building's own edge is left
			// exactly where it stands, because underground that rock IS the enclosure -- which is
			// the whole of the bargain that makes the doubled clearing cost worth paying. Only the
			// doorway is cut through it. The yard around it is cleared like any other ground, and
			// pays in stone for being cut out of rock.
			bool carveOnly = AuthoredCells == null && Roof == KingdomPlotRules.RoofState.Carved
				&& Footprint.Width > 2 && Footprint.Height > 2;
			for (int y = Plot.Y1; y <= Plot.Y2; y++)
			{
				for (int x = Plot.X1; x <= Plot.X2; x++)
				{
					if (AuthoredCells != null && !AuthoredCells.Contains(y * Z.Width + x))
						continue;
					if (carveOnly && Footprint.IsBorder(x, y) && !(Works.HasDoor && x == Works.DoorX && y == Works.DoorY))
					{
						continue;
					}
					Cell cell = Z.GetCell(x, y);
					if (cell == null)
					{
						continue;
					}
					List<GameObject> standing = new List<GameObject>(cell.GetObjects());
					for (int i = 0; i < standing.Count; i++)
					{
						GameObject item = standing[i];
						if (item == null || item == Works.ParentObject || item.IsCreature || item.IsPlayer())
						{
							continue;
						}
						KingdomPlotRules.GroundKind kind = ReadObject(item);
						if (kind == KingdomPlotRules.GroundKind.Bare || KingdomPlotRules.Refuses(kind))
						{
							// Bare is a floor and stays; a refusing cell cannot exist inside a
							// rect that was surveyed, but if the world changed under us while the
							// founder was away, the honest answer is to leave it standing.
							continue;
						}
							KingdomPlotRules.Material material = KingdomPlotRules.YieldOf(kind,
								out var amount);
							if (material == KingdomPlotRules.Material.None || amount <= 0) continue;
							ClearString(Works, ClearIdProperty, item.ID);
							ClearString(Works, ClearBlueprintProperty, item.Blueprint);
							ClearInt(Works, ClearXProperty, x);
							ClearInt(Works, ClearYProperty, y);
							ClearInt(Works, ClearMaterialProperty, (int)material);
							ClearInt(Works, ClearAmountProperty, amount);
							ClearInt(Works, ClearPhaseProperty, 1);
							if (!ExactClearSource(Works, Z, item, cell, material, amount))
								return QuarantineClear(Works,
									"Clearance source changed before its removal callback.");
							bool removed;
							try { removed = item.Destroy(null, Silent: true); }
							catch (System.Exception ex)
							{
								KingdomSurvey.ObserveCurrentTopologyInActive(Z, item);
								return QuarantineClear(Works,
									"Clearance removal threw: " + ex.Message);
							}
							if (!removed || GameObject.Validate(item) || GameObject.Validate(
								GameObject.FindByID(ClearString(Works, ClearIdProperty))))
								return QuarantineClear(Works,
									"Clearance removal was vetoed, moved, or replaced its exact source.");
							KingdomSurvey.ObserveRemovedFromActive(Z, item);
							ClearInt(Works, ClearRemovedProperty, 1);
							ClearInt(Works, ClearPhaseProperty, 2);
							if (!ResumeClearPayout(Works, Z)) return false;
						}
				}
			}
			// Carving pays in stone because the rock IS the ground here, and it costs twice what
			// clearing the open costs (KingdomPlotRules.UndergroundClearPercent, already spent in
			// the raising time). Nothing is added on top of what came out: the compensation for
			// the doubled effort is that the rock the carving left is the enclosure, and no wall
			// is ever raised down here.
			TellClearMaterials(new int[5]
			{
					0, ClearInt(Works, ClearTimberProperty), ClearInt(Works, ClearStoneProperty),
					ClearInt(Works, ClearMarbleProperty), ClearInt(Works, ClearScrapProperty)
			}, Works.DisplayName);
			return true;
		}

		private static bool ExactClearSource(r_KingdomPlotWorks Works, Zone Z,
			GameObject Item, Cell Cell, KingdomPlotRules.Material Material, int Amount)
		{
			if (Works == null || Z == null || !GameObject.Validate(Works.ParentObject)
				|| Works.ParentObject.CurrentZone != Z || Works.ParentObject.GetPart<r_KingdomPlotWorks>() != Works
				|| !GameObject.Validate(Item) || Item.ID != ClearString(Works, ClearIdProperty)
				|| Item.Blueprint != ClearString(Works, ClearBlueprintProperty) || Item.CurrentCell != Cell
				|| Cell.X != ClearInt(Works, ClearXProperty) || Cell.Y != ClearInt(Works, ClearYProperty)
				|| ClearInt(Works, ClearMaterialProperty) != (int)Material
				|| ClearInt(Works, ClearAmountProperty) != Amount)
				return false;
			KingdomPlotRules.GroundKind kind = ReadObject(Item);
			return KingdomPlotRules.YieldOf(kind, out var measured) == Material
				&& measured == Amount;
		}

	}
}
