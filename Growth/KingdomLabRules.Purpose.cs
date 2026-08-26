using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomLabRules
	{
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

	}
}
