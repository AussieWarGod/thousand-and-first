using System;
using System.Collections.Generic;

using ThousandAndFirst;

// XRL.World.Parts, for the reason r_KingdomPlot, r_KingdomSeed, r_KingdomMirrorGate and the lab's
// four all state: GamePartBlueprint resolves a part named in XML as exactly
// "XRL.World.Parts.<Name>" and tries no other name. Only the parts move; everything they do lives
// in ThousandAndFirst.KingdomAnnexe below.
namespace XRL.World.Parts
{
	/// <summary>
	/// The becoming annexe: a clean room, a book, and the city's claim to write in it. The chrome
	/// half of the body doctrine, and the city's one purpose (Addendum 22 A1, Design B).
	/// <para>
	/// It builds no cybernetics of its own and it should not. Vanilla's becoming nooks already
	/// install, remove and price every implant in the game; what they will not do is admit a
	/// mutant, because <c>CyberneticsTerminal.IsAuthorized</c> asks whether the subject is on the
	/// Eaters' rolls (<c>XRL/UI/CyberneticsTerminal.cs:481-488</c>). So the annexe answers that
	/// question rather than replacing the machine that asks it, and the whole shipped cybernetics
	/// system works untouched.
	/// </para>
	/// </summary>
	[Serializable]
	public class r_KingdomBecomingAnnexe : IPart
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
			E.AddAction("Register", "read the city's rolls", "r_OpenAnnexeRegister", null, 'g', FireOnActor: false, 5);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(InventoryActionEvent E)
		{
			if (E.Command == "r_OpenAnnexeRegister" && E.Actor != null && E.Actor.IsPlayer())
			{
				KingdomSystem.Guard("annexe register", delegate
				{
					KingdomAnnexe.OpenRegister(ParentObject, E.Actor);
				});
				return true;
			}
			return base.HandleEvent(E);
		}
	}

	/// <summary>
	/// One person's enrolment, carried on the person, answering the one question vanilla asks.
	/// <para>
	/// <b>The part holds a claim, not an answer.</b> What it stores is which city wrote the roll
	/// and under what id; whether the roll still STANDS is read live off the realm's own cities.
	/// That is what makes secession bite without a single line of secession code: the rolls ride
	/// <c>KingdomSettlement.KeepersRoster</c>, the container that secession, rejoin, exile and
	/// return already move whole (Addendum 22 B1/B6), so a city that walks out takes the book with
	/// it and the machines go back to asking.
	/// </para>
	/// <para>
	/// <b>It only ever raises the answer, never lowers it.</b> <c>IsTrueKinEvent.Check</c> seeds
	/// from the genotype and hands the seed to every handler
	/// (<c>XRL/World/IsTrueKinEvent.cs:32-47</c>); writing <c>false</c> here would let a lapsed
	/// roll un-Kin a True Kin who happened to be carrying this part. It cannot, because this never
	/// writes <c>false</c>.
	/// </para>
	/// </summary>
	[Serializable]
	public class r_KingdomEnrolled : IPart
	{
		/// <summary>The <c>GeneID</c> the roll was written under. The person, not the office &mdash;
		/// an heir is a different creature with a different id, so Addendum 21's succession
		/// honesty rule holds without anything here knowing what succession is.</summary>
		public string Who = "";

		/// <summary>The person as the founder reads them, kept for prose so no sentence anywhere
		/// has to hold a reference to a creature that may be dead.</summary>
		public string Named = "";

		/// <summary>The city that wrote it, for the telling.</summary>
		public string City = "";

		/// <summary>When, for the register's rows and the chronicle.</summary>
		public long Tick;

		/// <summary>Whether the founder has already been told this roll lapsed. STANDARDS 7b's
		/// once-flag, cleared the moment the roll is held again &mdash; a rejoin is not a second
		/// piece of bad news.</summary>
		public bool LapseAnnounced;

		// Tick the live read was last taken on, and what it said. The event fires from every
		// tonic, every terminal and every haggle in the game, so the read past this happens at
		// most once per tick per creature. Deliberately NOT serialized: a cached answer is not
		// state, and a save that carried one would answer for a realm it was not asked about.
		[NonSerialized]
		private long CachedTick = -1L;

		[NonSerialized]
		private bool CachedHeld;

		public override bool SameAs(IPart p)
		{
			return false;
		}

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade) || ID == PooledEvent<IsTrueKinEvent>.ID;
		}

		/// <summary>
		/// The whole override, and it is four lines because the door Freehold installed is four
		/// lines wide. <c>E.Object</c> is checked because a pooled event is dispatched to the
		/// object's parts and a part that answered for somebody else would be answering a question
		/// it was not asked.
		/// </summary>
		public override bool HandleEvent(IsTrueKinEvent E)
		{
			if (E.Object == ParentObject)
			{
				E.IsTrueKin = KingdomAnnexeRules.AnswersTrueKin(E.IsTrueKin, Held());
			}
			return base.HandleEvent(E);
		}

		/// <summary>
		/// Whether the realm still keeps this roll.
		/// <para>
		/// Preconditions: none. Side effects: may say the lapse line once. Failure mode: never
		/// throws &mdash; a failed read answers "not held", which closes a door rather than
		/// opening one, and this runs inside the engine's own event dispatch (STANDARDS 9).
		/// </para>
		/// </summary>
		public bool Held()
		{
			if (The.Game == null || string.IsNullOrEmpty(Who))
			{
				return false;
			}
			long now = The.Game.TimeTicks;
			if (CachedTick == now)
			{
				return CachedHeld;
			}
			bool held = false;
			KingdomSystem.Guard("enrolment read", delegate
			{
				held = KingdomAnnexe.RealmHolds(Who);
			});
			CachedTick = now;
			CachedHeld = held;
			if (held)
			{
				// Unsaid in silence, exactly as the mirror-gate's brownout is: coming back is not
				// a second piece of news, and a founder who rejoins should be able to be told
				// again if it ever happens twice.
				LapseAnnounced = false;
			}
			else if (!LapseAnnounced && ParentObject != null && ParentObject.IsPlayer())
			{
				LapseAnnounced = true;
				KingdomSystem.Guard("enrolment lapse", delegate
				{
					KingdomAnnexe.AnnounceLapse(City);
				});
			}
			return held;
		}

