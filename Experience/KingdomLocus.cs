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
	/// <summary>
	/// The settlement as a place, not a screen: staffs the gathering bench with a keeper whose
	/// talk reflects what is actually happening, and brings the occasional traveller through who
	/// is offered water and leaves &mdash; never enrolled, never housed, never counted.
	/// <para>
	/// Runs from the kingdom's one <c>ZoneActivatedEvent</c> pass, after growth and raids have
	/// already settled the state of the turn (<see cref="KingdomSystem"/>'s wiring calls this
	/// last), and reads that state rather than keeping any clock of its own. Guest arrival and
	/// unattended departure are both decided here, on activation, for the same reason the rest of
	/// the mod avoids a per-turn tick: time away must catch up in one pass, not accrue while
	/// nobody is watching.
	/// </para>
	/// </summary>
	public static class KingdomLocus
	{
		public static bool Enabled => Options.GetOption("r_TAF_OptionLocus") != "No";

		/// <summary>The one building the growth catalogue's "Staff" attribute makes a work; this
		/// names it so the keeper pass can find it among <see cref="KingdomSurvey.Works"/>
		/// without a marker property of its own.</summary>
		public const string BenchBlueprint = "r_KingdomBench";

		/// <summary>Population the keeper had last seen, read fresh each pass to decide
		/// <see cref="KingdomLocusRules.KeeperMood.Growing"/>.</summary>
		private const string KeeperLastPopulationProperty = "KingdomKeeperLastPopulation";

		private const string KeeperMoodProperty = "KingdomKeeperMood";

		public static void OnZoneActivated(KingdomSystem System, Zone Z, KingdomSurvey Survey)
		{
			if (!Enabled || System == null || !System.Founded || Z == null || Survey == null || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				return;
			}
			long timeTicks = The.Game.TimeTicks;
			RunKeeperPass(System, Survey);
			RunGuestPass(System, Z, timeTicks);
		}

		/// <summary>
		/// Crews the gathering bench from the settlement's own settlers and keeps its keeper's
		/// conversation current. The keeper is chosen once and kept while they remain a valid
		/// candidate (<see cref="KingdomLocusRules.SelectKeeper"/>); an unstaffed bench is
		/// demoted back to furniture and says so on examine.
		/// </summary>
		private static void RunKeeperPass(KingdomSystem System, KingdomSurvey Survey)
		{
			GameObject bench = FindBench(Survey);
			if (bench == null)
			{
				return;
			}
			bool staffed = bench.GetIntProperty("KingdomStaffed") == 1;
			GameObject markedKeeper = FindMarkedKeeper(Survey);
			if (!staffed)
			{
				if (markedKeeper != null)
				{
					DemoteKeeper(markedKeeper);
				}
				SetBenchDescription(bench, KingdomLocusRules.BenchDescription(Staffed: false, KeeperName: null));
				return;
			}
			List<string> candidateIDs = new List<string>(Survey.Settlers.Count);
			for (int i = 0; i < Survey.Settlers.Count; i++)
			{
				candidateIDs.Add(Survey.Settlers[i].ID);
			}
			string selectedID = KingdomLocusRules.SelectKeeper(candidateIDs, markedKeeper?.ID);
			if (selectedID == null)
			{
				// Staffed by headcount (KingdomStaffed reads the crewed total, not identities),
				// but nobody the survey can name a settler among this pass. Leave the bench as it
				// was rather than guess; the next pass tries again.
				return;
			}
			GameObject keeper = (markedKeeper != null && markedKeeper.ID == selectedID) ? markedKeeper : FindSettler(Survey, selectedID);
			if (keeper == null)
			{
				return;
			}
			if (markedKeeper != null && markedKeeper != keeper)
			{
				DemoteKeeper(markedKeeper);
			}
			// Read before either property is touched below: a keeper taking the bench for the
			// first time has never had a mood recorded, so their stale (default-zero) reading
			// would otherwise alias KeeperMood.Peaceful and skip building any conversation at
			// all the one time it is most needed.
			bool isNewKeeper = keeper.GetIntProperty("KingdomKeeper") != 1;
			bool grew = System.Population > keeper.GetIntProperty(KeeperLastPopulationProperty);
			KingdomLocusRules.KeeperMood mood = KingdomLocusRules.ClassifyMood(
				DryStreakActive: System.DryStreak > 0,
				RaidIncoming: System.RaidState == 1,
				RecentlyRaided: KingdomLocusRules.WasRecentlyRaided(System.LastRaidTick, The.Game.TimeTicks),
				Grew: grew);
			keeper.SetIntProperty("KingdomKeeper", 1);
			keeper.SetIntProperty(KeeperLastPopulationProperty, System.Population);
			// Rebuilt only on a change (or on first taking the bench) so the founder is never
			// handed an identical greeting twice in a row for no reason.
			if (isNewKeeper || keeper.GetIntProperty(KeeperMoodProperty) != (int)mood)
			{
				keeper.SetIntProperty(KeeperMoodProperty, (int)mood);
				KingdomLocusRules.KeeperSpeech speech = KingdomLocusRules.KeeperSpeechFor(mood, System.KingdomDisplayName);
				// Named Question/Answer, not positional: addSimpleConversationToObject has a
				// second overload (Filter/FilterExtras in those slots instead) that a 5-arg call
				// matches just as well by type, which the compiler cannot break the tie on.
				Qud.API.ConversationsAPI.addSimpleConversationToObject(keeper, speech.Greeting, "Live and drink.", Question: speech.Question, Answer: speech.Answer);
			}
			SetBenchDescription(bench, KingdomLocusRules.BenchDescription(Staffed: true, KeeperName: keeper.ShortDisplayName));
		}

		private static GameObject FindBench(KingdomSurvey Survey)
		{
			for (int i = 0; i < Survey.Works.Count; i++)
			{
				if (Survey.Works[i].Blueprint == BenchBlueprint)
				{
					return Survey.Works[i];
				}
			}
			return null;
		}

		private static GameObject FindMarkedKeeper(KingdomSurvey Survey)
		{
			for (int i = 0; i < Survey.Settlers.Count; i++)
			{
				if (Survey.Settlers[i].GetIntProperty("KingdomKeeper") == 1)
				{
					return Survey.Settlers[i];
				}
			}
			return null;
		}

		private static GameObject FindSettler(KingdomSurvey Survey, string ID)
		{
			for (int i = 0; i < Survey.Settlers.Count; i++)
			{
				if (Survey.Settlers[i].ID == ID)
				{
					return Survey.Settlers[i];
				}
			}
			return null;
		}

		private static void DemoteKeeper(GameObject Keeper)
		{
			Keeper.SetIntProperty("KingdomKeeper", 0);
			Keeper.SetIntProperty(KeeperMoodProperty, 0);
			Qud.API.ConversationsAPI.addSimpleConversationToObject(Keeper, "Someone else has the bench for now. Live and drink, all the same.", "Live and drink.");
		}

		private static void SetBenchDescription(GameObject Bench, string Text)
		{
			Description description = Bench.GetPart<Description>();
			if (description != null)
			{
				description.Short = Text;
			}
		}

		/// <summary>
		/// Brings the occasional traveller through, on the same cadence the pure rules define,
		/// and resolves an unattended departure the moment the founder is back to notice it
		/// happened.
		/// </summary>
		private static void RunGuestPass(KingdomSystem System, Zone Z, long TimeTicks)
		{
			GameObject guest = FindGuest(Z);
			if (guest != null)
			{
				bool offered = guest.GetIntProperty("KingdomGuestOffered") == 1;
				if (!offered && KingdomLocusRules.GuestShouldDepartUnattended(TimeTicks, System.GuestDepartTick))
				{
					DepartGuest(System, guest, Greeted: false);
				}
				return;
			}
			if (System.NextGuestTick <= 0)
			{
				// First eligible pass ever: defer rather than draw on the spot, the same way
				// KingdomGrowth seeds NextArrivalTick. A settlement gets a moment to exist before
				// word of it reaches the road.
				System.NextGuestTick = KingdomLocusRules.NextGuestDueTick(TimeTicks);
				return;
			}
			if (!KingdomLocusRules.GuestShouldArrive(TimeTicks, System.NextGuestTick))
			{
				return;
			}
			SpawnGuest(System, Z, TimeTicks);
		}

		private static GameObject FindGuest(Zone Z)
		{
			foreach (GameObject item in Z.GetObjects())
			{
				if (item.GetIntProperty("KingdomGuest") == 1)
				{
					return item;
				}
			}
			return null;
		}

		/// <summary>
		/// Picks which of the guest blueprints is walking up the road, the same way
		/// <c>KingdomGrowth.SettlerBlueprint</c> does: rolled from a population table other mods
		/// can merge into, with a hard fallback if the table is missing or empty.
		/// </summary>
		private static string GuestBlueprint()
		{
			try
			{
				PopulationResult result = PopulationManager.RollOneFrom("r_KingdomGuests");
				if (result != null && !string.IsNullOrEmpty(result.Blueprint) && GameObjectFactory.Factory.HasBlueprint(result.Blueprint))
				{
					return result.Blueprint;
				}
			}
			catch (Exception error)
			{
				MetricsManager.LogError("ThousandAndFirst guest roll", error);
			}
			return "r_KingdomGuest";
		}

		private static void SpawnGuest(KingdomSystem System, Zone Z, long TimeTicks)
		{
			List<Cell> emptyCells = Z.GetEmptyCells((Cell c) => c.IsPassable() && !c.HasObjectWithPart("LiquidVolume"));
			if (emptyCells == null || emptyCells.Count == 0)
			{
				emptyCells = Z.GetEmptyCells();
			}
			if (emptyCells == null || emptyCells.Count == 0)
			{
				// No open ground this pass. Try again on the next: a missed guest for want of
				// room is exactly the "no penalty" case, not a failure worth announcing.
				return;
			}
			Cell cell = emptyCells.GetRandomElement();
			GameObject guest = GameObject.Create(GuestBlueprint());
			if (guest == null)
			{
				return;
			}
			cell.AddObject(guest);
			guest.MakeActive();
			guest.SetIntProperty("KingdomGuest", 1);
			string origin = KingdomRules.Origins[Stat.Random(0, KingdomRules.Origins.Length - 1)];
			guest.SetStringProperty("KingdomOrigin", origin);
			string given = XRL.Names.NameMaker.MakeName(guest, null, null, "human", null, null, null, null, null, null, null, null, null, FailureOkay: true);
			if (!string.IsNullOrEmpty(given))
			{
				guest.DisplayName = given;
			}
			// Named Question/Answer — see the keeper call site above for why this can't be positional.
			Qud.API.ConversationsAPI.addSimpleConversationToObject(
				guest,
				"Live and drink, if you have it to spare. I'm not staying — just passing through.",
				"Live and drink.",
				Question: "Where are you bound?",
				Answer: "Wherever the road goes next. I heard there was water shared here, and wanted to see it for myself.");
			System.GuestDepartTick = KingdomLocusRules.GuestDepartTickFor(TimeTicks);
			if (KingdomLog.Enabled)
			{
				KingdomLog.Log("guest: arrived origin=" + origin + " depart=" + System.GuestDepartTick);
			}
		}

		/// <summary>
		/// Offers the settlement's own water to a guest, spent from its dedicated stores the same
		/// way an arriving settler's welcome is (<c>KingdomGrowth.ConsumeStoredWater</c>). Called
		/// from <see cref="XRL.World.Parts.r_KingdomGuest"/>'s inventory action; a no-op if the
		/// guest has already been offered water or is no longer present.
		/// </summary>
		/// <param name="Guest">The guest object the player targeted.</param>
		public static void OfferGuestWater(GameObject Guest)
		{
			if (Guest == null || Guest.GetIntProperty("KingdomGuest") != 1 || Guest.GetIntProperty("KingdomGuestOffered") == 1)
			{
				return;
			}
			Zone zone = Guest.CurrentZone;
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			if (zone == null || !system.Founded || !system.ClaimedZones.Contains(zone.ZoneID))
			{
				return;
			}
			int drawn = KingdomGrowth.ConsumeStoredWater(zone, KingdomLocusRules.GuestWaterCostDrams);
			if (drawn < KingdomLocusRules.GuestWaterCostDrams)
			{
				Popup.Show("There is no water to spare for " + Guest.ShortDisplayName + " here.");
				return;
			}
			Guest.SetIntProperty("KingdomGuestOffered", 1);
			Popup.Show(KingdomLocusRules.GuestThanks(Guest.ShortDisplayName, system.KingdomDisplayName));
			DepartGuest(system, Guest, Greeted: true);
		}

		private static void DepartGuest(KingdomSystem System, GameObject Guest, bool Greeted)
		{
			string name = Guest.ShortDisplayName;
			bool milestone = Greeted && !System.FirstGuestGreeted;
			string line = milestone
				? System.KingdomDisplayName + " gave water to its first guest since its founding, and the traveller went on speaking well of it"
				: KingdomLocusRules.GuestChronicleLine(Greeted, System.KingdomDisplayName);
			KingdomChronicle.Record(System, line, Accomplishment: milestone);
			if (milestone)
			{
				System.FirstGuestGreeted = true;
				MessageQueue.AddPlayerMessage("{{C|" + System.KingdomDisplayName + " has had its first guest.}}");
			}
			if (!Greeted)
			{
				System.Ledger.Note(KingdomLocusRules.GuestLedgerNote(name));
			}
			System.NextGuestTick = KingdomLocusRules.NextGuestDueTick(The.Game.TimeTicks);
			System.GuestDepartTick = 0L;
			Guest.Obliterate();
		}
	}
}

