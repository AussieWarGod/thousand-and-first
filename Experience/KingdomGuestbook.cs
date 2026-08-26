using System;
using System.Collections.Generic;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	/// <summary>
	/// The engine-coupled shell for two co-opted ideas that share one file set because they share
	/// one shape: something outside the settlement is marked, and porters or a notable's own feet
	/// close the distance on a later attended pass, never a background clock.
	/// <para>
	/// <b>Guests at the gate</b> extends <see cref="KingdomLocus"/>'s already-shipped plain
	/// travellers with a rarer, structurally different arrival: a notable who carries one
	/// outward-pointing hook, can be lodged into a real bed, and — ignored — leaves the hook
	/// behind as a standing rumor rather than losing it. Runs as a sibling pass to
	/// <c>KingdomLocus</c> on the same <c>ZoneActivatedEvent</c>, under its own marker property
	/// and its own cadence, so the shipped plain-traveller path is never touched.
	/// </para>
	/// <para>
	/// <b>The carry-sign</b> marks a container or pile the founder owns anywhere in the world for
	/// porters to fetch. CarryBook freezes each whole GameObject and central logistics carries that
	/// same reference; the legacy aggregate haul below is decode/reconciliation only.
	/// </para>
	/// </summary>
	public static class KingdomGuestbook
	{
		public static bool GuestsEnabled => Options.GetOption("r_TAF_OptionGuestbook") != "No";

		public static bool CarrySignEnabled => Options.GetOption("r_TAF_OptionCarrySign") != "No";

		/// <summary>Marks a notable guest, as opposed to <c>KingdomLocus</c>'s plain
		/// <c>KingdomGuest</c> travellers. The two never collide: a plain traveller never carries
		/// this property, and a notable never carries <c>KingdomGuest</c>.</summary>
		public const string NotableGuestProperty = "KingdomNotableGuest";

		/// <summary>Open blueprint tag for the luxury-lane arrival. A third-party guest may opt
		/// into the same exact fine-house/shop contract without replacing this class.</summary>
		public const string LegendaryTraderTag = "r_TAF_LegendaryTrader";

		/// <summary>Durable resident marker after the visitor settles. The exact home is the
		/// ordinary <c>KingdomLodgingPlotId</c>, so save/reload and every lodging reader share one
		/// authority rather than a parallel guest-only reservation.</summary>
		public const string LegendaryTraderResidentProperty = "KingdomLegendaryTrader";

		internal const string HookKindProperty = "KingdomGuestHookKind";

		internal const string HookTextProperty = "KingdomGuestHookText";

		internal const string LodgeReceiptProperty = "r_TAF_NotableLodgeReceipt";

		private const string OriginProperty = "KingdomOrigin";

		public static void OnZoneActivated(KingdomSystem System, Zone Z, KingdomSurvey Survey)
		{
			if (!KingdomMaster.AutomaticWorkAllowed(System)) return;
			if (System == null || !System.Founded || Z == null || Survey == null || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				return;
			}
			long timeTicks = The.Game.TimeTicks;
			if (KingdomGuestLifecycle.ObserveOption(System,
				KingdomLifecycleLane.NotableGuest, GuestsEnabled, timeTicks, out bool allowNew))
			{
				if (KingdomGuestLifecycle.Open(System, KingdomLifecycleLane.NotableGuest) != null)
					KingdomGuestLifecycle.Drive(System, Z, KingdomLifecycleLane.NotableGuest);
				if (allowNew && KingdomGuestLifecycle.Open(System,
					KingdomLifecycleLane.NotableGuest) == null) RunNotableGuestPass(System, Z, Survey, timeTicks);
			}
			if (CarrySignEnabled)
			{
				KingdomCarryRuntime.Drive(System, Z, timeTicks);
				ResolveLegacyHaulIfDue(System, Z, Survey, timeTicks);
			}
		}

		// ==================================================================================
		// Guests at the gate
		// ==================================================================================

		/// <summary>
		/// Brings notables up the road on their own cadence, whether or not anybody is home, and
		/// tells the founder at awareness what became of the ones nobody met.
		/// <para>
		/// Addendum 8 clause 1 and 3, the same shape <c>KingdomLocus.RunGuestPass</c> keeps for
		/// ambient travellers: everyone whose patience ran out during the absence left a letter,
		/// and the letters are one dated entry between them rather than a queue of strangers in
		/// the square. At most one is still standing, and only when they arrived recently enough
		/// to still be waiting &mdash; which is guaranteed by
		/// <c>NotableGuestPatienceTicks</c> being shorter than
		/// <c>NotableGuestIntervalTicks</c>, not by a live object blocking the spawn.
		/// </para>
		/// </summary>
		private static void RunNotableGuestPass(KingdomSystem System, Zone Z,
			KingdomSurvey Survey, long TimeTicks)
		{
			GameObject guest = FindNotableGuest(Survey);
			if (guest != null)
			{
				if (KingdomGuestRules.ShouldDepartUnattended(TimeTicks, System.NotableGuestDepartTick))
				{
					DepartUnattended(System, guest);
				}
				return;
			}
			long effectiveDue = KingdomGuestLifecycle.EffectiveDue(System,
				KingdomLifecycleLane.NotableGuest, KingdomGuestRules.NotableGuestIntervalTicks);
			if (effectiveDue <= 0L || TimeTicks < effectiveDue) return;
			KingdomRules.Passages passages = KingdomRules.PassagesThrough(
				effectiveDue, TimeTicks, KingdomGuestRules.NotableGuestIntervalTicks,
				KingdomGuestRules.NotableGuestPatienceTicks);
			Cell standingCell = passages.StandingSince > 0L ? KingdomLocus.HeartArrivalCell(Z) : null;
			long before = System.NextNotableGuestTick > 0L ? System.NextNotableGuestTick : 0L;
			long after = passages.StandingSince > 0L && standingCell == null
				? passages.StandingSince : passages.NextDueTick;
			int daysAgo = passages.Departed > 0
				? KingdomRules.ElapsedDays(TimeTicks - passages.LastDepartedTick) : 0;
			string chronicle = passages.Departed > 0
				? KingdomGuestRules.PassedChronicleLine(passages.Departed, KingdomPresentation.Rich(System.SeatName), daysAgo)
				: null;
			string ledger = passages.Departed > 0
				? KingdomGuestRules.PassedLedgerNote(passages.Departed, daysAgo) : null;
			string guestbook = passages.Departed > 0
				? KingdomGuestRules.PassedGuestbookLine(passages.Departed, daysAgo) : null;
			if (!KingdomGuestLifecycle.PublishPassages(System, Z,
				KingdomLifecycleLane.NotableGuest, TimeTicks, before, after, passages.Departed,
				passages.LastDepartedTick, passages.StandingSince, chronicle, ledger, guestbook))
				return;
			if (passages.StandingSince <= 0L)
			{
				return;
			}
			// Spawned at the tick they actually walked up: their patience is already partly spent,
			// their hook is drawn on their own arrival ordinal, and they leave when they were
			// always going to leave.
			if (standingCell != null)
				SpawnNotableGuest(System, Z, standingCell, passages.StandingSince);
		}

		private static GameObject FindNotableGuest(KingdomSurvey Survey)
		{
			return Survey != null && Survey.NotableGuests.Count > 0
				? Survey.NotableGuests[0] : null;
		}

		/// <summary>Puts one notable on the ground at the tick they walked up. False when there
		/// was nowhere to stand them, which is the caller's signal to leave their arrival unspent
		/// rather than losing them.</summary>
		private static bool SpawnNotableGuest(KingdomSystem System, Zone Z, Cell cell,
			long ArrivalTick)
		{
			if (cell == null) return false;
			KingdomSemanticPersonPlan plan;
			string planFailure;
			if (!KingdomGuestLifecycle.TryPrepareSpawnPlan(System,
				KingdomLifecycleLane.NotableGuest, "r_KingdomNotableGuests",
				"r_KingdomNotableGuest", out plan, out planFailure))
			{
				KingdomLog.Log("notable guest waits: " + planFailure);
				return false;
			}
			KingdomGuestRules.HookKind kind;
			string hookText;
			// Drawn on the tick they arrived on, not the tick the founder walked in: the hook is
			// this guest's own fact, and keying it to the arrival ordinal means a reload asks the
			// same question and gets the same answer.
			if (!DrawHook(System, plan, out kind, out hookText)) return false;
			long depart = KingdomGuestRules.DepartTickFor(ArrivalTick);
			string shownName = KingdomPresentation.Rich(plan.Name);
			string shownHook = KingdomPresentation.Rich(hookText);
			string chronicle = KingdomGuestRules.ArrivalChronicleLine(shownName,
				KingdomPresentation.Rich(System.SeatName));
			string ledger = shownName + " is waiting at the rite ground with word of "
				+ shownHook + ".";
			string message = "{{C|" + shownName
				+ " has arrived at the rite ground as a notable guest.}}";
			string guestbook = shownName + ", waiting at the rite ground with word of "
				+ shownHook + " {{K|(standing)}}";
			return KingdomGuestLifecycle.PublishSpawn(System, Z,
				KingdomLifecycleLane.NotableGuest, cell, The.Game.TimeTicks, depart,
				plan.Blueprint, plan.Name, plan.Origin, (int)kind, 0, hookText, null, null,
				chronicle, ledger, message, guestbook, semanticPlan: plan);
		}

		private static bool DrawHook(KingdomSystem System, KingdomSemanticPersonPlan Plan,
			out KingdomGuestRules.HookKind Kind, out string HookText)
		{
			SemanticEventKey key;
			KernelFaultCode fault;
			ulong kindRoll;
			ulong flavorRoll;
			if (System != null && Plan != null && Plan.Sequence > 0L
				&& SemanticEventKey.TryCreate(KingdomSemanticSelectionRules.RulesVersion,
					System.CurrentSettlementId, KingdomSemanticSelection.NotableGuestStream,
					KingdomSemanticSelection.HookEventKind, (ulong)Plan.Sequence,
					out key, out fault)
				&& CounterRandom.TryDrawBelow(System.SimulationSeed, key, 0u,
					(ulong)KingdomGuestRules.HookKindCount, out kindRoll, out fault)
				&& CounterRandom.TryDrawBelow(System.SimulationSeed, key, 1u, 1000uL,
					out flavorRoll, out fault))
			{
				Kind = KingdomGuestRules.PickHookKind(kindRoll);
				HookText = KingdomGuestRules.HookText(Kind, flavorRoll);
				return true;
			}
			// No immutable subject means no mutable fallback and therefore no published guest.
			Kind = KingdomGuestRules.PickHookKind(0UL);
			HookText = KingdomGuestRules.HookText(Kind, 0UL);
			return false;
		}

		private static void DepartUnattended(KingdomSystem System, GameObject Guest)
		{
			string name = PlainGuestName(Guest);
			string shownName = KingdomPresentation.Rich(name);
			KingdomGuestRules.HookKind kind = (KingdomGuestRules.HookKind)Guest.GetIntProperty(HookKindProperty);
			string hookText = Guest.GetStringProperty(HookTextProperty) ?? "";
			string shownHook = KingdomPresentation.Rich(hookText);
			string chronicle = KingdomGuestRules.DepartedChronicleLine(shownName,
				KingdomPresentation.Rich(System.SeatName)) + "; others said "
				+ KingdomGuestRules.DepartedOutsiderRumor(shownName, kind, shownHook);
			string ledger = KingdomGuestRules.DepartedLedgerNote(shownName,
				KingdomRules.ElapsedDays(The.Game.TimeTicks - System.NotableGuestDepartTick));
			string guestbook = KingdomGuestRules.GuestbookLine(shownName, kind, shownHook,
				Lodged: false);
			KingdomGuestLifecycle.PublishDeparture(System, Guest,
				KingdomLifecycleLane.NotableGuest, The.Game.TimeTicks,
				KingdomGuestRules.NextDueTick(The.Game.TimeTicks), greeted: false,
				chronicle, ledger, null, guestbook);
		}

		/// <summary>
		/// Offers a notable guest the settlement's own housing. Call from
		/// <see cref="XRL.World.Parts.r_KingdomNotableGuest"/>'s inventory action; a no-op if the
		/// guest has already resolved (lodged or departed) or is no longer present.
		/// </summary>
		/// <param name="Guest">The guest object the player targeted.</param>
		public static void TryLodge(GameObject Guest)
		{
			if (Guest == null || Guest.GetIntProperty(NotableGuestProperty) != 1)
			{
				return;
			}
			Zone zone = Guest.CurrentZone;
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			if (!KingdomMaster.NewWorkAllowed(system))
			{
				Popup.Show("Settlement simulation is paused; this guest cannot be lodged yet.");
				return;
			}
			if (zone == null || !system.Founded || !system.ClaimedZones.Contains(zone.ZoneID))
			{
				return;
			}
			KingdomGuestRules.HookKind kind = (KingdomGuestRules.HookKind)Guest.GetIntProperty(HookKindProperty);
			string hookText = Guest.GetStringProperty(HookTextProperty) ?? "";
			string shownHook = KingdomPresentation.Rich(hookText);
			bool legendaryTrader = Guest.HasTag(LegendaryTraderTag);
			GameObject fineHouse = null;
			KingdomPlotRules.PlotSize fineHouseTier = KingdomPlotRules.PlotSize.None;
			// A guest is judged by the raw bed count against population, on the same live survey,
			// and deliberately NOT by the settlers' own assignment-level gate: brief Addendum 4b
			// binds housing for people who JOIN the settlement, and says guests are unchanged
			// because they never stay without lodging anyway. A visitor is not assigned a home,
			// spends nobody's grace, and never leaves for want of one.
			KingdomSurvey survey = KingdomSurvey.Take(zone);
			KingdomGuestRules.LodgingVerdict verdict;
			if (legendaryTrader)
			{
				bool hasFineHouse;
				fineHouse = FindVacantFineHouse(zone, out hasFineHouse, out fineHouseTier);
				int liveShopTier = system.HasShopkeeper ? system.ShopTier : 0;
				verdict = KingdomGuestRules.AssessLegendaryTraderLodging(hasFineHouse,
					fineHouseTier, fineHouse != null, liveShopTier);
			}
			else
			{
				KingdomPlotRules.PlotSize bestTier = BestHousingTier(zone);
				bool hasRoom = KingdomRules.HasRoomToHouse(system.Population, survey.Beds);
				bool hasTier = bestTier != KingdomPlotRules.PlotSize.None
					&& bestTier >= KingdomGuestRules.RequiredTier(kind);
				verdict = KingdomGuestRules.AssessLodging(hasTier, hasRoom);
			}
			if (verdict != KingdomGuestRules.LodgingVerdict.Lodged)
			{
				Popup.Show(legendaryTrader
					? KingdomGuestRules.LegendaryTraderRefusal(verdict)
					: (verdict == KingdomGuestRules.LodgingVerdict.NoTier
						? KingdomGuestRules.NoTierRefusal(kind)
						: KingdomGuestRules.NoRoomRefusal()));
				return;
			}
			int arrivalCost = KingdomRules.DramsPerArrival;
			if (survey.StoredWater < arrivalCost)
			{
				Popup.Show("Lodging " + KingdomPresentation.Rich(PlainGuestName(Guest))
					+ " requires exactly {{C|"
					+ arrivalCost + " drams}} from the dedicated stores, and they cannot provide it.");
				return;
			}
			string name = PlainGuestName(Guest);
			string shownName = KingdomPresentation.Rich(name);
			bool milestone = !system.FirstNotableGuestLodged;
			string chronicle = KingdomGuestRules.LodgedChronicleLine(shownName,
				KingdomPresentation.Rich(system.SeatName), kind, legendaryTrader);
			string ledger = shownName + " joined the settlement from "
				+ KingdomPresentation.Rich(Guest.GetStringProperty(OriginProperty)
					?? "the road") + ".";
			string message = KingdomGuestRules.LodgedMessage(shownName, kind, legendaryTrader);
			string line = KingdomGuestRules.GuestbookLine(shownName, kind, shownHook,
				Lodged: true, LegendaryTrader: legendaryTrader);
			if (!KingdomGuestLifecycle.PublishLodge(system, Guest, fineHouse,
				The.Game.TimeTicks, KingdomGuestRules.NextDueTick(The.Game.TimeTicks),
				arrivalCost, chronicle, ledger, message, line, milestone))
				Popup.Show("Lodging could not complete. Its exact lifecycle receipt remains open; no second lodging can begin.");
		}

		/// <summary>
		/// The best housing tier standing in the zone, read off the plot rects stamped on its
		/// beds (<c>KingdomPlots.TryReadRect</c>). A bed with no readable rect is legacy
		/// single-cell furniture and reads as <see cref="KingdomPlotRules.PlotSize.Small"/>, never
		/// <c>None</c> — S plots never obsolete, and neither does the furniture that predates
		/// plots entirely.
		/// </summary>
		private static KingdomPlotRules.PlotSize BestHousingTier(Zone Z)
		{
			KingdomPlotRules.PlotSize best = KingdomPlotRules.PlotSize.None;
			foreach (GameObject item in KingdomSurvey.ObjectsFor(Z))
			{
				if (item.GetIntProperty("KingdomBuilt") != 1 || !item.HasPart("Bed"))
				{
					continue;
				}
				KingdomPlotRules.PlotSize tier = KingdomPlotRules.PlotSize.Small;
				if (KingdomPlots.TryReadRect(item, out KingdomPlotRules.PlotRect rect))
				{
					tier = KingdomGuestRules.ClassifyRectTier(rect.Width, rect.Height);
				}
				if (tier > best)
				{
					best = tier;
				}
			}
			return best;
		}

		/// <summary>One plain semantic guest name; output callers escape it separately.</summary>
		private static string PlainGuestName(GameObject guest)
		{
			if (!GameObject.Validate(guest)) return "a guest";
			string named = guest.GetStringProperty("KingdomName");
			if (string.IsNullOrEmpty(named)) named = guest.BaseDisplayNameStripped;
			return string.IsNullOrEmpty(named) ? "a guest" : named;
		}

		/// <summary>Finds one exact, sound, wholly vacant fine house. A manor, terrace, or large
		/// generic roof never aliases the named luxury good. The lowest stable LotId wins when
		/// several qualify; the returned tier is that vacant home's actual staked size, or the best
		/// exact fine-house tier seen when every one is occupied so the refusal remains exact.</summary>
		private static GameObject FindVacantFineHouse(Zone Z, out bool HasFineHouse,
			out KingdomPlotRules.PlotSize Tier)
		{
			HasFineHouse = false;
			Tier = KingdomPlotRules.PlotSize.None;
			GameObject chosen = null;
			string chosenPlot = null;
			KingdomPlotRules.PlotSize chosenTier = KingdomPlotRules.PlotSize.None;
			foreach (GameObject item in KingdomSurvey.ObjectsFor(Z))
			{
				if (item.GetIntProperty(KingdomUpgrade.BuiltProperty) != 1
					|| !string.Equals(KingdomUpgrade.DesignKeyOf(item), "finehouse",
						StringComparison.Ordinal)
					|| KingdomLodging.IsCondemned(item))
					continue;
				string plotId = item.GetStringProperty(KingdomPlots.PlotIdProperty);
				if (string.IsNullOrEmpty(plotId)) continue;
				HasFineHouse = true;
				KingdomPlotRules.PlotSize actual = KingdomPlotRules.PlotSize.Medium;
				if (KingdomPlots.TryReadRect(item, out KingdomPlotRules.PlotRect rect))
					actual = KingdomGuestRules.ClassifyRectTier(rect.Width, rect.Height);
				if (actual > Tier) Tier = actual;
				if (actual < KingdomGuestRules.LegendaryTraderFineHouseTier
					|| KingdomLodging.ResidentsOf(Z, item).Count != 0)
					continue;
				if (chosen == null || string.CompareOrdinal(plotId, chosenPlot) < 0)
				{
					chosen = item;
					chosenPlot = plotId;
					chosenTier = actual;
				}
			}
			if (chosen != null) Tier = chosenTier;
			return chosen;
		}

		private static bool ConfigureLegendaryTraderShop(GameObject Trader, int Tier)
		{
			if (!GameObject.Validate(Trader)
				|| Tier < KingdomGuestRules.LegendaryTraderMinimumShopTier)
				return false;
			GenericInventoryRestocker restocker = Trader.GetPart<GenericInventoryRestocker>();
			if (restocker == null)
			{
				MetricsManager.LogError("ThousandAndFirst legendary trader has no inventory restocker; "
					+ Trader.Blueprint + " remains a citizen but cannot publish a shop.");
				return false;
			}
			restocker.Clear();
			restocker.AddTable("Tier" + Tier + "Wares");
			restocker.Chance = 100;
			Trader.SetIntProperty("InventoryTier", Tier);
			Trader.SetIntProperty("VillageMerchant", 1);
			KingdomSystem.Guard("legendary trader restock", delegate
			{
				restocker.PerformRestock(Silent: true);
			});
			return true;
		}

		internal static void AppendGuestbookLine(KingdomSystem System, string Line)
		{
			if (System.GuestbookLines == null)
			{
				System.GuestbookLines = new List<string>();
			}
			System.GuestbookLines.Add(Line);
			if (System.GuestbookLines.Count > KingdomGuestRules.GuestbookMaxEntries)
			{
				System.GuestbookLines.RemoveAt(0);
			}
		}

		internal static void AppendLifecycleLine(KingdomSystem System, string Line)
		{
			if (!string.IsNullOrEmpty(Line)) AppendGuestbookLine(System, Line);
		}

		/// <summary>Creates an unplaced notable from one frozen lifecycle plan.</summary>
		internal static GameObject CreateLifecycleNotable(KingdomLifecycleOperation op,
			KingdomLifecycleProjection projection)
		{
			if (op == null || projection == null || op.Lane != KingdomLifecycleLane.NotableGuest
				|| op.Action != KingdomLifecycleAction.Spawn) return null;
			GameObject guest;
			try { guest = GameObject.Create(projection.Blueprint); }
			catch { return null; }
			if (!GameObject.Validate(guest)) return null;
			guest.SetIntProperty(NotableGuestProperty, 1);
			guest.SetIntProperty(HookKindProperty, op.Kind);
			guest.SetStringProperty(HookTextProperty, op.Detail ?? "a road still unwalked");
			guest.SetStringProperty(OriginProperty, op.Origin ?? "the road");
			if (!string.IsNullOrEmpty(op.ObjectName))
				guest.GiveProperName(op.ObjectName, Force: true);
			if (guest.HasTag(LegendaryTraderTag))
			{
				string title = op.DisplayFaction;
				if (!string.IsNullOrEmpty(title)) guest.RequirePart<Titles>().AddTitle(title, -40);
			}
			KingdomGuestRules.HookKind kind = (KingdomGuestRules.HookKind)op.Kind;
			// A third-party notable blueprint may already own a quest or conversation graph.
			// Qud's helper removes that part by default, so use it only on a genuinely blank body.
			if (guest.GetPart<ConversationScript>() == null)
			{
				Qud.API.ConversationsAPI.addSimpleConversationToObject(guest,
					KingdomGuestRules.ArrivalGreeting(kind), "Live and drink.",
					Question: "What are you really here for?", Answer: "There's " + op.Detail
						+ ", if I ever get around to it. For now I'm only walking.");
			}
			return guest;
		}

		/// <summary>One resident-row mutation enclosed by Lodge's domain lease. Re-entry recognizes
		/// exact already-enrolled evidence; it never adds a second row or consults compatibility lists.</summary>
		internal static bool ApplyLifecycleLodge(KingdomSystem system, GameObject guest,
			KingdomLifecycleOperation op)
		{
			if (system == null || op == null || !GameObject.Validate(guest)
				|| guest.ID != op.ObjectId || guest.Blueprint != op.Blueprint) return false;
			KingdomLifecycleResourceLease roster = op.ResourceLeases.Find(l =>
				l != null && l.Kind == KingdomLifecycleResourceKind.Roster);
			if (roster == null || roster.Before < 0L || roster.Before > int.MaxValue
				|| roster.After != roster.Before + 1L) return false;
			int before = (int)roster.Before;
			int onRoll = Simulation.City.KingdomResidents.OnRollCount(system);
			if (onRoll != before && onRoll != roster.After) return false;
			GameObject fineHouse = null;
			if (op.Target == 1)
			{
				fineHouse = string.IsNullOrEmpty(op.ObjectMarker)
					? null : GameObject.FindByID(op.ObjectMarker);
				if (!GameObject.Validate(fineHouse)
					|| fineHouse.CurrentZone == null || fineHouse.CurrentZone != guest.CurrentZone
					|| !string.Equals(KingdomUpgrade.DesignKeyOf(fineHouse), "finehouse",
						StringComparison.Ordinal)
					|| KingdomLodging.IsCondemned(fineHouse)
					|| !KingdomPlots.TryReadRect(fineHouse, out KingdomPlotRules.PlotRect rect)
					|| KingdomGuestRules.ClassifyRectTier(rect.Width, rect.Height)
						< KingdomGuestRules.LegendaryTraderFineHouseTier
					|| op.PlunderRequested < KingdomGuestRules.LegendaryTraderMinimumShopTier) return false;
				List<GameObject> residents = KingdomLodging.ResidentsOf(fineHouse.CurrentZone, fineHouse);
				for (int i = 0; i < residents.Count; i++)
					if (residents[i] != guest) return false;
			}
			string intent = "intent:" + op.Id;
			string receipt = guest.GetStringProperty(LodgeReceiptProperty);
			if (receipt != op.Id && receipt != intent)
			{
				if (guest.GetIntProperty(NotableGuestProperty) != 1) return false;
				guest.SetStringProperty(LodgeReceiptProperty, intent);
			}
			if (!string.IsNullOrEmpty(op.Creed))
			{
				string held = guest.GetStringProperty(KingdomCreed.CreedProperty);
				system.CreedCounts.TryGetValue(op.Creed, out int currentCreed);
				if (currentCreed != op.Count && currentCreed != op.Count + 1) return false;
				if (!string.IsNullOrEmpty(held)
					&& !string.Equals(held, op.Creed, StringComparison.Ordinal)) return false;
				guest.SetStringProperty(KingdomCreed.CreedProperty, op.Creed);
				if (currentCreed == op.Count) system.CreedCounts[op.Creed] = currentCreed + 1;
			}
			if (!KingdomFounding.EnrollCitizen(guest,
				KingdomCitizenshipEnrollmentReason.GuestAdoption,
				op.CreatedTick)) return false;
			guest.SetIntProperty("KingdomBorn", 1);
			guest.DisplayName = KingdomPresentation.Rich(op.ObjectName);
			guest.SetStringProperty("KingdomName", op.ObjectName);
			guest.SetStringProperty("KingdomOrigin", op.Origin ?? "");
			guest.SetIntProperty(NotableGuestProperty, 0);
			if (op.Target == 1)
			{
				guest.SetIntProperty(LegendaryTraderResidentProperty, 1);
				guest.SetStringProperty(KingdomLodging.HomePlotIdProperty,
					fineHouse.GetStringProperty(KingdomPlots.PlotIdProperty));
				if (!ConfigureLegendaryTraderShop(guest, op.PlunderRequested)) return false;
			}
			Simulation.City.KingdomCityBook residentBook;
			int residentId;
			if (!Simulation.City.KingdomResidents.TryEnsureRow(system, guest, op.Origin,
				op.Faction, op.CreatedTick, out residentBook, out residentId)) return false;
			guest.SetStringProperty(LodgeReceiptProperty, op.Id);
			// Lodging changes civic status, not the guest's native/owned conversation graph.
			if (op.Outbox != null && op.Outbox.ChronicleAccomplishment)
				system.FirstNotableGuestLodged = true;
			return true;
		}

		internal static bool LifecycleLodgeComplete(KingdomSystem system, GameObject guest,
			KingdomLifecycleOperation op)
		{
			GameObject fineHouse = op == null || string.IsNullOrEmpty(op.ObjectMarker)
				? null : GameObject.FindByID(op.ObjectMarker);
			Simulation.City.KingdomCityBook residentBook;
			int residentId;
			Simulation.City.KingdomCityState state;
			Simulation.City.KingdomCityFault fault;
			int rowIndex;
			Simulation.City.KingdomResidentRow row;
			bool exactRow = system != null && GameObject.Validate(guest)
				&& Simulation.City.KingdomResidents.TryLocate(system, guest, out residentBook,
					out residentId)
				&& residentBook.TryRead(out state, out fault)
				&& state.TryResidentIndex(residentId, out rowIndex)
				&& state.TryResident(rowIndex, out row)
				&& Simulation.City.KingdomResidentRules.OnTheRoll(row)
				&& string.Equals(row.Name, op?.ObjectName, StringComparison.Ordinal)
				&& string.Equals(row.Origin, op?.Origin ?? "", StringComparison.Ordinal)
				&& string.Equals(row.Arrived, op?.Faction ?? "", StringComparison.Ordinal);
			return system != null && op != null && GameObject.Validate(guest)
				&& guest.ID == op.ObjectId
				&& guest.GetStringProperty(LodgeReceiptProperty) == op.Id
				&& exactRow
				&& Simulation.City.KingdomResidents.OnRollCount(system) == op.Defence + 1
				&& (op.Target != 1 || (guest.GetIntProperty(LegendaryTraderResidentProperty) == 1
					&& GameObject.Validate(fineHouse)
					&& guest.GetStringProperty(KingdomLodging.HomePlotIdProperty)
						== fineHouse.GetStringProperty(KingdomPlots.PlotIdProperty)
					&& guest.GetIntProperty("VillageMerchant") == 1
					&& guest.GetIntProperty("InventoryTier") == op.PlunderRequested));
		}

		/// <summary>
		/// The guestbook's own reading, appended to the roll of settlers report. Call once from
		/// <c>KingdomReports.Roll</c>, after its own text is built. Empty string when there is
		/// nothing to add, so the appendix never leaves a bare heading behind.
		/// </summary>
		public static string RollAppendix(KingdomSystem System)
		{
			if (System == null || System.GuestbookLines == null || System.GuestbookLines.Count == 0)
			{
				return "";
			}
			StringBuilder text = new StringBuilder();
			text.Append("\n\n{{C|The guestbook}}");
			for (int i = 0; i < System.GuestbookLines.Count; i++)
			{
				text.Append("\n").Append(System.GuestbookLines[i]);
			}
			return text.ToString();
		}

		// ==================================================================================
		// The carry-sign
		// ==================================================================================

		/// <summary>
		/// Plants a carry-sign at <paramref name="Actor"/>'s current cell, on whatever container
		/// or pile stands there. Call from <see cref="XRL.World.Parts.r_KingdomCarrySign"/>'s
		/// inventory action.
		/// </summary>
		public static void AttemptPlantCarrySign(GameObject Actor, GameObject Sign)
		{
			if (!CarrySignEnabled || Actor == null || Sign == null)
			{
				return;
			}
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			if (!KingdomMaster.NewWorkAllowed(system))
			{
				Popup.Show("Settlement simulation is paused; no new haul can be marked.");
				return;
			}
			Cell cell = Actor.CurrentCell;
			Zone zone = Actor.CurrentZone;
			if (cell == null || zone == null)
			{
				Popup.Show("There is no ground here to plant a sign on.");
				return;
			}
			KingdomCarryRuntime.PlantPlan plan;
			string failure;
			long now = The.Game.TimeTicks;
			if (!KingdomCarryRuntime.TryPreparePlant(system, Actor, Sign, zone, cell, now,
				out plan, out failure))
			{
				Popup.Show(failure);
				return;
			}
			// Consent precedes reservation and every physical callback. The prompt names every
			// whole object/stack and the distance-scaled wait frozen by the draft plan.
			if (Popup.ShowYesNo(KingdomGuestRules.PlantConfirm(plan.Description, plan.Days))
				!= DialogResult.Yes)
			{
				return;
			}
			if (!KingdomCarryRuntime.PublishPlant(plan, out failure))
			{
				Popup.Show(failure);
				return;
			}
			MessageQueue.AddPlayerMessage(KingdomGuestRules.PlantedMessage(plan.Days));
			KingdomLog.Log("carry-sign: exact manifest planted days=" + plan.Days
				+ " objects=" + plan.Sources.Count);
		}

		/// <summary>Compatibility resolver for v5 saves only. New work never enters this scalar
		/// destroy/mint lane; a zero-material System.Haul is the v6 schedule projection.</summary>
		private static void ResolveLegacyHaulIfDue(KingdomSystem System, Zone Z,
			KingdomSurvey Survey, long TimeTicks)
		{
			KingdomCarryHaul haul = System.Haul;
			if (haul == null || !string.Equals(System.CurrentSettlementId,
				haul.DestinationSettlementId, StringComparison.Ordinal) ||
				!KingdomIdentityRules.IsSettlementId(haul.DestinationSettlementId) ||
				!KingdomGuestRules.ShouldResolveHaul(TimeTicks, haul.DueTick))
			{
				return;
			}
			KingdomMaterialTally manifest = new KingdomMaterialTally();
			manifest.Set(KingdomMaterial.Mud, haul.Mud);
			manifest.Set(KingdomMaterial.Brush, haul.Brush);
			manifest.Set(KingdomMaterial.Timber, haul.Timber);
			manifest.Set(KingdomMaterial.Stone, haul.Stone);
			manifest.Set(KingdomMaterial.Marble, haul.Marble);
			manifest.Set(KingdomMaterial.Scrap, haul.Scrap);
			if (manifest.Total() <= 0) return;
			string description = manifest.Describe() ?? "the load";
			bool raidActive = System.RaidState == 1;
			bool raidersPresent = Survey != null && Survey.Raiders.Count > 0;
			if (KingdomGuestRules.HaulWaitsForSafety(raidActive, raidersPresent))
			{
				KingdomLog.Log("carry-sign: due haul retained while threat stands manifest="
					+ description);
				return;
			}
			System.Haul = null;
			int spilled = KingdomMaterials.Deliver(System, Z, manifest);
			KingdomChronicle.Record(System, KingdomGuestRules.DeliveredChronicleLine(KingdomPresentation.Rich(System.SeatName), description));
			System.Ledger.Note(KingdomGuestRules.DeliveredLedgerNote(description));
			KingdomLog.Log("carry-sign: delivered manifest=" + description + " spilled=" + spilled);
		}

	}

	/// <summary>
	/// The realm's one carry-sign haul in flight: materials already swept from their origin,
	/// waiting to be poured into the destination settlement's stockpiles the next time it
	/// activates and the haul is due. Held on <see cref="KingdomSystem"/> directly, realm-level
	/// like <c>KingdomSystem.Manifest</c> — a haul is addressed to an immutable settlement id;
	/// the carried name is prose only, so it survives renames and every seat swap untouched.
	/// </summary>
	[Serializable]
	public class KingdomCarryHaul
