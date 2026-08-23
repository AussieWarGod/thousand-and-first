using System.Collections.Generic;

namespace ThousandAndFirst
{
	internal enum KingdomVatSettlement : byte
	{
		Wait = 0,
		CreateOutput = 1,
		ConsumeInput = 2,
		CollectOutput = 3,
		ReturnInput = 4,
		Missing = 5
	}

	internal readonly struct KingdomVatAccrual
	{
		public readonly long NextTick;
		public readonly int RemainingTicks;
		public readonly int WorkedTicks;
		public readonly bool Complete;

		public KingdomVatAccrual(long NextTick, int RemainingTicks, int WorkedTicks, bool Complete)
		{
			this.NextTick = NextTick;
			this.RemainingTicks = RemainingTicks;
			this.WorkedTicks = WorkedTicks;
			this.Complete = Complete;
		}
	}

	/// <summary>
	/// What one attempt to zone a megastructure came to.
	/// <para>
	/// A refusal here is reserved for a design the founder actually chose and cannot have on this
	/// ground, so the telling means something &mdash; the same shape <c>KingdomGateVerdict</c> keeps
	/// one lane over.
	/// </para>
	/// </summary>
	public enum KingdomPurposeVerdict : byte
	{
		/// <summary>Nothing in the way. Either the design is ordinary, or this city has no purpose
		/// yet and is about to have one.</summary>
		Allowed = 0,

		/// <summary>This city already keeps a megastructure, and it is not this one.</summary>
		RefusedKept = 1,

		/// <summary>The design is one only a capital may raise, and the crown is not set down in
		/// this city (Addendum 22 A4; the capital ruling extending Addendum 19).</summary>
		RefusedUncrowned = 2
	}

	/// <summary>
	/// The lab as a set of BUILDINGS: which rung a city has reached, what a megastructure costs a
	/// city in the only currency Addendum 22 A1 prices it in &mdash; the right to be about anything
	/// else &mdash; how the hall's creed friction is spoken, and every line the slate draws.
	/// <para>
	/// <b>The procedures themselves are next door</b>, in <see cref="KingdomProcedureRules"/>. The
	/// split is the split the whole folder keeps: what a body will take is one question, and what a
	/// city will build is another, and they are tested apart because they fail apart.
	/// </para>
	/// </summary>
	public static class KingdomLabRules
	{
		internal static KingdomVatAccrual AccrueVat(long LastTick, long TimeTick, int RemainingTicks,
			int CrewEffectiveness, int WearEffectiveness, bool Settled, bool Cancelled)
		{
			int remaining = (RemainingTicks > 0) ? RemainingTicks : 0;
			if (Settled || Cancelled)
			{
				return new KingdomVatAccrual(TimeTick, remaining, 0, Complete: false);
			}
			if (remaining == 0)
			{
				return new KingdomVatAccrual(TimeTick, 0, 0, Complete: true);
			}
			if (LastTick <= 0L || TimeTick <= LastTick)
			{
				return new KingdomVatAccrual((TimeTick > 0L) ? TimeTick : LastTick, remaining, 0, Complete: false);
			}
			int worked = KingdomProcedureRules.VatWorked(TimeTick - LastTick, CrewEffectiveness, WearEffectiveness);
			if (worked <= 0)
			{
				return new KingdomVatAccrual(TimeTick, remaining, 0, Complete: false);
			}
			if (worked >= remaining)
			{
				return new KingdomVatAccrual(TimeTick, 0, remaining, Complete: true);
			}
			return new KingdomVatAccrual(TimeTick, remaining - worked, worked, Complete: false);
		}