#if !TAF_TESTS
		/// <summary>
		/// Named fields, replacing the positional path outright &mdash; the discipline
		/// <c>r_KingdomLabRecord</c> states in full. A field-layout change between mod versions
		/// silently drops a positional part, and dropping THIS part costs a founder the thing
		/// their city paid a megastructure for.
		/// </summary>
		public override void Write(GameObject Basis, SerializationWriter Writer)
		{
			Writer.WriteNamedFields(this, typeof(r_KingdomEnrolled));
		}

		public override void Read(GameObject Basis, SerializationReader Reader)
		{
			Reader.ReadNamedFields(this, typeof(r_KingdomEnrolled));
			Who = Who ?? "";
			Named = Named ?? "";
			City = City ?? "";
			CachedTick = -1L;
		}
#endif
	}
}

namespace ThousandAndFirst
{
	using XRL;
	using XRL.Messages;
	using XRL.UI;
	using XRL.World;
	using XRL.World.Parts;

	/// <summary>
	/// The engine-coupled half of the becoming annexe: the register the founder reads, the
	/// once-ever enrolment ceremony, and the live read that tells a body whether its city still
	/// keeps its roll.
	/// <para>
	/// <b>Nothing here is a second cybernetics system.</b> The annexe grants two things and no
	/// more: a line on the city's roster, and the license points the vanilla terminal budgets in
	/// (<c>XRL/UI/CyberneticsTerminal.cs:71</c>, an int property character creation writes and the
	/// event never touches). Every implant, every price, every slot conflict and every removal
	/// after that is the shipped becoming nook's, unmodified.
	/// </para>
	/// <para>
	/// Every decision that does not need a real object &mdash; who may be entered, what it costs,
	/// what a refusal says, what the register draws &mdash; is delegated to the engine-free
	/// <see cref="KingdomAnnexeRules"/>, and the cardinality and creed-friction arithmetic to
	/// <see cref="KingdomLabRules"/>, which already ships them.
	/// </para>
	/// </summary>
	internal static class KingdomAnnexe
	{
		/// <summary>Whether the annexe is switched on. Off, the register does not open and no roll
		/// is written &mdash; but a roll already written still stands, because switching a module
		/// off is not a way to take a thing off a founder.</summary>
		internal static bool Enabled => Options.GetOption("r_TAF_OptionAnnexe") != "No";

		/// <summary>The engine's own license budget, by the name the terminal reads it under
		/// (<c>XRL/UI/CyberneticsTerminal.cs:71</c>).</summary>
		internal const string LicenseProperty = "CyberneticsLicenses";

