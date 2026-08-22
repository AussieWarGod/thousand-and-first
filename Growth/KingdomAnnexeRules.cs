using System.Collections.Generic;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>
	/// What one attempt to enter somebody on a city's rolls came to.
	/// <para>
	/// The order below is frozen and the refusals are ordered by what a founder can do about
	/// them, nearest first &mdash; the same discipline <c>CitizenRiteVerdict</c> keeps, and for
	/// the same reason: a refusal is a sentence, and a sentence that names the wrong obstacle is
	/// worse than none.
	/// </para>
	/// </summary>
	public enum KingdomEnrolVerdict : byte
	{
		/// <summary>Enter them.</summary>
		Allowed = 0,

		/// <summary>There is no realm, or this is not its ground.</summary>
		Unfounded = 1,

		/// <summary>No annexe stands in this city. Reachable only through a caller that did not
		/// come in through the building.</summary>
		NoAnnexe = 2,

		/// <summary>The annexe stands and nobody is at the register.</summary>
		Unstaffed = 3,

		/// <summary>Neither the founder nor one of this city's own. The rolls are a city's, and a
		/// city does not enrol a stranger who happened to walk past.</summary>
		NotOurs = 4,

		/// <summary>True Kin already, by birth. There is nothing here to give them &mdash; the
		/// machines have never once asked them a question.</summary>
		Kin = 5,

		/// <summary>Already on the rolls. Once is the whole of it.</summary>
		Enrolled = 6,

		/// <summary>The stores cannot spare the ceremony's water.</summary>
		Unpaid = 7
	}

	/// <summary>
	/// The becoming annexe: a city's own rolls, what it costs to be written into them, and every
	/// line the register draws.
	/// <para>
	/// <b>The fiction, and it was found rather than invented</b> (END-STATE-CITIES-RESEARCH
	/// &sect;2.5, F1): True Kin is a matter of RECORD, not of blood. The Eaters' nooks refuse a
	/// mutant because the mutant is not on the rolls &mdash; <c>CyberneticsTerminal.IsAuthorized</c>
	/// is an authorization check that ends in <c>GameObject.IsTrueKin()</c>
	/// (<c>XRL/UI/CyberneticsTerminal.cs:481-488</c>), and that in turn is a pure dispatch to an
	/// overridable event (<c>XRL/World/GameObject.cs:10560-10563</c>). The annexe is the city
	/// keeping its own rolls, and the machines are asked the question they have always asked.
	/// </para>
	/// <para>
	/// <b>The cost doctrine, and it is the whole of the balance answer</b> (&sect;1.4 and R-D):
	/// the objection to mutant-chrome is power, and the answer is COST, never refusal. The price
	/// is paid at the door, disclosed whole before consent, and REVERSIBLE IN KIND &mdash; the
	/// &sect;1.5 lesson, in a player's own words: <i>"a permanent debuff on arguably the most
	/// important stats in the game from a reversible action is a serious kick in the balls."</i>
	/// Nothing here rolls a die, nothing here applies a hidden penalty, and nothing anywhere in
	/// this file takes a thing back off a body.
	/// </para>
	/// <para>
	/// <b>The megastructure vocabulary is not forked.</b> Cardinality, the purpose refusal, the
	/// standing parser and the creed-friction arithmetic are all
	/// <see cref="KingdomLabRules"/>'s, consumed rather than copied: a chrome-city and a
	/// flesh-city are one doctrine's two answers, and they contend for the same thing.
	/// </para>
	/// </summary>
	public static class KingdomAnnexeRules
	{
		// --- The building -------------------------------------------------------------------------

		/// <summary>The catalogue key. Held here as well as in the XML because the rules below
		/// answer questions about it and a key nobody can name in code is a key that drifts.</summary>
		public const string AnnexeKey = "becomingannexe";

		/// <summary>
		/// The line the building's own record carries, and the one END-STATE-CITIES-RESEARCH
		/// &sect;7.4 asks for by name under Design B. Declared here so the catalogue entry and the
		/// register screen cannot say two different things about what the place is.
		/// </summary>
		public const string Charter =
			"The becoming nook asks whether you are on the Eaters' rolls. This is where your city keeps its own.";

		// --- The rolls (Addendum 22 B1: the container carries them) --------------------------------

		/// <summary>
		/// The knowledge kind an enrolment is stored under.
		/// <para>
		/// <b>The rolls ride the city's own roster and nothing new is serialized anywhere.</b>
		/// <c>KingdomSettlement.KeepersRoster</c> is already the container-carried register
		/// Addendum 22 B1 sited, already moved whole by secession, rejoin, exile and return with
		/// no knowledge-specific code in any of those four paths, and already worth zero craft
		/// points for a kind this build does not weigh
		/// (<c>KingdomZoningRules.PointsForKind</c> returns 0 for anything it does not know). So
		/// the fiction's teeth &mdash; a city that walks out takes its rolls with it &mdash; are
		/// not a feature of ours; they are what the container already does.
		/// </para>
		/// <para>
		/// The one thing this shares with the knowledge kinds is <c>Knows</c>'s unqualified match:
		/// a design written <c>Knowledge="47"</c> with no kind would be satisfied by
		/// <c>enrolled:47</c>. Every enrolment name is a <c>GeneID</c> &mdash; a bare counter no
		/// author writes a gate against &mdash; and every read below is QUALIFIED, so the
		/// collision cannot run the other way and let a gate enrol anybody.
		/// </para>
		/// </summary>
		public const string EnrolmentKind = "enrolled";

		/// <summary>
		/// The roster key for one person's enrolment.
		/// <para>
		/// Preconditions: none. Side effects: none. Failure mode: returns null for a name that
		/// could not survive a round trip through the store, exactly as
		/// <c>KingdomZoningRules.ComposeKey</c> does, so a hostile identity disables one roll
		/// rather than corrupting a city's whole roster.
		/// </para>
		/// </summary>
		/// <param name="Who">The person's <c>GameObject.GeneID</c> &mdash; the engine's own
		/// per-creature identity (<c>XRL/World/GameObject.cs:456-479</c>), minted from a game
		/// counter and carried as a string property. Keyed to the PERSON rather than to the
		/// office, which is what makes Addendum 21's succession honesty rule hold by construction:
		/// an heir is a different creature with a different id and walks in enrolled in nothing.</param>
		public static string EnrolmentKey(string Who)
		{
			return KingdomZoningRules.ComposeKey(EnrolmentKind, Who);
		}

		/// <summary>
		/// Whether a roster carries this person's roll.
		/// <para>
		/// Preconditions: none. Side effects: none. Failure mode: false for a null roster, a null
		/// id, or an id that cannot be keyed &mdash; an unreadable register must never be able to
		/// answer yes.
		/// </para>
		/// </summary>
		public static bool Enrolled(IEnumerable<string> Roster, string Who)
		{
			string key = EnrolmentKey(Who);
			return key != null && KingdomZoningRules.Knows(Roster, key);
		}

		/// <summary>
		/// What <c>IsTrueKinEvent</c> ends up answering for one body: the engine's own genotype
		/// seed, raised by a standing roll and never lowered by anything.
		/// <para>
		/// This is the whole of the override, in the one place it can be tested without a running
		/// game. <c>IsTrueKinEvent.Check</c> seeds <c>flag</c> from
		/// <c>Object?.genotypeEntry?.IsTrueKin</c> and then hands that seed to each handler to
		/// rewrite (<c>XRL/World/IsTrueKinEvent.cs:32-47</c>), so a handler that ORs is a handler
		/// that cannot take True Kin away from somebody born to it &mdash; which is exactly the
		/// property a mod answering a genotype question owes the base game.
		/// </para>
		/// <para>
		/// Preconditions: none. Side effects: none. Failure mode: none.
		/// </para>
		/// </summary>
		/// <param name="Seeded">What the event already believed when it reached us.</param>
		/// <param name="Held">Whether the realm still keeps this body's roll.</param>
		public static bool AnswersTrueKin(bool Seeded, bool Held)
		{
			return Seeded || Held;
		}

		/// <summary>The same answer composed from a roster, for a caller that has one rather than
		/// a live body. Same contract.</summary>
		public static bool AnswersTrueKin(bool KinByBirth, IEnumerable<string> Roster, string Who)
		{
			return AnswersTrueKin(KinByBirth, Enrolled(Roster, Who));
		}

		/// <summary>Every roll on one city's roster, in the order it was written, as bare ids.
		/// What the register screen draws its rows from. Never null.</summary>
		public static List<string> Rolls(IEnumerable<string> Roster)
		{
			List<string> rolls = new List<string>();
			if (Roster == null)
			{
				return rolls;
			}
			foreach (string entry in Roster)
			{
				if (KingdomZoningRules.KindOf(entry) == EnrolmentKind)
				{
					string name = KingdomZoningRules.NameOf(entry);
					if (name != null && !rolls.Contains(name))
					{
						rolls.Add(name);
					}
				}
			}
			return rolls;
		}

		// --- The price (R4: cost, never refusal) ---------------------------------------------------

		/// <summary>
		/// Water the ceremony costs the city's stores, paid at the door and disclosed whole
		/// before consent.
		/// <para>
		/// Set at the top of the lab's own Class III band (<c>KingdomProcedures.xml</c> runs
		/// 120-180 for a named procedure) because this is the annexe's whole point rather than one
		/// of a ladder of things it does, and because the Reddit read is explicit that chrome
		/// exclusivity is the last standing reason to pick True Kin at all: <i>one rung of the
		/// annexe priced too cheap deletes a genotype</i> (&sect;1.7 R-D).
		/// </para>
		/// </summary>
		public const int EnrolmentDrams = 180;

		/// <summary>
		/// Cybernetics licenses the ceremony grants.
		/// <para>
		/// <b>Two, because two is what the genotype itself is worth</b> &mdash;
		/// <c>Base/Genotypes.xml:20</c> gives True Kin <c>CyberneticsLicensePoints="2"</c>, and a
		/// caste adds its own on top (<c>QudSubtypeModule.cs:117</c>). An enrolled citizen gets
		/// the genotype's share and no caste's, which is the honest reading of what a city can
		/// grant: it can put you on the rolls, it cannot make you an aristocrat.
		/// </para>
		/// <para>
		/// It has to be granted at all, and that is a FINDING rather than a design choice.
		/// Answering <c>IsTrueKinEvent</c> opens the nook's door and nothing else: the terminal's
		/// budget is <c>Subject.GetIntProperty("CyberneticsLicenses")</c>
		/// (<c>XRL/UI/CyberneticsTerminal.cs:71</c>), an int property written at character
		/// creation from the genotype entry and never from the event. Without this grant an
		/// enrolled mutant walks through an open door into an empty room. Vanilla's own precedent
		/// for granting licenses outside character creation is the nook itself, on an exceptional
		/// hack: <c>Actor.ModIntProperty("CyberneticsLicenses", num2)</c>
		/// (<c>XRL/World/Parts/CyberneticsTerminal2.cs:125</c>), with no genotype check anywhere
		/// near it.
		/// </para>
		/// </summary>
		public const int EnrolmentLicenses = 2;

		/// <summary>
		/// The creeds a city offends by writing its own rolls, in the <c>-Faction</c> removal
		/// idiom the quality-of-life vocabulary already speaks and
		/// <see cref="KingdomLabRules.StandingCost"/> already parses.
		/// <para>
		/// One name, and it is the right one. In vanilla Qud chrome is the sacrament of the people
		/// who want mutants dead: the Putus Templar are the only creature family in shipped data
		/// that carries cybernetics, their base standing with mutated humans is -700, and the
		/// game frames their crusade as villainy (&sect;1.6). Granting chrome to a mutant is
		/// therefore not a transgression against Qud's fiction &mdash; it is a transgression
		/// against the Templar, and the annexe does not need to apologise for it. It needs to be
		/// dangerous.
		/// </para>
		/// </summary>
		public const string OffendedCreeds = "-Templar";

		/// <summary>
		/// Standing one enrolment costs with each creed it offends.
		/// <para>
		/// Three times the lab's flat <c>StandingPerCreed</c>, and flat for the same reason: a
		/// graft is a private act and a roll is a public record, and the Templar's whole polity IS
		/// a record of who may be counted. Deliberately not a ladder &mdash; DIVERSITY &sect;3.6
		/// forbids turning a belief into a meter by name.
		/// </para>
		/// </summary>
		public const int StandingPerCreed = 150;

		/// <summary>
		/// What the ceremony costs the founder in standing, ready for the shipped
		/// <c>AdjustStanding</c> path with its existing chronicle entry and outsider-register
		/// drift. Nothing new is written; the parser is the lab's.
		/// </summary>
		/// <returns>Faction name to standing delta, deltas negative. Never null.</returns>
		public static List<KeyValuePair<string, int>> StandingCost()
		{
			return KingdomLabRules.StandingCost(OffendedCreeds, StandingPerCreed);
		}

		// --- The verdict --------------------------------------------------------------------------

		/// <summary>
		/// Whether this person may be entered on this city's rolls today.
		/// <para>
		/// Preconditions: none. Side effects: none. Failure mode: none &mdash; total over its
		/// inputs.
		/// </para>
		/// </summary>
		/// <param name="Founded">A realm holds this ground.</param>
		/// <param name="Annexe">A finished annexe stands in this city.</param>
		/// <param name="Staffed">Somebody who knows the work lives here.</param>
		/// <param name="Ours">The founder themselves, or one of this city's own citizens.</param>
		/// <param name="AlreadyKin">The engine already answers True Kin for them without us.</param>
		/// <param name="AlreadyEnrolled">This city, or the realm's other one, already holds their
		/// roll.</param>
		/// <param name="StoredWater">Drams in the dedicated stores.</param>
		public static KingdomEnrolVerdict Judge(bool Founded, bool Annexe, bool Staffed, bool Ours,
			bool AlreadyKin, bool AlreadyEnrolled, int StoredWater)
		{
			if (!Founded)
			{
				return KingdomEnrolVerdict.Unfounded;
			}
			if (!Annexe)
			{
				return KingdomEnrolVerdict.NoAnnexe;
			}
			if (!Staffed)
			{
				return KingdomEnrolVerdict.Unstaffed;
			}
			if (!Ours)
			{
				return KingdomEnrolVerdict.NotOurs;
			}
			if (AlreadyKin)
			{
				return KingdomEnrolVerdict.Kin;
			}
			if (AlreadyEnrolled)
			{
				return KingdomEnrolVerdict.Enrolled;
			}
			if (StoredWater < EnrolmentDrams)
			{
				return KingdomEnrolVerdict.Unpaid;
			}
			return KingdomEnrolVerdict.Allowed;
		}

		/// <summary>
		/// The refusal, naming the thing in the way rather than the rule (STANDARDS 7b). Every
		/// one of these says what would fix it, because a refusal that does not is a stall the
		/// founder has to reverse-engineer.
		/// </summary>
		/// <param name="Verdict">What <see cref="Judge"/> answered.</param>
		/// <param name="Who">The person, as the founder reads them.</param>
		/// <param name="CityName">The city.</param>
		/// <param name="StoredWater">Drams actually in the stores, for the shortfall sentence.</param>
		/// <returns>Null for <see cref="KingdomEnrolVerdict.Allowed"/>, which is not a sentence
		/// worth writing.</returns>
		public static string RefusalLine(KingdomEnrolVerdict Verdict, string Who, string CityName, int StoredWater)
		{
			string who = Named(Who, "they");
			string city = Named(CityName, "this city");
			switch (Verdict)
			{
			case KingdomEnrolVerdict.Unfounded:
				return "You rule nothing yet. A roll is a city's claim about who it counts, and there is no city.";
			case KingdomEnrolVerdict.NoAnnexe:
				return "There is no annexe standing in " + city + ". Rolls are kept where the register is.";
			case KingdomEnrolVerdict.Unstaffed:
				return "The register is open and nobody is at it. The annexe writes nothing until somebody who has had it done to themselves lives in " + city + ".";
			case KingdomEnrolVerdict.NotOurs:
				return who + " is not one of ours. " + city + " may write down who IT counts, and no more than that — bring them in, and then bring them here.";
			case KingdomEnrolVerdict.Kin:
				return "The machines have never once asked " + who + " a question. There is nothing here to give them.";
			case KingdomEnrolVerdict.Enrolled:
				return who + " is already on the rolls. It is a thing a city says once about a person.";
			case KingdomEnrolVerdict.Unpaid:
				return "The stores at " + city + " hold {{C|" + StoredWater + "}} drams and the ceremony wants {{C|"
					+ EnrolmentDrams + "}}. Fill them, and the register will open.";
			default:
				return null;
			}
		}

		// --- Disclosure (STANDARDS 7b) -------------------------------------------------------------

		/// <summary>The prefix every disclosed consequence takes, so a founder reads a price in
		/// the same colour wherever the mod shows them one. The lab's own.</summary>
		public const string EffectPrefix = "{{rules|--}} ";

		/// <summary>The whole price in one sentence, in the units the founder already reads
		/// everywhere else.</summary>
		public static string PriceLine()
		{
			StringBuilder text = new StringBuilder();
			text.Append(EnrolmentDrams).Append(" drams from the stores, ");
			List<KeyValuePair<string, int>> standing = StandingCost();
			for (int i = 0; i < standing.Count; i++)
			{
				if (i > 0)
				{
					text.Append(", ");
				}
				text.Append(standing[i].Value).Append(" standing with ").Append(standing[i].Key);
			}
			if (standing.Count == 0)
			{
				text.Append("nothing anybody minds");
			}
			return text.ToString();
		}

		/// <summary>
		/// Everything a founder is owed before they agree, and it is deliberately long.
		/// <para>
		/// The &sect;1.5 lesson is that the failure players actually hate is a consequence they
		/// were not told about; the fix is not to have fewer consequences, it is to state them all
		/// at the door. So this states the price, what a roll IS, what carrying one changes about
		/// the world beyond the nook, and what would take it away again &mdash; before consent,
		/// not after.
		/// </para>
		/// <para>
		/// The third line is the one nothing else in the game would ever say. Answering the
		/// engine's own <c>IsTrueKinEvent</c> is answering it for EVERY caller, and the callers
		/// are not only the nook: the tonics read it for their potency
		/// (<c>Blaze_Tonic.cs:88</c> speeds 20 against 10, <c>HulkHoney_Tonic.cs:114-116</c>),
		/// the social minigames read it (<c>HagglingSifrah.cs:130</c> and four others), the
		/// baetyls read it (<c>RandomAltarBaetylRewardManager.cs:200</c>), and some
		/// conversations read it (<c>ConversationDelegates.cs:690</c>). None of that is a bug and
		/// all of it is invisible, so it is disclosed.
		/// </para>
		/// </summary>
		/// <param name="CityName">The city whose rolls these are.</param>
		public static string DisclosureLines(string CityName)
		{
			string city = Named(CityName, "this city");
			StringBuilder text = new StringBuilder();
			text.Append(EffectPrefix).Append("It costs ").Append(PriceLine()).Append('.');
			text.Append('\n').Append(EffectPrefix)
				.Append("What it buys is a line in a book: the machines of the old world ask whether you are on somebody's rolls, and from today you are on ")
				.Append(city).Append("'s. The becoming nooks will open. Whatever credit you have been carrying since the water ritual finally spends.");
			text.Append('\n').Append(EffectPrefix)
				.Append("It is not only the nooks that ask. Tonics, hagglers, baetyls and some people's opinion of you all read the same answer, and from today they read it differently. Nothing about your body changes; what changes is what the world has written down about it.");
			text.Append('\n').Append(EffectPrefix)
				.Append("It lasts exactly as long as ").Append(city)
				.Append(" is yours. If this city walks out of the realm, or the realm is taken from you, the book walks out with it and the nooks close again. Nothing already fitted to you is touched, and nothing is ever taken back out of you.");
			return text.ToString();
		}

		/// <summary>The two-answer consent prompt. There is no third answer here and there should
		/// not be: the lab's "never offer this again" belongs to a list of procedures a founder
		/// scrolls past, and this is one act at one building they walked to on purpose.</summary>
		public static readonly string[] ConsentOptions = new string[2]
		{
			"Enter them on the rolls.",
			"Not today."
		};

		// --- What is said afterward ----------------------------------------------------------------

		/// <summary>What the founder is told the moment the register closes.</summary>
		public static string DoneLine(string Who, string CityName)
		{
			return "{{G|" + Named(Who, "They") + " is on the rolls of " + Named(CityName, "this city")
				+ ". The machines will not ask again.}}";
		}

		/// <summary>The same moment, for the chronicle.</summary>
		public static string DoneTelling(string Who, string CityName)
		{
			return Named(CityName, "the city") + " wrote " + Named(Who, "one of its own")
				+ " into its own rolls, and decided for itself who may be counted";
		}

		/// <summary>
		/// What is said when a roll lapses because the city that kept it is no longer the realm's.
		/// <para>
		/// STANDARDS 7b's applicable-but-blocked case and the single most important sentence this
		/// system can say: a founder whose nook silently stops opening has been handed a bug. The
		/// second half is not decoration &mdash; it is the &sect;1.5 promise being kept out loud.
		/// </para>
		/// </summary>
		public static string LapseLine(string CityName)
		{
			return "{{r|The rolls of " + Named(CityName, "your city")
				+ " are not yours to be on any more, and the nooks have gone back to asking.}} What was fitted to you stays fitted. Nothing was taken out.";
		}

		/// <summary>The same moment, for the chronicle.</summary>
		public static string LapseTelling(string CityName)
		{
			return "the book at " + Named(CityName, "the city") + " left with the city that kept it, and the old machines began asking again";
		}

		// --- The register screen -------------------------------------------------------------------

		/// <summary>The register's own heading.</summary>
		public static string RegisterTitle(string CityName)
		{
			return "the rolls of " + Named(CityName, "this city");
		}

		/// <summary>
		/// The two lines above the list: who keeps the book, and how many names are in it. Both
		/// are facts a founder would otherwise have to go and count.
		/// </summary>
		/// <param name="Keeper">Whoever is lodged at the register, or null when nobody is.</param>
		/// <param name="Count">Names on this city's rolls.</param>
		public static string RegisterIntro(string Keeper, int Count)
		{
			StringBuilder text = new StringBuilder();
			if (string.IsNullOrEmpty(Keeper))
			{
				// 7b: an annexe with nobody in it will write nothing, ever, and that is the single
				// most important thing on this screen.
				text.Append("{{r|Nobody is at the register. The annexe writes no names until somebody who has had it done to themselves lives in this city.}}");
			}
			else
			{
				text.Append("at the register: {{W|").Append(Keeper).Append("}}");
			}
			text.Append("\nnames in the book: ");
			text.Append((Count > 0) ? ("{{C|" + Count + "}}") : "{{K|none}}");
			text.Append("\n{{K|").Append(Charter).Append("}}");
			return text.ToString();
		}

		/// <summary>One row of the register: a person, and whether the book still holds them.</summary>
		/// <param name="Who">The person, as the founder reads them.</param>
		/// <param name="Held">Whether the realm still keeps their roll.</param>
		public static string RegisterRow(string Who, bool Held)
		{
			return Named(Who, "somebody") + "  "
				+ (Held ? "{{green|[þ]}} {{K|on the rolls}}" : "{{red|[X]}} {{K|the book that held them is gone}}");
		}

		/// <summary>The line a city's own book carries about its rolls. Rendered rather than
		/// stored, so nothing anywhere has to keep it in step.</summary>
		public static string RollsLine(int Count)
		{
			if (Count <= 0)
			{
				return "{{K|This city keeps no rolls.}}";
			}
			return "{{W|This city keeps its own rolls, and there " + ((Count == 1) ? "is {{C|1}} name" : ("are {{C|" + Count + "}} names"))
				+ " in them.}}";
		}

		// --- Creed friction (F4: the debt, END-STATE §2.4) ------------------------------------------
		//
		// The trigger arithmetic is KingdomLabRules.SpeaksAgainstHall, consumed rather than copied:
		// a tenth of the city, a minority rather than a majority, and once is the whole of it. What
		// is different here is WHO speaks. The lab's petitioner is offended BY the act; the annexe's
		// is of the creed the act belongs to and minds the manner of it -- the Mechanimists hold
		// chrome as a debt owed to Shekhinah (B/Books.xml:165,170,171), not as a purchase, and a
		// city handing chrome out on its own authority has not settled anything with anybody.

		/// <summary>
		/// The creed that holds the debt, and therefore the creed the petitioner speaks for.
		/// <para>
		/// The Mechanimists, who are "mainly comprised of mutant humanoids" and whose own liturgy
		/// is a liturgy of chrome as an obligation: <i>"Unburden yourself from the weight of your
		/// chrome guilt"</i>, <i>"Repay that debt, lightseeker! Offer your chrome to Shekhinah!"</i>
		/// (<c>B/Books.xml:165,170</c>). They are the one people in Qud for whom the annexe is
		/// neither transgression nor novelty &mdash; it is an unsettled account.
		/// </para>
		/// </summary>
		public const string Creditors = "Mechanimists";

		/// <summary>What the petitioner is waiting to speak about.</summary>
		public static string SpokenAboutSubject()
		{
			return "the debt on the chrome";
		}

		/// <summary>
		/// What they actually say, and there is no correct answer to it. The founder's call,
		/// exactly as DIVERSITY &sect;3.6 asks: friction is named people and placement, never a
		/// meter.
		/// </summary>
		/// <param name="Creed">The creed the speaker holds, as the founder reads it.</param>
		public static string SpokenAboutSpeech(string Creed)
		{
			return "\"I am not here to argue that it should not be done. I have chrome in me and I am glad of it. But chrome is borrowed, and "
				+ Named(Creed, "my people")
				+ " teach that what is borrowed is repaid — down the well, at the Heart, in front of somebody. Your annexe writes a name in a book and calls the matter closed. "
				+ "I would like you to say, out loud, who you think the city owes for what it is handing out.\"";
		}

		/// <summary>The deed, for the chronicle, when the founder answers.</summary>
		public static string SpokenAboutDeed(string Name)
		{
			return "the debt on the chrome of " + Named(Name, "the realm") + " was named out loud, in front of the people who believe it is owed";
		}

		// --- Shared -------------------------------------------------------------------------------

		/// <summary>A name as a founder would say it, or an honest word when nothing named one.
		/// The lab's <c>Named</c> one lane over, with a caller-chosen fallback because a person
		/// and a procedure do not degrade to the same word.</summary>
		public static string Named(string Text, string Fallback)
		{
			return string.IsNullOrEmpty(Text) ? Fallback : Text.Trim();
		}
	}
}