		internal static KingdomVatSettlement VatSettlement(bool InputPresent, bool OutputPresent,
			bool WorkComplete, bool CancelRequested)
		{
			if (CancelRequested)
			{
				if (OutputPresent)
				{
					return KingdomVatSettlement.CollectOutput;
				}
				return InputPresent ? KingdomVatSettlement.ReturnInput : KingdomVatSettlement.Missing;
			}
			if (!WorkComplete)
			{
				return InputPresent ? KingdomVatSettlement.Wait
					: (OutputPresent ? KingdomVatSettlement.CollectOutput : KingdomVatSettlement.Missing);
			}
			if (OutputPresent)
			{
				return InputPresent ? KingdomVatSettlement.ConsumeInput : KingdomVatSettlement.CollectOutput;
			}
			return InputPresent ? KingdomVatSettlement.CreateOutput : KingdomVatSettlement.Missing;
		}

		// --- The four buildings ------------------------------------------------------------------
		//
		// Catalogue keys, held here rather than in the XML alone, because the rung a city has
		// reached is arithmetic over which of these stand and that arithmetic is testable. The
		// registry is still the authority on what each one COSTS; this is only the ladder.

		/// <summary>Rung 0. Not the lab: the work that turns what you drag home into parts.</summary>
		public const string SlabKey = "butcherslab";

		/// <summary>Rung 1. Nothing is grafted here; things are kept.</summary>
		public const string VatKey = "vathouse";

		/// <summary>Rung 2. The lab proper.</summary>
		public const string HallKey = "graftinghall";

		/// <summary>Rung 3. Where the anatomy actually changes, and the city's one purpose.</summary>
		public const string TheatreKey = "chimerictheatre";

		/// <summary>
		/// The rung a city has reached, from what is actually standing in it.
		/// <para>
		/// A ladder rather than a sum: a theatre with no vat-house under it can graft nothing,
		/// because the theatre's own inputs come out of the vats. So the rung is the highest
		/// UNBROKEN step, and a founder who raised the grand thing first is told what is missing
		/// underneath rather than being quietly given nothing.
		/// </para>
		/// </summary>
		/// <param name="Slab">Whether a finished butcher's slab stands.</param>
		/// <param name="Vat">Whether a finished vat-house stands.</param>
		/// <param name="Hall">Whether a finished grafting hall stands.</param>
		/// <param name="Theatre">Whether a finished chimeric theatre stands.</param>
		/// <returns>-1 when not even a slab stands, which is every city in the world until one is
		/// built.</returns>
		public static int RungReached(bool Slab, bool Vat, bool Hall, bool Theatre)
		{
			if (!Slab)
			{
				return -1;
			}
			if (!Vat)
			{
				return KingdomProcedureRules.RungSlab;
			}
			if (!Hall)
			{
				return KingdomProcedureRules.RungVat;
			}
			return Theatre ? KingdomProcedureRules.RungTheatre : KingdomProcedureRules.RungHall;
		}

		/// <summary>
		/// What a founder is told when a work stands above a gap. STANDARDS 7b's
		/// applicable-but-blocked case for the one stall this ladder can have: the grand thing is
		/// built, and it can do nothing, and nothing else would ever say why.
		/// </summary>
		/// <returns>Null when the ladder is unbroken, which is a sentence not worth writing.</returns>
		public static string LadderGapLine(bool Slab, bool Vat, bool Hall, bool Theatre)
		{
			if (Theatre || Hall)
			{
				if (!Slab)
				{
					return "The hall stands and nobody is bringing it anything. Raise a butcher's slab: what is dragged home has to become parts before it can become anything else.";
				}
				if (!Vat)
				{
					return "The hall stands over no vats. Raise a vat-house — the hall will not open a body for a thing that was not kept.";
				}
			}
			if (Theatre && !Hall)
			{
				return "The theatre stands and there is no grafting hall under it. The great work is the last step of a chain, not the first.";
			}
			return null;
		}

		// --- Megastructure cardinality (Addendum 22 A1, Design B) ------------------------------

		/// <summary>
		/// The building-record attribute that says a design is a megastructure: <c>"yes"</c>, in the
		/// same shape <c>Open</c> and <c>Sky</c> are already written in.
		/// <para>
		/// <b>Deliberately one attribute and one gate check, and no more.</b> Addendum 22 A1 rules
		/// the capital's extras and the annexe to later waves; the vocabulary that ships now is the
		/// smallest thing that can express "one purposeful megastructure per ordinary city", and if
		/// it ever wants a second attribute that is a design question rather than a patch.
		/// </para>
		/// </summary>
		public const string MegastructureAttribute = "Megastructure";