		/// <summary>
		/// Game-state key for the once-ever chrome-debt petition latch.
		/// <para>
		/// <b>Game state rather than a field, deliberately.</b> The lab's twin latch sits on the
		/// founder's own lab record because that record already existed; nothing here does, and
		/// appending a bool to <c>KingdomSystem</c> for one bit would be a serialized field bought
		/// with a save migration. The mirror-gate's register is the shipped precedent for
		/// realm-scoped state that is not a city's property, and this is one bit of exactly that
		/// kind: whether the founder has been asked, once, about the debt.
		/// </para>
		/// </summary>
		internal const string SpokenState = "r_TAF_AnnexeChromeSpoken";

		// ==================================================================================
		// The live read
		// ==================================================================================

		/// <summary>
		/// Whether any city the realm still holds carries this roll.
		/// <para>
		/// <b>Both cities, not the seat only.</b> Addendum 22 B4's seat-only rule is about what
		/// the KEEPERS know, and knowledge is a thing a city teaches. A roll is not knowledge: it
		/// is the realm asserting who it counts, and a founder standing in their other city has
		/// not stopped being counted. What ends a roll is the realm ceasing to hold the book
		/// &mdash; which is exactly what secession and exile do to the container.
		/// </para>
		/// </summary>
		/// <param name="Who">The <c>GeneID</c> the roll was written under.</param>
		internal static bool RealmHolds(string Who)
		{
			if (string.IsNullOrEmpty(Who) || The.Game == null)
			{
				return false;
			}
			return HeldBy(The.Game.RequireSystem<KingdomSystem>(), Who);
		}

		/// <summary>The same read against a realm already in hand, for callers holding one.</summary>
		internal static bool HeldBy(KingdomSystem Realm, string Who)
		{
			if (Realm == null || !Realm.Founded || string.IsNullOrEmpty(Who))
			{
				return false;
			}
			if (KingdomAnnexeRules.Enrolled(KingdomZoning.Roster(Realm), Who))
			{
				return true;
			}
			return Realm.Away != null && KingdomAnnexeRules.Enrolled(KingdomZoning.RosterOf(Realm.Away), Who);
		}

		/// <summary>Says the one sentence a founder whose nooks stopped opening is owed, once.</summary>
		internal static void AnnounceLapse(string City)
		{
			MessageQueue.AddPlayerMessage(KingdomAnnexeRules.LapseLine(City));
			KingdomSystem realm = The.Game?.RequireSystem<KingdomSystem>();
			if (realm != null && realm.Founded)
			{
				KingdomChronicle.Record(realm, KingdomAnnexeRules.LapseTelling(City));
			}
			KingdomLog.Log("annexe: roll lapsed (" + City + ")");
		}

		/// <summary>
		/// Whether the engine would call this creature True Kin WITHOUT us.
		/// <para>
		/// Read off <c>genotypeEntry</c> directly rather than through <c>IsTrueKin()</c>, and the
		/// difference is the whole point: <c>IsTrueKin()</c> would include our own answer, so an
		/// already-enrolled citizen would be refused as "already Kin" and the founder told
		/// nonsense. This is the raw seed <c>IsTrueKinEvent.Check</c> itself starts from
		/// (<c>XRL/World/IsTrueKinEvent.cs:32</c>).
		/// </para>
		/// </summary>
		internal static bool KinByBirth(GameObject Who)
		{
			return Who != null && Who.genotypeEntry != null && Who.genotypeEntry.IsTrueKin;
		}

		// ==================================================================================
		// The register
		// ==================================================================================

