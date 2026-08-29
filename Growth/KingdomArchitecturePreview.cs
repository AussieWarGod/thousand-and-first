using System;
using System.Collections.Generic;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>
	/// Color-independent production-data preview for one already-frozen authored building. The
	/// renderer consumes the same canonical snapshot the stamper consumes; it never invents a shell,
	/// resolves a second variant, or reads the live map to guess what might be placed.
	/// </summary>
	public static class KingdomArchitecturePreview
	{
		public const int MaxPreviewChars = 4096;

		/// <summary>Renders the exact posed lot and the price/authority carried by its design.</summary>
		public static bool TryRender(KingdomArchitectureIntent Intent,
			KingdomRules.BuildEntry Entry, long LabourTicks, out string Preview,
			out string Failure)
		{
			return TryRenderExact(Intent, Entry, LabourTicks,
				Entry == null ? 0 : Entry.CostDrams,
				Entry == null ? null : KingdomMaterials.CostFor(Entry.Key)?.Describe(),
				Entry == null ? null : KingdomMaterials.BitCostFor(Entry.Key)?.Describe(),
				Entry == null ? null : KingdomMaterials.ExoticCostFor(Entry.Key)?.Describe(),
				null, null, out Preview, out Failure);
		}

		/// <summary>Renders a same-set target with only its directional declaration's price.</summary>
		public static bool TryRenderTransition(KingdomArchitectureIntent Intent,
			KingdomRules.BuildEntry Entry, KingdomSocketTransition Transition,
			ArchitectureLayoutDelta Delta, out string Preview, out string Failure)
		{
			if (Transition == null)
			{
				Preview = null;
				Failure = "The building preview has no declared transition receipt.";
				return false;
			}
			return TryRenderExact(Intent, Entry, Transition.WorkTicks, Transition.WaterDrams,
				Transition.Materials?.Describe(), null, null,
				"Declared same-set change: no strike or salvage; exact lot, pose, and retained fabric stay.",
				Delta, out Preview, out Failure);
		}

		/// <summary>Renders a freshly sited retype target and its full new-build debit.</summary>
		public static bool TryRenderRetype(KingdomArchitectureIntent Intent,
			KingdomRules.BuildEntry Entry, KingdomSocketRules.ConversionQuote Quote,
			out string Preview, out string Failure)
		{
			string detail = "Fresh retype: old work takes " + Quote.EffortDays
				+ (Quote.EffortDays == 1 ? " hand-day" : " hand-days")
				+ " to strike; successor uses a new LotId at this frozen site.";
			return TryRenderExact(Intent, Entry, Entry == null ? 0L : Entry.BuildTicks,
				Quote.NewDrams, Quote.NewMaterials?.Describe(),
				Entry == null ? null : KingdomMaterials.BitCostFor(Entry.Key)?.Describe(),
				Entry == null ? null : KingdomMaterials.ExoticCostFor(Entry.Key)?.Describe(),
				detail, null, out Preview, out Failure);
		}

		private static bool TryRenderExact(KingdomArchitectureIntent Intent,
			KingdomRules.BuildEntry Entry, long LabourTicks, int CostDrams,
			string MaterialCost, string BitCost, string ExoticCost, string ReceiptLine,
			ArchitectureLayoutDelta Delta, out string Preview, out string Failure)
		{
			Preview = null;
			Failure = null;
			ArchitectureLayoutSnapshot snapshot;
			if (Entry == null || LabourTicks < 1L
				|| !KingdomArchitectureRuntime.TryDecode(Intent, out snapshot, out Failure)
				|| !KingdomArchitectureRules.IsCurrentSnapshotEncoding(Intent.EncodedSnapshot)
				|| snapshot.BuildKey != Entry.Key)
			{
				if (Failure == null) Failure = "The building preview has no exact frozen production map.";
				return false;
			}
			int width;
			int height;
			if (!KingdomArchitectureRules.TryWorldDimensions(snapshot.Width, snapshot.Height,
				snapshot.Facing, out width, out height))
			{
				Failure = "The building preview has an impossible pose.";
				return false;
			}
			char[,] map = new char[width, height];
			for (int y = 0; y < height; y++)
				for (int x = 0; x < width; x++) map[x, y] = ' ';

			for (int i = 0; i < snapshot.Cells.Count; i++)
			{
				ArchitectureCellState cell = snapshot.Cells[i];
				if (!cell.Claim) continue;
				if (!TryRelative(snapshot, cell.X, cell.Y, out int x, out int y))
				{
					Failure = "The building preview contains a cell outside its posed lot.";
					return false;
				}
				map[x, y] = cell.Passability == ArchitecturePassability.Blocked ? '#'
					: (cell.Passability == ArchitecturePassability.Adjacent ? ':' : '.');
			}

			for (int i = 0; i < snapshot.Placements.Count; i++)
			{
				ArchitecturePlacement placement = snapshot.Placements[i];
				if (!TryRelative(snapshot, placement.X, placement.Y, out int x, out int y))
				{
					Failure = "The building preview contains a fixture outside its posed lot.";
					return false;
				}
				char glyph = placement.Layer == ArchitectureLayer.Structure ? '#'
					: (placement.Layer == ArchitectureLayer.Object ? 'o' : '.');
				string blueprint = placement.Blueprint ?? "";
				if (blueprint.IndexOf("Door", StringComparison.OrdinalIgnoreCase) >= 0) glyph = '+';
				else if (blueprint.IndexOf("StairsDown", StringComparison.OrdinalIgnoreCase) >= 0) glyph = '>';
				else if (blueprint.IndexOf("StairsUp", StringComparison.OrdinalIgnoreCase) >= 0) glyph = '<';
				map[x, y] = glyph;
			}

			List<string> functions = new List<string>();
			for (int i = 0; i < snapshot.Anchors.Count; i++)
			{
				ArchitectureAnchor anchor = snapshot.Anchors[i];
				if (!TryRelative(snapshot, anchor.X, anchor.Y, out int x, out int y))
				{
					Failure = "The building preview contains an anchor outside its posed lot.";
					return false;
				}
				string key = anchor.Key ?? "";
				if (key == "main") continue;
				if (key.StartsWith("entrance:public", StringComparison.Ordinal)) map[x, y] = '+';
				else if (key.IndexOf("stairs-down", StringComparison.Ordinal) >= 0
					|| key.IndexOf("travel:down", StringComparison.Ordinal) >= 0) map[x, y] = '>';
				else if (key.IndexOf("stairs-up", StringComparison.Ordinal) >= 0
					|| key.IndexOf("travel:up", StringComparison.Ordinal) >= 0) map[x, y] = '<';
				else
				{
					if (map[x, y] == '.' || map[x, y] == ':' || map[x, y] == ' ') map[x, y] = '!';
					if (!functions.Contains(key)) functions.Add(key);
				}
			}
			if (!TryRelative(snapshot, snapshot.MainX, snapshot.MainY, out int mainX, out int mainY))
			{
				Failure = "The building preview's behavior root is outside its posed lot.";
				return false;
			}
			map[mainX, mainY] = '@';

			StringBuilder text = new StringBuilder();
			text.Append(Entry.Name).Append("\n")
				.Append(snapshot.LotType).Append(' ').Append(SizeName(snapshot.LotSize))
				.Append(" lot, ").Append(width).Append('x').Append(height)
				.Append(", faces ").Append(snapshot.Facing.ToString().ToLowerInvariant())
				.Append(" (north is up)\n");
			text.Append('+').Append(new string('-', width)).Append("+\n");
			for (int y = 0; y < height; y++)
			{
				text.Append('|');
				for (int x = 0; x < width; x++) text.Append(map[x, y]);
				text.Append("|\n");
			}
			text.Append('+').Append(new string('-', width)).Append("+\n")
				.Append("Legend: @ building; + public door; # blocked; o fixture; ! use point; ")
				.Append(". walkable; : adjacent-use; blank yard.\n")
				.Append("Plan: ").Append(snapshot.PlanKey).Append(" / ").Append(snapshot.TierKey)
				.Append("; variant ").Append(snapshot.VariantKey)
				.Append("; palette ").Append(snapshot.PaletteKey).Append(".\n")
				.Append("Cost: ").Append(CostDrams).Append(" drams");
			AppendCost(text, MaterialCost);
			AppendCost(text, BitCost);
			AppendCost(text, ExoticCost);
			text.Append(". Labour: ").Append(LabourTicks).Append(" crew-ticks.\n");
			if (!string.IsNullOrEmpty(ReceiptLine)) text.Append(ReceiptLine).Append('\n');
			if (Delta != null)
				text.Append("Exact map delta: retain ").Append(Delta.Retained.Count)
					.Append(", remove ").Append(Delta.Removed.Count)
					.Append(", add ").Append(Delta.Added.Count)
					.Append(", change ").Append(Delta.Cells.Count).Append(" claimed cells.\n");

			List<KingdomPlotRules.ChainStep> chain = KingdomPlots.ChainOf(Entry);
			if (chain.Count > 0)
			{
				text.Append("Tier path: ");
				for (int i = 0; i < chain.Count; i++)
				{
					if (i > 0) text.Append(" -> ");
					text.Append(chain[i].Name).Append(' ')
						.Append(chain[i].Width).Append('x').Append(chain[i].Height);
				}
				text.Append(".\n");
			}
			if (functions.Count > 0)
				text.Append("Functional anchors: ").Append(string.Join(", ", functions.ToArray()))
					.Append(".\n");

			List<string> gates = PlacementGates(snapshot);
			if (gates.Count > 0)
				text.Append("Frozen production gates: ").Append(string.Join(", ", gates.ToArray()))
					.Append(".\n");
			ZoneGate gate = KingdomZoning.GateFor(Entry.Key);
			if (gate.Megastructure)
				text.Append("IRREVERSIBLE CITY PURPOSE: this is the city's one ordinary megastructure; ")
					.Append("strike it before choosing another.\n");
			if (gate.Capital)
				text.Append("CAPITAL-ONLY GREAT WORK: this commitment is available only at the crowned seat.\n");

			if (text.Length > MaxPreviewChars)
			{
				Failure = "The exact production preview exceeds its safe display bound.";
				return false;
			}
			Preview = text.ToString();
			return true;
		}

		private static bool TryRelative(ArchitectureLayoutSnapshot Snapshot, int U, int V,
			out int X, out int Y)
		{
			return KingdomArchitectureRules.TryToWorld(0, 0, Snapshot.Width, Snapshot.Height,
				Snapshot.Facing, U, V, out X, out Y);
		}

		private static void AppendCost(StringBuilder Text, string Cost)
		{
			if (!string.IsNullOrEmpty(Cost)) Text.Append("; ").Append(Cost);
		}

		private static string SizeName(ArchitectureLotSize Size)
		{
			switch (Size)
			{
			case ArchitectureLotSize.Small: return "small";
			case ArchitectureLotSize.Medium: return "middling";
			case ArchitectureLotSize.Large: return "large";
			case ArchitectureLotSize.Huge: return "great";
			default: return "unknown";
			}
		}

		private static List<string> PlacementGates(ArchitectureLayoutSnapshot Snapshot)
		{
			List<string> result = new List<string>();
			for (int i = 0; i < Snapshot.Placements.Count; i++)
			{
				ArchitecturePlacement placement = Snapshot.Placements[i];
				AddGate(result, "tech:" + placement.MinTech, placement.MinTech);
				AddGate(result, "knowledge:" + placement.Knowledge, placement.Knowledge);
				AddGate(result, "power:" + placement.Power, placement.Power);
			}
			return result;
		}

		private static void AddGate(List<string> Into, string Label, string Value)
		{
			if (!string.IsNullOrEmpty(Value) && !Into.Contains(Label)) Into.Add(Label);
		}
	}
}
