using System;
using System.Collections.Generic;
using System.Text;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	internal static class KingdomScenarioGroundCensus
	{
		internal static void Record(Zone Zone, string Checkpoint)
		{
			try
			{
				var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
				var samples = new StringBuilder();
				int shown = 0, observed = 0;
				foreach (GameObject body in Zone.GetObjects())
				{
					if (!GameObject.Validate(body) || body.IsPlayer()) continue;
					Cell cell = body.CurrentCell;
					if (cell == null || !ReferenceEquals(cell.ParentZone, Zone)) continue;
					bool border = cell.X == 0 || cell.Y == 0
						|| cell.X == Zone.Width - 1 || cell.Y == Zone.Height - 1;
					bool vortex = body.HasPart("SpaceTimeVortex") || body.HasPart("SpaceTimeRift");
					bool widget = body.GetBlueprint()?.InheritsFrom("Widget") ?? false;
					string kind = vortex ? "vortex" : body.IsCreature ? "creature"
						: widget ? "widget" : null;
					if (kind == null) continue;
					string key = (border ? "border-" : "interior-") + kind;
					counts.TryGetValue(key, out int count);
					counts[key] = count + 1;
					if (kind == "widget") continue;
					observed++;
					if (shown++ >= 16) continue;
					samples.Append("; sample=").Append(key).Append(':').Append(body.Blueprint)
						.Append('@').Append(cell.X).Append(',').Append(cell.Y)
						.Append("/hostile=").Append(body.Brain?.Hostile ?? false);
				}
				var text = new StringBuilder("checkpoint=").Append(Checkpoint)
					.Append("; zone=").Append(Zone.ZoneID);
				foreach (var pair in counts)
					text.Append("; ").Append(pair.Key).Append('=').Append(pair.Value);
				text.Append("; sample-count=").Append(Math.Min(shown, 16))
					.Append("; omitted=").Append(Math.Max(0, observed - 16)).Append(samples);
				KingdomScenarioJournal.Append("TESTGROUND-CENSUS", true, text.ToString());
			}
			catch (Exception error)
			{
				KingdomScenarioJournal.Append("TESTGROUND-CENSUS", false,
					"checkpoint=" + Checkpoint + "; observation-error=" + error.GetType().Name);
			}
		}
	}
}