// The engine resolves an XML <part Name="X"/> as the single type "XRL.World.Parts.X":
// GamePartBlueprint.Namespace defaults to that string (GamePartBlueprint.cs:178) and
// T => ModManager.ResolveType(Namespace, Name) (:240) tries only that one name.
// ModManager.ResolveType's doc comment promises a bare-TypeID fallback, but the code
// (ModManager.cs:307-321) does not do it. So a part named in XML MUST live in this
// namespace or the object is built without it, silently.
namespace XRL.World.Parts
{
	/// <summary>
	/// Carried by a spawned guest (<see cref="ThousandAndFirst.KingdomLocus"/>'s
	/// <c>SpawnGuest</c>). Adds the one interactive moment a guest offers: the founder can offer
	/// them water from the settlement's own stores. Everything the action actually does lives in
	/// <see cref="ThousandAndFirst.KingdomLocus.OfferGuestWater"/>; this part is only the event
	/// plumbing, the same split <c>r_FounderBasin</c> and <c>r_KingdomScaffold</c> use.
	/// </summary>
	[Serializable]
	public class r_KingdomGuest : IPart
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
			if (ParentObject.GetIntProperty("KingdomGuestOffered") == 0)
			{
				E.AddAction("Offer Water", "offer water", "r_OfferGuestWater", null, 'o', FireOnActor: false, 5);
			}
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(InventoryActionEvent E)
		{
			if (E.Command == "r_OfferGuestWater" && E.Actor != null && E.Actor.IsPlayer())
			{
				ThousandAndFirst.KingdomLocus.OfferGuestWater(ParentObject);
			}
			return base.HandleEvent(E);
		}
	}
}
