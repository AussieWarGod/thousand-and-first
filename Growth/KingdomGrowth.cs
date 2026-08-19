using System;
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
			return KingdomRules.PolicyInterval(KingdomRules.ArrivalIntervalTicks(System.Population, district), System.Gate, System.Stores);
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
			}
			int fetched = survey.Store(survey.DrawFromPools(KingdomRules.FetchableDrams(System.Population, survey.OpenWater, survey.StorageSpace)));
			System.Ledger.Fetched += fetched;
			if (fetched > 0 && KingdomLog.Enabled)
			{
				KingdomLog.Log("growth: fetched " + fetched + " drams from open water into stores");
			}
			bool heartbeatHealthy = ResolveHeartbeat(System, Z, survey, timeTicks);
			int arrivals = 0;
			while (heartbeatHealthy && timeTicks >= System.NextArrivalTick && arrivals < KingdomRules.MaxArrivalsPerVisit && System.Population < KingdomRules.MaxPopulation)
			{
				if (survey.StoredWater < KingdomRules.DramsPerArrival)
				{
					System.NextArrivalTick = timeTicks + Interval(System, Z);
					break;
				}
				if (!KingdomRules.HasRoomToHouse(System.Population, survey.Beds))
				{
					if (!System.NoRoomAnnounced)
					{
						System.NoRoomAnnounced = true;
						KingdomChronicle.Record(System, "a settler reached " + System.KingdomDisplayName + " and found no bed to claim");
						System.Ledger.Note("{{r|A settler came and found no bed. Commission housing and they will stay.}}");
					}
					System.NextArrivalTick = timeTicks + Interval(System, Z);
					break;
				}
				if (!SpawnSettler(System, Z, survey))
				{
					if (!System.NoRoomAnnounced)
					{
						System.NoRoomAnnounced = true;
						KingdomChronicle.Record(System, "a settler reached " + System.KingdomDisplayName + " and found nowhere to stand");
						System.Ledger.Note("{{r|A settler came and found nowhere to stand. There is no open ground left here.}}");
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
			AssignWork(System, survey);
			UpdateStage(System, Z, survey);
			KingdomPetitions.OnSettlementPass(System, Z, survey);
			if (KingdomLog.Enabled) KingdomLog.Log("growth pass done: pop=" + System.Population + " stage=" + System.Stage + " arrivals=" + arrivals + " dry=" + System.DryStreak + " next=" + System.NextArrivalTick);
		}

		private static bool ResolveHeartbeat(KingdomSystem System, Zone Z, KingdomSurvey Survey, long TimeTicks)
		{
			if (System.LastHeartbeatTick <= 0 || TimeTicks <= System.LastHeartbeatTick)
			{
				System.LastHeartbeatTick = TimeTicks;
				return true;
			}
			long elapsed = TimeTicks - System.LastHeartbeatTick;
			int days = KingdomRules.HeartbeatDays(elapsed);
			if (days <= 0)
			{
				return true;
			}
			System.LastHeartbeatTick = KingdomRules.HeartbeatCheckpoint(System.LastHeartbeatTick, TimeTicks);
			if (!ThirstEnabled)
			{
				RecoverFromThirst(System);
				return true;
			}
			int upkeep = KingdomRules.PolicyUpkeepForElapsed(System.Population, elapsed, System.Stores);
			int paid = Survey.Consume(upkeep);
			System.Ledger.UpkeepDrawn += paid;
			if (paid >= upkeep)
			{
				RecoverFromThirst(System);
				return true;
			}
			System.DryStreak++;
			KingdomChronicle.Record(System, "the stores ran low, and " + System.KingdomDisplayName + " thirsted");
			System.Ledger.Note("{{r|The cistern ran dry. Settlers will leave if the water does not return.}}");
			KingdomRules.ThirstOutcome outcome = KingdomRules.ResolveThirst(System.DryStreak, System.Stage, System.Population);
			if (KingdomLog.Enabled) KingdomLog.Log("thirst: days=" + days + " upkeep=" + paid + "/" + upkeep + " streak=" + System.DryStreak + " outcome=" + outcome);
			if (outcome == KingdomRules.ThirstOutcome.Emigration)
			{
				Emigrate(System, Z, Survey);
			}
			else if (outcome == KingdomRules.ThirstOutcome.Withering)
			{
				Emigrate(System, Z, Survey);
				if (!System.Withered)
				{
					System.Withered = true;
					KingdomChronicle.Record(System, System.KingdomDisplayName + " withered in the long thirst");
					System.Ledger.Note("{{R|The settlement is withering in the long thirst.}}");
				}
			}
			return false;
		}

		private static void RecoverFromThirst(KingdomSystem System)
		{
			System.DryStreak = 0;
			if (System.Withered)
			{
				System.Withered = false;
				KingdomChronicle.Record(System, "the water returned, and " + System.KingdomDisplayName + " drank deep and recovered");
				System.Ledger.Note("{{G|The water returned, and the settlement recovered.}}");
			}
		}

		/// <summary>
		/// Picks which of the settler blueprints is walking up the road. The roster lives in the
		/// <c>r_KingdomSettlers</c> population table so other mods can put their own people on it
		/// by merging a line, and so the mix can be retuned without touching code.
		/// <para>
		/// Falls back to the base blueprint if the table is missing or rolls nothing: a settlement
		/// that stops growing because a table was overridden badly is a worse failure than a
		/// settlement whose arrivals all look alike.
		/// </para>
		/// </summary>
		public static string SettlerBlueprint()
		{
			try
			{
				PopulationResult result = PopulationManager.RollOneFrom("r_KingdomSettlers");
				if (result != null && !string.IsNullOrEmpty(result.Blueprint) && GameObjectFactory.Factory.HasBlueprint(result.Blueprint))
				{
					return result.Blueprint;
				}
			}
			catch (Exception error)
			{
				MetricsManager.LogError("ThousandAndFirst settler roll", error);
			}
			return "r_KingdomSettler";
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
			GameObject settler = GameObject.Create(SettlerBlueprint());
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
			string given = XRL.Names.NameMaker.MakeName(settler, null, null, "human", null, System.KingdomFactionName, null, null, null, null, null, null, null, FailureOkay: true);
			if (!string.IsNullOrEmpty(given))
			{
				settler.DisplayName = given;
				settler.SetStringProperty("KingdomName", given);
				System.RosterNames.Add(given);
				System.RosterOrigins.Add(origin);
				System.RosterArrived.Add(XRL.World.Calendar.GetDay() + " of " + XRL.World.Calendar.GetMonth() + ", " + XRL.World.Calendar.GetYear() + " AR");
			}
			Qud.API.ConversationsAPI.addSimpleConversationToObject(settler, "Live and drink, friend. We heard there was water here, and a place worth the walk.", "Live and drink.", Question: "Why did you come?", Answer: "The road from " + origin + " was long, and the wells there are bitter. Here the water is shared. That is the whole of it.");
			System.OriginCounts.TryGetValue(origin, out var count);
			System.OriginCounts[origin] = count + 1;
			if (Survey != null) { Survey.Consume(KingdomRules.DramsPerArrival); } else { ConsumeStoredWater(Z, KingdomRules.DramsPerArrival); }
			System.Population++;
			string reason = KingdomRules.ArrivalReason(System.LastDeed, The.Game.TimeTicks - System.LastDeedTick, origin);
			KingdomChronicle.Record(System, reason + ", and a settler came to " + System.KingdomDisplayName + " and drank of the shared water");
			System.Ledger.Arrivals++;
			System.Ledger.Note("{{G|" + XRL.Language.Grammar.InitCap(reason) + " - a settler has come.}}");
			System.Ledger.ArrivalCost += KingdomRules.DramsPerArrival;
			return true;
		}

		/// <summary>
		/// Crews the settlement's works from its citizens, in placement order. A work without
		/// its crew is idle, not broken: it keeps its charge and its contents and simply does
		/// not run, and the settlement says which works want hands.
		/// </summary>
		public static void AssignWork(KingdomSystem System, KingdomSurvey Survey)
		{
			if (Survey.Works.Count == 0)
			{
				return;
			}
			int[] demands = new int[Survey.Works.Count];
			bool[] thresholds = new bool[Survey.Works.Count];
			for (int i = 0; i < Survey.Works.Count; i++)
			{
				demands[i] = Survey.Works[i].GetIntProperty("KingdomStaffNeeded");
				thresholds[i] = Survey.Works[i].GetIntProperty("KingdomThresholdManning") == 1;
			}
			int[] crew = KingdomRules.AssignCrew(System.Population, demands, thresholds);
			int idle = 0;
			int shorthanded = 0;
			for (int j = 0; j < Survey.Works.Count; j++)
			{
				GameObject work = Survey.Works[j];
				int effectiveness = KingdomRules.CrewEffectiveness(crew[j], demands[j]);
				work.SetIntProperty("KingdomStaffed", (effectiveness > 0) ? 1 : 0);
				work.SetIntProperty("KingdomEffectiveness", effectiveness);
				if (effectiveness <= 0)
				{
					idle++;
				}
				else
				{
					if (effectiveness < 100)
					{
						shorthanded++;
					}
					if (work.GetIntProperty("KingdomHandCranked") == 1)
					{
						Capacitor capacitor = work.GetPart<Capacitor>();
						if (capacitor != null)
						{
							int target = capacitor.MaxCharge * effectiveness / 100;
							if (capacitor.Charge < target)
							{
								capacitor.Charge = target;
							}
						}
					}
				}
			}
			System.ShorthandedWorks = shorthanded;
			System.IdleWorks = idle;
			if (idle > 0 && !System.IdleWorksAnnounced)
			{
				System.IdleWorksAnnounced = true;
				MessageQueue.AddPlayerMessage("{{r|" + idle + " of the works of " + System.KingdomDisplayName + " stand idle for want of hands.}}");
			}
			else if (idle == 0)
			{
				System.IdleWorksAnnounced = false;
			}
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
			string roll = leaver.GetStringProperty("KingdomName");
			if (!string.IsNullOrEmpty(roll))
			{
				int at = System.RosterNames.IndexOf(roll);
				if (at >= 0)
				{
					System.RosterNames.RemoveAt(at);
					if (at < System.RosterOrigins.Count)
					{
						System.RosterOrigins.RemoveAt(at);
					}
					if (at < System.RosterArrived.Count)
					{
						System.RosterArrived.RemoveAt(at);
					}
				}
			}
			leaver.Obliterate();
			System.Population--;
			KingdomChronicle.Record(System, XRL.Language.Grammar.A(name) + " left " + System.KingdomDisplayName + " for wetter country, the cisterns having run dry");
			System.Ledger.Departures++;
			System.Ledger.Note("{{R|" + XRL.Language.Grammar.A(name, Capitalize: true) + " left for wetter country. \"There is no water here,\" " + (leaver.IsPlural ? "they said" : "the settler said") + ".}}");
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
					drained += KingdomLiquids.Drain(part, fetchable - drained);
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
					stored += KingdomLiquids.Fill(part, "water", drams);
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
					remaining -= KingdomLiquids.Drain(part, remaining);
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

		/// <summary>Counts beds the settlement built. These are the population ceiling.</summary>
		public static int CountBeds(Zone Z)
		{
			int total = 0;
			foreach (GameObject item in Z.GetObjects())
			{
				if (item.GetIntProperty("KingdomBuilt") == 1 && item.HasPart("Bed"))
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
				System.RecordDeed("the growth of " + System.KingdomDisplayName + "");
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
			int tier = KingdomRules.ShopTierForStage(System.Stage);
			if (System.HasShopkeeper && tier > System.ShopTier)
			{
				RestockShops(System, Z, tier);
			}
		}

		/// <summary>
		/// Raises the settlement's shops to a new stock tier: the trader's stock table and the
		/// per-creature InventoryTier both climb, and the shelves are restocked at once so the
		/// change is visible the moment the player next trades.
		/// </summary>
		public static void RestockShops(KingdomSystem System, Zone Z, int Tier)
		{
			int raised = 0;
			foreach (GameObject item in Z.GetObjects())
			{
				if (item.GetIntProperty("VillageMerchant") != 1 || item.GetIntProperty("KingdomCitizen") != 1)
				{
					continue;
				}
				GenericInventoryRestocker restocker = item.GetPart<GenericInventoryRestocker>();
				if (restocker == null)
				{
					continue;
				}
				restocker.Clear();
				restocker.AddTable("Tier" + Tier + "Wares");
				restocker.Chance = 100;
				item.SetIntProperty("InventoryTier", Tier);
				restocker.PerformRestock(Silent: true);
				raised++;
			}
			if (raised > 0)
			{
				System.ShopTier = Tier;
				KingdomChronicle.Record(System, "the stalls of " + System.KingdomDisplayName + " began carrying finer goods");
				MessageQueue.AddPlayerMessage("{{G|The traders of " + System.KingdomDisplayName + " have better wares to show you.}}");
				if (KingdomLog.Enabled) KingdomLog.Log("shops raised to tier " + Tier + " (" + raised + " traders)");
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
