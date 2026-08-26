using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomConstructionRules
	{
		public static bool TryEncodeCells(IList<KingdomConstructionCell> Cells, out string Payload)
		{
			Payload = null;
			if (Cells == null || Cells.Count <= 0 || Cells.Count > MaxRouteCells)
			{
				return false;
			}
			StringBuilder text = new StringBuilder("v1");
			HashSet<int> seen = new HashSet<int>();
			for (int i = 0; i < Cells.Count; i++)
			{
				int x = Cells[i].X;
				int y = Cells[i].Y;
				int packed = x + y * 1024;
				if (x < 0 || x > 1023 || y < 0 || y > 1023 || !seen.Add(packed))
				{
					return false;
				}
				text.Append(';').Append(x.ToString(CultureInfo.InvariantCulture)).Append(',')
					.Append(y.ToString(CultureInfo.InvariantCulture));
			}
			Payload = text.ToString();
			return true;
		}

		/// <summary>Canonical, bounded physical strike receipt.</summary>
		public static bool TryEncodeStrikeIntent(KingdomStrikeIntent Intent, out string Receipt)
		{
			Receipt = null;
			KingdomMaterialDebitCost salvage;
			if (Intent == null || !TextLength(Intent.DisplayName, 1, 512)
				|| !TextLength(Intent.BuildKey, 0, MaxTargetChars)
				|| !TextLength(Intent.TargetDisplayName, 0, 512)
				|| !TextLength(Intent.PlotId, 0, MaxSubjectChars)
				|| Intent.Effort <= 0 || Intent.Effort > int.MaxValue
				|| Intent.Targets == null || Intent.Targets.Count > MaxStrikeTargets
				|| !KingdomMaterialDebitCost.TryParseClaim(Intent.SalvageClaim, out salvage)
				|| !salvage.Bits.IsEmpty() || !salvage.Exotics.IsEmpty())
			{
				return false;
			}
			bool networkStrike = KingdomGatehouseRules.IsNetworkStrike(Intent.BuildKey,
				Intent.HasPlot, Intent.X1, Intent.Y1, Intent.X2, Intent.Y2, Intent.PlotId,
				Intent.Targets.Count);
			if (Intent.HasPlot)
			{
				if (Intent.X1 < 0 || Intent.X1 > Intent.X2 || Intent.X2 > 1023
					|| Intent.Y1 < 0 || Intent.Y1 > Intent.Y2 || Intent.Y2 > 1023
					|| string.IsNullOrEmpty(Intent.PlotId)) return false;
			}
			else if (!networkStrike && (Intent.X1 != -1 || Intent.Y1 != -1 || Intent.X2 != -1
				|| Intent.Y2 != -1 || !string.IsNullOrEmpty(Intent.PlotId)
				|| Intent.Targets.Count != 0)) return false;
			List<KingdomStrikeTarget> targets = new List<KingdomStrikeTarget>(Intent.Targets);
			targets.Sort(delegate(KingdomStrikeTarget a, KingdomStrikeTarget b)
			{
				int compare = a.Y.CompareTo(b.Y);
				if (compare != 0) return compare;
				compare = a.X.CompareTo(b.X);
				return compare != 0 ? compare : string.CompareOrdinal(a.Id, b.Id);
			});
			StringBuilder targetText = new StringBuilder();
			HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < targets.Count; i++)
			{
				KingdomStrikeTarget target = targets[i];
				if (target == null || !TextLength(target.Id, 1, MaxSubjectChars)
					|| !TextLength(target.Blueprint, 1, MaxTargetChars)
					|| target.X < Intent.X1 || target.X > Intent.X2
					|| target.Y < Intent.Y1 || target.Y > Intent.Y2 || !ids.Add(target.Id))
					return false;
				if (i > 0) targetText.Append(';');
				targetText.Append(EncodeText(target.Id)).Append(',')
					.Append(EncodeText(target.Blueprint)).Append(',')
					.Append(target.X.ToString(CultureInfo.InvariantCulture)).Append(',')
					.Append(target.Y.ToString(CultureInfo.InvariantCulture));
			}
			string text = "v2|" + EncodeText(Intent.DisplayName) + "|"
				+ EncodeText(Intent.BuildKey) + "|" + EncodeText(Intent.TargetDisplayName) + "|"
				+ EncodeText(Intent.SalvageClaim) + "|"
				+ (Intent.HasPlot ? "1" : "0") + "|"
				+ Intent.X1.ToString(CultureInfo.InvariantCulture) + "|"
				+ Intent.Y1.ToString(CultureInfo.InvariantCulture) + "|"
				+ Intent.X2.ToString(CultureInfo.InvariantCulture) + "|"
				+ Intent.Y2.ToString(CultureInfo.InvariantCulture) + "|"
				+ EncodeText(Intent.PlotId) + "|"
				+ Intent.Effort.ToString(CultureInfo.InvariantCulture) + "|" + targetText;
			if (text.Length > MaxPhysicalReceiptChars) return false;
			Receipt = text;
			return true;
		}

		public static bool TryDecodeStrikeIntent(string Receipt, out KingdomStrikeIntent Intent)
		{
			Intent = null;
			if (string.IsNullOrEmpty(Receipt) || Receipt.Length > MaxPhysicalReceiptChars)
				return false;
			string[] f = Receipt.Split('|');
			string displayName, buildKey, targetDisplayName, salvageClaim, plotId;
			int x1, y1, x2, y2;
			if (!((f.Length == 11 && f[0] == "v1")
					|| (f.Length == 13 && f[0] == "v2"))
				|| (f[5] != "0" && f[5] != "1")
				|| !TryDecodeText(f[1], 512, out displayName)
				|| !TryDecodeText(f[2], MaxTargetChars, out buildKey)
				|| !TryDecodeText(f[3], 512, out targetDisplayName)
				|| !TryDecodeText(f[4], 4096, out salvageClaim)
				|| !TryInt(f[6], -1, 1023, out x1) || !TryInt(f[7], -1, 1023, out y1)
				|| !TryInt(f[8], -1, 1023, out x2) || !TryInt(f[9], -1, 1023, out y2)
				|| !TryDecodeText(f[10], MaxSubjectChars, out plotId)) return false;
			KingdomStrikeIntent parsed = new KingdomStrikeIntent
			{
				DisplayName = displayName, BuildKey = buildKey,
				TargetDisplayName = targetDisplayName, SalvageClaim = salvageClaim,
				HasPlot = f[5] == "1", X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
				PlotId = plotId, Effort = 0, Targets = null
			};
			if (f[0] == "v1")
			{
				// Legacy receipts did not freeze exact plot-part IDs or effort. They remain
				// readable only so execution can fail closed; never infer a new target set.
				string legacy = "v1|" + EncodeText(parsed.DisplayName) + "|"
					+ EncodeText(parsed.BuildKey) + "|" + EncodeText(parsed.TargetDisplayName) + "|"
					+ EncodeText(parsed.SalvageClaim) + "|" + (parsed.HasPlot ? "1" : "0") + "|"
					+ parsed.X1.ToString(CultureInfo.InvariantCulture) + "|"
					+ parsed.Y1.ToString(CultureInfo.InvariantCulture) + "|"
					+ parsed.X2.ToString(CultureInfo.InvariantCulture) + "|"
					+ parsed.Y2.ToString(CultureInfo.InvariantCulture) + "|" + EncodeText(parsed.PlotId);
				if (legacy != Receipt) return false;
				Intent = parsed;
				return true;
			}
			int effort;
			if (!TryInt(f[11], 1, int.MaxValue, out effort)) return false;
			List<KingdomStrikeTarget> targets = new List<KingdomStrikeTarget>();
			if (!string.IsNullOrEmpty(f[12]))
			{
				string[] rows = f[12].Split(';');
				if (rows.Length > MaxStrikeTargets) return false;
				for (int i = 0; i < rows.Length; i++)
				{
					string[] values = rows[i].Split(',');
					string id, blueprint; int x, y;
					if (values.Length != 4
						|| !TryDecodeText(values[0], MaxSubjectChars, out id)
						|| !TryDecodeText(values[1], MaxTargetChars, out blueprint)
						|| !TryInt(values[2], 0, 1023, out x)
						|| !TryInt(values[3], 0, 1023, out y)) return false;
					targets.Add(new KingdomStrikeTarget
						{ Id = id, Blueprint = blueprint, X = x, Y = y });
				}
			}
			parsed.Effort = effort;
			parsed.Targets = targets;
			string canonical;
			if (!TryEncodeStrikeIntent(parsed, out canonical) || canonical != Receipt) return false;
			Intent = parsed;
			return true;
		}

		public static bool TryDecodeCells(string Payload, out List<KingdomConstructionCell> Cells)
		{
			Cells = null;
			if (string.IsNullOrEmpty(Payload) || Payload.Length > MaxPayloadChars)
			{
				return false;
			}
			string[] terms = Payload.Split(';');
			if (terms.Length < 2 || terms.Length - 1 > MaxRouteCells || terms[0] != "v1")
			{
				return false;
			}
			List<KingdomConstructionCell> cells = new List<KingdomConstructionCell>();
			HashSet<int> seen = new HashSet<int>();
			for (int i = 1; i < terms.Length; i++)
			{
				string[] pair = terms[i].Split(',');
				int x;
				int y;
				if (pair.Length != 2 || !TryInt(pair[0], -1, 1023, out x)
					|| !TryInt(pair[1], -1, 1023, out y) || x < 0 || y < 0
					|| !seen.Add(x + y * 1024))
				{
					return false;
				}
				cells.Add(new KingdomConstructionCell(x, y));
			}
			Cells = cells;
			return true;
		}

	}
}