		/// <summary>Whether a design's <c>Megastructure</c> declaration means yes. Anything else,
		/// including absence, means no &mdash; a design is ordinary until it says otherwise.</summary>
		public static bool IsMegastructure(string Declared)
		{
			if (string.IsNullOrEmpty(Declared))
			{
				return false;
			}
			string folded = Declared.Trim().ToLowerInvariant();
			return folded == "yes" || folded == "true" || folded == "1";
		}

		/// <summary>
		/// Whether this city may raise this megastructure, given what it already keeps.
		/// <para>
		/// <b>A city gets one purpose.</b> The theatre, the arcology, and every megastructure after
		/// them contend for the same thing, and it is not ground &mdash; it is what the city is
		/// FOR. Re-keying the same design is allowed and is not a second purpose: a founder mending,
		/// re-siting or re-staking the one they already have is not choosing again.
		/// </para>
		/// </summary>
		/// <param name="Megastructure">Whether the design being zoned is one.</param>
		/// <param name="Kept">The key of the megastructure this city already keeps, or null.</param>
		/// <param name="Key">The design being zoned.</param>
		public static KingdomPurposeVerdict JudgePurpose(bool Megastructure, string Kept, string Key)
		{
			return JudgePurpose(Megastructure, CapitalOnly: false, Crowned: false, Kept: Kept, Key: Key);
		}

		/// <summary>
		/// The building-record attribute that says a design is one only the capital may raise:
		/// <c>"yes"</c>, in the same shape <see cref="MegastructureAttribute"/> is already written.
		/// <para>
		/// <b>The second cardinality lane, and it is deliberately a separate one.</b>
		/// <see cref="MegastructureAttribute"/> asks the city to spend its one purpose;
		/// <c>Capital</c> asks the realm to have set its crown down here. The capital ruling
		/// (author, extending Addendum 19) is exactly that split: an ordinary city gets ONE
		/// purposeful megastructure, and the capital gets its one PLUS extras that are capital
		/// specific. Two questions, two attributes, and neither one is the other's degree.
		/// </para>
		/// </summary>
		public const string CapitalAttribute = "Capital";

		/// <summary>Whether a design's <c>Capital</c> declaration means yes. Anything else,
		/// including absence, means no &mdash; a design stands in any city until it says
		/// otherwise.</summary>
		public static bool IsCapitalOnly(string Declared)
		{
			return IsMegastructure(Declared);
		}

