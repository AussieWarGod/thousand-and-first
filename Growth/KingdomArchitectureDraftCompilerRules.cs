using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomArchitectureRules
	{
		// --- Private compiler helpers -------------------------------------------------------

		private static bool TryPalette(ArchitecturePaletteDraft Palette,
			out Dictionary<string, ArchitecturePaletteSlot> Slots, out string Failure)
		{
			Slots = null;
			Failure = null;
			if (Palette == null || !ValidKey(Palette.Key) || Palette.Slots == null
				|| Palette.Slots.Count == 0 || Palette.Slots.Count > MaxPaletteSlots)
				return Fail("palette is absent, empty, or over the bound", out Failure);
			Dictionary<string, ArchitecturePaletteSlot> slots =
				new Dictionary<string, ArchitecturePaletteSlot>(StringComparer.Ordinal);
			for (int i = 0; i < Palette.Slots.Count; i++)
			{
				ArchitecturePaletteSlot slot = Palette.Slots[i];
				KingdomMaterial material;
				int tech;
				if (slot == null || !ValidKey(slot.Key) || !ValidBlueprint(slot.Blueprint)
					|| slot.Blueprint[0] == '$' || !ValidOptionalKey(slot.Role)
					|| !KingdomMaterialRules.TryParseMaterial(slot.Material, out material)
					|| !TryParseTech(slot.MinTech, out tech) || !ValidOptionalKey(slot.Knowledge)
					|| !ValidOptionalKey(slot.Power)
					|| slots.ContainsKey(slot.Key))
					return Fail("palette has a malformed or duplicate slot", out Failure);
				slots.Add(slot.Key, slot);
			}
			Slots = slots;
			return true;
		}

		private static bool TryValidateGlyph(ArchitectureGlyphDraft Glyph,
			Dictionary<string, ArchitecturePaletteSlot> Slots,
			ArchitecturePoseRegistry Poses, out string Failure)
		{
			Failure = null;
			HashSet<string> anchors = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < Glyph.Anchors.Count; i++)
			{
				string anchor = Glyph.Anchors[i];
				if (!ValidKey(anchor) || !anchors.Add(anchor))
					return Fail("glyph has a malformed or duplicate anchor", out Failure);
			}
			if (!ValidGlyphToken(Glyph.Ground, ArchitectureLayer.Ground, Slots)
				|| !ValidGlyphToken(Glyph.Structure, ArchitectureLayer.Structure, Slots)
				|| !ValidGlyphToken(Glyph.Object, ArchitectureLayer.Object, Slots))
				return Fail("glyph has a malformed or unresolved placement token", out Failure);
			if (!TryValidateGlyphPose(Glyph.Ground, Glyph.HasGroundOrientation,
				Glyph.GroundOrientation, Slots, Poses, out Failure)
				|| !TryValidateGlyphPose(Glyph.Structure, Glyph.HasStructureOrientation,
					Glyph.StructureOrientation, Slots, Poses, out Failure)
				|| !TryValidateGlyphPose(Glyph.Object, Glyph.HasObjectOrientation,
					Glyph.ObjectOrientation, Slots, Poses, out Failure)) return false;
			if (Glyph.Object == "$building" && !Glyph.StatefulObject)
				return Fail("$building must be declared stateful", out Failure);
			// A semantic anchor says what a cell is for; Stateful says the object carries custody
			// that an upgrade may not discard. Seats, lamps and other replaceable fittings may be
			// functional without becoming permanent merely because a plan names their role.
			if (Glyph.StatefulObject && string.IsNullOrEmpty(Glyph.Object))
				return Fail("stateful fixture has no object", out Failure);
			if (Glyph.StatefulObject && Glyph.Object != "$building")
				return TrySelectStatefulAnchor(Glyph.Anchors, out _, out Failure);
			return true;
		}

		/// <summary>A benefit anchor is provider custody, not another topology role. When present it
		/// is the sole protected identity and may coexist with functional anchors; an ordinary
		/// stateful fixture instead needs one exact non-main, non-entrance functional identity.</summary>
		private static bool TrySelectStatefulAnchor(IList<string> Anchors,
			out string Anchor, out string Failure)
		{
			Anchor = null;
			Failure = null;
			string benefit = null;
			string functional = null;
			bool severalFunctional = false;
			for (int i = 0; i < Anchors.Count; i++)
			{
				string key = Anchors[i];
				if (key == "main" || key.StartsWith("entrance:",
					StringComparison.Ordinal)) continue;
				if (key.StartsWith("benefit:", StringComparison.Ordinal))
				{
					if (benefit != null)
						return Fail("stateful benefit fixture must own exactly one benefit custody anchor", out Failure);
					benefit = key;
				}
				else if (functional == null) functional = key;
				else severalFunctional = true;
			}
			if (benefit != null)
			{
				Anchor = benefit;
				return true;
			}
			if (functional == null || severalFunctional)
				return Fail("stateful fixture without benefit custody must own exactly one stable functional anchor", out Failure);
			Anchor = functional;
			return true;
		}

		private static bool ValidGlyphToken(string Token, ArchitectureLayer Layer,
			Dictionary<string, ArchitecturePaletteSlot> Slots)
		{
			if (string.IsNullOrEmpty(Token)) return true;
			if (Token == "$building") return Layer == ArchitectureLayer.Object;
			// Scenery must resolve through a palette slot so its durable receipt can freeze
			// material, minimum-tech, and natural truth.
			if (Token[0] != '$') return false;
			string slot = Token.Substring(1);
			return ValidKey(slot) && Slots.ContainsKey(slot);
		}

		private static bool HasSceneryToken(string Token)
		{
			return !string.IsNullOrEmpty(Token) && Token != "$building";
		}

		private static bool TryAddPlacement(ArchitectureLayoutSnapshot Snapshot,
			ArchitectureLayer Layer, int X, int Y, string Token, bool HasOrientation,
			ArchitectureFacing Orientation, bool Stateful,
			IList<ArchitectureAnchor> CellAnchors,
			Dictionary<string, ArchitecturePaletteSlot> Slots,
			ArchitecturePoseRegistry Poses,
			ref bool HasBuilding, out string Failure)
		{
			Failure = null;
			if (string.IsNullOrEmpty(Token))
			{
				if (Stateful) return Fail("stateful object glyph has no object", out Failure);
				return true;
			}
			if (Token == "$building")
			{
				if (Layer != ArchitectureLayer.Object || HasBuilding)
					return Fail("$building is only valid once on the object layer", out Failure);
				// Root behavior is owned by commission/upgrade runtime. Recording it as removable
				// scenery would let an authored delta destroy its ID, inventory, parts, and save state.
				HasBuilding = true;
				return true;
			}
			if (Token[0] != '$')
				return Fail("map scenery must reference a palette slot", out Failure);
			string key = Token.Substring(1);
			ArchitecturePaletteSlot slot;
			if (!ValidKey(key) || !Slots.TryGetValue(key, out slot))
				return Fail("map references an unknown palette slot", out Failure);
			if (!TryResolvePose(Poses, slot.Blueprint, HasOrientation, Orientation,
				Snapshot.Facing, out string blueprint, out Failure)) return false;
			KingdomMaterial material;
			int tech;
			if (!KingdomMaterialRules.TryParseMaterial(slot.Material, out material)
				|| !TryParseTech(slot.MinTech, out tech))
				return Fail("map placement palette truth is malformed", out Failure);
			string statefulAnchor = null;
			if (Stateful)
			{
				List<string> anchorKeys = new List<string>(CellAnchors.Count);
				for (int i = 0; i < CellAnchors.Count; i++)
					anchorKeys.Add(CellAnchors[i].Key);
				if (!TrySelectStatefulAnchor(anchorKeys, out statefulAnchor,
					out Failure)) return false;
			}
			Snapshot.Placements.Add(new ArchitecturePlacement
			{
				Layer = Layer,
				X = X,
				Y = Y,
				Blueprint = blueprint,
				Slot = SlotFor(Layer, X, Y),
				Material = KingdomMaterialRules.MaterialKey(material),
				MinTech = KingdomZoningRules.TechLevelNames[tech],
				Knowledge = slot.Knowledge,
				Power = slot.Power,
				Natural = slot.Natural,
				ExistingAuthority = blueprint == "r_KingdomFirstBasin",
				StatefulAnchor = statefulAnchor
			});
			if (Snapshot.Placements.Count > MaxPlacements)
				return Fail("map placements exceed the bound", out Failure);
			return true;
		}

		private static bool TryValidateSnapshotShape(ArchitectureLayoutSnapshot Snapshot,
			bool AllowLegacyPlacementTruth, out string Failure)
		{
			Failure = null;
			if (Snapshot == null || !ValidKey(Snapshot.PlanKey) || !ValidKey(Snapshot.BindingKey)
				|| !ValidKey(Snapshot.BuildKey) || !ValidKey(Snapshot.TierKey)
				|| !ValidKey(Snapshot.VariantKey) || !ValidKey(Snapshot.PaletteKey)
				|| FoldType(Snapshot.LotType) == null || !KnownLotSize(Snapshot.LotSize)
				|| !KingdomArchitectureTransitionRules.IsKnown(
					Snapshot.IncomingTransitionMode)
				|| !KnownFacing(Snapshot.Facing))
				return Fail("snapshot metadata is malformed", out Failure);
			if (!TryCanonicalDimensions(Snapshot.LotSize, out int lotWidth, out int lotHeight)
				|| Snapshot.Width != lotWidth || Snapshot.Height != lotHeight
				|| (long)Snapshot.Width * Snapshot.Height > MaxMapArea
				|| Snapshot.MainX < 0 || Snapshot.MainX >= Snapshot.Width
				|| Snapshot.MainY < 0 || Snapshot.MainY >= Snapshot.Height
				|| !ValidFootprint(Snapshot.Width, Snapshot.Height, Snapshot.FootprintX,
					Snapshot.FootprintY, Snapshot.FootprintWidth, Snapshot.FootprintHeight)
				|| !ContainsFootprintCell(Snapshot, Snapshot.MainX, Snapshot.MainY))
				return Fail("snapshot dimensions or main coordinate are invalid", out Failure);
			if (Snapshot.Cells == null || Snapshot.Cells.Count != Snapshot.Width * Snapshot.Height
				|| Snapshot.Placements == null || Snapshot.Placements.Count > MaxPlacements
				|| Snapshot.Anchors == null || Snapshot.Anchors.Count == 0
				|| Snapshot.Anchors.Count > MaxAnchors)
				return Fail("snapshot collections are absent, incomplete, or over bounds", out Failure);
			HashSet<int> cells = new HashSet<int>();
			for (int i = 0; i < Snapshot.Cells.Count; i++)
			{
				ArchitectureCellState cell = Snapshot.Cells[i];
				if (cell == null || cell.X < 0 || cell.X >= Snapshot.Width
					|| cell.Y < 0 || cell.Y >= Snapshot.Height
					|| !KnownClaim(cell.Claim) || !KnownPassability(cell.Passability)
					|| !KnownCover(cell.Cover)
					|| (cell.Claim == ArchitectureClaim.Building
						&& !ContainsFootprintCell(Snapshot, cell.X, cell.Y))
					|| !cells.Add(CellKey(cell.X, cell.Y, Snapshot.Width)))
					return Fail("snapshot has a malformed or duplicate cell", out Failure);
			}
			HashSet<string> anchorKeys = new HashSet<string>(StringComparer.Ordinal);
			Dictionary<string, ArchitectureAnchor> anchors = new Dictionary<string, ArchitectureAnchor>(StringComparer.Ordinal);
			int main = 0;
			for (int i = 0; i < Snapshot.Anchors.Count; i++)
			{
				ArchitectureAnchor anchor = Snapshot.Anchors[i];
				if (anchor == null || !ValidKey(anchor.Key) || !anchorKeys.Add(anchor.Key)
					|| anchor.X < 0 || anchor.X >= Snapshot.Width || anchor.Y < 0 || anchor.Y >= Snapshot.Height
					|| !KnownAccess(anchor.Access))
					return Fail("snapshot has a malformed or duplicate anchor", out Failure);
				anchors[anchor.Key] = anchor;
				if (anchor.Key == "main")
				{
					main++;
					if (anchor.X != Snapshot.MainX || anchor.Y != Snapshot.MainY)
						return Fail("snapshot main metadata and anchor disagree", out Failure);
				}
			}
			if (main != 1) return Fail("snapshot must have exactly one main anchor", out Failure);
			HashSet<string> slots = new HashSet<string>(StringComparer.Ordinal);
			HashSet<string> stateful = new HashSet<string>(StringComparer.Ordinal);
			HashSet<string> blueprints = new HashSet<string>(StringComparer.Ordinal);
			Dictionary<int, ArchitectureCellState> placementCells = CellDictionary(
				Snapshot.Cells, Snapshot.Width);
			for (int i = 0; i < Snapshot.Placements.Count; i++)
			{
				ArchitecturePlacement placement = Snapshot.Placements[i];
				KingdomMaterial material;
				int tech;
				bool legacyTruth = AllowLegacyPlacementTruth
					&& string.IsNullOrEmpty(placement == null ? null : placement.Material)
					&& string.IsNullOrEmpty(placement == null ? null : placement.MinTech);
				if (placement == null || !KnownLayer(placement.Layer)
					|| placement.X < 0 || placement.X >= Snapshot.Width
					|| placement.Y < 0 || placement.Y >= Snapshot.Height
					|| !ValidBlueprint(placement.Blueprint)
					|| (!legacyTruth && (!KingdomMaterialRules.TryParseMaterial(
						placement.Material, out material) || KingdomMaterialRules.MaterialKey(material)
						!= placement.Material || !TryParseTech(placement.MinTech, out tech)
						|| KingdomZoningRules.TechLevelNames[tech] != placement.MinTech
						|| !ValidOptionalKey(placement.Knowledge)
						|| !ValidOptionalKey(placement.Power)
						|| placement.ExistingAuthority
							!= (placement.Blueprint == "r_KingdomFirstBasin")
						|| (placement.ExistingAuthority && placement.Natural)))
					|| placement.Slot != SlotFor(placement.Layer, placement.X, placement.Y)
					|| !IsClaimed(placementCells[CellKey(placement.X, placement.Y,
						Snapshot.Width)].Claim)
					|| !slots.Add(placement.Slot))
					return Fail("snapshot has a malformed or duplicate placement", out Failure);
				blueprints.Add(placement.Blueprint);
				if (!string.IsNullOrEmpty(placement.StatefulAnchor))
				{
					if (!anchors.TryGetValue(placement.StatefulAnchor, out ArchitectureAnchor anchor)
						|| anchor.X != placement.X || anchor.Y != placement.Y
						|| !stateful.Add(placement.StatefulAnchor))
						return Fail("stateful placement anchor is missing, moved, or duplicated", out Failure);
				}
			}
			if (blueprints.Count > MaxPaletteSlots)
				return Fail("snapshot blueprint table exceeds the bound", out Failure);
			return true;
		}

	}
}
