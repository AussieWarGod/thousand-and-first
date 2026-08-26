using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomRoads
	{
		private static bool FreezeRoadReceipt(Zone Z, IList<KingdomConstructionCell> Cells,
			out RoadReceipt Receipt)
		{
			Receipt = null;
			string raw = Z.GetZoneProperty(TallyProperty, null) ?? "";
			if (!KingdomRoadRules.TryDecode(raw, out var tally, out _)) return false;
			RoadReceipt receipt = new RoadReceipt
			{
				TallyBefore = raw,
				FullBefore = Z.GetZoneProperty(FullSaidProperty, null) ?? "",
				FullAfter = "0"
			};
			HashSet<string> ids = new HashSet<string>(System.StringComparer.Ordinal);
			for (int i = 0; i < Cells.Count; i++)
			{
				Cell cell = Z.GetCell(Cells[i].X, Cells[i].Y);
				GameObject old = null;
				foreach (GameObject item in cell?.GetObjects() ?? new List<GameObject>())
				{
					if (GameObject.Validate(item) && item.GetIntProperty(PathStateProperty) > 0)
					{
						if (old != null) return false;
						old = item;
					}
				}
				if (!GameObject.Validate(old)
					|| old.GetIntProperty(PathStateProperty) != (int)KingdomRoadRules.WearState.Path
					|| !ids.Add(old.ID)) return false;
				if (KingdomConstruction.FindExactId(Z, old.ID, out var exactOld)
					!= KingdomPhysicalLookupState.Exact || !ReferenceEquals(exactOld, old)) return false;
				string outputId;
				do { outputId = System.Guid.NewGuid().ToString("N"); }
				while (ids.Contains(outputId));
				if (KingdomConstruction.FindExactId(Z, outputId, out _)
					!= KingdomPhysicalLookupState.Absent || !ids.Add(outputId)) return false;
				receipt.Rows.Add(new RoadRow { X = Cells[i].X, Y = Cells[i].Y,
					OldId = old.ID, OldBlueprint = old.Blueprint, NewId = outputId });
				KingdomRoadRules.Retire(tally, Cells[i].X, Cells[i].Y);
			}
			receipt.TallyAfter = KingdomRoadRules.Encode(tally) ?? "";
			Receipt = receipt;
			return true;
		}

		private static string EncodeRoadReceipt(RoadReceipt Receipt)
		{
			if (Receipt == null || Receipt.Rows == null
				|| Receipt.Rows.Count > KingdomRoadRules.MaxRouteCells) return null;
			System.Text.StringBuilder text = new System.Text.StringBuilder("r1|")
				.Append(RoadText(Receipt.TallyBefore)).Append('|').Append(RoadText(Receipt.TallyAfter))
				.Append('|').Append(RoadText(Receipt.FullBefore)).Append('|')
				.Append(RoadText(Receipt.FullAfter)).Append('|').Append(Receipt.State.ToString(
					global::System.Globalization.CultureInfo.InvariantCulture));
			for (int i = 0; i < Receipt.Rows.Count; i++)
			{
				RoadRow row = Receipt.Rows[i];
				if (row == null || string.IsNullOrEmpty(row.OldId)
					|| string.IsNullOrEmpty(row.OldBlueprint)) return null;
				text.Append(';').Append(row.X.ToString(
					global::System.Globalization.CultureInfo.InvariantCulture)).Append(',')
					.Append(row.Y.ToString(global::System.Globalization.CultureInfo.InvariantCulture)).Append(',')
					.Append(RoadText(row.OldId)).Append(',').Append(RoadText(row.OldBlueprint))
					.Append(',').Append(RoadText(row.NewId ?? "")).Append(',')
					.Append(row.Settled ? '1' : '0');
			}
			return text.Length <= KingdomConstructionRules.MaxPhysicalReceiptChars
				? text.ToString() : null;
		}

		private static bool TryDecodeRoadReceipt(string Text, out RoadReceipt Receipt)
		{
			Receipt = null;
			if (string.IsNullOrEmpty(Text)
				|| Text.Length > KingdomConstructionRules.MaxPhysicalReceiptChars) return false;
			string[] terms = Text.Split(';');
			string[] head = terms[0].Split('|');
			if (head.Length != 6 || head[0] != "r1" || terms.Length - 1 > KingdomRoadRules.MaxRouteCells
				|| !TryRoadInt(head[5], 2, out int state)) return false;
			try
			{
				RoadReceipt parsed = new RoadReceipt { TallyBefore = UnroadText(head[1]),
					TallyAfter = UnroadText(head[2]), FullBefore = UnroadText(head[3]),
					FullAfter = UnroadText(head[4]), State = state };
				HashSet<string> ids = new HashSet<string>(System.StringComparer.Ordinal);
				for (int i = 1; i < terms.Length; i++)
				{
					string[] f = terms[i].Split(',');
					if (f.Length != 6 || (f[5] != "0" && f[5] != "1")
						|| !TryRoadInt(f[0], 1023, out int x)
						|| !TryRoadInt(f[1], 1023, out int y)) return false;
					string id = UnroadText(f[2]), blueprint = UnroadText(f[3]);
					string output = UnroadText(f[4]);
					if (string.IsNullOrEmpty(id) || id.Length > 128
						|| string.IsNullOrEmpty(blueprint) || blueprint.Length > 256
						|| output.Length > 128 || !ids.Add(id)
						|| (output.Length > 0 && !ids.Add(output))) return false;
					parsed.Rows.Add(new RoadRow { X = x, Y = y, OldId = id,
						OldBlueprint = blueprint, NewId = output.Length == 0 ? null : output,
						Settled = f[5] == "1" });
				}
				if (EncodeRoadReceipt(parsed) != Text) return false;
				Receipt = parsed;
				return true;
			}
			catch { return false; }
		}

		private static bool TryRoadInt(string Text, int Maximum, out int Value)
		{
			return int.TryParse(Text, global::System.Globalization.NumberStyles.None,
				global::System.Globalization.CultureInfo.InvariantCulture, out Value)
				&& Value >= 0 && Value <= Maximum
				&& Value.ToString(global::System.Globalization.CultureInfo.InvariantCulture) == Text;
		}

		private static string RoadText(string Value)
		{
			return System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(Value ?? ""));
		}

		private static string UnroadText(string Value)
		{
			return System.Text.Encoding.UTF8.GetString(System.Convert.FromBase64String(Value));
		}

	}
}