		/// <summary>
		/// The screen at the annexe: who keeps the book, whose names are in it, and the one act
		/// that adds a name.
		/// <para>
		/// One <c>Popup.PickOption</c> and no new screen class, which is the house idiom the lab's
		/// slate and the keepers' screen both keep. The rolls are shown HERE and nowhere else: the
		/// keepers' screen lists what a city KNOWS, and who a city COUNTS is a different book kept
		/// in a different room.
		/// </para>
		/// </summary>
		internal static void OpenRegister(GameObject Building, GameObject Actor)
		{
			if (!Enabled)
			{
				return;
			}
			KingdomSystem realm = The.Game?.RequireSystem<KingdomSystem>();
			if (realm == null || !realm.Founded)
			{
				Popup.Show("You rule nothing yet.");
				return;
			}
			while (true)
			{
				List<string> rolls = KingdomAnnexeRules.Rolls(KingdomZoning.Roster(realm));
				List<GameObject> candidates = Candidates(realm, Building, Actor);
				List<string> options = new List<string>();
				List<GameObject> targets = new List<GameObject>();
				for (int i = 0; i < candidates.Count; i++)
				{
					options.Add("{{W|Enter " + candidates[i].DisplayNameOnly + " on the rolls}}");
					targets.Add(candidates[i]);
				}
				if (options.Count == 0)
				{
					// 7b's applicable-but-blocked case: the register is open, there is nobody in
					// front of it this city could write down, and nothing else would say so.
					options.Add("{{K|There is nobody here this city could enter}}");
					targets.Add(null);
				}
				List<string> names = RollNames(Building, rolls);
				for (int i = 0; i < names.Count; i++)
				{
					options.Add(KingdomAnnexeRules.RegisterRow(names[i], Held: true));
					targets.Add(null);
				}
				options.Add("Close");
				targets.Add(null);
				int picked = Popup.PickOption(
					Title: KingdomAnnexeRules.RegisterTitle(realm.SeatName),
					Intro: KingdomAnnexeRules.RegisterIntro(KeeperAt(realm), rolls.Count),
					Options: options, AllowEscape: true, RespectOptionNewlines: true);
				if (picked < 0 || picked >= targets.Count || targets[picked] == null)
				{
					return;
				}
				Offer(realm, Building, Actor, targets[picked]);
			}
		}

		/// <summary>
		/// The whole cost, then the answer, then the act. The disclosure is not a courtesy: it is
		/// the &sect;1.5 lesson applied, which is that what players will not forgive is a
		/// consequence nobody told them about.
		/// </summary>
		private static void Offer(KingdomSystem Realm, GameObject Building, GameObject Actor, GameObject Who)
		{
			string city = Realm.SeatName;
			string named = Who.DisplayNameOnly;
			KingdomEnrolVerdict verdict = JudgeFor(Realm, Building, Who);
			if (verdict != KingdomEnrolVerdict.Allowed)
			{
				Popup.Show(KingdomAnnexeRules.RefusalLine(verdict, named, city, StoredWater(Realm, Building)));
				return;
			}
			int consent = Popup.PickOption(
				Title: "Enter " + named + " on the rolls of " + city,
				Intro: KingdomAnnexeRules.DisclosureLines(city),
				Options: KingdomAnnexeRules.ConsentOptions, AllowEscape: true, RespectOptionNewlines: true);
			if (consent != 0)
			{
				return;
			}
			Enrol(Realm, Building, Who);
		}