#if !TAF_TESTS
		: IComposite
#endif
	{
		/// <summary>Zone the sign was planted in, kept for the chronicle and for nothing the
		/// resolver reads back.</summary>
		public string OriginZoneID;

		public int OriginX;

		public int OriginY;

		/// <summary>Immutable destination authority. The name below is prose only.</summary>
		public string DestinationSettlementId;

		/// <summary>The settlement's frozen display name, used only in prose.</summary>
		public string DestinationSettlementName;

		public long PlantedTick;

		/// <summary>Absolute tick the haul is ready to resolve. No expiry beyond this — absence
		/// never punishes; a haul left unresolved simply waits for the next attended pass of its
		/// destination, exactly as a raid warning waits out an absent founder.</summary>
		public long DueTick;

		public int Mud;

		public int Brush;

		public int Timber;

		public int Stone;

		public int Marble;

		public int Scrap;

#if !TAF_TESTS
		public bool WantFieldReflection => false;

		public void Write(SerializationWriter Writer)
		{
			Writer.WriteNamedFields(this, typeof(KingdomCarryHaul));
		}

		public void Read(SerializationReader Reader)
		{
			Reader.ReadNamedFields(this, typeof(KingdomCarryHaul));
		}
#endif
	}
}

// The engine resolves an XML <part Name="X"/> as the single type "XRL.World.Parts.X":
// GamePartBlueprint.Namespace defaults to that string (GamePartBlueprint.cs:178) and
// T => ModManager.ResolveType(Namespace, Name) (:240) tries only that one name.
// ModManager.ResolveType's doc comment promises a bare-TypeID fallback, but the code
// (ModManager.cs:307-321) does not do it. So a part named in XML MUST live in this
// namespace or the object is built without it, silently. Only the parts move; the rest of
// the guestbook and the carry-sign stay where the mod's code lives.
namespace XRL.World.Parts
{
	/// <summary>
	/// Carried by a spawned notable guest (<see cref="ThousandAndFirst.KingdomGuestbook"/>'s
	/// <c>SpawnNotableGuest</c>). Offers the one interactive moment a notable presents: the
	/// founder can lodge them into the settlement. Everything the action actually does lives in
	/// <see cref="ThousandAndFirst.KingdomGuestbook.TryLodge"/>; this part is only the event
	/// plumbing, the same split <c>r_KingdomGuest</c> uses for offering water.
	/// </summary>
	[Serializable]
	public class r_KingdomNotableGuest : IPart
	{
		public override bool WantEvent(int ID, int cascade)
		{
			if (!base.WantEvent(ID, cascade) && ID != GetInventoryActionsEvent.ID)
			{
				return ID == InventoryActionEvent.ID;
			}
			return true;
		}