		/// <summary>
		/// The whole cardinality verdict: the city's one purpose, and the crown.
		/// <para>
		/// <b>A capital-specific design is judged against the CROWN and never against the purpose
		/// slot</b>, and that precedence is the capital ruling rather than an implementation
		/// convenience. "A couple of extra capital-specific megastructures BEYOND its one" only
		/// means anything if the extras do not eat the one; a capital whose arcology had taken its
		/// purpose would be a capital that could not also be the flesh-city or the chrome-city, and
		/// the ruling says the opposite in the same breath it says A3. So the crown check runs
		/// first and returns, and <paramref name="Kept"/> is not consulted at all for such a design.
		/// </para>
		/// <para>
		/// <b>A3 still holds and is not weakened by any of this</b>: the theatre and the annexe are
		/// megastructures and neither is capital-specific, so the capital is judged against the
		/// purpose slot for both exactly as every other city is, and it may keep one of them, never
		/// two.
		/// </para>
		/// <para>
		/// <b>Fails CLOSED on the crown and OPEN on the purpose slot</b>, and the two directions are
		/// both deliberate. An unknown purpose permits, because a derivation that could not read the
		/// city must not brick the realm. An unknown crown refuses, because the crown is a fact
		/// about the REALM rather than about a dormant city &mdash; one string, always readable
		/// (<c>KingdomCrownRules.RegisterStateKey</c>) &mdash; so "we could not tell" is not a state
		/// the crown has, and treating a missing crown as a present one would hand every uncrowned
		/// realm the capital's whole catalogue.
		/// </para>
		/// </summary>
		/// <param name="Megastructure">Whether the design being zoned is one.</param>
		/// <param name="CapitalOnly">Whether the design declares <see cref="CapitalAttribute"/>.</param>
		/// <param name="Crowned">Whether the realm's crown is set down in THIS city
		/// (<c>KingdomCrown.CrownedHere</c>).</param>
		/// <param name="Kept">The key of the megastructure this city already keeps, or null.</param>
		/// <param name="Key">The design being zoned.</param>
		public static KingdomPurposeVerdict JudgePurpose(bool Megastructure, bool CapitalOnly, bool Crowned, string Kept, string Key)
		{
			if (CapitalOnly)
			{
				return Crowned ? KingdomPurposeVerdict.Allowed : KingdomPurposeVerdict.RefusedUncrowned;
			}
			if (!Megastructure || string.IsNullOrEmpty(Kept))
			{
				return KingdomPurposeVerdict.Allowed;
			}
			return string.Equals(Kept, Key, System.StringComparison.OrdinalIgnoreCase)
				? KingdomPurposeVerdict.Allowed
				: KingdomPurposeVerdict.RefusedKept;
		}

		/// <summary>
		/// The refusal for a design only a capital may raise. Names where the crown IS rather than
		/// the rule that keeps it there, so a founder learns the act rather than the law (STANDARDS
		/// 7b) &mdash; and the act is a real one either way: go and build there, or bring the crown
		/// here.
		/// </summary>
		/// <param name="CapitalName">The city keeping the crown, as the founder reads it, or null
		/// when the realm has no capital at all.</param>
		public static string UncrownedRefusalLine(string CapitalName)
		{
			if (string.IsNullOrEmpty(CapitalName))
			{
				return "Only a capital raises this, and the realm has no capital. Raise a crown hall in one of your cities and set the crown down in it.";
			}
			return "Only a capital raises this, and the crown is at " + Named(CapitalName)
				+ ". Build it there, or raise a crown hall here and move the crown to it.";
		}

		/// <summary>
		/// The refusal, and it names the thing in the way rather than the rule (STANDARDS 7b). A
		/// founder told "one megastructure per city" has learned a rule; a founder told which
		/// building is standing between them and this one has learned what to do about it.
		/// </summary>
		/// <param name="KeptName">What the city already keeps, as the founder reads it.</param>
		public static string PurposeRefusalLine(string KeptName)
		{
			return "This city already has its purpose, and it is " + Named(KeptName)
				+ ". A city is about one great thing. Take that one down, or raise this somewhere else.";
		}

		/// <summary>The line a city's own book carries about what it is for. Rendered rather than
		/// stored, so nothing anywhere has to keep it in step.</summary>
		public static string PurposeLine(string KeptName)
		{
			return string.IsNullOrEmpty(KeptName)
				? "{{K|This city is about nothing in particular yet.}}"
				: ("{{W|This city is about one thing, and it is " + Named(KeptName) + ".}}");
		}

		// --- Creed friction (DIVERSITY §3.6) ---------------------------------------------------
		//
		// Nothing new is written. The tags are the shipped QoL vocabulary, the ceiling is Addendum
		// 4d's own, and the standing cost rides the AdjustStanding path that already exists. What
		// is here is the arithmetic of WHEN the city speaks against the hall, and what it says.

		/// <summary>What a vat-house offers the quarter around it, and it is not a compliment.
		/// A resident who <c>Refuses="taf:offal"</c> will not live here &mdash; authored, never
		/// derived, because revulsion is a belief and <c>KingdomQolRules.Derive</c> deliberately
		/// never produces a refusal.</summary>
		public const string TagDamp = "taf:damp";

		/// <summary>The other half of the same sentence.</summary>
		public const string TagOffal = "taf:offal";

