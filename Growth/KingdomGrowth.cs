using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.Rules;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static class KingdomGrowth
	{
		public static bool Enabled => Options.GetOption("r_TAF_OptionGrowth") != "No";

		public static bool ThirstEnabled => Options.GetOption("r_TAF_OptionThirst") != "No";

		public static void OnZoneActivated(KingdomSystem System, Zone Z)
		{
			if (!Enabled || !System.Founded || Z == null || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				return;
			}
			long timeTicks = The.Game.TimeTicks;
			if (System.NextArrivalTick <= 0)
			{
				System.NextArrivalTick = timeTicks + KingdomRules.ArrivalIntervalTicks(System.Population);
				return;
			}
			FetchWater(System, Z);
			int arrivals = 0;
			while (timeTicks >= System.NextArrivalTick && arrivals < KingdomRules.MaxArrivalsPerVisit)
			{
				int upkeep = ThirstEnabled ? KingdomRules.UpkeepDrams(System.Population) : 0;
				int paid = ConsumeStoredWater(Z, upkeep);
				if (paid < upkeep || CountStoredWater(Z) < KingdomRules.DramsPerArrival)
				{
					if (!ThirstEnabled)
					{
						System.NextArrivalTick = timeTicks + KingdomRules.ArrivalIntervalTicks(System.Population);
						break;
					}
					System.DryStreak++;
					KingdomChronicle.Record(System, "the stores ran low, and " + System.KingdomDisplayName + " thirsted");
					MessageQueue.AddPlayerMessage("{{r|" + System.KingdomDisplayName + " thirsts. The stores are nearly dry.}}");
					if (System.DryStreak >= KingdomRules.DryIntervalsToEmigrate && Emigrate(System, Z))
					{
						System.DryStreak = 0;
					}
					System.NextArrivalTick = timeTicks + KingdomRules.ArrivalIntervalTicks(System.Population);
					break;
				}
				System.DryStreak = 0;
				if (!SpawnSettler(System, Z))
				{
					break;
				}
				arrivals++;
				System.NextArrivalTick += KingdomRules.ArrivalIntervalTicks(System.Population);
			}
			if (timeTicks >= System.NextArrivalTick)
			{
				System.NextArrivalTick = timeTicks + KingdomRules.ArrivalIntervalTicks(System.Population);
			}
			UpdateStage(System, Z);
		}

		public static bool SpawnSettler(KingdomSystem System, Zone Z)
		{
			List<Cell> emptyCells = Z.GetEmptyCells();
			if (emptyCells == null || emptyCells.Count == 0)
			{
				return false;
			}
			Cell cell = emptyCells[Stat.Random(0, emptyCells.Count - 1)];
			GameObject settler = GameObject.Create("r_KingdomSettler");
			if (settler == null)
			{
				return false;
			}
			cell.AddObject(settler);
			settler.MakeActive();
			KingdomFounding.EnrollCitizen(settler);
			string origin = KingdomRules.Origins[Stat.Random(0, KingdomRules.Origins.Length - 1)];
			settler.SetStringProperty("KingdomOrigin", origin);
			System.OriginCounts.TryGetValue(origin, out var count);
			System.OriginCounts[origin] = count + 1;
			ConsumeStoredWater(Z, KingdomRules.DramsPerArrival);
			System.Population++;
			KingdomChronicle.Record(System, "a settler from " + origin + " arrived at " + System.KingdomDisplayName + " and drank of the shared water");
			MessageQueue.AddPlayerMessage("{{G|A settler from " + origin + " has arrived at " + System.KingdomDisplayName + ".}}");
			return true;
		}

		public static bool Emigrate(KingdomSystem System, Zone Z)
		{
			if (System.Population <= 0)
			{
				return false;
			}
			GameObject leaver = null;
			foreach (GameObject item in Z.GetObjects())
			{
				if (item.GetIntProperty("KingdomCitizen") == 1 && !item.IsPlayer())
				{
					leaver = item;
					break;
				}
			}
			if (leaver == null)
			{
				return false;
			}
			string name = leaver.ShortDisplayName;
			leaver.Obliterate();
			System.Population--;
			KingdomChronicle.Record(System, "a " + name + " left " + System.KingdomDisplayName + " for wetter country");
			MessageQueue.AddPlayerMessage("{{R|A " + name + " has left " + System.KingdomDisplayName + " for wetter country.}}");
			return true;
		}

		public static int CountStoredWater(Zone Z)
		{
			int total = 0;
			foreach (GameObject item in Z.GetObjects())
			{
				LiquidVolume part = item.GetPart<LiquidVolume>();
				if (part != null && part.MaxVolume > 0 && part.Volume > 0 && part.GetPrimaryLiquidID() == "water")
				{
					total += part.Volume;
				}
			}
			return total;
		}

		public static int CountOpenWater(Zone Z)
		{
			int total = 0;
			foreach (GameObject item in Z.GetObjects())
			{
				LiquidVolume part = item.GetPart<LiquidVolume>();
				if (part != null && part.MaxVolume < 0 && part.Volume > 0 && part.GetPrimaryLiquidID() == "water")
				{
					total += part.Volume;
				}
			}
			return total;
		}

		public static int CountStorageSpace(Zone Z)
		{
			int total = 0;
			foreach (GameObject item in Z.GetObjects())
			{
				LiquidVolume part = item.GetPart<LiquidVolume>();
				if (part != null && part.MaxVolume > 0 && part.Volume < part.MaxVolume && (part.Volume == 0 || part.GetPrimaryLiquidID() == "water"))
				{
					total += part.MaxVolume - part.Volume;
				}
			}
			return total;
		}

		public static int FetchWater(KingdomSystem System, Zone Z)
		{
			int fetchable = KingdomRules.FetchableDrams(System.Population, CountOpenWater(Z), CountStorageSpace(Z));
			if (fetchable <= 0)
			{
				return 0;
			}
			int drained = 0;
			foreach (GameObject item in Z.GetObjects())
			{
				if (drained >= fetchable)
				{
					break;
				}
				LiquidVolume part = item.GetPart<LiquidVolume>();
				if (part != null && part.MaxVolume < 0 && part.Volume > 0 && part.GetPrimaryLiquidID() == "water")
				{
					int drams = (part.Volume < fetchable - drained) ? part.Volume : (fetchable - drained);
					if (part.UseDrams(drams))
					{
						drained += drams;
					}
				}
			}
			int stored = 0;
			foreach (GameObject item in Z.GetObjects())
			{
				if (stored >= drained)
				{
					break;
				}
				LiquidVolume part = item.GetPart<LiquidVolume>();
				if (part != null && part.MaxVolume > 0 && part.Volume < part.MaxVolume && (part.Volume == 0 || part.GetPrimaryLiquidID() == "water"))
				{
					int drams = part.MaxVolume - part.Volume;
					if (drams > drained - stored)
					{
						drams = drained - stored;
					}
					if (part.AddDrams("water", drams))
					{
						stored += drams;
					}
				}
			}
			return stored;
		}

		public static int ConsumeStoredWater(Zone Z, int Drams)
		{
			int remaining = Drams;
			foreach (GameObject item in Z.GetObjects())
			{
				if (remaining <= 0)
				{
					break;
				}
				LiquidVolume part = item.GetPart<LiquidVolume>();
				if (part != null && part.MaxVolume > 0 && part.Volume > 0 && part.GetPrimaryLiquidID() == "water")
				{
					int drams = (part.Volume < remaining) ? part.Volume : remaining;
					if (part.UseDrams(drams))
					{
						remaining -= drams;
					}
				}
			}
			return Drams - remaining;
		}

		public static void UpdateStage(KingdomSystem System, Zone Z)
		{
			GrowthStage stage = KingdomRules.StageFor(System.Population, CountStoredWater(Z));
			if (stage > System.Stage)
			{
				System.Stage = stage;
				string text = System.KingdomDisplayName + " has grown into a " + stage.ToString().ToLower();
				KingdomChronicle.Record(System, text, Accomplishment: true);
				Popup.Show("{{C|" + text + ".}}");
			}
		}
	}
}