		/// <summary>
		/// Takes the water, writes the roll, grants the licenses the terminal budgets in, pays the
		/// standing, and puts the part on the body.
		/// <para>
		/// <b>The verdict is asked AGAIN here</b>, for the reason the lab states one lane over: a
		/// founder may have opened this screen, walked away, come back a season later and had the
		/// answer change under them. A commit that trusts the screen that opened it will one day
		/// take a city's water for a thing it cannot do.
		/// </para>
		/// </summary>
		private static void Enrol(KingdomSystem Realm, GameObject Building, GameObject Who)
		{
			string city = Realm.SeatName;
			string named = Who.DisplayNameOnly;
			KingdomEnrolVerdict verdict = JudgeFor(Realm, Building, Who);
			if (verdict != KingdomEnrolVerdict.Allowed)
			{
				Popup.Show(KingdomAnnexeRules.RefusalLine(verdict, named, city, StoredWater(Realm, Building)));
				return;
			}
			string id = Who.GeneID;
			if (string.IsNullOrEmpty(id) || KingdomAnnexeRules.EnrolmentKey(id) == null)
			{
				// Hostile-input discipline (STANDARDS 9): an identity that could not survive the
				// store disables one enrolment and says so, rather than writing a key that would
				// corrupt the city's whole roster.
				Popup.Show("The register cannot get a clean hand on " + named + ". Nothing was written and nothing was spent.");
				return;
			}
			// Measured, never trusted: what the ceremony actually cost the city is the difference
			// the draw reports, not the number we asked for (STANDARDS §1). A short draw is a
			// refusal with the water already back where it was.
			// The verdict above already refuses a groundless annexe as Unpaid, because a null zone
			// stores no water -- but the draw itself walks the zone, so the invariant is asserted
			// here rather than inferred from a rule one edit away from changing.
			Zone zone = Building?.CurrentZone;
			if (zone == null
				|| KingdomGrowth.ConsumeStoredWater(zone, KingdomAnnexeRules.EnrolmentDrams) < KingdomAnnexeRules.EnrolmentDrams)
			{
				Popup.Show(KingdomAnnexeRules.RefusalLine(KingdomEnrolVerdict.Unpaid, named, city, StoredWater(Realm, Building)));
				return;
			}
			if (!KingdomZoning.Learn(Realm, KingdomAnnexeRules.EnrolmentKind, id))
			{
				// Learn refuses only what this city already holds, which Judge already excluded,
				// so reaching here means the store itself refused the key. The water is gone, so
				// the founder is told plainly rather than quietly given nothing.
				Popup.Show("The book would not take the entry. Nothing is on the rolls, and the ceremony's water is spent.");
				KingdomLog.Log("annexe: store refused roll for " + id);
				return;
			}
			r_KingdomEnrolled record = Who.RequirePart<r_KingdomEnrolled>();
			record.Who = id;
			record.Named = named;
			record.City = city;
			record.Tick = (The.Game != null) ? The.Game.TimeTicks : 0L;
			record.LapseAnnounced = false;
			// The door the event opens leads into an empty room without this. See
			// KingdomAnnexeRules.EnrolmentLicenses for the finding it answers.
			Who.ModIntProperty(LicenseProperty, KingdomAnnexeRules.EnrolmentLicenses);
			List<KeyValuePair<string, int>> standing = KingdomAnnexeRules.StandingCost();
			for (int i = 0; i < standing.Count; i++)
			{
				Realm.AdjustStanding(standing[i].Key, standing[i].Value);
			}
			MessageQueue.AddPlayerMessage(KingdomAnnexeRules.DoneLine(named, city));
			KingdomChronicle.Record(Realm, KingdomAnnexeRules.DoneTelling(named, city), Accomplishment: true);
			Realm.RecordDeed(KingdomAnnexeRules.DoneTelling(named, city));
			KingdomLog.Log("annexe: enrolled " + id + " (" + named + ") at " + city);
			Speak(Realm);
		}

		/// <summary>
		/// F4's friction, riding the petitions surface that already ships rather than building
		/// anything parallel: a named person, waiting at the Charter, about a thing they actually
		/// mind.
		/// <para>
		/// The speaker is a Mechanimist and that inverts the lab's shape on purpose. The hall's
		/// petitioner is offended BY what is done there; the annexe's holds with the creed the act
		/// belongs to and minds the MANNER of it &mdash; chrome in Qud is borrowed from Shekhinah
		/// and repaid down the Sacred Well (<c>B/Books.xml:165,170,171</c>), and a city handing it
		/// out on its own authority has settled nothing with anybody. There is no correct answer,
		/// which is the point.
		/// </para>
		/// <para>
		/// The trigger arithmetic is <see cref="KingdomLabRules.SpeaksAgainstHall"/>, consumed
		/// rather than copied: a tenth of the city, a minority rather than a majority, and once is
		/// the whole of it. The latch is set only when a petition was really raised, so a founder
		/// who happened to be carrying another petition still gets this one next time.
		/// </para>
		/// </summary>
		private static void Speak(KingdomSystem Realm)
		{
			bool spoken = The.Game != null && The.Game.GetIntGameState(SpokenState) == 1;
			int holding = CreedCount(Realm, KingdomAnnexeRules.Creditors);
			if (!KingdomLabRules.SpeaksAgainstHall(holding, Realm.Population, spoken))
			{
				return;
			}
			if (KingdomPetitions.Raise(Realm, KingdomRules.PetitionKind.Chrome, KingdomAnnexeRules.Creditors))
			{
				The.Game?.SetIntGameState(SpokenState, 1);
				KingdomLog.Log("annexe: chrome debt spoken about (" + KingdomAnnexeRules.Creditors + " x" + holding + ")");
			}
		}

		private static int CreedCount(KingdomSystem Realm, string Creed)
		{
			int count;
			return (Realm.CreedCounts != null && Creed != null && Realm.CreedCounts.TryGetValue(Creed, out count)) ? count : 0;
		}

		// ==================================================================================
		// Reading the world
		// ==================================================================================