		/// <summary>
		/// The share of a city that must hold a creed the hall offends before anybody speaks
		/// against it. A tenth: below that the objection is one person's, and one person's objection
		/// is a conversation rather than a petition.
		/// </summary>
		public const int SpokenAgainstPercent = 10;

		/// <summary>
		/// Whether the city would speak against the hall for a procedure.
		/// <para>
		/// The trigger DIVERSITY &sect;3.6 names: a first procedure of consequence performed while a
		/// hostile-creed minority lives in the city. A minority, not a majority &mdash; a city where
		/// the offended creed is dominant never gets this petition, because that city could not
		/// staff the hall in the first place (Addendum 4d's fault-line ceiling does that work, and
		/// no rule of ours says so).
		/// </para>
		/// </summary>
		/// <param name="Offended">People here holding a creed the procedure costs standing with.</param>
		/// <param name="People">Everyone here.</param>
		/// <param name="AlreadySpoken">Whether the hall has been spoken against before. Once is the
		/// whole of it: a city that petitioned about the hall and was answered does not petition
		/// again every time the hall is used.</param>
		public static bool SpeaksAgainstHall(int Offended, int People, bool AlreadySpoken)
		{
			if (AlreadySpoken || People <= 0 || Offended <= 0 || Offended * 2 >= People)
			{
				return false;
			}
			return Offended * 100 >= People * SpokenAgainstPercent;
		}

		/// <summary>What the petitioner is waiting to speak about.</summary>
		public static string SpokenAgainstSubject()
		{
			return "what is done at the hall";
		}

		/// <summary>
		/// What they actually say, in their own mouth, and there is no correct answer to it. The
		/// founder's call, exactly as &sect;3.6 asks: friction is named people and placement, never
		/// a meter.
		/// </summary>
		/// <param name="Creed">The creed the speaker holds, as the founder reads it.</param>
		public static string SpokenAgainstSpeech(string Creed)
		{
            return "\"I have no quarrel with the hall's people and I will not pretend I do. But I was raised to believe a body is not a "
				+ "workshop, and I walk past that door every morning. I am not asking you to pull it down. I am asking you to say, out "
				+ "loud, in front of " + Named(Creed) + " and everyone else, that you know what it is you have built here.\"";
		}

		/// <summary>The deed, for the chronicle, when the founder answers.</summary>
		public static string SpokenAgainstDeed(string Name)
		{
			return "the hall at " + Named(Name) + " was spoken for out loud, in front of everyone who had to walk past it";
		}

		/// <summary>
		/// What a procedure costs the founder in standing, disclosed before it is committed.
		/// <para>
		/// The record's <c>Creeds</c> is read in the same <c>-Faction</c> removal idiom the QoL
		/// vocabulary already speaks, and spent through the shipped <c>AdjustStanding</c> path with
		/// its existing chronicle entry and outsider-register drift. Nothing new is written; what is
		/// here is only the reading and the sentence.
		/// </para>
		/// </summary>
		/// <returns>Faction name to standing delta, deltas negative. Never null.</returns>
		public static List<KeyValuePair<string, int>> StandingCost(string Creeds, int PerCreed)
		{
			List<KeyValuePair<string, int>> cost = new List<KeyValuePair<string, int>>();
			if (string.IsNullOrEmpty(Creeds) || PerCreed <= 0)
			{
				return cost;
			}
			string[] tokens = Creeds.Split(',');
			for (int i = 0; i < tokens.Length; i++)
			{
				string token = tokens[i].Trim();
				if (token.Length < 2 || token[0] != '-')
				{
					continue;
				}
				string faction = token.Substring(1).Trim();
				if (faction.Length > 0)
				{
					cost.Add(new KeyValuePair<string, int>(faction, -PerCreed));
				}
			}
			return cost;
		}

		/// <summary>
		/// Standing one procedure costs with each creed it offends. Deliberately modest and
		/// deliberately flat across the classes: a graft is a thing you did once, and a ladder of
		/// escalating standing costs would turn a belief into a meter, which &sect;3.6 forbids by
		/// name.
		/// </summary>
		public const int StandingPerCreed = 50;

