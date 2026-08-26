using System.Collections.Generic;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomAnnexeRules
	{
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
	}
}