		/// <summary>
		/// Everyone standing where the register can see them: the annexe's own cell and the ring
		/// around it &mdash; vanilla's own reach for exactly this act
		/// (<c>CyberneticsTerminal.GetAuthorizedSubjects</c> walks the terminal's cell and its
		/// adjacent cells), plus the founder unconditionally, which is that same method's own
		/// first line. Anyone already enrolled, already Kin by birth, or not one of the city's own
		/// is dropped here, so a refusal the founder can do nothing about is never offered as a
		/// row.
		/// </summary>
		private static List<GameObject> Candidates(KingdomSystem Realm, GameObject Building, GameObject Actor)
		{
			List<GameObject> found = new List<GameObject>();
			if (Actor != null && Admits(Realm, Actor))
			{
				found.Add(Actor);
			}
			Cell cell = Building?.CurrentCell;
			if (cell == null)
			{
				return found;
			}
			Gather(Realm, found, cell);
			foreach (Cell adjacent in cell.GetLocalAdjacentCells())
			{
				Gather(Realm, found, adjacent);
			}
			return found;
		}

		private static void Gather(KingdomSystem Realm, List<GameObject> Found, Cell Where)
		{
			if (Where == null)
			{
				return;
			}
			foreach (GameObject item in Where.GetObjects())
			{
				if (item != null && !Found.Contains(item) && Admits(Realm, item))
				{
					Found.Add(item);
				}
			}
		}

		/// <summary>Whether a body is one the register could write down at all.</summary>
		private static bool Admits(KingdomSystem Realm, GameObject Who)
		{
			if (Who == null || !Who.IsCreature || Who.Body == null || KinByBirth(Who))
			{
				return false;
			}
			if (!Who.IsPlayer() && Who.GetIntProperty("KingdomCitizen") != 1)
			{
				return false;
			}
			return !HeldBy(Realm, Who.GeneID);
		}

		private static KingdomEnrolVerdict JudgeFor(KingdomSystem Realm, GameObject Building, GameObject Who)
		{
			bool ours = Who != null && (Who.IsPlayer() || Who.GetIntProperty("KingdomCitizen") == 1);
			return KingdomAnnexeRules.Judge(
				Founded: Realm != null && Realm.Founded,
				Annexe: Building != null && Building.HasPart("r_KingdomBecomingAnnexe"),
				Staffed: !string.IsNullOrEmpty(KeeperAt(Realm)),
				Ours: ours,
				AlreadyKin: KinByBirth(Who),
				AlreadyEnrolled: Who != null && HeldBy(Realm, Who.GeneID),
				StoredWater: StoredWater(Realm, Building));
		}

		private static int StoredWater(KingdomSystem Realm, GameObject Building)
		{
			Zone zone = Building?.CurrentZone;
			return (zone == null) ? 0 : KingdomSurvey.Take(zone, Realm).StoredWater;
		}

		/// <summary>
		/// Whoever keeps the book, or null when nobody does. Derived from the crew the lodging
		/// machinery already placed &mdash; the annexe assigns nobody, exactly as Addendum 6 says
		/// a great work never does, and exactly as the grafting hall's savant is read.
		/// </summary>
		private static string KeeperAt(KingdomSystem Realm)
		{
			return (Realm != null && Realm.RosterNames != null && Realm.RosterNames.Count > 0) ? Realm.RosterNames[0] : null;
		}

		/// <summary>
		/// Row labels for the register: the book stores ids, and the NAMES live on the people, so
		/// the zone the annexe stands in is read once per redraw to put a face to each id. Anybody
		/// who has moved on gets the honest line rather than a number.
		/// </summary>
		private static List<string> RollNames(GameObject Building, List<string> Rolls)
		{
			Dictionary<string, string> known = new Dictionary<string, string>();
			Zone zone = Building?.CurrentZone;
			if (zone != null)
			{
				foreach (GameObject item in zone.GetObjects())
				{
					r_KingdomEnrolled record = (item == null) ? null : item.GetPart<r_KingdomEnrolled>();
					if (record != null && !string.IsNullOrEmpty(record.Who) && !known.ContainsKey(record.Who))
					{
						known[record.Who] = string.IsNullOrEmpty(record.Named) ? item.DisplayNameOnly : record.Named;
					}
				}
			}
			List<string> names = new List<string>();
			for (int i = 0; i < Rolls.Count; i++)
			{
				string name;
				names.Add(known.TryGetValue(Rolls[i], out name) ? name : "somebody who is not here today");
			}
			return names;
		}
	}
}
