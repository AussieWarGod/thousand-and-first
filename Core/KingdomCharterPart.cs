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
				// Taken hotkeys, all thirty-five of them: every letter a-z is spoken for, so a
				// new entry takes the next digit. Used so far: h s w c a n l p d m t v r i f e u
				// g b j k o y x q z and the digits 1 2 3 4 5 6 7 8 9. Check this list before
				// adding an entry - a duplicated hotkey silently picks whichever option comes
				// first, and that has bitten this file before. ONE digit is left. When it goes,
				// the next entry is a chapter inside an existing one, the way the city book
				// (W5, '7') holds six readings behind one letter - not a second Charter.
				int num = Popup.PickOption(Title: system.SeatName + KingdomSettlement.VocationSuffix(system.Vocation), Options: new string[35] { (system.PetitionKind != KingdomRules.PetitionKind.None) ? ("{{W|Hear " + system.PetitionPetitioner + "}}") : "{{K|No one is waiting to speak}}", "Status", "What happened while you were away", "The Chronicle", "As others tell it", "Standings", "The roll of settlers", "Standing policy", "Designate district", "Commission a building", "Answer a threat", "Dedicate a vessel, larder, or stockpile", "Strike a trade charter", "Send a water manifest", "Share a meal from the larder", "Certify a machine", "Set the water detail", "Plans staked for later", "Adopt a building", "Release an adoption", (system.SettlementCount >= 2 || system.Seceded != null) ? "How your cities hold each other" : "{{K|One city cannot fall out with itself}}", "What the keepers know", "Your works, and what they become", "Name a building", "Set the crew on the ground", "Take down a building", "Post a price at the heart", "Change what a plot is", "Give a building a new look", "Consecrate a shrine", "Share water with a settler", "Claim this ground", "The book of the city", "Where the keepers' craft could go", "What the city is asking for"}, Hotkeys: new char[35] { 'h', 's', 'w', 'c', 'a', 'n', 'l', 'p', 'd', 'm', 't', 'v', 'r', 'i', 'f', 'e', 'u', 'g', 'b', 'j', 'k', 'o', 'y', 'x', 'q', 'z', '1', '2', '3', '4', '5', '6', '7', '8', '9'}, AllowEscape: true);
				switch (num)
				{
				case 0:
					HearPetition(system);
					break;
				case 1:
					Popup.Show(KingdomReports.Status(system, ParentObject?.CurrentZone));
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
				case 16:
					SetWaterDetail(system);
					break;
				case 15:
					CertifyMachine(system);
					break;
				case 17:
					ManagePlans(system);
					break;
				case 18:
					AdoptBuilding(system);
					break;
				case 19:
					ReleaseBuilding(system);
					break;
				case 20:
					ManageCreed(system);
					break;
				case 21:
					KingdomZoning.ShowKeepers(system);
					break;
				case 22:
					KingdomYards.ShowWorksAndTrades(system);
					break;
				case 23:
					KingdomDesign.RenameBuilding(system, ParentObject);
					break;
				case 24:
					GroundWork(system);
					break;
				case 25:
					StrikeBuilding(system);
					break;
				case 26:
					KingdomBounty.OpenNotices(system, ParentObject);
					break;
				case 27:
					KingdomSocket.OpenConvert(system, ParentObject);
					break;
				case 28:
					KingdomSocket.OpenRedress(system, ParentObject);
					break;
				case 29:
					KingdomFaith.OpenConsecration(system, ParentObject);
					break;
				case 30:
					KingdomWaterRite.OpenRite(system, ParentObject);
					break;
				case 31:
					ClaimGround(system);
					break;
				// W5. Three readings, and nothing on any of them can be pressed: the city book
				// (LIVING-CITY-ARCHITECTURE §5's works board and everything beside it), the
				// keepers' map (DIVERSITY-AND-TECH-TREES §2.8 - a MAP, never a spend), and what
				// the city is asking for (§5's petitions and bounties, issued from model state).
				case 32:
					Simulation.City.KingdomBookReport.Open(system);
					break;
				case 33:
					Popup.Show(KingdomTechMap.Draw(system));
					break;
				case 34:
					Popup.Show(KingdomAsks.Board(system));
					break;
				default:
					return;
				}
			}
		}

		/// <summary>
		/// Takes the ground the founder is standing on into the seated city.
		/// <para>
		/// The one verb the whole multi-zone half of the design was waiting on. Everything behind
		/// it was already built and already tested and had nothing to exercise it: growth,
		/// commission, plots, yards, sockets, faith and the locus all gate on
		/// <c>ClaimedZones.Contains</c>, districts are a real per-zone map, eight designs gate on
		/// <c>MinZones</c>, and the wall line is recomputed against the whole claim on every
		/// placement. In normal play a city held exactly one parasang forever, because
		/// <c>KingdomFounding.ClaimZone</c>'s only callers were the founding rite and two debug
		/// wishes. This is the founder's own claim.
		/// </para>
		/// <para>
		/// <b>It costs nothing, and that is a decision.</b> The brief prices the founding rite (a
		/// basin of fresh water) and prices every building, and names no price at all for a
		/// claim: expansion direction is "entirely the founder's". What a claim actually costs is
		/// paid afterwards and in kind &mdash; a new parasang is a new wall line to raise, a new
		/// budget of ground to lay, and a stage that must have been earned first. Inventing a
		/// dram price here would be inventing a ruling nobody made.
		/// </para>
		/// <para>
		/// Every refusal names what would lift it, and a claim that goes through says what it did
		/// to the wall line &mdash; including when the answer is "nothing", which is the honest
		/// answer for ground taken diagonally across a corner or straight down into the rock.
		/// </para>
		/// </summary>
		public void ClaimGround(KingdomSystem System)
		{
			Zone zone = ParentObject.CurrentZone;
			KingdomZoningRules.ClaimVerdict verdict = KingdomFounding.JudgeClaim(System, zone);
			if (verdict != KingdomZoningRules.ClaimVerdict.Allowed)
			{
				Popup.Show(KingdomZoningRules.ClaimRefusal(verdict, System.SeatName, System.Stage));
				return;
			}
			// The wall line is measured on the ground the city ALREADY holds, before and after,
			// with the same zones asked both times: what changes is which neighbours exist, and
			// an edge that stops facing the world is an edge that stops being wall ground.
			// Measuring the new zone's own edges instead would answer nothing - a zone is not its
			// own neighbour, so its frontier reads the same either side of the claim.
			System.Collections.Generic.List<string> held = new System.Collections.Generic.List<string>(System.ClaimedZones);
			int before = WorldFacingEdges(held, System.ClaimedZones);
			if (Popup.ShowYesNo("Take " + XRL.Language.Grammar.GetProsaicZoneName(zone) + " into {{C|" + System.SeatName
				+ "}}? Nothing is spent. This ground becomes the city's: its plots, its wall line, its district to name.") != DialogResult.Yes)
			{
				return;
			}
			if (!KingdomFounding.ClaimZone(zone))
			{
				// The primitive judges the same ground a second time and is entitled to disagree
				// - it is the one that actually writes the zone property. Refused rather than
				// reported as done.
				Popup.Show("The claim did not hold. This ground is not the city's.");
				return;
			}
			int after = WorldFacingEdges(held, System.ClaimedZones);
			Popup.Show("{{G|" + System.SeatName + " holds " + XRL.Language.Grammar.GetProsaicZoneName(zone) + ".}}\n\n"
				+ KingdomZoningRules.ClaimedWallClause(before, after, System.SeatName)
				+ "\n\n" + KingdomZoningRules.ClaimHoldingLine(System.ClaimedZones.Count, KingdomZoningRules.ZonesForStage(System.Stage)));
		}

		/// <summary>How many zone edges the named ground turns to the world, summed, judged
		/// against a claim. Asked either side of a claim with the same ground and a widened claim,
		/// which is the difference the founder is told about.</summary>
		private static int WorldFacingEdges(System.Collections.Generic.IList<string> Held, System.Collections.Generic.List<string> Claim)
		{
			int edges = 0;
			for (int i = 0; (Held != null) && i < Held.Count; i++)
			{
				edges += KingdomZoningRules.EdgeCount(KingdomRules.FrontierEdges(Held[i], Claim));
			}
			return edges;
		}

		/// <summary>
		/// The water detail: who walks to the river, and how many of them.
		/// <para>
		/// A settlement fetches nothing until the founder says so. Before this, water arrived by
		/// itself from the moment of founding, which was automation nobody chose and made a
		/// riverside site self-sustaining forever. Now the founder either carries water in
		/// themselves, or spends people on carrying it - and those people are not manning
		/// anything else while they do.
		/// </para>
		/// </summary>
		public void SetWaterDetail(KingdomSystem System)
		{
			int most = System.Population;
			if (most <= 0)
			{
				Popup.Show("There is nobody here to send. Pour what the settlement needs yourself, or wait for settlers.");
				return;
			}
			string[] options = new string[most + 1];
			options[0] = "{{K|Nobody}} — the settlement drinks what you bring it";
			for (int i = 1; i <= most; i++)
			{
				options[i] = i + ((i == 1) ? " settler" : " settlers")
					+ " {{K|(" + (i * KingdomRules.FetchDramsPerSettler) + " drams a day, if there is open water here)}}"
					+ ((i == System.WaterCrew) ? " {{G|[current]}}" : "");
			}
			int num = Popup.PickOption(
				Title: "The water detail of " + System.SeatName,
				Intro: "Everyone you put on the water is one you cannot put on a work. " + System.Population
					+ ((System.Population == 1) ? " settler lives here." : " settlers live here."),
				Options: options, AllowEscape: true);
			if (num < 0 || num == System.WaterCrew)
			{
				return;
			}
			System.WaterCrew = num;
			KingdomChronicle.Record(System, (num == 0)
				? ("the water detail of " + System.SeatName + " was stood down")
				: (num + ((num == 1) ? " settler was" : " settlers were") + " set to carrying water for " + System.SeatName));
			Popup.Show((num == 0)
				? "The buckets are hung up. " + System.SeatName + " will drink what you bring it."
				: (num + ((num == 1) ? " settler walks" : " settlers walk") + " to the water now. The works have " + (System.Population - num) + " left to draw on."));
		}

		/// <summary>
		/// The two things a crew does to bare ground: take down what stands on it, or lay what the
		/// settlement's own feet have already decided. The report of what is worn is the intro,
		/// because the founder should be able to see whether there is anything to pave in the same
		/// breath they are asked.
		/// </summary>
		public void GroundWork(KingdomSystem System)
		{
			Zone zone = ParentObject.CurrentZone;
			int num = Popup.PickOption(
				Title: "The ground at " + System.SeatName,
				Intro: KingdomRoads.WornLine(zone),
				Options: new string[2] { "Order ground cleared", "Pave a worn path" },
				Hotkeys: new char[2] { 'c', 'p' },
				AllowEscape: true);
			if (num == 0)
			{
				ClearGround(System);
				return;
			}
			if (num == 1 && !KingdomRoads.Pave(System, zone, ParentObject.CurrentCell, out var failure) && failure != null)
			{
				Popup.Show(failure);
			}
		}

		/// <summary>
		/// Orders ground cleared around where the founder is standing. The size is the founder's
		/// choice and nothing here gates it: what may then be laid on cleared ground is the plan's
		/// business, not the clearing gang's. The service does its own messaging; this only picks
		/// the rect and surfaces a decline.
		/// </summary>
		public void ClearGround(KingdomSystem System)
		{
			Zone zone = ParentObject.CurrentZone;
			Cell cell = ParentObject.CurrentCell;
			if (zone == null || cell == null || !System.ClaimedZones.Contains(zone.ZoneID))
			{
				Popup.Show("Ground is cleared on the kingdom's own claim.");
				return;
			}
			int[] widths = new int[3] { 3, 5, 9 };
			int[] heights = new int[3] { 3, 5, 7 };
			string[] names = new string[3] { "the ground you stand on", "a working yard", "a wide clearing" };
			string[] options = new string[3];
			int[][] rects = new int[3][];
			for (int i = 0; i < 3; i++)
			{
				int x1 = cell.X - widths[i] / 2;
				int y1 = cell.Y - heights[i] / 2;
				rects[i] = new int[4] { x1, y1, x1 + widths[i] - 1, y1 + heights[i] - 1 };
				KingdomMaterials.ClearanceAssessment assessment = KingdomMaterials.Assess(System, zone, rects[i][0], rects[i][1], rects[i][2], rects[i][3]);
				string size = " (" + widths[i] + " by " + heights[i] + ")";
				if (!assessment.Valid)
				{
					options[i] = "{{K|" + names[i] + size + " — runs off the edge of this ground}}";
				}
				else if (assessment.Refusal != null)
				{
					options[i] = "{{K|" + names[i] + size + " — something stands in it}}";
				}
				else
				{
					string yield = assessment.Yield.Describe();
					options[i] = names[i] + size + " — " + KingdomMaterialRules.DaysForOneHand(assessment.Effort) + " hand-days"
						+ ((yield == null) ? "" : (", for " + yield));
				}
			}
			int num = Popup.PickOption(Title: "Clear ground at " + System.SeatName,
				Intro: "Clearing spends no water. It spends the hands the water detail and the works have left over, and everything that comes down is carried to the stockpiles.",
				Options: options, AllowEscape: true);
			if (num < 0)
			{
				return;
			}
			if (!KingdomMaterials.StakeClearance(System, zone, rects[num][0], rects[num][1], rects[num][2], rects[num][3], out var failure))
			{
				Popup.Show(failure);
			}
		}

		/// <summary>
		/// Condemns one of the settlement's own buildings, or calls off a condemnation already
		/// standing. The service does its own eligibility check and its own messaging; this only
		/// picks the target and surfaces a decline.
		/// </summary>
		public void StrikeBuilding(KingdomSystem System)
		{
			Zone zone = ParentObject.CurrentZone;
			Cell cell = ParentObject.CurrentCell;
			if (zone == null || cell == null || !System.ClaimedZones.Contains(zone.ZoneID))
			{
				Popup.Show("Buildings are struck on the kingdom's own ground.");
				return;
			}
			System.Collections.Generic.List<GameObject> candidates = new System.Collections.Generic.List<GameObject>();
			CollectBuiltNear(cell, candidates);
			foreach (Cell adjacent in cell.GetLocalAdjacentCells())
			{
				CollectBuiltNear(adjacent, candidates);
			}
			if (candidates.Count == 0)
			{
				Popup.Show("Stand beside something " + System.SeatName + " built to take it down.");
				return;
			}
			string[] options = new string[candidates.Count];
			for (int i = 0; i < candidates.Count; i++)
			{
				int left = candidates[i].GetIntProperty(KingdomMaterials.StrikeEffortProperty);
				options[i] = candidates[i].ShortDisplayName
					+ ((left > 0) ? (" {{r|[condemned, " + KingdomMaterialRules.DaysForOneHand(left) + " hand-days left]}}") : "");
			}
			int num = Popup.PickOption(Title: "Take down a building at " + System.SeatName,
				Intro: "Striking frees the plot and returns half of what the building was made of. It refunds no water, and picking one already condemned calls the order off.",
				Options: options, AllowEscape: true);
			if (num < 0)
			{
				return;
			}
			if (!KingdomMaterials.OrderStrike(System, zone, candidates[num], out var failure))
			{
				Popup.Show(failure);
			}
		}

		private static void CollectBuiltNear(Cell C, System.Collections.Generic.List<GameObject> Into)
		{
			if (C == null)
			{
				return;
			}
			foreach (GameObject item in C.GetObjects())
			{
				if (item.GetIntProperty("KingdomBuilt") == 1 && !Into.Contains(item))
				{
					Into.Add(item);
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
				// Zoning is a decision, and a decision whose price the founder cannot see is a
				// trap: what this naming would put out of reach here is said before it does it.
				string lockout = KingdomZoning.LockoutWarning(System, zone.ZoneID, district);
				if (lockout != null && Popup.ShowYesNo(lockout + "\n\nName it the " + KingdomRules.DistrictName(district) + " anyway?") != DialogResult.Yes)
				{
					return;
				}
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
				// Style, stage, and the visibility law (KingdomZoning.Offered): the whole
				// catalogue is shown with its gates named, EXCEPT a creed-work this city has no
				// way to at all, which is not a locked door but a door in another city.
				if (KingdomZoning.Offered(System, entry))
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
				// The whole catalogue is shown, blocked designs included, each carrying the one
				// thing standing in its way. A list that silently shortens teaches nothing.
				options[i] = available[i].DisplayName + " {{C|[" + available[i].CostDrams + " drams]}}"
					+ (KingdomZoning.GateNote(System, zone?.ZoneID, available[i]) ?? "");
			}
			int num = Popup.PickOption(Title: "Commission ({{C|" + stored + " drams}} in the stores)", Options: options, AllowEscape: true);
			if (num >= 0)
			{
				// Asked before the water moves, and only for a design that actually has a look to
				// choose; escaping the skin prompt takes the design's own. Not asked at all off the
				// kingdom's ground, where the commission below is going to be refused anyway.
				bool onGround = zone != null && System.ClaimedZones.Contains(zone.ZoneID);
				string skin = onGround ? KingdomDesign.ChooseSkin(available[num], System.Style)?.Key : null;
				// How much ground to stake is the founder's decision, and it is the one decision that
				// sets the ceiling on everything this plot will ever become. The whole chain's
				// footprints are shown before the stake goes in, never after it.
				KingdomPlotRules.PlotSize stake = KingdomPlotRules.PlotSize.None;
				if (onGround)
				{
					System.Collections.Generic.List<KingdomPlotRules.PlotSize> grounds = KingdomPlots.StakeableSizes(System, available[num]);
					if (grounds.Count > 1)
					{
						System.Collections.Generic.List<KingdomPlotRules.ChainStep> chain = KingdomPlots.ChainOf(available[num]);
						string[] stakes = new string[grounds.Count];
						for (int i = 0; i < grounds.Count; i++)
						{
							stakes[i] = KingdomPlotRules.StakeOptionLine(grounds[i], chain);
						}
						int ground = Popup.PickOption(Title: "How much ground?", Intro: KingdomPlotRules.ForesightLine(grounds[0], chain), Options: stakes, AllowEscape: true);
						if (ground < 0)
						{
							return;
						}
						stake = grounds[ground];
					}
				}
				if (!KingdomCommission.Commission(System, available[num].Key, skin, stake, out var failure))
				{
					Popup.Show(failure);
				}
			}
		}

		/// <summary>
		/// Stakes a plan on the ground the founder is standing on: names a design from the same
		/// registry <see cref="CommissionBuilding"/> reads, and leaves a marker for the settlement
		/// to raise later, on its own settlement pass, once the stores and the plan allow it.
		/// Nothing is spent here -- see <c>ThousandAndFirst.KingdomPlanMarker.OnSettlementPass</c>
		/// (Growth/KingdomPlanMarker.cs) for where the water actually leaves the stores, only once,
		/// at the moment the scaffold goes up.
		/// </summary>
		public void PlaceBuildingPlan(KingdomSystem System)
		{
			Zone zone = ParentObject.CurrentZone;
			if (zone == null || !System.ClaimedZones.Contains(zone.ZoneID))
			{
				Popup.Show("Plans are staked on the kingdom's own ground.");
				return;
			}
			Cell cell = ParentObject.CurrentCell;
			if (cell == null || !cell.IsPassable() || cell.HasObjectWithPart("LiquidVolume"))
			{
				Popup.Show("Stand on clear, dry ground to stake a plan.");
				return;
			}
			foreach (GameObject occupant in cell.GetObjects())
			{
				if (occupant.HasPart("r_KingdomPlanMarker") || occupant.HasPart("r_KingdomScaffold") || occupant.GetIntProperty("KingdomBuilt") == 1)
				{
					Popup.Show("Something already stands here.");
					return;
				}
			}
			System.Collections.Generic.List<KingdomRules.BuildEntry> available = new System.Collections.Generic.List<KingdomRules.BuildEntry>();
			foreach (KingdomRules.BuildEntry entry in KingdomData.Buildings)
			{
				// Style, stage, and the visibility law (KingdomZoning.Offered): the whole
				// catalogue is shown with its gates named, EXCEPT a creed-work this city has no
				// way to at all, which is not a locked door but a door in another city.
				if (KingdomZoning.Offered(System, entry))
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
				options[i] = available[i].DisplayName + " {{C|[" + available[i].CostDrams + " drams]}}"
					+ (KingdomZoning.GateNote(System, zone.ZoneID, available[i]) ?? "");
			}
			int num = Popup.PickOption(Title: "Stake a plan", Intro: "Nothing is spent now. " + System.SeatName + " raises this when the stores and the plan allow it.", Options: options, AllowEscape: true);
			if (num < 0)
			{
				return;
			}
			// Judged where the plan is staked, not where it is realised, because this cell is the
			// founder's decision and they are standing on it now.
			if (!KingdomZoning.Permits(System, zone.ZoneID, available[num], out var refusal))
			{
				Popup.Show(refusal);
				return;
			}
			GameObject marker = GameObject.Create("r_KingdomPlanMarker");
			if (marker == null)
			{
				Popup.Show("The plan could not be staked.");
				return;
			}
			KingdomRules.BuildEntry chosen = available[num];
			string plannedSkin = KingdomDesign.ChooseSkin(chosen, System.Style)?.Key;
			if (!string.IsNullOrEmpty(plannedSkin))
			{
				marker.SetStringProperty(KingdomDesign.PlannedSkinProperty, plannedSkin);
			}
			r_KingdomPlanMarker part = marker.GetPart<r_KingdomPlanMarker>();
			part?.ApplyDesign(chosen);
			KingdomCeremony.StakePlan(marker, chosen, plannedSkin);
			cell.AddObject(marker);
			KingdomChronicle.Record(System, "a plan for " + XRL.Language.Grammar.A(chosen.Name) + " was staked at " + System.KingdomDisplayName);
			Popup.Show("{{G|The plan is staked.}} " + System.SeatName + " will raise it when the water and the room allow.");
		}

		/// <summary>
		/// Where the founder sees what is staked and calls plans off. Cancelling costs nothing and
		/// returns nothing, because a staked plan never spends anything until the moment it is
		/// realised -- there is nothing to refund.
		/// </summary>
		public void ManagePlans(KingdomSystem System)
		{
			Zone zone = ParentObject.CurrentZone;
			System.Collections.Generic.List<GameObject> markers = KingdomPlanMarker.FindPending(zone);
			while (true)
			{
				string[] options = new string[markers.Count + 1];
				options[0] = "{{W|Stake a new plan}}";
				for (int i = 0; i < markers.Count; i++)
				{
					options[i + 1] = KingdomPlanMarker.Describe(markers[i]) + " {{K|[cancel]}}";
				}
				int num = Popup.PickOption(Title: "Plans staked at " + System.SeatName, Options: options, AllowEscape: true);
				if (num < 0)
				{
					return;
				}
				if (num == 0)
				{
					PlaceBuildingPlan(System);
					markers = KingdomPlanMarker.FindPending(zone);
					continue;
				}
				GameObject target = markers[num - 1];
				string name = KingdomPlanMarker.Describe(target);
				if (Popup.ShowYesNo("Cancel the plan for " + name + "? Nothing was spent, and nothing is returned, because nothing was taken.") == DialogResult.Yes)
				{
					KingdomChronicle.Record(System, "the plan for " + name + " at " + System.KingdomDisplayName + " was called off");
					KingdomPlanMarker.Cancel(target);
					markers = KingdomPlanMarker.FindPending(zone);
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
				KingdomManifest lapsed = KingdomTrade.ExpireManifestIfStale(System, zone, The.Game.TimeTicks);
				if (lapsed != null)
				{
					Popup.Show(KingdomManifestRules.ManifestLapseNotice(lapsed.OriginName, lapsed.DestinationName, lapsed.Drams));
					return;
				}
			}
			bool hasSecondCity = System.Away != null;
			int stored = onGround ? KingdomGrowth.CountStoredWater(zone) : 0;
			// Sized against what the realm BELIEVES the other city can take - the figure it had
			// when the founder last stood there. Loading to that belief is what makes arriving
			// with nowhere to put it a rare, specific event rather than routine spillage.
			int believedRoom = System.Away?.LastKnownStorageSpace ?? 0;
			int amount = KingdomManifestRules.CapToDestination(KingdomManifestRules.ManifestAmount(stored, System.Population), believedRoom);
			KingdomManifestRules.ManifestVerdict verdict = KingdomManifestRules.JudgeManifest(onGround, hasSecondCity, System.Manifest != null, KingdomManifestRules.ManifestAmount(stored, System.Population), believedRoom);
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
				bool isLarder = larders[i].GetIntProperty("KingdomLarder") == 1;
				bool isStockpile = KingdomMaterials.IsStockpile(larders[i]);
				options[vessels.Count + i + 1] = larders[i].ShortDisplayName + " {{K|(store)}}"
					+ (isLarder ? " {{G|[larder]}}" : "")
					+ (isStockpile ? " {{G|[stockpile]}}" : "")
					+ ((!isLarder && !isStockpile) ? " {{K|[personal]}}" : "");
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
				GameObject store = larders[num - vessels.Count - 1];
				bool isLarder = store.GetIntProperty("KingdomLarder") == 1;
				bool isStockpile = KingdomMaterials.IsStockpile(store);
				// Food and material are separate accounts kept by separate people, so one chest may be
				// a larder, a stockpile, or both. Dedication is a mark either way: what is inside stays
				// where it is and stays the founder's. The settlement only counts it.
				int pick = Popup.PickOption(Title: store.ShortDisplayName,
					Intro: "What should the settlement count what is in here as?",
					Options: new string[2] {
						(isLarder ? "{{G|Stop counting it as a larder}}" : "Dedicate it as a {{W|larder}} — food for the shared meal"),
						(isStockpile ? "{{G|Stop counting it as a stockpile}}" : "Dedicate it as a {{W|stockpile}} — timber, stone, and whatever else is cleared")
					}, AllowEscape: true);
				if (pick < 0)
				{
					return;
				}
				if (pick == 0)
				{
					if (!isLarder && KingdomGrowth.CountDedicatedLarders(zone) >= KingdomRules.MaxDedicatedLarders)
					{
						Popup.Show("The settlement keeps as many larders as anyone can keep an honest account of.");
						return;
					}
					if (isLarder)
					{
						store.SetIntProperty("KingdomLarder", 0);
						Popup.Show("The " + store.ShortDisplayName + " is no longer a larder. Nothing in it will be counted as food.");
					}
					else
					{
						store.SetIntProperty("KingdomLarder", 1);
						Popup.Show("The " + store.ShortDisplayName + " is a larder of " + System.SeatName + " now. What is in it is counted, and still yours.");
					}
					return;
				}
				if (!KingdomMaterials.DedicateStockpile(System, zone, store, out var stockpileFailure))
				{
					Popup.Show(stockpileFailure);
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

		/// <summary>
		/// Certifies a machine hauled home from a ruin fit for the settlement's grid, or takes
		/// an already-certified one back off it. The cost is disclosed here, before anything is
		/// spent; KingdomSalvage does the actual eligibility check, the spend, and the messaging.
		/// </summary>
		public void CertifyMachine(KingdomSystem System)
		{
			Cell cell = ParentObject.CurrentCell;
			if (cell == null)
			{
				return;
			}
			Zone zone = ParentObject.CurrentZone;
			if (zone == null || !System.ClaimedZones.Contains(zone.ZoneID))
			{
				Popup.Show("A machine is certified on the kingdom's own ground, not in other people's houses.");
				return;
			}
			System.Collections.Generic.List<GameObject> machines = new System.Collections.Generic.List<GameObject>();
			foreach (Cell adjacentCell in cell.GetLocalAdjacentCells())
			{
				foreach (GameObject item in adjacentCell.GetObjects())
				{
					if (item.IsCreature || item.GetPart<XRL.World.Parts.LiquidVolume>() != null || machines.Contains(item))
					{
						continue;
					}
					// A "machine" here is anything the settlement's own Examiner/TinkerItem
					// readings can actually inspect; plain scenery never shows up in this list.
					if (item.GetPart<XRL.World.Parts.Examiner>()?.Complexity > 0 || item.GetPart<XRL.World.Parts.TinkerItem>() != null)
					{
						machines.Add(item);
					}
				}
			}
			if (machines.Count == 0)
			{
				Popup.Show("Stand beside a machine to certify it. Nothing here reads as one.");
				return;
			}
			string[] options = new string[machines.Count];
			for (int i = 0; i < machines.Count; i++)
			{
				options[i] = machines[i].ShortDisplayName + ((machines[i].GetIntProperty(KingdomSalvage.CertifiedProperty) == 1) ? " {{G|[certified]}}" : " {{K|[uncertified]}}");
			}
			int index = Popup.PickOption(Title: "Certify a machine", Options: options, AllowEscape: true);
			if (index < 0)
			{
				return;
			}
			GameObject machine = machines[index];
			KingdomSalvage.SalvageAssessment assessment = KingdomSalvage.Assess(System, zone, machine);
			if (assessment.AlreadyCertified)
			{
				if (Popup.ShowYesNo("Take " + machine.ShortDisplayName + " off the grid of " + System.SeatName + "? It will stand exactly where it stands; the settlement will simply stop answering for it.") != DialogResult.Yes)
				{
					return;
				}
			}
			else if (assessment.Verdict == KingdomSalvageRules.SalvageVerdict.Certified)
			{
				if (Popup.ShowYesNo("Certify " + machine.ShortDisplayName + " fit for the grid of " + System.SeatName + "?\n\nIt will cost {{C|" + assessment.WaterCost + "}} drams and " + assessment.HandsRequired + " hands free to test it.") != DialogResult.Yes)
				{
					return;
				}
			}
			if (!KingdomSalvage.Certify(System, zone, machine, out var failure))
			{
				Popup.Show(failure);
			}
		}

		public void AdoptBuilding(KingdomSystem System)
		{
			Cell cell = ParentObject.CurrentCell;
			if (cell == null)
			{
				return;
			}
			Zone zone = ParentObject.CurrentZone;
			if (zone == null || !System.ClaimedZones.Contains(zone.ZoneID))
			{
				Popup.Show("A building is adopted on the kingdom's own ground, not in other people's houses.");
				return;
			}
			System.Collections.Generic.List<KingdomRules.BuildEntry> buildings = KingdomData.Buildings;
			if (buildings.Count == 0)
			{
				Popup.Show("There is nothing in the plan to adopt a building as.");
				return;
			}
			string[] designOptions = new string[buildings.Count];
			for (int i = 0; i < buildings.Count; i++)
			{
				designOptions[i] = buildings[i].Name + " {{K|(" + buildings[i].Category + ")}}";
			}
			int designIndex = Popup.PickOption(Title: "Adopt a building as...", Intro: "Choose what kind of building this counts as. The settlement checks the space, not who built it.", Options: designOptions, AllowEscape: true);
			if (designIndex < 0)
			{
				return;
			}
			KingdomRules.BuildEntry entry = buildings[designIndex];
			KingdomAdoptRules.RoleKind role = KingdomAdoptRules.ClassifyRole(entry.Category);
			if (role == KingdomAdoptRules.RoleKind.Work)
			{
				if (Popup.ShowYesNo("Adopt this room as " + XRL.Language.Grammar.A(entry.Name) + "? A small marker is set down where you stand; nothing you built is touched.") != DialogResult.Yes)
				{
					return;
				}
				if (!KingdomAdopt.AdoptWork(System, zone, cell, entry.Key, out var workFailure))
				{
					Popup.Show(workFailure);
				}
				return;
			}
			System.Collections.Generic.List<GameObject> candidates = new System.Collections.Generic.List<GameObject>();
			foreach (Cell adjacentCell in cell.GetLocalAdjacentCells())
			{
				foreach (GameObject item in adjacentCell.GetObjects())
				{
					if (item.GetIntProperty("KingdomBuilt") == 1 || candidates.Contains(item))
					{
						continue;
					}
					if (role == KingdomAdoptRules.RoleKind.Housing && item.HasPart("Bed"))
					{
						candidates.Add(item);
					}
					else if (role == KingdomAdoptRules.RoleKind.Storage)
					{
						XRL.World.Parts.LiquidVolume lv = item.GetPart<XRL.World.Parts.LiquidVolume>();
						bool isVessel = lv != null && lv.MaxVolume > 0;
						bool isLarder = lv == null && item.Inventory != null;
						if (isVessel || isLarder)
						{
							candidates.Add(item);
						}
					}
				}
			}
			if (candidates.Count == 0)
			{
				Popup.Show((role == KingdomAdoptRules.RoleKind.Housing) ? "Stand beside a bed to adopt it. Nothing here is one." : "Stand beside a vessel or a store to adopt it. Nothing here is one.");
				return;
			}
			string[] candidateOptions = new string[candidates.Count];
			for (int i = 0; i < candidates.Count; i++)
			{
				candidateOptions[i] = candidates[i].ShortDisplayName + ((candidates[i].GetIntProperty("KingdomStores") == 1 || candidates[i].GetIntProperty("KingdomLarder") == 1) ? " {{G|[dedicated]}}" : "");
			}
			int candidateIndex = Popup.PickOption(Title: "Adopt which one?", Options: candidateOptions, AllowEscape: true);
			if (candidateIndex < 0)
			{
				return;
			}
			GameObject candidate = candidates[candidateIndex];
			if (Popup.ShowYesNo("Adopt " + candidate.ShortDisplayName + " as " + XRL.Language.Grammar.A(entry.Name) + "?") != DialogResult.Yes)
			{
				return;
			}
			if (!KingdomAdopt.AdoptExisting(System, zone, candidate, entry.Key, out var failure))
			{
				Popup.Show(failure);
			}
		}

		public void ReleaseBuilding(KingdomSystem System)
		{
			Cell cell = ParentObject.CurrentCell;
			if (cell == null)
			{
				return;
			}
			Zone zone = ParentObject.CurrentZone;
			if (zone == null || !System.ClaimedZones.Contains(zone.ZoneID))
			{
				Popup.Show("A building is released on the kingdom's own ground, not in other people's houses.");
				return;
			}
			System.Collections.Generic.List<GameObject> adopted = new System.Collections.Generic.List<GameObject>();
			foreach (GameObject item in cell.GetObjects())
			{
				if (item.GetIntProperty(KingdomAdopt.AdoptedProperty) == 1)
				{
					adopted.Add(item);
				}
			}
			foreach (Cell adjacentCell in cell.GetLocalAdjacentCells())
			{
				foreach (GameObject item in adjacentCell.GetObjects())
				{
					if (item.GetIntProperty(KingdomAdopt.AdoptedProperty) == 1 && !adopted.Contains(item))
					{
						adopted.Add(item);
					}
				}
			}
			if (adopted.Count == 0)
			{
				Popup.Show("Nothing here is adopted into the settlement.");
				return;
			}
			string[] options = new string[adopted.Count];
			for (int i = 0; i < adopted.Count; i++)
			{
				options[i] = adopted[i].ShortDisplayName + " {{K|(" + adopted[i].GetStringProperty(KingdomAdopt.AdoptedKeyProperty) + ")}}";
			}
			int index = Popup.PickOption(Title: "Release which adoption?", Options: options, AllowEscape: true);
			if (index < 0)
			{
				return;
			}
			GameObject target = adopted[index];
			if (Popup.ShowYesNo("Release " + target.ShortDisplayName + " from " + System.SeatName + "'s standing? It stands exactly where it stands; the settlement will simply stop answering for it.") != DialogResult.Yes)
			{
				return;
			}
			if (!KingdomAdopt.Release(System, zone, target, out var failure))
			{
				Popup.Show(failure);
			}
		}

		/// <summary>
		/// What the realm's two cities believe, and the founder's levers over it: read the
		/// standing report, pour a rite of shared water, declare (or recant) the realm's own
		/// creed, or ask a seceded city to come back. Every lever below is safe to call at any
		/// temper &mdash; each one checks its own preconditions and declines in the settlement's
		/// own voice rather than trusting this menu to gate anything. See <see cref="KingdomCreed"/>.
		/// </summary>
		public void ManageCreed(KingdomSystem System)
		{
			Zone zone = ParentObject.CurrentZone;
			while (true)
			{
				bool riteAvailable = KingdomCreed.RiteAvailable(System);
				System.Collections.Generic.List<string> declarable = KingdomCreed.DeclarableCreeds(System);
				string[] options = new string[4]
				{
					"{{W|Read the report}}",
					riteAvailable ? "Hold a rite of shared water" : "{{K|Hold a rite of shared water}}",
					(declarable.Count > 0) ? "Declare the realm's creed" : "{{K|Declare the realm's creed}}",
					(System.Seceded != null) ? "Ask them back" : "{{K|Ask them back}}"
				};
				int num = Popup.PickOption(Title: "How your cities hold each other", Options: options, AllowEscape: true);
				if (num < 0)
				{
					return;
				}
				if (num == 0)
				{
					Popup.Show(KingdomCreed.Report(System));
				}
				else if (num == 1)
				{
					if (!KingdomCreed.HoldRite(System, zone, out var riteFailure))
					{
						Popup.Show(riteFailure);
					}
				}
				else if (num == 2)
				{
					DeclareCreed(System, declarable);
				}
				else if (num == 3)
				{
					if (!KingdomCreed.TryRejoin(System, zone, out var rejoinFailure))
					{
						Popup.Show(rejoinFailure);
					}
				}
			}
		}

		/// <summary>The declare/recant sub-menu <see cref="ManageCreed"/> opens. A separate method
		/// only to keep that loop's body short; it owns no state of its own.</summary>
		private void DeclareCreed(KingdomSystem System, System.Collections.Generic.List<string> Declarable)
		{
			if (Declarable.Count == 0)
			{
				Popup.Show("Neither city holds a creed strongly enough to declare it the realm's own.");
				return;
			}
			string[] options = new string[Declarable.Count + 1];
			for (int i = 0; i < Declarable.Count; i++)
			{
				options[i] = KingdomCreed.CreedName(Declarable[i]) + ((Declarable[i] == System.DeclaredCreed) ? " {{G|[declared]}}" : "");
			}
			options[Declarable.Count] = "{{K|Unsay it}}";
			int num = Popup.PickOption(Title: "Declare the realm's creed", Options: options, AllowEscape: true);
			if (num < 0)
			{
				return;
			}
			string chosen = (num == Declarable.Count) ? null : Declarable[num];
			// The price is named before it is paid, the same as sending a manifest or calling a
			// meal. This is the heaviest thing in the Charter - it moves a faction's regard for
			// the realm across the whole world and bends every settler who comes afterwards - and
			// it was the one spending action that committed without a word.
			if (chosen != null && Popup.ShowYesNo("Declare " + System.KingdomDisplayName + " for " + KingdomCreed.CreedName(chosen)
				+ "?\n\nEvery settler who comes after will lean toward them. Those who stand against them will hold it against the whole realm, wherever they are, and the cities will feel it before they feel the peace.") != DialogResult.Yes)
			{
				return;
			}
			if (!KingdomCreed.Declare(System, chosen, out var failure))
			{
				Popup.Show(failure);
			}
		}
	}
}
