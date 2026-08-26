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
		private static string EncodeGrowthPlan(GrowthPlan Plan)
		{
			if (Plan == null || Plan.Rows == null || Plan.Rows.Count > MaxGrowthRows
				|| !BoundedGrowthIdentity(Plan.PredecessorId)
				|| !BoundedGrowthIdentity(Plan.SuccessorId)
				|| string.IsNullOrEmpty(Plan.SuccessorKey)
				|| !BoundedGrowthText(Plan.SuccessorKey, 256)
				|| string.IsNullOrEmpty(Plan.PlotId)
				|| !BoundedGrowthText(Plan.PlotId, 128)
				|| !BoundedGrowthText(Plan.Wall, 256)) return null;
			System.Text.StringBuilder text = new System.Text.StringBuilder("g1")
				.Append(',').Append(GrowthText(Plan.PredecessorId))
				.Append(',').Append(GrowthText(Plan.SuccessorId))
				.Append(',').Append(GrowthText(Plan.SuccessorKey))
				.Append(',').Append(GrowthText(Plan.PlotId));
			AppendGrowthInt(text, Plan.Old.X1); AppendGrowthInt(text, Plan.Old.Y1);
			AppendGrowthInt(text, Plan.Old.X2); AppendGrowthInt(text, Plan.Old.Y2);
			AppendGrowthInt(text, Plan.Grown.X1); AppendGrowthInt(text, Plan.Grown.Y1);
			AppendGrowthInt(text, Plan.Grown.X2); AppendGrowthInt(text, Plan.Grown.Y2);
			AppendGrowthInt(text, (int)Plan.Roof); AppendGrowthInt(text, Plan.HeartX);
			AppendGrowthInt(text, Plan.HeartY); AppendGrowthInt(text, Plan.KeepInner ? 1 : 0);
			text.Append(',').Append(GrowthText(Plan.Wall));
			AppendGrowthInt(text, Plan.Done ? 1 : 0);
			for (int i = 0; i < Plan.Rows.Count; i++)
			{
				GrowthRow row = Plan.Rows[i];
				if (row == null || (row.Kind != 1 && row.Kind != 2)
					|| row.State < 0 || row.State > 2 || row.X < 0 || row.X > 1023
					|| row.Y < 0 || row.Y > 1023 || !BoundedGrowthText(row.Blueprint, 256)
					|| string.IsNullOrEmpty(row.Blueprint) || !BoundedGrowthIdentity(row.Id)) return null;
				text.Append(';').Append(row.Kind.ToString(System.Globalization.CultureInfo.InvariantCulture));
				AppendGrowthInt(text, row.X); AppendGrowthInt(text, row.Y);
				text.Append(',').Append(GrowthText(row.Blueprint))
					.Append(',').Append(GrowthText(row.Id));
				AppendGrowthInt(text, row.State);
				if (text.Length > KingdomConstructionRules.MaxPhysicalReceiptChars) return null;
			}
			return text.ToString();
		}

		private static void AppendGrowthInt(System.Text.StringBuilder Text, int Value)
		{
			Text.Append(',').Append(Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
		}

		private static bool TryDecodeGrowthPlan(string Receipt, out GrowthPlan Plan)
		{
			Plan = null;
			if (string.IsNullOrEmpty(Receipt)
				|| Receipt.Length > KingdomConstructionRules.MaxPhysicalReceiptChars) return false;
			string[] terms = Receipt.Split(';');
			if (terms.Length - 1 > MaxGrowthRows) return false;
			string[] h = terms[0].Split(',');
			if (h.Length != 19 || h[0] != "g1") return false;
			try
			{
				string predecessor = DecodeGrowthText(h[1]);
				string successor = DecodeGrowthText(h[2]);
				string key = DecodeGrowthText(h[3]);
				string plot = DecodeGrowthText(h[4]);
				string wall = DecodeGrowthText(h[17]);
				int ox1, oy1, ox2, oy2, gx1, gy1, gx2, gy2, roof, hx, hy, keep, done;
				if (!TryGrowthInt(h[5], 0, 1023, out ox1)
					|| !TryGrowthInt(h[6], 0, 1023, out oy1)
					|| !TryGrowthInt(h[7], 0, 1023, out ox2)
					|| !TryGrowthInt(h[8], 0, 1023, out oy2)
					|| !TryGrowthInt(h[9], 0, 1023, out gx1)
					|| !TryGrowthInt(h[10], 0, 1023, out gy1)
					|| !TryGrowthInt(h[11], 0, 1023, out gx2)
					|| !TryGrowthInt(h[12], 0, 1023, out gy2)
					|| !TryGrowthInt(h[13], 0, 3, out roof)
					|| !TryGrowthInt(h[14], 0, 1023, out hx)
					|| !TryGrowthInt(h[15], 0, 1023, out hy)
					|| !TryGrowthInt(h[16], 0, 1, out keep)
					|| !TryGrowthInt(h[18], 0, 1, out done)
					|| ox1 > ox2 || oy1 > oy2 || gx1 > gx2 || gy1 > gy2
					|| !BoundedGrowthIdentity(predecessor) || !BoundedGrowthIdentity(successor)
					|| string.IsNullOrEmpty(key) || key.Length > 256
					|| string.IsNullOrEmpty(plot) || plot.Length > 128 || wall.Length > 256) return false;
				List<GrowthRow> rows = new List<GrowthRow>();
				HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
				for (int i = 1; i < terms.Length; i++)
				{
					string[] f = terms[i].Split(',');
					int kind, x, y, state;
					if (f.Length != 6 || !TryGrowthInt(f[0], 1, 2, out kind)
						|| !TryGrowthInt(f[1], 0, 1023, out x)
						|| !TryGrowthInt(f[2], 0, 1023, out y)
						|| !TryGrowthInt(f[5], 0, 2, out state)) return false;
					string blueprint = DecodeGrowthText(f[3]);
					string id = DecodeGrowthText(f[4]);
					if (string.IsNullOrEmpty(blueprint) || blueprint.Length > 256
						|| !BoundedGrowthIdentity(id) || !ids.Add(id)) return false;
					GrowthRow row = new GrowthRow { Kind = kind, X = x, Y = y,
						Blueprint = blueprint, Id = id, State = state };
					if (rows.Count > 0 && CompareGrowthRows(rows[rows.Count - 1], row) >= 0)
						return false;
					rows.Add(row);
				}
				Plan = new GrowthPlan { PredecessorId = predecessor, SuccessorId = successor,
					SuccessorKey = key, PlotId = plot,
					Old = new KingdomPlotRules.PlotRect(ox1, oy1, ox2, oy2),
					Grown = new KingdomPlotRules.PlotRect(gx1, gy1, gx2, gy2),
					Roof = (KingdomPlotRules.RoofState)roof, HeartX = hx, HeartY = hy,
					KeepInner = keep == 1, Wall = wall, Done = done == 1, Rows = rows };
			}
			catch { return false; }
			return EncodeGrowthPlan(Plan) == Receipt;
		}

		private static string DecodeGrowthText(string Encoded)
		{
			byte[] bytes = Convert.FromBase64String(Encoded);
			string decoded = System.Text.Encoding.UTF8.GetString(bytes);
			if (GrowthText(decoded) != Encoded) throw new FormatException();
			return decoded;
		}

		private static bool TryGrowthInt(string Text, int Minimum, int Maximum, out int Value)
		{
			return int.TryParse(Text, System.Globalization.NumberStyles.None,
				System.Globalization.CultureInfo.InvariantCulture, out Value)
				&& Value >= Minimum && Value <= Maximum
				&& Text == Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
		}

		/// <summary>Whether anything in a cell stops the settlement building on it: something the
		/// founder owns or placed, another work, open water, or a household's own yard trade. This
		/// plot's own floor and walls do not, which is what lets a grown tier build through what
		/// its smaller self left standing.</summary>
		private static bool BlockedForPlot(Cell C)
		{
			if (C == null)
			{
				return true;
			}
			foreach (GameObject item in C.GetObjects())
			{
				if (item == null || item.IsCreature || item.IsPlayer())
				{
					continue;
				}
				if (item.GetIntProperty(KingdomYards.YardWorkProperty) == 1)
				{
					return true;
				}
				if (item.GetIntProperty(PlotPartProperty) == 1)
				{
					continue;
				}
				if (ReadObject(item) != KingdomPlotRules.GroundKind.Bare)
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>Places one object and marks it as this plot's, so a later striking takes down
		/// exactly what the settlement raised and nothing else. Does nothing when the cell already
		/// holds one of this plot's own objects of that blueprint, so re-stamping a grown tier
		/// never doubles a wall or lays a second floor.</summary>
		private static GameObject PlaceForPlot(Cell C, string Blueprint, string Id)
		{
			if (C == null || string.IsNullOrEmpty(Blueprint))
			{
				return null;
			}
			foreach (GameObject item in C.GetObjects())
			{
				if (item != null && item.Blueprint == Blueprint)
				{
					return null;
				}
			}
			GameObject placed = GameObject.Create(Blueprint);
			if (placed == null)
			{
				return null;
			}
			placed.SetIntProperty(PlotPartProperty, 1);
			if (!string.IsNullOrEmpty(Id))
			{
				placed.SetStringProperty(PlotIdProperty, Id);
			}
			GameObject accepted = null;
			try { accepted = C.AddObject(placed); }
			finally { KingdomSurvey.ObserveAddResultInActive(C.ParentZone, placed, accepted); }
			if (!ReferenceEquals(accepted, placed)) return null;
			return placed;
		}

		// --- The stamp --------------------------------------------------------------------

		/// <summary>
		/// Advances one plot to whatever stage its crew's labour has honestly bought, applying every
		/// stage crossed in order. New works consume elapsed intervals through their exact stamped
		/// gang; only the oldest active raising receives one, an empty settlement raises nothing,
		/// and idle or queued intervals never bank. A works object without the named schema is from
		/// the pre-polish save shape and retains its absolute-clock path unchanged.
		/// </summary>
	}
}
