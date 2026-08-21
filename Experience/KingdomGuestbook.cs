using System;
using System.Collections.Generic;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.Rules;
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
	/// porters to fetch. Mirrors <c>KingdomManifest</c>'s own precedent exactly: one haul in
	/// flight at a time, the goods drawn from their origin the moment the sign is planted (escrow,
	/// not a promise — the same idiom <c>KingdomManifest.Drams</c> documents), and delivery
	/// resolved only on an attended pass of the destination settlement.
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

		private const string HookKindProperty = "KingdomGuestHookKind";

		private const string HookTextProperty = "KingdomGuestHookText";

		private const string OriginProperty = "KingdomOrigin";

		public static void OnZoneActivated(KingdomSystem System, Zone Z, KingdomSurvey Survey)
		{
			if (System == null || !System.Founded || Z == null || Survey == null || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				return;
			}
			long timeTicks = The.Game.TimeTicks;
			if (GuestsEnabled)
			{
				RunNotableGuestPass(System, Z, timeTicks);
			}
			if (CarrySignEnabled)
			{
				ResolveHaulIfDue(System, Z, timeTicks);
			}
		}

		// ==================================================================================
		// Guests at the gate
		// ==================================================================================

		private static void RunNotableGuestPass(KingdomSystem System, Zone Z, long TimeTicks)
		{
			GameObject guest = FindNotableGuest(Z);
			if (guest != null)
			{
				if (KingdomGuestRules.ShouldDepartUnattended(TimeTicks, System.NotableGuestDepartTick))
				{
					DepartUnattended(System, guest);
				}
				return;
			}
			if (System.NextNotableGuestTick <= 0)
			{
				// First eligible pass ever: defer rather than draw on the spot, the same way
				// KingdomLocus seeds its own NextGuestTick. A settlement gets a moment to exist
				// before word of anything notable reaches the road.
				System.NextNotableGuestTick = KingdomGuestRules.NextDueTick(TimeTicks);
				return;
			}
			if (!KingdomGuestRules.ShouldArrive(TimeTicks, System.NextNotableGuestTick))
			{
				return;
			}
			SpawnNotableGuest(System, Z, TimeTicks);
		}

		private static GameObject FindNotableGuest(Zone Z)
		{
			foreach (GameObject item in Z.GetObjects())
			{
				if (item.GetIntProperty(NotableGuestProperty) == 1)
				{
					return item;
				}
			}
			return null;
		}

		/// <summary>
		/// Picks which notable guest blueprint is walking up the road, the same way
		/// <c>KingdomLocus.GuestBlueprint</c> does: rolled from a population table other mods can
		/// merge into, with a hard fallback if the table is missing or empty.
		/// </summary>
		private static string NotableGuestBlueprint()
		{
			try
			{
				PopulationResult result = PopulationManager.RollOneFrom("r_KingdomNotableGuests");
				if (result != null && !string.IsNullOrEmpty(result.Blueprint) && GameObjectFactory.Factory.HasBlueprint(result.Blueprint))
				{
					return result.Blueprint;
				}
			}
			catch (Exception error)
			{
				MetricsManager.LogError("ThousandAndFirst notable guest roll", error);
			}
			return "r_KingdomNotableGuest";
		}

		private static void SpawnNotableGuest(KingdomSystem System, Zone Z, long TimeTicks)
		{
			List<Cell> emptyCells = Z.GetEmptyCells((Cell c) => c.IsPassable() && !c.HasObjectWithPart("LiquidVolume"));
			if (emptyCells == null || emptyCells.Count == 0)
			{
				emptyCells = Z.GetEmptyCells();
			}
			if (emptyCells == null || emptyCells.Count == 0)
			{
				// No open ground this pass. Try again next pass — a missed notable for want of
				// room is the same "no penalty" case KingdomLocus already accepts for its own
				// travellers.
				return;
			}
			Cell cell = emptyCells.GetRandomElement();
			GameObject guest = GameObject.Create(NotableGuestBlueprint());
			if (guest == null)
			{
				return;
			}
			cell.AddObject(guest);
			guest.MakeActive();
			guest.SetIntProperty(NotableGuestProperty, 1);
			KingdomGuestRules.HookKind kind;
			string hookText;
			DrawHook(System, TimeTicks, out kind, out hookText);
			guest.SetIntProperty(HookKindProperty, (int)kind);
			guest.SetStringProperty(HookTextProperty, hookText);
			string origin = KingdomRules.Origins[Stat.Random(0, KingdomRules.Origins.Length - 1)];
			guest.SetStringProperty(OriginProperty, origin);
			string given = XRL.Names.NameMaker.MakeName(guest, null, null, "human", null, null, null, null, null, null, null, null, null, FailureOkay: true);
			if (!string.IsNullOrEmpty(given))
			{
				guest.DisplayName = given;
			}
			Qud.API.ConversationsAPI.addSimpleConversationToObject(
				guest,
				KingdomGuestRules.ArrivalGreeting(kind),
				"Live and drink.",
				Question: "What are you really here for?",
				Answer: "There's " + hookText + ", if I ever get around to it. For now I'm only walking.");
			System.NotableGuestDepartTick = KingdomGuestRules.DepartTickFor(TimeTicks);
			KingdomChronicle.Record(System, KingdomGuestRules.ArrivalChronicleLine(guest.ShortDisplayName, System.SeatName));
			KingdomLog.Log("guestbook: notable arrived kind=" + kind + " depart=" + System.NotableGuestDepartTick);
		}

		private static void DrawHook(KingdomSystem System, long TimeTicks, out KingdomGuestRules.HookKind Kind, out string HookText)
		{
			string settlementId = KingdomChronicle.SettlementId(System.KingdomFactionName);
			ulong ordinal = (ulong)TimeTicks;
			SemanticEventKey key;
			KernelFaultCode fault;
			ulong kindRoll;
			ulong flavorRoll;
			if (SemanticEventKey.TryCreate(1, settlementId, "taf:guest:hook:v1", 1u, ordinal, out key, out fault)
				&& CounterRandom.TryDrawBelow(default(KernelSeed128), key, 0u, (ulong)KingdomGuestRules.HookKindCount, out kindRoll, out fault)
				&& CounterRandom.TryDrawBelow(default(KernelSeed128), key, 1u, 1000uL, out flavorRoll, out fault))
			{
				Kind = KingdomGuestRules.PickHookKind(kindRoll);
				HookText = KingdomGuestRules.HookText(Kind, flavorRoll);
				return;
			}
			// The kernel draw refused — no settlement name yet, or this machine's crypto provider
			// is failing. A notable still arrives; the hook just loses its reload-stable drift for
			// this one guest rather than the arrival being lost outright.
			Kind = KingdomGuestRules.PickHookKind((ulong)Stat.Random(0, KingdomGuestRules.HookKindCount - 1));
			HookText = KingdomGuestRules.HookText(Kind, (ulong)Stat.Random(0, 999));
		}

		private static void DepartUnattended(KingdomSystem System, GameObject Guest)
		{
			string name = Guest.ShortDisplayName;
			KingdomGuestRules.HookKind kind = (KingdomGuestRules.HookKind)Guest.GetIntProperty(HookKindProperty);
			string hookText = Guest.GetStringProperty(HookTextProperty) ?? "";
			KingdomChronicle.RecordDisputed(
				System,
				KingdomGuestRules.DepartedChronicleLine(name, System.SeatName),
				KingdomGuestRules.DepartedOutsiderRumor(name, kind, hookText));
			System.Ledger.Note("{{K|" + name + " waited a while at the gate, found no bed offered, and moved on. What "
				+ name + " was chasing is a rumor on the road now — nothing is lost.}}");
			AppendGuestbookLine(System, KingdomGuestRules.GuestbookLine(name, kind, hookText, Lodged: false));
			System.NextNotableGuestTick = KingdomGuestRules.NextDueTick(The.Game.TimeTicks);
			System.NotableGuestDepartTick = 0L;
			Guest.Obliterate();
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
			if (zone == null || !system.Founded || !system.ClaimedZones.Contains(zone.ZoneID))
			{
				return;
			}
			KingdomGuestRules.HookKind kind = (KingdomGuestRules.HookKind)Guest.GetIntProperty(HookKindProperty);
			string hookText = Guest.GetStringProperty(HookTextProperty) ?? "";
			KingdomPlotRules.PlotSize bestTier = BestHousingTier(zone);
			// A guest is judged by the raw bed count against population, on the same live survey,
			// and deliberately NOT by the settlers' own assignment-level gate: brief Addendum 4b
			// binds housing for people who JOIN the settlement, and says guests are unchanged
			// because they never stay without lodging anyway. A visitor is not assigned a home,
			// spends nobody's grace, and never leaves for want of one.
			KingdomSurvey survey = KingdomSurvey.Take(zone);
			bool hasRoom = KingdomRules.HasRoomToHouse(system.Population, survey.Beds);
			bool hasTier = bestTier != KingdomPlotRules.PlotSize.None && bestTier >= KingdomGuestRules.RequiredTier(kind);
			KingdomGuestRules.LodgingVerdict verdict = KingdomGuestRules.AssessLodging(hasTier, hasRoom);
			if (verdict != KingdomGuestRules.LodgingVerdict.Lodged)
			{
				Popup.Show(verdict == KingdomGuestRules.LodgingVerdict.NoTier
					? KingdomGuestRules.NoTierRefusal(kind)
					: KingdomGuestRules.NoRoomRefusal());
				return;
			}
			KingdomFounding.EnrollCitizen(Guest);
			Guest.SetIntProperty("KingdomBorn", 1);
			Guest.SetIntProperty(NotableGuestProperty, 0);
			string name = Guest.ShortDisplayName;
			string origin = Guest.GetStringProperty(OriginProperty) ?? "";
			system.RosterNames.Add(name);
			system.RosterOrigins.Add(origin);
			system.RosterArrived.Add(XRL.World.Calendar.GetDay() + " of " + XRL.World.Calendar.GetMonth() + ", " + XRL.World.Calendar.GetYear() + " AR");
			system.OriginCounts.TryGetValue(origin, out int originCount);
			system.OriginCounts[origin] = originCount + 1;
			KingdomCreed.Record(system, Guest, KingdomCreed.Draw(system));
			Qud.API.ConversationsAPI.addSimpleConversationToObject(
				Guest,
				"Live and drink. I mean to stay this time.",
				"Live and drink.",
				Question: "What were you bound for, before?",
				Answer: KingdomGuestRules.LodgedConversationAnswer(kind, hookText));
			KingdomGrowth.ConsumeStoredWater(zone, KingdomRules.DramsPerArrival);
			system.Population++;
			bool milestone = !system.FirstNotableGuestLodged;
			KingdomChronicle.Record(system, KingdomGuestRules.LodgedChronicleLine(name, system.SeatName, kind), Accomplishment: milestone);
			if (milestone)
			{
				system.FirstNotableGuestLodged = true;
				MessageQueue.AddPlayerMessage("{{C|" + system.KingdomDisplayName + " has lodged its first notable guest.}}");
			}
			MessageQueue.AddPlayerMessage(KingdomGuestRules.LodgedMessage(name, kind));
			AppendGuestbookLine(system, KingdomGuestRules.GuestbookLine(name, kind, hookText, Lodged: true));
			system.NextNotableGuestTick = KingdomGuestRules.NextDueTick(The.Game.TimeTicks);
			system.NotableGuestDepartTick = 0L;
			KingdomLog.Log("guestbook: lodged " + name + " kind=" + kind);
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
			foreach (GameObject item in Z.GetObjects())
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

		private static void AppendGuestbookLine(KingdomSystem System, string Line)
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
			Cell cell = Actor.CurrentCell;
			Zone zone = Actor.CurrentZone;
			if (cell == null || zone == null)
			{
				Popup.Show("There is no ground here to plant a sign on.");
				return;
			}
			bool hasRoad = false;
			int distance = 0;
			if (KingdomRules.TryParseZoneID(zone.ZoneID, out string world, out int gx, out int gy, out int z))
			{
				hasRoad = NearestClaimedDistance(system, world, gx, gy, z, out distance);
			}
			GameObject container;
			KingdomMaterialTally manifest = ScanManifest(cell, out container);
			int total = manifest.Total();
			KingdomGuestRules.PlantVerdict verdict = KingdomGuestRules.AssessPlant(system.Founded, system.Haul != null, hasRoad, total);
			if (verdict != KingdomGuestRules.PlantVerdict.Planted)
			{
				Popup.Show(KingdomGuestRules.PlantRefusal(verdict));
				return;
			}
			int days = KingdomGuestRules.HaulDays(distance);
			string description = manifest.Describe() ?? "nothing";
			// Consent before cost: the founder sees the exact manifest and the exact wait before
			// anything is swept. Nothing here is reversible once confirmed.
			if (Popup.ShowYesNo(KingdomGuestRules.PlantConfirm(description, days)) != DialogResult.Yes)
			{
				return;
			}
			RemoveManifestItems(cell, container);
			long plantedTick = The.Game.TimeTicks;
			KingdomCarryHaul haul = new KingdomCarryHaul
			{
				OriginZoneID = zone.ZoneID,
				OriginX = cell.X,
				OriginY = cell.Y,
				DestinationSettlementName = system.SeatName,
				PlantedTick = plantedTick,
				DueTick = KingdomGuestRules.HaulDueTick(plantedTick, days),
				Mud = manifest.Get(KingdomMaterial.Mud),
				Brush = manifest.Get(KingdomMaterial.Brush),
				Timber = manifest.Get(KingdomMaterial.Timber),
				Stone = manifest.Get(KingdomMaterial.Stone),
				Marble = manifest.Get(KingdomMaterial.Marble),
				Scrap = manifest.Get(KingdomMaterial.Scrap)
			};
			system.Haul = haul;
			Sign.Obliterate();
			KingdomChronicle.Record(system, KingdomGuestRules.PlantedChronicleLine(system.SeatName, description, days));
			MessageQueue.AddPlayerMessage(KingdomGuestRules.PlantedMessage(days));
			KingdomLog.Log("carry-sign: planted days=" + days + " manifest=" + description);
		}

		/// <summary>The shortest same-world zone-grid distance from (<paramref name="GX"/>,
		/// <paramref name="GY"/>, <paramref name="Z"/>) to any of the realm's claimed zones.</summary>
		/// <returns>False when no claimed zone shares the given world &mdash; there is no road
		/// to compute a distance along.</returns>
		private static bool NearestClaimedDistance(KingdomSystem System, string World, int GX, int GY, int Z, out int Distance)
		{
			Distance = int.MaxValue;
			bool found = false;
			foreach (string zoneId in System.ClaimedZones)
			{
				if (!KingdomRules.TryParseZoneID(zoneId, out string otherWorld, out int ogx, out int ogy, out int oz) || otherWorld != World)
				{
					continue;
				}
				int d = KingdomGuestRules.ZoneGridDistance(GX, GY, Z, ogx, ogy, oz);
				if (!found || d < Distance)
				{
					Distance = d;
					found = true;
				}
			}
			return found;
		}

		/// <summary>
		/// Reads what a carry-sign would take from <paramref name="CellAt"/>: everything held in
		/// the first container found there, or — with no container — everything recognized lying
		/// directly on the ground. Nothing is removed; see <see cref="RemoveManifestItems"/> for
		/// the half that actually takes it.
		/// </summary>
		private static KingdomMaterialTally ScanManifest(Cell CellAt, out GameObject Container)
		{
			KingdomMaterialTally tally = new KingdomMaterialTally();
			Container = null;
			if (CellAt == null)
			{
				return tally;
			}
			foreach (GameObject item in CellAt.GetObjects())
			{
				if (item.Inventory != null && !item.IsCreature && !item.IsPlayer())
				{
					Container = item;
					break;
				}
			}
			if (Container != null)
			{
				foreach (GameObject held in Container.Inventory.Objects)
				{
					if (KingdomMaterials.TryMaterialOf(held, out KingdomMaterial material))
					{
						tally.Add(material, held.Count);
					}
				}
				return tally;
			}
			foreach (GameObject item in CellAt.GetObjects())
			{
				if (KingdomMaterials.TryMaterialOf(item, out KingdomMaterial material))
				{
					tally.Add(material, item.Count);
				}
			}
			return tally;
		}

		/// <summary>
		/// Actually takes what <see cref="ScanManifest"/> counted, stack-aware
		/// (<c>GameObject.Destroy</c> on a stack decrements it by one and only removes the object
		/// on the last unit — the same idiom <c>KingdomMaterials.MaterialStock.Take</c> uses, and
		/// for the same reason: never trust that a call removed what was asked without measuring
		/// the count before and after, STANDARDS.md &sect;1).
		/// </summary>
		private static void RemoveManifestItems(Cell CellAt, GameObject Container)
		{
			List<GameObject> snapshot = (Container != null)
				? new List<GameObject>(Container.Inventory.Objects)
				: new List<GameObject>(CellAt.GetObjects());
			foreach (GameObject item in snapshot)
			{
				if (!KingdomMaterials.TryMaterialOf(item, out _))
				{
					continue;
				}
				while (GameObject.Validate(item))
				{
					int before = item.Count;
					item.Destroy(null, Silent: true);
					if (GameObject.Validate(item) && item.Count >= before)
					{
						break;
					}
				}
			}
		}

		private static void ResolveHaulIfDue(KingdomSystem System, Zone Z, long TimeTicks)
		{
			KingdomCarryHaul haul = System.Haul;
			if (haul == null || System.SeatName != haul.DestinationSettlementName || !KingdomGuestRules.ShouldResolveHaul(TimeTicks, haul.DueTick))
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
			string description = manifest.Describe() ?? "the load";
			bool raidActive = System.RaidState == 1;
			int riskRoll = DrawRoadRisk(System, haul);
			bool lost = KingdomGuestRules.HaulAtRisk(raidActive, riskRoll);
			System.Haul = null;
			if (lost)
			{
				string factionDisplay = string.IsNullOrEmpty(System.RaidFactionName) ? "raiders" : Faction.GetFormattedName(System.RaidFactionName);
				KingdomChronicle.Record(System, KingdomGuestRules.LostChronicleLine(System.SeatName, factionDisplay, description));
				System.Ledger.Note(KingdomGuestRules.LostLedgerNote(description));
				KingdomLog.Log("carry-sign: lost manifest=" + description);
				return;
			}
			int spilled = KingdomMaterials.Deliver(System, Z, manifest);
			KingdomChronicle.Record(System, KingdomGuestRules.DeliveredChronicleLine(System.SeatName, description));
			System.Ledger.Note(KingdomGuestRules.DeliveredLedgerNote(description));
			KingdomLog.Log("carry-sign: delivered manifest=" + description + " spilled=" + spilled);
		}

		private static int DrawRoadRisk(KingdomSystem System, KingdomCarryHaul Haul)
		{
			string settlementId = KingdomChronicle.SettlementId(System.KingdomFactionName);
			SemanticEventKey key;
			KernelFaultCode fault;
			if (SemanticEventKey.TryCreate(1, settlementId, "taf:carry:risk:v1", 1u, (ulong)Haul.PlantedTick, out key, out fault)
				&& CounterRandom.TryDrawBelow(default(KernelSeed128), key, 0u, 100uL, out ulong value, out fault))
			{
				return (int)value;
			}
			return Stat.Random(0, 99);
		}
	}

	/// <summary>
	/// The realm's one carry-sign haul in flight: materials already swept from their origin,
	/// waiting to be poured into the destination settlement's stockpiles the next time it
	/// activates and the haul is due. Held on <see cref="KingdomSystem"/> directly, realm-level
	/// like <c>KingdomSystem.Manifest</c> — a haul is addressed to a settlement by name, not to
	/// whichever city happens to be seated, so it survives every seat swap untouched.
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

		/// <summary>The settlement this haul is bound for. Delivery fires when this equals the
		/// currently seated city's own name, the same contract <c>KingdomManifest.DestinationName</c>
		/// keeps.</summary>
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
