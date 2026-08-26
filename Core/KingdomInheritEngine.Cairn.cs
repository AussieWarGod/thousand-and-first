using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

#if !TAF_TESTS
using XRL;
using XRL.World;
using XRL.World.Parts;
#endif

namespace ThousandAndFirst
{
	internal static partial class KingdomInheritEngine
	{
		private static bool HasEntryToHeartPath(SiteSnapshot Site, Prepared Prepared)
		{
			KingdomInheritBuildSpec heart = null;
			for (int i = 0; i < Prepared.Specs.Length; i++)
			{
				if (Prepared.Specs[i].X == Prepared.Placement.HeartX
					&& Prepared.Specs[i].Y == Prepared.Placement.HeartY)
				{
					heart = Prepared.Specs[i];
					break;
				}
			}
			if (heart == null)
			{
				return false;
			}

			int heartLeft = heart.FootprintX;
			int heartTop = heart.FootprintY;
			int heartRight = heartLeft + heart.FootprintWidth - 1;
			int heartBottom = heartTop + heart.FootprintHeight - 1;
			int width = Site.Cells.GetLength(0);
			int height = Site.Cells.GetLength(1);
			bool[,] visited = new bool[width, height];
			Queue<int> queue = new Queue<int>();
			int startX = Prepared.Placement.EntryX;
			int startY = Prepared.Placement.EntryY;
			visited[startX, startY] = true;
			queue.Enqueue(startY * width + startX);
			while (queue.Count > 0)
			{
				int packed = queue.Dequeue();
				int x = packed % width;
				int y = packed / width;
				if (x >= heartLeft - 1 && x <= heartRight + 1
					&& y >= heartTop - 1 && y <= heartBottom + 1
					&& (x < heartLeft || x > heartRight || y < heartTop || y > heartBottom))
				{
					return true;
				}
				for (int dy = -1; dy <= 1; dy++)
				{
					for (int dx = -1; dx <= 1; dx++)
					{
						// Four-way reachability is conservative: a diagonal slit between two occupied
						// corners is not accepted as the old settlement's only road to its heart.
						if (Math.Abs(dx) + Math.Abs(dy) != 1)
						{
							continue;
						}
						int nx = x + dx;
						int ny = y + dy;
						if (nx < 0 || ny < 0 || nx >= width || ny >= height || visited[nx, ny]
							|| Site.Claimed[nx, ny])
						{
							continue;
						}
						KingdomInheritCellFacts facts = Site.Cells[nx, ny];
						if (facts.Exists && !facts.Occupied && !facts.Terrain && !facts.Stairs
							&& facts.Walkable)
						{
							visited[nx, ny] = true;
							queue.Enqueue(ny * width + nx);
						}
					}
				}
			}
			return false;
		}

