using System.Collections.Generic;
using System.Text;

namespace ThousandAndFirst
{
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
	public static partial class KingdomAnnexeRules
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

	}
}
