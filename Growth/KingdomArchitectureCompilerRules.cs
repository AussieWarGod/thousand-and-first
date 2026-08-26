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
		// --- Compilation --------------------------------------------------------------------

		public static bool TryCompile(ArchitectureCompileRequest Request,
			out ArchitectureLayoutSnapshot Snapshot, out string Failure)
		{
			Snapshot = null;
			Failure = null;
			if (Request == null || !ValidKey(Request.PlanKey) || Request.Binding == null
				|| Request.Tier == null || Request.Variant == null || Request.Map == null
				|| Request.Palette == null || !ValidBlueprint(Request.BuildingBlueprint)
				|| !KnownFacing(Request.Facing))
				return Fail("compile request is incomplete or malformed", out Failure);
			ArchitectureBindingDraft binding = Request.Binding;
			ArchitectureTierDraft tier = Request.Tier;
			ArchitectureVariantDraft variant = Request.Variant;
			ArchitectureMapDraft map = Request.Map;
			ArchitecturePaletteDraft palette = Request.Palette;
			string type = FoldType(binding.TypeKey);
			if (!ValidKey(binding.Key) || type == null || !KnownLotSize(binding.Size)
				|| !KnownFrontage(binding.Frontage) || !ValidKey(tier.Key) || !ValidKey(tier.BuildKey)
				|| !ValidKey(tier.MapKey) || !ValidKey(tier.PaletteKey)
				|| !ValidKey(variant.Key) || !ValidSelector(variant.Selector, out Failure)
				|| !ValidKey(map.Key) || !ValidKey(palette.Key))
				return Fail("selected plan metadata is malformed", out Failure);
			string expectedMap = string.IsNullOrEmpty(variant.MapKey) ? tier.MapKey : variant.MapKey;
			string expectedPalette = string.IsNullOrEmpty(variant.PaletteKey)
				? tier.PaletteKey : variant.PaletteKey;
			if (expectedMap != map.Key || expectedPalette != palette.Key)
				return Fail("selected map or palette does not match the tier and variant", out Failure);
			if (!TryCanonicalDimensions(binding.Size, out int lotWidth, out int lotHeight)
				|| map.Width != lotWidth || map.Height != lotHeight
				|| (long)map.Width * map.Height > MaxMapArea
				|| map.Rows == null || map.Rows.Count != map.Height
				|| map.Glyphs == null || map.Glyphs.Count > MaxGlyphs
				|| !KnownCover(map.DefaultCover))
				return Fail("map dimensions, rows, glyph count, or default cover are invalid", out Failure);
			if (!TryPalette(palette, out Dictionary<string, ArchitecturePaletteSlot> slots,
				out Failure)) return false;
			Dictionary<char, ArchitectureGlyphDraft> glyphs = new Dictionary<char, ArchitectureGlyphDraft>();
			for (int i = 0; i < map.Glyphs.Count; i++)
			{
				ArchitectureGlyphDraft glyph = map.Glyphs[i];
				if (glyph == null || glyph.Character < '!' || glyph.Character > '~'
					|| glyph.Character == '.' || glyphs.ContainsKey(glyph.Character)
					|| !KnownPassability(glyph.Passability)
					|| (glyph.HasCover && !KnownCover(glyph.Cover))
					|| glyph.Anchors == null || glyph.Anchors.Count > MaxAnchors)
					return Fail("map has a malformed, reserved, or duplicate glyph", out Failure);
				if (!TryValidateGlyph(glyph, slots, out Failure)) return false;
				glyphs.Add(glyph.Character, glyph);
			}

			ArchitectureLayoutSnapshot snapshot = new ArchitectureLayoutSnapshot
			{
				PlanKey = Request.PlanKey,
				BindingKey = binding.Key,
				BuildKey = tier.BuildKey,
				TierKey = tier.Key,
				VariantKey = variant.Key,
				PaletteKey = palette.Key,
				LotType = type,
				LotSize = binding.Size,
				Facing = Request.Facing,
				Width = map.Width,
				Height = map.Height,
				MainX = -1,
				MainY = -1
			};
			HashSet<string> anchorKeys = new HashSet<string>(StringComparer.Ordinal);
			int buildingCount = 0;
			for (int y = 0; y < map.Height; y++)
			{
				string row = map.Rows[y];
				if (row == null || row.Length != map.Width)
					return Fail("map row width does not match its declaration", out Failure);
				for (int x = 0; x < map.Width; x++)
				{
					char symbol = row[x];
					ArchitectureGlyphDraft glyph = null;
					if (symbol != '.' && !glyphs.TryGetValue(symbol, out glyph))
						return Fail("map row uses an undefined glyph", out Failure);
					ArchitectureCellState cell = new ArchitectureCellState
					{
						X = x,
						Y = y,
						Claim = glyph != null && glyph.Claim,
						Passability = glyph == null ? ArchitecturePassability.Walkable : glyph.Passability,
						Cover = glyph == null ? ArchitectureCover.Open
							: (glyph.HasCover ? glyph.Cover : map.DefaultCover)
					};
					snapshot.Cells.Add(cell);
					if (glyph == null) continue;
					if (!cell.Claim && (HasSceneryToken(glyph.Ground)
						|| HasSceneryToken(glyph.Structure) || HasSceneryToken(glyph.Object)))
						return Fail("map places scenery on an unclaimed cell", out Failure);

					List<ArchitectureAnchor> cellAnchors = new List<ArchitectureAnchor>();
					for (int a = 0; a < glyph.Anchors.Count; a++)
					{
						string role = glyph.Anchors[a];
						string key = role == "main" ? role : StableAnchorKey(role, x, y);
						if (!ValidKey(key) || !anchorKeys.Add(key))
							return Fail("map has a malformed or duplicate anchor", out Failure);
						ArchitectureAnchor anchor = new ArchitectureAnchor
						{
							Key = key,
							X = x,
							Y = y,
							Access = glyph.Passability == ArchitecturePassability.Walkable
								? ArchitectureAnchorAccess.OnCell : ArchitectureAnchorAccess.Adjacent
						};
						cellAnchors.Add(anchor);
						snapshot.Anchors.Add(anchor);
					}

					bool hasBuilding = false;
					if (!TryAddPlacement(snapshot, ArchitectureLayer.Ground, x, y, glyph.Ground,
						false, cellAnchors, slots, ref hasBuilding, out Failure)
						|| !TryAddPlacement(snapshot, ArchitectureLayer.Structure, x, y, glyph.Structure,
						false, cellAnchors, slots, ref hasBuilding, out Failure)
						|| !TryAddPlacement(snapshot, ArchitectureLayer.Object, x, y, glyph.Object,
						glyph.StatefulObject, cellAnchors, slots, ref hasBuilding, out Failure))
						return false;
					if (hasBuilding)
					{
						buildingCount++;
						if (!ContainsAnchor(cellAnchors, "main"))
							return Fail("$building must share its cell with the main anchor", out Failure);
						snapshot.MainX = x;
						snapshot.MainY = y;
					}
					else if (ContainsAnchor(cellAnchors, "main"))
						return Fail("main anchor must share its cell with $building", out Failure);
				}
			}
			if (buildingCount != 1) return Fail("map must place exactly one $building", out Failure);
			if (snapshot.Placements.Count > MaxPlacements || snapshot.Anchors.Count > MaxAnchors)
				return Fail("compiled placements or anchors exceed the bound", out Failure);
			SortSnapshot(snapshot);
			if (!TryValidateTopology(snapshot, tier.Requirements, out Failure)) return false;
			if (!TryEncodeSnapshot(snapshot, out _, out Failure)) return false;
			Snapshot = snapshot;
			return true;
		}

		// --- Topology -----------------------------------------------------------------------

		public static bool TryValidateTopology(ArchitectureLayoutSnapshot Snapshot,
			IList<ArchitectureAnchorRequirement> Requirements, out string Failure)
		{
			return TryValidateTopologyCore(Snapshot, Requirements, false, out Failure);
		}

		private static bool TryValidateTopologyCore(ArchitectureLayoutSnapshot Snapshot,
			IList<ArchitectureAnchorRequirement> Requirements, bool AllowLegacyPlacementTruth,
			out string Failure)
		{
			if (!TryValidateSnapshotShape(Snapshot, AllowLegacyPlacementTruth, out Failure)) return false;
			Dictionary<int, ArchitectureCellState> cells = CellDictionary(Snapshot.Cells, Snapshot.Width);
			List<ArchitectureAnchor> entrances = new List<ArchitectureAnchor>();
			for (int i = 0; i < Snapshot.Anchors.Count; i++)
			{
				ArchitectureAnchor anchor = Snapshot.Anchors[i];
				if (AnchorRole(anchor.Key) == "entrance:public") entrances.Add(anchor);
			}
			if (entrances.Count == 0) return Fail("map has no entrance:public anchor", out Failure);
			Queue<ArchitecturePoint> frontier = new Queue<ArchitecturePoint>();
			HashSet<int> reached = new HashSet<int>();
			for (int i = 0; i < entrances.Count; i++)
			{
				ArchitectureAnchor entrance = entrances[i];
				ArchitectureCellState cell = cells[CellKey(entrance.X, entrance.Y, Snapshot.Width)];
				if (!cell.Claim || cell.Passability != ArchitecturePassability.Walkable
					|| !ClaimBoundary(cells, Snapshot.Width, Snapshot.Height, entrance.X, entrance.Y))
					return Fail("public entrance is not a walkable claimed boundary cell", out Failure);
				int key = CellKey(entrance.X, entrance.Y, Snapshot.Width);
				if (reached.Add(key)) frontier.Enqueue(new ArchitecturePoint(entrance.X, entrance.Y));
			}
			int[] dx = new int[4] { 0, 1, 0, -1 };
			int[] dy = new int[4] { -1, 0, 1, 0 };
			while (frontier.Count > 0)
			{
				ArchitecturePoint current = frontier.Dequeue();
				for (int d = 0; d < 4; d++)
				{
					int x = current.X + dx[d];
					int y = current.Y + dy[d];
					if (x < 0 || x >= Snapshot.Width || y < 0 || y >= Snapshot.Height) continue;
					int key = CellKey(x, y, Snapshot.Width);
					ArchitectureCellState cell = cells[key];
					if (cell.Claim && cell.Passability == ArchitecturePassability.Walkable
						&& reached.Add(key)) frontier.Enqueue(new ArchitecturePoint(x, y));
				}
			}
			for (int i = 0; i < Snapshot.Anchors.Count; i++)
			{
				ArchitectureAnchor anchor = Snapshot.Anchors[i];
				bool accessible = anchor.Access == ArchitectureAnchorAccess.OnCell
					? reached.Contains(CellKey(anchor.X, anchor.Y, Snapshot.Width))
					: AdjacentReached(anchor.X, anchor.Y, Snapshot.Width, Snapshot.Height, reached);
				if (!accessible) return Fail("anchor " + anchor.Key + " is unreachable", out Failure);
			}
			if (Requirements != null)
			{
				if (Requirements.Count > MaxRequirementsPerTier)
					return Fail("anchor requirements exceed the bound", out Failure);
				for (int r = 0; r < Requirements.Count; r++)
				{
					ArchitectureAnchorRequirement requirement = Requirements[r];
					if (requirement == null || !ValidKey(requirement.Role)
						|| requirement.Minimum < 0 || requirement.Maximum < 0
						|| (requirement.Maximum > 0 && requirement.Maximum < requirement.Minimum))
						return Fail("anchor requirement is malformed", out Failure);
					int count = 0;
					for (int i = 0; i < Snapshot.Anchors.Count; i++)
						if (AnchorMatchesRole(Snapshot.Anchors[i].Key, requirement.Role)) count++;
					if (count < requirement.Minimum
						|| (requirement.Maximum > 0 && count > requirement.Maximum))
						return Fail("anchor role " + requirement.Role + " has the wrong count", out Failure);
				}
			}
			return true;
		}
	}
}