		// --- The slate (DIVERSITY §3.8) ---------------------------------------------------------
		//
		// Two levels of Popup.PickOption and no new screen class, which is the golem's own shape,
		// Playable Golem's shape and the control menu's shape at once. The strings are here because
		// every one of them is a pure function of model state and none of them needs an engine.

		/// <summary>The mark a slot with something on it carries. Vanilla's own, from the golem
		/// mound's option list.</summary>
		public const string MarkFilled = "{{green|[þ]}}";

		/// <summary>The mark an empty slot carries.</summary>
		public const string MarkEmpty = "{{red|[X]}}";

		/// <summary>The prefix every effect line takes, so a founder reads a consequence in the same
		/// colour wherever the game shows them one.</summary>
		public const string EffectPrefix = "{{rules|--}} ";

		/// <summary>The slate's own heading.</summary>
		public static string SlateTitle(string CityName)
		{
			return "the grafting hall of " + Named(CityName);
		}

		/// <summary>
		/// The two lines above the list: who does the work, and what there is to work with. Both are
		/// facts a founder would otherwise have to go and count.
		/// </summary>
		/// <param name="Savant">The lodged savant's name, or null when the hall has none.</param>
		/// <param name="Was">What they were before they came, or null.</param>
		/// <param name="Kept">Preserved parts in the vat-house.</param>
		public static string SlateIntro(string Savant, string Was, int Kept)
		{
			System.Text.StringBuilder text = new System.Text.StringBuilder();
			if (string.IsNullOrEmpty(Savant))
			{
				// 7b: a hall with nobody in it will work no days at all, and that is the single
				// most important thing on this screen.
				text.Append("{{r|No savant is lodged here. The hall opens nothing until somebody who knows the work lives in this city.}}");
			}
			else
			{
				text.Append("savant: {{W|").Append(Savant).Append("}}");
				if (!string.IsNullOrEmpty(Was))
				{
					text.Append(", who was ").Append(Was);
				}
			}
			text.Append("\npreserved parts in the vat-house: ");
			text.Append((Kept > 0) ? ("{{C|" + Kept + "}}") : "{{K|none}}");
			return text.ToString();
		}

		/// <summary>
		/// One row of the slate: a place on the founder's body and what is on it.
		/// </summary>
		/// <param name="SlotName">The part, as the founder would say it &mdash; "your left arm".</param>
		/// <param name="GraftedName">What is grafted there, or null.</param>
		/// <param name="Offers">Whether the hall has anything at all it could put there.</param>
		public static string SlotRow(string SlotName, string GraftedName, bool Offers)
		{
			if (!string.IsNullOrEmpty(GraftedName))
			{
				return Named(SlotName) + "  " + MarkFilled + " " + GraftedName;
			}
			return Named(SlotName) + "  " + (Offers ? (MarkEmpty + " {{K|<nothing grafted>}}") : "{{K|nothing the hall knows would go there}}");
		}

		/// <summary>
		/// One candidate row, with its price stated before anything is committed. The fix for the
		/// one documented complaint about the vanilla picker (DIVERSITY &sect;3.0d): players treat
		/// the golem's atzmus as a lottery because the payoff is opaque at the point of choosing,
		/// and ours is not a lottery, so ours has no excuse.
		/// </summary>
		public static string CandidateRow(LabProcedure Procedure, int Kept)
		{
			if (Procedure == null)
			{
				return "";
			}
			System.Text.StringBuilder text = new System.Text.StringBuilder(Procedure.Named);
			text.Append("  {{K|[kept x").Append(Kept).Append("]}}");
			for (int i = 0; i < Procedure.Discloses.Count; i++)
			{
				text.Append("\n  ").Append(EffectPrefix).Append(Procedure.Discloses[i]);
			}
			text.Append("\n  ").Append(EffectPrefix).Append(PriceLine(Procedure));
			return text.ToString();
		}

