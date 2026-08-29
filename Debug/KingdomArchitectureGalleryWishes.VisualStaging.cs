using System;
using System.Collections.Generic;

using ThousandAndFirst.Simulation.City;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public partial class KingdomArchitectureGalleryWishes
	{
		private static bool TryStageVisual(Zone Zone, VisualCase Case, int Total,
			out string Receipt, out string Digest, out KingdomPlotRules.PlotRect Rect,
			out string Failure)
		{
			Receipt = null;
			Digest = null;
			Rect = default(KingdomPlotRules.PlotRect);
			Failure = null;
			if (!ValidateVisualCatalogueCase(Case, out Failure)
				|| !TryFindVisualCanvas(Zone, Case, out Rect, out Failure)) return false;
			bool priorTallyPresent = Zone.HasZoneProperty(KingdomRoads.TallyProperty);
			string priorTally = Zone.GetZoneProperty(KingdomRoads.TallyProperty, null);
			List<VisualCreated> created = new List<VisualCreated>();
			bool tallyChanged = false;
			bool committed = false;
			try
			{
				if (Case.Kind == VisualCaseKind.Objects)
				{
					if (!TryStageVisualObjects(Zone, Case, Rect, created, out Failure)) return false;
				}
				else if (Case.Kind == VisualCaseKind.Gatehouse)
				{
					if (!TryStageVisualGatehouse(Zone, Case, Rect, created, out Failure)) return false;
				}
				else if (Case.Kind == VisualCaseKind.RoadWorn)
				{
					if (!TryStageWorn(Zone, Rect, priorTally, out Failure)) return false;
					tallyChanged = true;
				}
				else if (!TryStageRoadFloors(Zone, Case, Rect, created, out Failure)) return false;
				string extra = Case.Kind == VisualCaseKind.RoadWorn
					? Zone.GetZoneProperty(KingdomRoads.TallyProperty, "") : "";
				Digest = VisualDigest(Case, Zone, Rect, created, extra);
				Receipt = VisualReceiptFor(Case, Total, Digest);
				for (int i = 0; i < created.Count; i++)
					StampVisualItem(created[i].Item, Receipt, Case.Key, created[i].Role);
				StampVisualAnchor(Case, Zone, Rect, Total, Receipt, Digest,
					priorTallyPresent, priorTally);
				List<VisualCreated> observed;
				if (!TryVisualItems(Zone, Case, Receipt, out observed, out Failure)
					|| VisualDigest(Case, Zone, Rect, observed, extra) != Digest)
				{
					Failure = Failure ?? "The staged visual objects did not retain their exact digest.";
					return false;
				}
				KingdomLog.Log("[TAF visual-gallery] receipt=" + Receipt + " case=" + Case.Key
					+ " digest=" + Digest + " zone=" + Zone.ZoneID + " rect=" + Rect.X1 + ","
					+ Rect.Y1 + "," + Rect.X2 + "," + Rect.Y2
					+ " stage=complete scope=isolated-visual-proof");
				committed = true;
				return true;
			}
			catch (Exception exception)
			{
				Failure = "Visual gallery staging threw: " + Bounded(exception.Message, MaxNoteChars);
				return false;
			}
			finally
			{
				if (!committed)
				{
					RollbackVisual(created);
					if (tallyChanged) RestoreVisualTally(Zone, priorTallyPresent, priorTally);
					ClearVisualAnchor();
				}
			}
		}

		private static bool ValidateVisualCatalogueCase(VisualCase Case, out string Failure)
		{
			Failure = null;
			if (Case == null) return Fail("The selected visual case is absent.", out Failure);
			if (Case.CatalogueKey == null) return true;
			KingdomRules.BuildEntry entry;
			if (!KingdomData.TryGetBuilding(Case.CatalogueKey, out entry))
				return Fail("The non-plot catalogue no longer contains " + Case.CatalogueKey + ".", out Failure);
			string expected = Case.Kind == VisualCaseKind.Gatehouse ? "r_KingdomGatehouse"
				: PrimaryVisualBlueprint(Case);
			return entry.Blueprint == expected
				|| Fail("The visual case blueprint disagrees with the live catalogue.", out Failure);
		}

		private static string PrimaryVisualBlueprint(VisualCase Case)
		{
			if (Case.Key == "liquidcrossing") return "r_KingdomLiquidCrossing";
			if (Case.Key == "watertap") return "r_KingdomWaterTap";
			if (Case.Key == "brinetap") return "r_KingdomBrineTap";
			return Case.Placements.Count == 0 ? null : Case.Placements[0].Blueprint;
		}

		private static bool TryStageVisualObjects(Zone Zone, VisualCase Case,
			KingdomPlotRules.PlotRect Rect, List<VisualCreated> Created, out string Failure)
		{
			Failure = null;
			for (int i = 0; i < Case.Placements.Count; i++)
			{
				VisualPlacement placement = Case.Placements[i];
				GameObject item = GameObject.Create(placement.Blueprint);
				if (!GameObject.Validate(item) || item.Blueprint != placement.Blueprint)
					return Fail("The visual blueprint " + placement.Blueprint + " could not be created exactly.",
						out Failure);
				Created.Add(new VisualCreated { Item = item, Role = placement.Role });
				if (!TryApplyVisualDeclaration(item, placement, out Failure)) return false;
				Cell cell = Zone.GetCell(Rect.X1 + placement.X, Rect.Y1 + placement.Y);
				GameObject accepted = cell.AddObject(item, NoStack: true, Silent: true);
				if (!ReferenceEquals(accepted, item)
					|| !ReferenceEquals(item.CurrentCell, cell) || item.InInventory != null)
					return Fail("The engine refused, replaced, or displaced visual role "
						+ placement.Role + ".", out Failure);
				item.MakeActive();
			}
			return true;
		}

		private static bool TryApplyVisualDeclaration(GameObject Item,
			VisualPlacement Placement, out string Failure)
		{
			Failure = null;
			if (Placement.Declaration == null) return true;
			r_KingdomLiquidConduit conduit = Item.GetPart<r_KingdomLiquidConduit>();
			if (conduit != null)
			{
				conduit.Joins = Placement.Declaration;
				return KingdomLiquidConfigurationRules.DeclarationReadsBack(conduit.Joins,
					conduit.JoinMask) || Fail("The gallery main declaration did not read back.", out Failure);
			}
			r_KingdomLiquidTap tap = Item.GetPart<r_KingdomLiquidTap>();
			if (tap != null)
			{
				tap.Joins = Placement.Declaration;
				return KingdomLiquidConfigurationRules.DeclarationReadsBack(tap.Joins,
					tap.JoinMask) || Fail("The gallery tap declaration did not read back.", out Failure);
			}
			r_KingdomLiquidCrossover crossing = Item.GetPart<r_KingdomLiquidCrossover>();
			if (crossing != null)
			{
				crossing.Pairs = Placement.Declaration;
				int glyph;
				bool freshVertical;
				return KingdomLiquidVisualRules.TryCrossingCue(crossing.Pairs, out glyph,
					out freshVertical) || Fail("The gallery crossing declaration did not read back.",
					out Failure);
			}
			return Fail("A gallery declaration was assigned to a non-liquid visual role.", out Failure);
		}

		private static bool TryStageWorn(Zone Zone, KingdomPlotRules.PlotRect Rect,
			string PriorTally, out string Failure)
		{
			Failure = null;
			List<KingdomRoadRules.WornCell> prior;
			string error;
			if (!KingdomRoadRules.TryDecode(PriorTally, out prior, out error) || prior.Count != 0)
				return Fail("The worn-ground case requires an isolated zone with an empty valid tally.",
					out Failure);
			List<KingdomRoadRules.WornCell> staged = new List<KingdomRoadRules.WornCell>
			{
				new KingdomRoadRules.WornCell(Rect.CenterX, Rect.CenterY, KingdomRoadRules.WornTraffic)
			};
			KingdomRoads.WriteTally(Zone, staged);
			List<KingdomRoadRules.WornCell> read = KingdomRoads.ReadTally(Zone);
			return read.Count == 1 && read[0].X == Rect.CenterX && read[0].Y == Rect.CenterY
				&& read[0].Traffic == KingdomRoadRules.WornTraffic
				|| Fail("The exact worn-ground tally did not read back.", out Failure);
		}

		private static bool TryStageRoadFloors(Zone Zone, VisualCase Case,
			KingdomPlotRules.PlotRect Rect, List<VisualCreated> Created, out string Failure)
		{
			Failure = null;
			KingdomRoadRules.WearState state = Case.Kind == VisualCaseKind.RoadTrodden
				? KingdomRoadRules.WearState.Trodden : Case.Kind == VisualCaseKind.RoadPath
					? KingdomRoadRules.WearState.Path : KingdomRoadRules.WearState.Paved;
			string paving = state == KingdomRoadRules.WearState.Paved
				? KingdomRoadRules.PavedFloorFor("Limestone") : null;
			for (int x = 0; x < Case.Width; x++)
			{
				Cell cell = Zone.GetCell(Rect.X1 + x, Rect.Y1);
				if (!KingdomRoads.Lay(cell, state, paving))
					return Fail("The production road layer refused lane cell " + x + ".", out Failure);
				GameObject floor = KingdomRoads.OurFloor(cell);
				if (!GameObject.Validate(floor) || KingdomRoads.AppliedState(cell) != state)
					return Fail("The production road layer did not read back its exact state.", out Failure);
				Created.Add(new VisualCreated { Item = floor, Role = "lane-" + x });
			}
			return true;
		}

		private static void RollbackVisual(List<VisualCreated> Created)
		{
			for (int i = Created.Count - 1; i >= 0; i--)
			{
				GameObject item = Created[i].Item;
				if (!GameObject.Validate(item)) continue;
				try { item.Obliterate(null, Silent: true); }
				catch { }
			}
		}

		private static void RestoreVisualTally(Zone Zone, bool Present, string Value)
		{
			if (Present) Zone.SetZoneProperty(KingdomRoads.TallyProperty, Value);
			else Zone.RemoveZoneProperty(KingdomRoads.TallyProperty);
		}
	}
}
