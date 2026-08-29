using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomLodging
	{
		private static List<GameObject> HousingIn(Zone Z)
		{
			List<GameObject> list = new List<GameObject>();
			foreach (GameObject item in KingdomSurvey.ObjectsFor(Z))
			{
				if (item.GetIntProperty(KingdomUpgrade.BuiltProperty) != 1)
				{
					continue;
				}
				if (string.IsNullOrEmpty(item.GetStringProperty(KingdomPlots.PlotIdProperty)))
				{
					continue;
				}
				KingdomRules.BuildEntry entry;
				if (!TryGetBuiltEntry(item, out entry) || RoofCapacity(entry) <= 0)
				{
					continue;
				}
				list.Add(item);
			}
			return list;
		}

		private static bool TryGetBuiltEntry(GameObject Work, out KingdomRules.BuildEntry Entry)
		{
			string key = KingdomUpgrade.DesignKeyOf(Work);
			if (string.IsNullOrEmpty(key))
			{
				Entry = null;
				return false;
			}
			return KingdomData.TryGetBuilding(key, out Entry);
		}

		// The rung this design's own arithmetic puts it on, or the one its author declared. The
		// footprint is the ground the TIER stands on -- KingdomPlotRules.TryFootprint answers with
		// the whole plot for a tier that declares no footprint of its own, which is exactly right:
		// the stone house fills its plot and the tent does not. A design with no plot spec at all is
		// a single-cell work with a bunk in it, and reads Packed, which is what one cell is.
		private static KingdomLodgingRules.Closeness QuartersOf(KingdomRules.BuildEntry Entry)
		{
			if (Entry == null)
			{
				return KingdomLodgingRules.Closeness.Packed;
			}
			KingdomLodgingRules.Closeness declared;
			if (Declared.TryGetValue(Entry.Key, out declared))
			{
				return declared;
			}
			KingdomPlotRules.PlotSpec spec;
			int width;
			int height;
			int cells = (KingdomPlots.TryGetSpec(Entry.Key, out spec) && KingdomPlotRules.TryFootprint(spec, out width, out height))
				? (width * height)
				: 0;
			return KingdomLodgingRules.ClosenessFromDensity(cells, RoofCapacity(Entry));
		}

		private static int RoofCapacity(KingdomRules.BuildEntry Entry)
		{
			if (Entry == null)
			{
				return 0;
			}
			List<KindAmount> carries;
			KingdomCatalogueRules.TryParseTally(Entry.Carries, out carries, out _);
			return KingdomCatalogueRules.AmountOf(carries, KingdomCatalogueRules.SupportRoof);
		}

		private static GameObject FindResidentByName(Zone Z, string ResidentName)
		{
			if (Z == null || string.IsNullOrEmpty(ResidentName))
			{
				return null;
			}
			KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
			foreach (GameObject item in KingdomSurvey.ObjectsFor(Z))
			{
				if (KingdomCitizenship.BelongsTo(system, item)
					&& item.GetStringProperty("KingdomName") == ResidentName)
				{
					return item;
				}
			}
			return null;
		}

		private static GameObject HomeOf(Zone Z, GameObject Resident)
		{
			if (Z == null || Resident == null)
			{
				return null;
			}
			string plotId = Resident.GetStringProperty(HomePlotIdProperty);
			if (string.IsNullOrEmpty(plotId))
			{
				return null;
			}
			foreach (GameObject item in KingdomSurvey.ObjectsFor(Z))
			{
				if (item.GetStringProperty(KingdomPlots.PlotIdProperty) == plotId && item.GetIntProperty(KingdomUpgrade.BuiltProperty) == 1)
				{
					return item;
				}
			}
			return null;
		}

		/// <summary>The suffix the roll of settlers appends to a resident's own line: where they
		/// sleep, or that they do not yet. Empty when this resident is not standing in
		/// <paramref name="Z"/> right now &mdash; the roll already reads only the zone the founder
		/// is standing in for the same reason the yard-trades lines do.</summary>
		public static string RollLine(Zone Z, string ResidentName)
		{
			GameObject resident = FindResidentByName(Z, ResidentName);
			if (resident == null)
			{
				return "";
			}
			GameObject home = HomeOf(Z, resident);
			if (home == null)
			{
				return (resident.GetIntProperty(UnhousedAnnouncedProperty) == 1) ? " {{r|(sleeps in the open)}}" : "";
			}
			KingdomRules.BuildEntry entry;
			TryGetBuiltEntry(home, out entry);
			List<string> needs = new List<string>(KingdomQol.ProfileOf(resident).Needs);
			string matched = KingdomLodgingRules.MatchedTag(needs, (entry == null) ? null : new List<string>(KingdomQol.OfferOf(entry.Key, Z)));
			return " {{K|(" + KingdomLodgingRules.HomeSuffix((entry != null) ? entry.Name : null, matched) + ")}}";
		}

		/// <summary>The lodging line <c>kingdom:dump</c> appends for the zone the founder is
		/// standing in: how many of the residents present are housed, who is not, and how much of
		/// their brink window (Addendum 4b) they have spent, with how long they have actually been
		/// without a roof.</summary>
		public static string DumpLine(KingdomSystem System, Zone Z)
		{
			if (Z == null)
			{
				return "";
			}
			List<GameObject> residents = ResidentsIn(Z);
			if (residents.Count == 0)
			{
				return "";
			}
			int housed = 0;
			List<string> sleepingOpen = new List<string>();
			for (int i = 0; i < residents.Count; i++)
			{
				GameObject resident = residents[i];
				if (!string.IsNullOrEmpty(resident.GetStringProperty(HomePlotIdProperty)))
				{
					housed++;
					continue;
				}
				string name = KingdomPresentation.Rich(NameOf(resident));
				BrinkRecord brink = KingdomBrink.Of(resident, BrinkKind.Roof);
				if (brink.Stands)
				{
					long now = (The.Game != null) ? The.Game.TimeTicks : 0L;
					name += " (brink " + KingdomBrinkRules.DaysLeft(BrinkKind.Roof, brink.WarnedTick, now)
						+ "/" + KingdomLodgingRules.GraceDays + "d left"
						+ (brink.Warned ? "" : ", unwarned")
						+ ", stood " + KingdomBrinkRules.DaysStood(brink.ReachedTick, now) + "d)";
				}
				sleepingOpen.Add(name);
			}
			string line = "\nLodging: " + housed + "/" + residents.Count + " housed";
			if (sleepingOpen.Count > 0)
			{
				line += "  sleeping in the open: " + string.Join(", ", sleepingOpen);
			}
			return line;
		}	}
}