		/// <summary>The whole price in one sentence, in the units the founder already reads
		/// everywhere else in the mod.</summary>
		public static string PriceLine(LabProcedure Procedure)
		{
			if (Procedure == null)
			{
				return "";
			}
			System.Text.StringBuilder text = new System.Text.StringBuilder();
			text.Append(Procedure.Cost).Append(" drams");
			if (!string.IsNullOrEmpty(Procedure.Bits))
			{
				text.Append(", ").Append(Procedure.Bits).Append(" in bits");
			}
			text.Append(", ").Append(Procedure.Preserved)
				.Append((Procedure.Preserved == 1) ? " kept part" : " kept parts");
			text.Append(", and ").Append(Procedure.StaffDays)
				.Append((Procedure.StaffDays == 1) ? " day" : " days").Append(" of the hall's work");
			return text.ToString();
		}

		/// <summary>
		/// The line a founder is owed before a graft that will change what they can do in the world
		/// at all.
		/// <para>
		/// Playable Golem's dominant complaint is that its golems cannot equip most gear or enter
		/// the Spindle (DIVERSITY &sect;3.0c, &sect;3.9 risk 4). Every Class III procedure needs an
		/// explicit answer to <i>"what does this stop you doing?"</i> stated before commitment, and
		/// the honest general answer &mdash; that the hall can take it off again &mdash; is stated
		/// with it, because that is the consent story.
		/// </para>
		/// </summary>
		public static string ReversibilityLine()
		{
			return "{{rules|--}} Whatever this stops you doing, the hall can take it off again. It costs less than the graft and returns nothing.";
		}

		/// <summary>The three-way consent prompt, in the precedent's own words. The third answer
		/// writes to a permanent exclusion list, so a founder who never wants to see a thing again
		/// never does.</summary>
		public static readonly string[] ConsentOptions = new string[3]
		{
			"Have it done.",
			"Not now.",
			"Never offer this again."
		};

		/// <summary>What the hall says when a commission is staked. Commissioning is not clicking:
		/// the crews work it over world-days and the founder may walk away and come home to it done,
		/// which is the whole mod's grammar and the lab may not be the one place that breaks it.</summary>
		public static string StakedLine(string ProcedureName, int StaffDays)
		{
			return "The hall has taken it on. " + Named(ProcedureName) + " wants {{C|" + StaffDays
				+ "}}" + ((StaffDays == 1) ? " day" : " days")
				+ " of real work from the people who live here. Go and do something else; it will be done when it is done.";
		}

		/// <summary>What the founder is told the day it is finished, wherever they are.</summary>
		public static string DoneLine(string ProcedureName, string CityName)
		{
			return "{{G|It is done. " + Named(ProcedureName) + " was performed on you at " + Named(CityName) + ".}}";
		}

		/// <summary>The same moment, dated, for the chronicle.</summary>
		public static string DoneTelling(string ProcedureName, string CityName)
		{
			return "the hall at " + Named(CityName) + " performed " + Named(ProcedureName) + ", and the founder walked out changed";
		}

		/// <summary>What the founder is told when a graft is taken off. Said plainly, including the
		/// part nobody wants to hear: it returns nothing.</summary>
		public static string RemovedTelling(string ProcedureName, string CityName)
		{
			return "the hall at " + Named(CityName) + " took " + Named(ProcedureName) + " back off again, and nothing was given back for it";
		}

		/// <summary>Vanilla's own register for a slot with no legal candidate at all
		/// (<c>Popup.ShowFail</c>'s <c>NO_REQUIRED</c> shape).</summary>
		public static string NothingMeetsRequirement(string SlotName)
		{
			return "You have nothing that meets the requirement of the hall for " + Named(SlotName) + ".";
		}

		/// <summary>A name as a founder would say it, or an honest word when nothing named one.</summary>
		public static string Named(string Text)
		{
			return string.IsNullOrEmpty(Text) ? "the work" : Text.Trim();
		}
	}
}
