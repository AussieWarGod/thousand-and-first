using System.Collections.Generic;
using System.Text;
using XRL.UI;
using XRL.Wish;
using XRL.World;

namespace ThousandAndFirst
{
	[HasWishCommand]
	public static class KingdomCreedContentWish
	{
		[WishCommand("kingdom:creedcontent", null)]
		public static void Check()
		{
			Dictionary<string, int> works = new Dictionary<string, int>();
			HashSet<string> workKeys = new HashSet<string>();
			List<KingdomRules.BuildEntry> buildings = KingdomData.Buildings;
			for (int i = 0; i < buildings.Count; i++)
			{
				string creed = KingdomZoning.GateFor(buildings[i].Key).Creed;
				if (string.IsNullOrEmpty(creed)) continue;
				workKeys.Add(buildings[i].Key);
				works.TryGetValue(creed, out int count);
				works[creed] = count + 1;
			}
			HashSet<string> mapped = new HashSet<string>();
			IList<KingdomArchitectureMapping> mappings = KingdomArchitecture.InspectMappings();
			int creedMappings = 0;
			for (int i = 0; i < mappings.Count; i++)
			{
				mapped.Add(mappings[i].BuildKey);
				if (workKeys.Contains(mappings[i].BuildKey)) creedMappings++;
			}
			List<string> faults = new List<string>();
			int census = 0;
			foreach (Faction faction in Factions.Loop())
			{
				if (!KingdomCreed.CanBeCreed(faction)) continue;
				census++;
				if (!works.TryGetValue(faction.Name, out int count) || count == 0)
				{
					faults.Add(faction.Name + " has no creed-work");
					continue;
				}
				bool architecture = false;
				for (int i = 0; i < buildings.Count; i++)
				{
					if (KingdomZoning.GateFor(buildings[i].Key).Creed == faction.Name
						&& mapped.Contains(buildings[i].Key)
						&& (!string.IsNullOrEmpty(buildings[i].Carries) || buildings[i].Defence > 0))
					{
						architecture = true;
						break;
					}
				}
				if (!architecture) faults.Add(faction.Name + " has no behavior-bearing mapped creed-work");
			}
			StringBuilder text = new StringBuilder("{{C|Creed-content runtime check}}: ")
				.Append(census).Append(" admitted creeds, ").Append(works.Count)
				.Append(" covered creed keys, ").Append(creedMappings)
				.Append(" creed-work exact mappings.");
			for (int i = 0; i < faults.Count; i++) text.Append("\n{{R|FAIL}} ").Append(faults[i]);
			if (faults.Count == 0) text.Append("\n{{G|PASS}} every admitted creed has mapped, behavior-bearing content.");
			Popup.Show(text.ToString());
		}
	}
}
