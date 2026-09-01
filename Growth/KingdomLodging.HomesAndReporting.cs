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
		private static bool TryBenefitIndex(Zone Z, KingdomSurvey Survey,
			out KingdomBenefitIndex Benefits, out string Failure)
		{
			Benefits = null;
			Failure = null;
			if (Z == null)
			{
				Failure = "lodging has no loaded ground";
				return false;
			}
			Survey = Survey ?? KingdomSurvey.ActiveFor(Z) ?? KingdomSurvey.Take(Z);
			if (Survey == null || !ReferenceEquals(Survey.Ground, Z))
			{
				Failure = "lodging has no exact current-ground survey";
				return false;
			}
			return Survey.TryBenefits(out Benefits, out Failure);
		}

		private static void LogBenefitFailure(Zone Z, string Context, string Failure)
		{
			KingdomLog.Log("Lodging " + Context + " refused physical benefits in "
				+ (Z?.ZoneID ?? "<no-zone>") + ": " + (Failure ?? "unknown failure"));
		}

		internal static bool TryHomeReading(GameObject Home, KingdomBenefitIndex Benefits,
			out KingdomBenefitReading Reading, out string PlotId)
		{
			Reading = null;
			PlotId = null;
			if (!KingdomUpgrade.IsFunctionallyBuilt(Home) || Benefits == null
				|| string.IsNullOrEmpty(Home.IDIfAssigned)) return false;
			Reading = Benefits.ReadingForRoot(Home.IDIfAssigned);
			PlotId = Home.GetStringProperty(KingdomPlots.PlotIdProperty);
			return Reading?.Designation != null
				&& string.Equals(Reading.Designation.RootId, Home.IDIfAssigned,
					StringComparison.Ordinal)
				&& string.Equals(Reading.Designation.ZoneId, Home.CurrentZone?.ZoneID,
					StringComparison.Ordinal)
				&& !string.IsNullOrEmpty(Reading.Designation.Identity)
				&& !string.IsNullOrEmpty(PlotId)
				&& string.Equals(Reading.Designation.LotId, PlotId, StringComparison.Ordinal)
				&& Reading.Designation.Cells.Count > 0;
		}

		internal static int RoofCapacity(GameObject Home, KingdomBenefitIndex Benefits)
		{
			return TryHomeReading(Home, Benefits, out _, out _)
				? Benefits.AmountForRoot(Home.IDIfAssigned, "roof") : 0;
		}

		private static string[] HomeTags(GameObject Home, KingdomBenefitIndex Benefits)
		{
			return TryHomeReading(Home, Benefits, out _, out _)
				? Benefits.TagsForRoot(Home.IDIfAssigned) : new string[0];
		}

		private static string HomeBuildingKey(GameObject Home, KingdomBenefitIndex Benefits)
		{
			return TryHomeReading(Home, Benefits, out KingdomBenefitReading reading, out _)
				? reading.Designation.BuildingKey : null;
		}

		private static List<GameObject> HousingIn(Zone Z, KingdomBenefitIndex Benefits)
		{
			List<GameObject> list = new List<GameObject>();
			foreach (GameObject item in KingdomSurvey.ObjectsFor(Z))
			{
				if (RoofCapacity(item, Benefits) <= 0) continue;
				list.Add(item);
			}
			return list;
		}

		private static bool TryGetBuiltEntry(GameObject Work, KingdomBenefitIndex Benefits,
			out KingdomRules.BuildEntry Entry)
		{
			string key = HomeBuildingKey(Work, Benefits);
			if (string.IsNullOrEmpty(key))
			{
				Entry = null;
				return false;
			}
			return KingdomData.TryGetBuilding(key, out Entry);
		}

		// Trusted architecture may declare a rung. Every adopted or foreign designation instead
		// derives it from exact designated plot cells and this root's live physical roof capacity;
		// an identity string or borrowed catalogue key never grants authored geometry.
		private static KingdomLodgingRules.Closeness QuartersOf(GameObject Home,
			KingdomBenefitIndex Benefits)
		{
			if (!TryHomeReading(Home, Benefits, out KingdomBenefitReading reading, out _))
			{
				return KingdomLodgingRules.Closeness.Packed;
			}
			KingdomLodgingRules.Closeness declared;
			if (string.Equals(reading.Designation.ProviderId, "taf.architecture",
				StringComparison.Ordinal)
				&& Declared.TryGetValue(reading.Designation.BuildingKey, out declared))
			{
				return declared;
			}
			int cells = 0;
			for (int i = 0; i < reading.Designation.Cells.Count; i++)
				if ((reading.Designation.Cells[i].Use & KingdomBenefitCellUse.Plot) != 0) cells++;
			return KingdomLodgingRules.ClosenessFromDensity(cells,
				Benefits.AmountForRoot(Home.IDIfAssigned, "roof"));
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

		private static GameObject HomeOf(Zone Z, GameObject Resident,
			KingdomBenefitIndex Benefits)
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
			foreach (GameObject item in HousingIn(Z, Benefits))
			{
				if (item.GetStringProperty(KingdomPlots.PlotIdProperty) == plotId
					&& !IsCondemned(item))
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
			if (!TryBenefitIndex(Z, null, out KingdomBenefitIndex benefits,
				out string failure))
			{
				LogBenefitFailure(Z, "roll", failure);
				return " {{r|(lodging evidence unavailable)}}";
			}
			GameObject home = HomeOf(Z, resident, benefits);
			if (home == null)
			{
				return (resident.GetIntProperty(UnhousedAnnouncedProperty) == 1) ? " {{r|(sleeps in the open)}}" : "";
			}
			KingdomRules.BuildEntry entry;
			TryGetBuiltEntry(home, benefits, out entry);
			List<string> needs = new List<string>(KingdomQol.ProfileOf(resident).Needs);
			string matched = KingdomLodgingRules.MatchedTag(needs,
				new List<string>(HomeTags(home, benefits)));
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
			if (!TryBenefitIndex(Z, null, out KingdomBenefitIndex benefits,
				out string failure))
			{
				LogBenefitFailure(Z, "dump", failure);
				return "\nLodging: physical evidence unavailable (" + failure + ")";
			}
			int housed = 0;
			List<string> sleepingOpen = new List<string>();
			for (int i = 0; i < residents.Count; i++)
			{
				GameObject resident = residents[i];
				if (HomeOf(Z, resident, benefits) != null)
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
