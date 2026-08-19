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

		public static long Interval(KingdomSystem System, Zone Z)
		{
			System.ZoneDistricts.TryGetValue(Z.ZoneID, out var district);
			return KingdomRules.ArrivalIntervalTicks(System.Population, district);
		}

		public static void OnZoneActivated(KingdomSystem System, Zone Z, KingdomSurvey Shared = null)
		{
			if (!Enabled || !System.Founded || Z == null || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				return;
			}
			long timeTicks = The.Game.TimeTicks;
			KingdomSurvey survey = Shared ?? KingdomSurvey.Take(Z);
			if (KingdomLog.Enabled)
			{
				KingdomLog.Log("growth pass " + Z.ZoneID + " tick=" + timeTicks + " next=" + System.NextArrivalTick + " pop=" + System.Population + " stage=" + System.Stage + " stored=" + survey.StoredWater + " open=" + survey.OpenWater + " space=" + survey.StorageSpace + " cap=" + survey.StorageCapacity + " dry=" + System.DryStreak + " withered=" + System.Withered);
			}
			if (System.NextArrivalTick <= 0)
			{
				System.NextArrivalTick = timeTicks + Interval(System, Z);
				return;
			}
			int fetched = survey.Store(survey.DrawFromPools(KingdomRules.FetchableDrams(System.Population, survey.OpenWater, survey.StorageSpace)));
			if (fetched > 0 && KingdomLog.Enabled)
			{
				KingdomLog.Log("growth: fetched " + fetched + " drams from open water into stores");
			}
			int arrivals = 0;
			while (timeTicks >= System.NextArrivalTick && arrivals < KingdomRules.MaxArrivalsPerVisit && System.Population < KingdomRules.MaxPopulation)
			{
				int upkeep = ThirstEnabled ? KingdomRules.UpkeepForElapsed(System.Population, Interval(System, Z)) : 0;
				int paid = survey.Consume(upkeep);
				if (paid < upkeep || survey.StoredWater < KingdomRules.DramsPerArrival)
				{
					if (!ThirstEnabled)
					{
						System.NextArrivalTick = timeTicks + Interval(System, Z);
						break;
					}
					System.DryStreak++;
					KingdomChronicle.Record(System, "the stores ran low, and " + System.KingdomDisplayName + " thirsted");
					MessageQueue.AddPlayerMessage("{{r|" + System.KingdomDisplayName + " thirsts. The cistern is dry; settlers will leave if the water does not return.}}");
					KingdomRules.ThirstOutcome outcome = KingdomRules.ResolveThirst(System.DryStreak, System.Stage, System.Population);
					if (KingdomLog.Enabled) KingdomLog.Log("thirst: streak=" + System.DryStreak + " outcome=" + outcome);
					if (outcome == KingdomRules.ThirstOutcome.Emigration)
					{
						Emigrate(System, Z, survey);
					}
					else if (outcome == KingdomRules.ThirstOutcome.Withering)
					{
						Emigrate(System, Z, survey);
						if (!System.Withered)
						{
							System.Withered = true;
							KingdomChronicle.Record(System, System.KingdomDisplayName + " withered in the long thirst");
							MessageQueue.AddPlayerMessage("{{R|" + System.KingdomDisplayName + " is withering.}}");
						}
					}
					System.NextArrivalTick = timeTicks + Interval(System, Z);
					break;
				}
				System.DryStreak = 0;
				if (System.Withered)
				{
					System.Withered = false;
					KingdomChronicle.Record(System, "the water returned, and " + System.KingdomDisplayName + " drank deep and recovered");
					MessageQueue.AddPlayerMessage("{{G|" + System.KingdomDisplayName + " has recovered from the long thirst.}}");
				}
				if (!SpawnSettler(System, Z, survey))
				{
					if (!System.NoRoomAnnounced)
					{
						System.NoRoomAnnounced = true;
						KingdomChronicle.Record(System, "a settler reached " + System.KingdomDisplayName + " and found nowhere to stand");
						MessageQueue.AddPlayerMessage("{{r|A settler arrives at " + System.KingdomDisplayName + " and finds nowhere to stand. There is no open ground left here.}}");
					}
					System.NextArrivalTick = timeTicks + Interval(System, Z);
					break;
				}
				System.NoRoomAnnounced = false;
				arrivals++;
				System.NextArrivalTick += Interval(System, Z);
			}
			if (timeTicks >= System.NextArrivalTick)
			{
				System.NextArrivalTick = timeTicks + Interval(System, Z);
			}
			UpdateStage(System, Z, survey);
			if (KingdomLog.Enabled) KingdomLog.Log("growth pass done: pop=" + System.Population + " stage=" + System.Stage + " arrivals=" + arrivals + " dry=" + System.DryStreak + " next=" + System.NextArrivalTick);
		}

		public static bool SpawnSettler(KingdomSystem System, Zone Z, KingdomSurvey Survey = null)
		{
			List<Cell> emptyCells = Z.GetEmptyCells((Cell c) => c.IsPassable() && !c.HasObjectWithPart("LiquidVolume"));
			if (emptyCells == null || emptyCells.Count == 0)
			{
				emptyCells = Z.GetEmptyCells();
			}
			if (emptyCells == null || emptyCells.Count == 0)
			{
				return false;
			}
			Cell cell = emptyCells.GetRandomElement();
			GameObject settler = GameObject.Create("r_KingdomSettler");
			if (settler == null)
			{
				return false;
			}
			cell.AddObject(settler);
			settler.MakeActive();
			KingdomFounding.EnrollCitizen(settler);
			settler.SetIntProperty("KingdomBorn", 1);
			string origin = KingdomRules.Origins[Stat.Random(0, KingdomRules.Origins.Length - 1)];
			settler.SetStringProperty("KingdomOrigin", origin);
			Qud.API.ConversationsAPI.addSimpleConversationToObject(settler, "Live and drink, friend. We heard there was water here, and a place worth the walk.", "Live and drink.", Question: "Why did you come?", Answer: "The road from " + origin + " was long, and the wells there are bitter. Here the water is shared. That is the whole of it.");
			System.OriginCounts.TryGetValue(origin, out var count);
			System.OriginCounts[origin] = count + 1;
			if (Survey != null) { Survey.Consume(KingdomRules.DramsPerArrival); } else { ConsumeStoredWater(Z, KingdomRules.DramsPerArrival); }
			System.Population++;
			KingdomChronicle.Record(System, "a settler from " + origin + " arrived at " + System.KingdomDisplayName + " and drank of the shared water");
			MessageQueue.AddPlayerMessage("{{G|A settler from " + origin + " has arrived at " + System.KingdomDisplayName + ".}}");
			return true;
		}

		public static bool Emigrate(KingdomSystem System, Zone Z, KingdomSurvey Survey = null)
		{
			if (System.Population <= KingdomRules.LoyalCoreSettlers)
			{
				return false;
			}
			GameObject leaver = null;
			foreach (GameObject item in Z.GetObjects())
			{
				if (item.GetIntProperty("KingdomBorn") == 1 && item.GetIntProperty("VillageMerchant") == 0 && !item.IsPlayer() && !item.IsPlayerLed())
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
			string origin = leaver.GetStringProperty("KingdomOrigin");
			if (!string.IsNullOrEmpty(origin))
			{
				System.OriginCounts.TryGetValue(origin, out var count);
				if (count > 0)
				{
					System.OriginCounts[origin] = count - 1;
				}
			}
			leaver.Obliterate();
			System.Population--;
			KingdomChronicle.Record(System, XRL.Language.Grammar.A(name) + " left " + System.KingdomDisplayName + " for wetter country, the cisterns having run dry");
			MessageQueue.AddPlayerMessage("{{R|" + XRL.Language.Grammar.A(name, Capitalize: true) + " leaves " + System.KingdomDisplayName + ". \"There is no water here,\" " + (leaver.IsPlural ? "they say" : "the settler says") + ".}}");
			if (KingdomLog.Enabled) KingdomLog.Log("emigrate: pop now " + System.Population + " origin=" + (origin ?? "-"));
			return true;
		}

		public static int CountStoredWater(Zone Z)
		{
			int total = 0;
			foreach (GameObject item in Z.GetObjects())
			{
				LiquidVolume part = item.GetPart<LiquidVolume>();
				if (part != null && part.MaxVolume > 0 && item.GetIntProperty("KingdomStores") == 1 && part.Volume > 0 && part.GetPrimaryLiquidID() == "water")
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
				if (part != null && part.MaxVolume > 0 && item.GetIntProperty("KingdomStores") == 1 && part.Volume < part.MaxVolume && (part.Volume == 0 || part.GetPrimaryLiquidID() == "water"))
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
				if (part != null && part.MaxVolume > 0 && item.GetIntProperty("KingdomStores") == 1 && part.Volume < part.MaxVolume && (part.Volume == 0 || part.GetPrimaryLiquidID() == "water"))
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
				if (part != null && part.MaxVolume > 0 && item.GetIntProperty("KingdomStores") == 1 && part.Volume > 0 && part.GetPrimaryLiquidID() == "water")
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

		/// <summary>Counts vessels currently dedicated to the settlement's stores in a zone.</summary>
		public static int CountDedicatedVessels(Zone Z)
		{
			int total = 0;
			foreach (GameObject item in Z.GetObjects())
			{
				if (item.GetIntProperty("KingdomStores") == 1)
				{
					total++;
				}
			}
			return total;
		}

		public static int CountStorageCapacity(Zone Z)
		{
			int total = 0;
			foreach (GameObject item in Z.GetObjects())
			{
				LiquidVolume part = item.GetPart<LiquidVolume>();
				if (part != null && part.MaxVolume > 0 && item.GetIntProperty("KingdomStores") == 1)
				{
					total += part.MaxVolume;
				}
			}
			return total;
		}

		public static void UpdateStage(KingdomSystem System, Zone Z, KingdomSurvey Survey = null)
		{
			GrowthStage stage = KingdomRules.StageFor(System.Population, (Survey != null) ? Survey.StorageCapacity : CountStorageCapacity(Z));
			if (stage > System.Stage)
			{
				System.Stage = stage;
				string text = System.KingdomDisplayName + " has grown into a " + stage.ToString().ToLower();
				KingdomChronicle.Record(System, text, Accomplishment: true);
				Popup.Show("{{C|" + text + ".}}");
			}
			if (System.HasShopkeeper)
			{
				bool stillTrading = false;
				foreach (GameObject item in Z.GetObjects())
				{
					if (item.GetIntProperty("VillageMerchant") == 1 && item.GetIntProperty("KingdomCitizen") == 1)
					{
						stillTrading = true;
						break;
					}
				}
				if (!stillTrading)
				{
					System.HasShopkeeper = false;
					KingdomLog.Log("shopkeeper lost; the post reopens");
				}
			}
			if (System.Stage >= GrowthStage.Steading && !System.HasShopkeeper)
			{
				PromoteShopkeeper(System, Z);
			}
		}

		public static void PromoteShopkeeper(KingdomSystem System, Zone Z)
		{
			GameObject citizen = null;
			foreach (GameObject item in Z.GetObjects())
			{
				if (item.GetIntProperty("KingdomCitizen") == 1 && item.GetIntProperty("VillageMerchant") == 0 && !item.IsPlayer())
				{
					citizen = item;
					break;
				}
			}
			if (citizen == null)
			{
				return;
			}
			GenericInventoryRestocker restocker = citizen.RequirePart<GenericInventoryRestocker>();
			restocker.Clear();
			restocker.AddTable("Tier1Wares");
			restocker.Chance = 100;
			restocker.PerformRestock(Silent: true);
			citizen.SetIntProperty("VillageMerchant", 1);
			TakeOnRoleEvent.Send(citizen, "Merchant");
			System.HasShopkeeper = true;
			KingdomChronicle.Record(System, "a settler took up the trade, and the first stall opened at " + System.KingdomDisplayName);
			MessageQueue.AddPlayerMessage("{{G|A settler has taken up the trade. The first stall of " + System.KingdomDisplayName + " is open.}}");
		}
	}
}
