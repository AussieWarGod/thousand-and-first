using System;
using System.Collections.Generic;
using System.Text;
using XRL;
using XRL.UI;
using XRL.World;
using ThousandAndFirst.Api;

namespace ThousandAndFirst.Simulation.City
{
	public static partial class KingdomBookReport
	{
		private static string Writers()
		{
			StringBuilder builder = new StringBuilder();
			builder.Append("This city's model is a published contract, at version ")
				.Append(KingdomExtensions.Version).Append(".");
			if (!KingdomExtensions.Enabled)
			{
				builder.Append("\n\n{{K|You have turned the behaviour lane off. Other mods can still add buildings, deals, works and settlers through their data files; none of them may run code against your city.}}");
				return builder.ToString();
			}
			List<string> admitted = KingdomExtensions.Admitted();
			builder.Append((admitted.Count == 0)
				? "\n\n{{K|Nothing is extending it. The city is entirely its own.}}"
				: ("\n\nWriting in it:"));
			for (int i = 0; i < admitted.Count; i++)
			{
				builder.Append("\n  {{W|").Append(admitted[i]).Append("}}");
			}
			List<string> refused = KingdomExtensions.Refusals();
			if (refused.Count > 0)
			{
				builder.Append("\n\n{{R|Refused:}}");
				for (int i = 0; i < refused.Count; i++)
				{
					builder.Append("\n  {{r|").Append(refused[i]).Append("}}");
				}
			}
			return builder.ToString();
		}

		private static string GroundName(string zoneId)
		{
			if (string.IsNullOrEmpty(zoneId) || The.ZoneManager == null)
			{
				return null;
			}
			Zone here = The.Player?.CurrentZone;
			if (here != null && here.ZoneID == zoneId)
			{
				return "Here, where you are standing";
			}
			// Named from the id and never fetched: GetZone builds ground that is not resident, and
			// a report that materialises a parasang to write a heading would be the most expensive
			// sentence in the mod.
			string name = The.ZoneManager.GetZoneDisplayName(zoneId, WithIndefiniteArticle: true);
			return string.IsNullOrEmpty(name) ? null : XRL.Language.Grammar.InitCap(name);
		}
	}
}