		internal static string ComposeCairnText(KingdomSealRecord Legacy)
		{
			if (Legacy == null)
			{
				return "A founder's cairn. Chronicle: no chronicle lines survived the sealing.";
			}
			StringBuilder sb = new StringBuilder();
			AppendBounded(sb, "A founder's cairn for ", MaxCairnChars);
			AppendBounded(sb, CairnText(Legacy.FounderName, KingdomSealRecord.MaxNameChars), MaxCairnChars);
			AppendBounded(sb, ", founder of ", MaxCairnChars);
			AppendBounded(sb, CairnText(Legacy.SettlementName, KingdomSealRecord.MaxNameChars), MaxCairnChars);
			if (!string.IsNullOrEmpty(Legacy.RealmName))
			{
				AppendBounded(sb, " in ", MaxCairnChars);
				AppendBounded(sb, CairnText(Legacy.RealmName, KingdomSealRecord.MaxNameChars), MaxCairnChars);
			}
			AppendBounded(sb, ".", MaxCairnChars);
			if (!string.IsNullOrEmpty(Legacy.CauseText))
			{
				AppendBounded(sb, " They died: ", MaxCairnChars);
				AppendBounded(sb, CairnText(Legacy.CauseText, KingdomSealRecord.MaxLineChars), MaxCairnChars);
				AppendBounded(sb, ".", MaxCairnChars);
			}
			AppendBounded(sb, "\n\nChronicle of the old kingdom:\n", MaxCairnChars);
			if (Legacy.Chronicle == null || Legacy.Chronicle.Count == 0)
			{
				AppendBounded(sb, "No chronicle lines survived the sealing.", MaxCairnChars);
			}
			else
			{
				for (int i = 0; i < Legacy.Chronicle.Count; i++)
				{
					AppendBounded(sb, "- ", MaxCairnChars);
					AppendBounded(sb, CairnText(Legacy.Chronicle[i], KingdomSealRecord.MaxLineChars),
						MaxCairnChars);
					AppendBounded(sb, "\n", MaxCairnChars);
				}
			}
			if (Legacy.RollNames != null && Legacy.RollNames.Count > 0)
			{
				AppendBounded(sb, "\n\nRemembered settlers:\n", MaxCairnChars);
				for (int i = 0; i < Legacy.RollNames.Count; i++)
				{
					AppendBounded(sb, "- ", MaxCairnChars);
					AppendBounded(sb, CairnText(Legacy.RollNames[i],
						KingdomSealRecord.MaxNameChars), MaxCairnChars);
					if (Legacy.RollOrigins != null && i < Legacy.RollOrigins.Count
						&& !string.IsNullOrEmpty(Legacy.RollOrigins[i]))
					{
						AppendBounded(sb, ", from ", MaxCairnChars);
						AppendBounded(sb, CairnText(Legacy.RollOrigins[i],
							KingdomSealRecord.MaxNameChars), MaxCairnChars);
					}
					if (Legacy.RollArrived != null && i < Legacy.RollArrived.Count
						&& !string.IsNullOrEmpty(Legacy.RollArrived[i]))
					{
						AppendBounded(sb, " — ", MaxCairnChars);
						AppendBounded(sb, CairnText(Legacy.RollArrived[i],
							KingdomSealRecord.MaxNameChars), MaxCairnChars);
					}
					AppendBounded(sb, "\n", MaxCairnChars);
				}
			}
			string state = (Legacy.InheritedState >= 0
				&& Legacy.InheritedState < KingdomRules.InheritedStateNames.Length)
				? KingdomRules.InheritedStateNames[Legacy.InheritedState] : "unknown";
			AppendBounded(sb, "\nInterregnum draw: ", MaxCairnChars);
			AppendBounded(sb, Legacy.InterregnumRoll.ToString(CultureInfo.InvariantCulture), MaxCairnChars);
			AppendBounded(sb, ". Inherited state: ", MaxCairnChars);
			AppendBounded(sb, state, MaxCairnChars);
			AppendBounded(sb, ".", MaxCairnChars);
			return sb.ToString();
		}

		private static string CairnText(string Value, int MaxChars)
		{
			// Tilde has Description-specific alternate-text meaning. It is prose in the seal but is
			// flattened here along with Qud markup/control syntax.
			return KingdomSealRules.SanitizeText(Value, MaxChars).Replace('~', '-');
		}

		private static void AppendBounded(StringBuilder Builder, string Value, int MaxChars)
		{
			if (Builder.Length >= MaxChars || string.IsNullOrEmpty(Value))
			{
				return;
			}
			int room = MaxChars - Builder.Length;
			Builder.Append(Value, 0, Math.Min(room, Value.Length));
		}

		private static bool DiscardAll(IKingdomInheritEngineHost Host, object[] Handles)
		{
			bool clean = true;
			if (Host == null || Handles == null)
			{
				return false;
			}
			for (int i = Handles.Length - 1; i >= 0; i--)
			{
				if (Handles[i] == null)
				{
					continue;
				}
				try
				{
					if (!Host.Discard(Handles[i]))
					{
						clean = false;
					}
				}
				catch
				{
					clean = false;
				}
				Handles[i] = null;
			}
			return clean;
		}

		private static KingdomInheritApplyResult Refused(KingdomInheritApplyFault Fault,
			string Detail, string Marker)
		{
			return new KingdomInheritApplyResult(KingdomInheritApplyStatus.Refused, Fault,
				Detail, Marker, 0, false);
		}

		private static KingdomInheritApplyResult Failed(KingdomInheritApplyFault Fault,
			string Detail, string Marker)
		{
			return new KingdomInheritApplyResult(KingdomInheritApplyStatus.Failed, Fault,
				Detail, Marker, 0, false);
		}

		private static string Nonempty(string Value, string Fallback)
		{
			return string.IsNullOrEmpty(Value) ? Fallback : Value;
		}

	}
}