		public override bool HandleEvent(GetInventoryActionsEvent E)
		{
			if (ParentObject.GetIntProperty(ThousandAndFirst.KingdomGuestbook.NotableGuestProperty) == 1)
			{
				E.AddAction("Lodge", "lodge notable guest", "r_LodgeNotableGuest", null, 'l', FireOnActor: false, 5);
			}
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(InventoryActionEvent E)
		{
			if (E.Command == "r_LodgeNotableGuest" && E.Actor != null && E.Actor.IsPlayer())
			{
				ThousandAndFirst.KingdomGuestbook.TryLodge(ParentObject);
			}
			return base.HandleEvent(E);
		}
	}

	/// <summary>
	/// Carried by the carry-sign item. Offers the plant action; everything the action actually
	/// does lives in <see cref="ThousandAndFirst.KingdomGuestbook.AttemptPlantCarrySign"/>, the
	/// same split <c>r_FounderBasin</c> uses for founding.
	/// </summary>
	[Serializable]
	public class r_KingdomCarrySign : IPart
	{
		public override bool WantEvent(int ID, int cascade)
		{
			if (!base.WantEvent(ID, cascade) && ID != GetInventoryActionsEvent.ID)
			{
				return ID == InventoryActionEvent.ID;
			}
			return true;
		}

		public override bool HandleEvent(GetInventoryActionsEvent E)
		{
			E.AddAction("Plant carry-sign", "plant carry-sign", "r_PlantCarrySign", null, 'p', FireOnActor: false, 5);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(InventoryActionEvent E)
		{
			if (E.Command == "r_PlantCarrySign" && E.Actor != null && E.Actor.IsPlayer())
			{
				ThousandAndFirst.KingdomGuestbook.AttemptPlantCarrySign(E.Actor, E.Item);
			}
			return base.HandleEvent(E);
		}
	}
}
