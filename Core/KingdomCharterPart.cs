using System;
using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	[Serializable]
	public class KingdomCharterPart : IPart
	{
		public const string COMMAND = "r_KingdomCharterMenu";

		public Guid ActivatedAbilityID = Guid.Empty;

		// Mid-session mod rebuilds mint a second assembly generation; the stale part shares
		// this name but not this Type, so RequirePart cannot see it. Purge by name.
		public override void Attach()
		{
			base.Attach();
			for (int num = ParentObject.PartsList.Count - 1; num >= 0; num--)
			{
				IPart part = ParentObject.PartsList[num];
				if (part != this && part.GetType().Name == "KingdomCharterPart")
				{
					ParentObject.PartsList.RemoveAt(num);
				}
			}
		}

		public override void Register(GameObject Object, IEventRegistrar Registrar)
		{
			Registrar.Register(COMMAND);
			base.Register(Object, Registrar);
		}

		public void EnsureAbility()
		{
			if (ActivatedAbilityID != Guid.Empty)
			{
				return;
			}
			if (ParentObject.ActivatedAbilities != null && ParentObject.ActivatedAbilities.AbilityByGuid != null)
			{
				foreach (System.Collections.Generic.KeyValuePair<Guid, ActivatedAbilityEntry> item in ParentObject.ActivatedAbilities.AbilityByGuid)
				{
					if (item.Value.Command == COMMAND)
					{
						ActivatedAbilityID = item.Key;
						return;
					}
				}
			}
			ActivatedAbilityID = AddMyActivatedAbility("Charter", COMMAND, "Skills");
		}

		public void RemoveAbility()
		{
			RemoveMyActivatedAbility(ref ActivatedAbilityID);
		}

		public override bool FireEvent(Event E)
		{
			if (E.ID == COMMAND)
			{
				OpenMenu();
			}
			return base.FireEvent(E);
		}

		public void OpenMenu()
		{
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			if (!system.Founded)
			{
				Popup.Show("You rule nothing yet.");
				return;
			}
			while (true)
			{
				int num = Popup.PickOption(Title: system.SeatName + KingdomSettlement.VocationSuffix(system.Vocation), Options: new string[15] { (system.PetitionKind != KingdomRules.PetitionKind.None) ? ("{{W|Hear " + system.PetitionPetitioner + "}}") : "{{K|No one is waiting to speak}}", "Status", "What happened while you were away", "The Chronicle", "As others tell it", "Standings", "The roll of settlers", "Standing policy", "Designate district", "Commission a building", "Answer a threat", "Dedicate a vessel or larder", "Strike a trade charter", "Send a water manifest", "Share a meal from the larder" }, Hotkeys: new char[15] { 'h', 's', 'w', 'c', 'a', 'n', 'l', 'p', 'd', 'm', 't', 'v', 'r', 'i', 'f' }, AllowEscape: true);
				switch (num)
				{
				case 0:
					HearPetition(system);
					break;
				case 1:
					Popup.Show(KingdomReports.Status(system));
					break;
				case 2:
					ShowHomecoming(system);
					break;
				case 3:
					Popup.Show(KingdomReports.Chronicle(system));
					break;
				case 4:
					Popup.Show(KingdomReports.Chronicle(system, Outsider: true));
					break;
				case 5:
					Popup.Show(KingdomReports.Standings(system));
					break;
				case 6:
					Popup.Show(KingdomReports.Roll(system));
					break;
				case 7:
					SetPolicy(system);
					break;
				case 8:
					DesignateDistrict(system);
					break;
				case 9:
					CommissionBuilding(system);
					break;
				case 10:
					AnswerThreat(system);
					break;
				case 11:
					DedicateVessel(system);
					break;
				case 12:
					StrikeTradeCharter(system);
					break;
				case 13:
					LoadManifest(system);
					break;
				case 14:
					HoldSharedMeal(system);
					break;
				default:
					return;
				}
			}
		}

		/// <summary>
		/// The homecoming report, asked for rather than pushed. The settlement says it has news
		/// on the way in (nonmodal); this is where the founder reads it, if they want it.
		/// </summary>
		public void ShowHomecoming(KingdomSystem System)
		{
			if (!System.Ledger.Any)
			{
				Popup.Show("Nothing has happened here since you last stood on this ground.");
				return;
			}
			Popup.Show(System.Ledger.Digest(System.SeatName, System.HomecomingDays));
		}

		/// <summary>Hears the settler who is waiting, and lets the founder decline.</summary>
		public void HearPetition(KingdomSystem System)
		{
			if (System.PetitionKind == KingdomRules.PetitionKind.None)
			{
				Popup.Show("No one is waiting. The settlement is content, or too busy to complain.");
				return;
			}
			int num = Popup.PickOption(Title: System.PetitionPetitioner + " of " + System.SeatName, Intro: KingdomPetitions.Speech(System), Options: new string[2] { "Say it will be seen to", "Tell them it must wait" }, AllowEscape: true);
			if (num == 1)
			{
				KingdomChronicle.Record(System, System.PetitionPetitioner + " was told the matter must wait");
				KingdomPetitions.Close(System);
				Popup.Show("They nod, and go back to work. Nothing is held against you; the thing simply remains undone.");
			}
		}

		/// <summary>
		/// Standing policy: the founder sets intent once and the settlement lives by it. Both
		/// choices trade one good thing for another, so neither is correct.
		/// </summary>
		public void SetPolicy(KingdomSystem System)
		{
			while (true)
			{
				int num = Popup.PickOption(Title: "The standing policy of " + System.SeatName, Options: new string[2]
				{
					"Gates: {{C|" + KingdomRules.GatePolicyNames[(int)System.Gate] + "}} — " + KingdomRules.GatePolicyBlurbs[(int)System.Gate],
					"Stores: {{C|" + KingdomRules.StoresPolicyNames[(int)System.Stores] + "}} — " + KingdomRules.StoresPolicyBlurbs[(int)System.Stores]
				}, AllowEscape: true);
				if (num < 0)
				{
					return;
				}
				if (num == 0)
				{
					System.Gate = (System.Gate == KingdomRules.GatePolicy.Open) ? KingdomRules.GatePolicy.Guarded : KingdomRules.GatePolicy.Open;
					KingdomChronicle.Record(System, System.SeatName + " set its gates " + ((System.Gate == KingdomRules.GatePolicy.Open) ? "open to all comers" : "under the watch"));
				}
				else
				{
					System.Stores = (System.Stores == KingdomRules.StoresPolicy.Plenty) ? KingdomRules.StoresPolicy.Thrift : KingdomRules.StoresPolicy.Plenty;
					KingdomChronicle.Record(System, "the water-keepers of " + System.SeatName + " were told to " + ((System.Stores == KingdomRules.StoresPolicy.Thrift) ? "ration" : "pour freely"));
				}
			}
		}

		public void DesignateDistrict(KingdomSystem System)
		{
			Zone zone = ParentObject.CurrentZone;
			if (zone == null || !System.ClaimedZones.Contains(zone.ZoneID))
			{
				Popup.Show("Districts are declared on the kingdom's own ground.");
				return;
			}
			int num = Popup.PickOption(Title: "Declare this ground", Options: KingdomRules.DistrictNames, AllowEscape: true);
			if (num >= 0)
			{
				string district = KingdomRules.Districts[num];
				System.ZoneDistricts[zone.ZoneID] = district;
				KingdomChronicle.Record(System, "the ground here was named the " + KingdomRules.DistrictName(district) + " of " + System.SeatName);
				Popup.Show("This ground is the {{C|" + KingdomRules.DistrictName(district) + "}} of " + System.SeatName + ".");
			}
		}

		public void CommissionBuilding(KingdomSystem System)
		{
			Zone zone = ParentObject.CurrentZone;
			int stored = (zone != null) ? KingdomGrowth.CountStoredWater(zone) : 0;
			System.Collections.Generic.List<KingdomRules.BuildEntry> available = new System.Collections.Generic.List<KingdomRules.BuildEntry>();
			foreach (KingdomRules.BuildEntry entry in KingdomData.Buildings)
			{
				if (KingdomRules.StyleAllows(entry.Styles, System.Style) && System.Stage >= entry.MinStage)
				{
					available.Add(entry);
				}
			}
			if (available.Count == 0)
			{
				Popup.Show("No designs are known here.");
				return;
			}
			string[] options = new string[available.Count];
			for (int i = 0; i < available.Count; i++)
			{
				options[i] = available[i].DisplayName + " {{C|[" + available[i].CostDrams + " drams]}}";
			}
			int num = Popup.PickOption(Title: "Commission ({{C|" + stored + " drams}} in the stores)", Options: options, AllowEscape: true);
			if (num >= 0)
			{
				if (!KingdomCommission.Commission(System, available[num].Key, out var failure))
				{
					Popup.Show(failure);
				}
			}
		}

		/// <summary>The three exits from a threat: pay, talk, or meet it.</summary>
		public void AnswerThreat(KingdomSystem System)
		{
			if (System.RaidState != 1)
			{
				Popup.Show("Nothing threatens the kingdom just now.");
				return;
			}
			Zone zone = ParentObject.CurrentZone;
			if (zone == null || !System.ClaimedZones.Contains(zone.ZoneID))
			{
				Popup.Show("Answer a threat from the ground it threatens. Go there.");
				return;
			}
			int demand = KingdomRules.TributeDemand(KingdomRules.RaidTributeDrams, System.RaidTimesDeferred);
			bool canTalk = KingdomRules.CanTalkDown(System.GetStanding(System.RaidFactionName), System.RaidTimesDeferred);
			int num = Popup.PickOption(Title: "Scouts of " + Faction.GetFormattedName(System.RaidFactionName) + " are watching the stores", Options: new string[3]
			{
				"Pay tribute in water {{C|[" + demand + " drams]}}",
				canTalk ? "Send word and trade on our standing {{G|[free]}}" : "{{K|Send word (they hold us in too little regard)}}",
				"Let them come, and meet them {{r|[the demand grows if they are not answered]}}"
			}, AllowEscape: true);
			switch (num)
			{
			case 0:
				if (!KingdomRaids.TryTribute(System, zone, out var payFail))
				{
					Popup.Show(payFail);
				}
				break;
			case 1:
				if (!KingdomRaids.TryTalkDown(System, out var talkFail))
				{
					Popup.Show(talkFail);
				}
				break;
			case 2:
				System.RaidTimesDeferred++;
				Popup.Show("You let the demand stand. They will come, and what they ask next time will be more.");
				break;
			}
		}

		public void StrikeTradeCharter(KingdomSystem System)
		{
			System.Collections.Generic.List<KingdomRules.DealEntry> deals = KingdomData.Deals;
			if (deals.Count == 0)
			{
				Popup.Show("No charters are known.");
				return;
			}
			string[] dealOptions = new string[deals.Count];
			for (int i = 0; i < deals.Count; i++)
			{
				dealOptions[i] = deals[i].DisplayName + " {{C|[standing " + deals[i].MinStanding + "+]}}";
			}
			int dealPick = Popup.PickOption(Title: "Which charter?", Options: dealOptions, AllowEscape: true);
			if (dealPick < 0)
			{
				return;
			}
			System.Collections.Generic.List<string> eligible = new System.Collections.Generic.List<string>();
			System.Collections.Generic.List<string> labels = new System.Collections.Generic.List<string>();
			foreach (Faction faction in Factions.Loop())
			{
				if (faction.Visible && faction.Name != System.KingdomFactionName && faction.Name != "Player" && System.GetStanding(faction.Name) >= deals[dealPick].MinStanding)
				{
					eligible.Add(faction.Name);
					labels.Add(faction.DisplayName + " (standing " + System.GetStanding(faction.Name) + ")");
					if (eligible.Count >= 20)
					{
						break;
					}
				}
			}
			if (eligible.Count == 0)
			{
				Popup.Show("No faction holds the kingdom in high enough regard for that charter.");
				return;
			}
			int factionPick = Popup.PickOption(Title: "With whom?", Options: labels.ToArray(), AllowEscape: true);
			if (factionPick >= 0)
			{
				if (!KingdomTrade.StrikeDeal(System, deals[dealPick].Key, eligible[factionPick], out var failure))
				{
					Popup.Show(failure);
				}
			}
		}

		/// <summary>
		/// Loads the realm's one in-flight water manifest, drawing drams from the stores of
		/// whichever city the founder is standing in and addressing them to its sibling. The
		/// draw is immediate and physical, through <see cref="KingdomGrowth.ConsumeStoredWater"/>
		/// &mdash; the same measured-delta path every other charge on the stores uses &mdash; so
		/// nothing is promised before it is actually taken.
		/// </summary>
		public void LoadManifest(KingdomSystem System)
		{
			Zone zone = ParentObject.CurrentZone;
			bool onGround = zone != null && System.ClaimedZones.Contains(zone.ZoneID);
			if (onGround)
			{
				// A load attempt is itself a witnessed moment: a manifest that already missed
				// its window is cleared here rather than left blocking the one in-flight slot
				// until the founder happens to visit whichever city it was bound for.
				KingdomManifest lapsed = KingdomTrade.ExpireManifestIfStale(System, The.Game.TimeTicks);
				if (lapsed != null)
				{
					Popup.Show(KingdomManifestRules.ManifestLapseNotice(lapsed.OriginName, lapsed.DestinationName, lapsed.Drams));
					return;
				}
			}
			bool hasSecondCity = System.Away != null;
			int stored = onGround ? KingdomGrowth.CountStoredWater(zone) : 0;
			int amount = KingdomManifestRules.ManifestAmount(stored, System.Population);
			KingdomManifestRules.ManifestVerdict verdict = KingdomManifestRules.JudgeManifest(onGround, hasSecondCity, System.Manifest != null, amount);
			if (verdict == KingdomManifestRules.ManifestVerdict.AlreadyInFlight)
			{
				Popup.Show(KingdomManifestRules.ManifestInFlightStatus(System.Manifest.OriginName, System.Manifest.DestinationName, System.Manifest.Drams, The.Game.TimeTicks, System.Manifest.DeadlineTick));
				return;
			}
			if (verdict != KingdomManifestRules.ManifestVerdict.Allowed)
			{
				Popup.Show(KingdomManifestRules.ManifestRefusal(verdict, System.Away?.SettlementName));
				return;
			}
			// The price is named before the water moves. Every other spending action in this
			// menu tells the founder what it costs and lets them back out; a manifest sends the
			// largest single amount of water in the mod, and must not be the exception.
			if (Popup.ShowYesNo("Send {{C|" + amount + " drams}} from " + System.SeatName + " to " + System.Away.SettlementName
				+ "?\n\nThe water leaves the stores here now. It arrives when you next stand in "
				+ System.Away.SettlementName + ", and if you have not come within "
				+ KingdomManifestRules.ManifestWindowDays + " days the carters turn back and bring it home.") != DialogResult.Yes)
			{
				return;
			}
			int drawn = KingdomGrowth.ConsumeStoredWater(zone, amount);
			if (drawn <= 0)
			{
				Popup.Show(KingdomManifestRules.ManifestRefusal(KingdomManifestRules.ManifestVerdict.StoresCannotSpare, System.Away.SettlementName));
				return;
			}
			long now = The.Game.TimeTicks;
			string origin = System.SeatName;
			string destination = System.Away.SettlementName;
			System.Manifest = new KingdomManifest
			{
				OriginName = origin,
				DestinationName = destination,
				Drams = drawn,
				LoadedTick = now,
				DeadlineTick = KingdomManifestRules.ManifestDeadline(now)
			};
			KingdomChronicle.Record(System, "the water-keepers of " + origin + " sent " + drawn + " drams toward " + destination);
			Popup.Show("{{G|" + drawn + " drams leave the stores of " + origin + ", bound for " + destination + ".}} The road is given " + KingdomManifestRules.ManifestWindowDays + " days; stand in " + destination + " within that time and the water will already be waiting in its stores.");
			KingdomLog.Log("manifest: loaded " + drawn + " from " + origin + " to " + destination + " deadline=" + System.Manifest.DeadlineTick);
		}

		public void DedicateVessel(KingdomSystem System)
		{
			Cell cell = ParentObject.CurrentCell;
			if (cell == null)
			{
				return;
			}
			Zone zone = ParentObject.CurrentZone;
			if (zone == null || !System.ClaimedZones.Contains(zone.ZoneID))
			{
				Popup.Show("Vessels are dedicated on the kingdom's own ground, not in other people's houses.");
				return;
			}
			System.Collections.Generic.List<GameObject> vessels = new System.Collections.Generic.List<GameObject>();
			System.Collections.Generic.List<GameObject> larders = new System.Collections.Generic.List<GameObject>();
			foreach (Cell adjacentCell in cell.GetLocalAdjacentCells())
			{
				foreach (GameObject item in adjacentCell.GetObjectsWithPart("LiquidVolume"))
				{
					if (item.GetPart<XRL.World.Parts.LiquidVolume>().MaxVolume > 0)
					{
						vessels.Add(item);
					}
				}
				// A larder is anything that holds things rather than liquid: a chest, a
				// footlocker, a shelf. Water and food are accounted separately, by different
				// people, so they carry different marks and different caps.
				foreach (GameObject item in adjacentCell.GetObjects())
				{
					if (item.Inventory != null && item.GetPart<XRL.World.Parts.LiquidVolume>() == null && !larders.Contains(item))
					{
						larders.Add(item);
					}
				}
			}
			if (vessels.Count == 0 && larders.Count == 0)
			{
				Popup.Show("Stand beside a vessel or a store to dedicate it. What is dedicated feeds the settlement; what is not is yours alone, and no settler will touch it.");
				return;
			}
			string[] options = new string[vessels.Count + larders.Count + 1];
			options[0] = "{{W|Dedicate everything here}}";
			for (int i = 0; i < vessels.Count; i++)
			{
				options[i + 1] = vessels[i].ShortDisplayName + ((vessels[i].GetIntProperty("KingdomStores") == 1) ? " {{G|[dedicated]}}" : " {{K|[personal]}}");
			}
			for (int i = 0; i < larders.Count; i++)
			{
				options[vessels.Count + i + 1] = larders[i].ShortDisplayName + " {{K|(larder)}}" + ((larders[i].GetIntProperty("KingdomLarder") == 1) ? " {{G|[dedicated]}}" : " {{K|[personal]}}");
			}
			int num = Popup.PickOption(Title: "Dedicate or release", Options: options, AllowEscape: true);
			if (num == 0)
			{
				int dedicated = 0;
				int room = KingdomRules.MaxDedicatedVessels - KingdomGrowth.CountDedicatedVessels(zone);
				foreach (GameObject candidate in vessels)
				{
					if (room <= 0)
					{
						break;
					}
					if (candidate.GetIntProperty("KingdomStores") != 1)
					{
						candidate.SetIntProperty("KingdomStores", 1);
						dedicated++;
						room--;
					}
				}
				int larderRoom = KingdomRules.MaxDedicatedLarders - KingdomGrowth.CountDedicatedLarders(zone);
				foreach (GameObject candidate in larders)
				{
					if (larderRoom <= 0)
					{
						break;
					}
					if (candidate.GetIntProperty("KingdomLarder") != 1)
					{
						candidate.SetIntProperty("KingdomLarder", 1);
						dedicated++;
						larderRoom--;
					}
				}
				Popup.Show((dedicated > 0) ? (dedicated + " are dedicated to the stores of " + System.SeatName + ".") : "Everything here is already dedicated, or the keepers can account for no more.");
				return;
			}
			if (num > 0 && num <= vessels.Count)
			{
				GameObject vessel = vessels[num - 1];
				if (vessel.GetIntProperty("KingdomStores") != 1 && KingdomGrowth.CountDedicatedVessels(zone) >= KingdomRules.MaxDedicatedVessels)
				{
					Popup.Show("The stores are already as many vessels as the water-keepers can account for.");
					return;
				}
				if (vessel.GetIntProperty("KingdomStores") == 1)
				{
					vessel.SetIntProperty("KingdomStores", 0);
					Popup.Show("The " + vessel.ShortDisplayName + " is yours alone again.");
				}
				else
				{
					vessel.SetIntProperty("KingdomStores", 1);
					Popup.Show("The " + vessel.ShortDisplayName + " is dedicated to the stores of " + System.SeatName + ".");
				}
				return;
			}
			if (num > vessels.Count)
			{
				GameObject larder = larders[num - vessels.Count - 1];
				if (larder.GetIntProperty("KingdomLarder") != 1 && KingdomGrowth.CountDedicatedLarders(zone) >= KingdomRules.MaxDedicatedLarders)
				{
					Popup.Show("The settlement keeps as many larders as anyone can keep an honest account of.");
					return;
				}
				if (larder.GetIntProperty("KingdomLarder") == 1)
				{
					larder.SetIntProperty("KingdomLarder", 0);
					Popup.Show("The " + larder.ShortDisplayName + " is yours alone again. Nothing in it will be counted.");
				}
				else
				{
					// Dedication is a mark, not a transfer: what is inside stays where it is and
					// stays the founder's. The settlement only counts it.
					larder.SetIntProperty("KingdomLarder", 1);
					Popup.Show("The " + larder.ShortDisplayName + " is a larder of " + System.SeatName + " now. What is in it is counted, and still yours.");
				}
			}
		}

		/// <summary>
		/// Calls a shared meal from the ground's dedicated larders. The service does its own
		/// eligibility check and success messaging; this only surfaces a decline, matching
		/// every other action here.
		/// </summary>
		public void HoldSharedMeal(KingdomSystem System)
		{
			Zone zone = ParentObject.CurrentZone;
			// Asked before anything is eaten. The food is the founder's - dedicating a larder is
			// consent to it being counted, not consent to it being spent without a word.
			KingdomSurvey survey = (zone != null) ? KingdomSurvey.Take(zone, System) : null;
			if (survey != null && survey.FoodAbundance != KingdomRules.PantryTier.Empty)
			{
				int cost = KingdomRules.MealCost(survey.FoodAbundance);
				if (Popup.ShowYesNo("Call " + KingdomRules.MealSizeName(survey.FoodAbundance) + " for " + System.SeatName
					+ "?\n\nIt will take {{C|" + cost + "}} of the " + survey.FoodStored
					+ " the larders hold.") != DialogResult.Yes)
				{
					return;
				}
			}
			if (!KingdomLarder.HoldSharedMeal(System, zone, out var failure))
			{
				Popup.Show(failure);
			}
		}
	}
}
