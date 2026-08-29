using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomQolRules
	{
		public static string[] DesignOffer(string Declared, KingdomPlotRules.RoofState Roof, bool IsPlot, bool Underground)
		{
			string[] declared = ParseTags(Declared);
			return IsPlot ? Merge(ProvidedByRoof(Roof, Underground), declared) : declared;
		}

		/// <summary>The same offer, from a design standing on the surface.</summary>
		public static string[] DesignOffer(string Declared, KingdomPlotRules.RoofState Roof, bool IsPlot)
		{
			return DesignOffer(Declared, Roof, IsPlot, Underground: false);
		}

		// --- The match ------------------------------------------------------------------------

		/// <summary>
		/// Whether this resident lives here.
		/// <para>
		/// Refusals are checked before needs, deliberately. Both are hard, but an unmet need is a
		/// thing the founder can go and build and a refusal is not, so naming the refusal first
		/// never sends anyone off to raise a charging post for somebody who was never going to
		/// sleep there.
		/// </para>
		/// </summary>
		/// <param name="Offer">What the place provides. Null or empty provides nothing, which is
		/// fine for a resident who asks nothing.</param>
		/// <param name="Profile">The resident. Null asks nothing and always matches.</param>
		/// <param name="Tag">The tag that decided it: the one refused, or the first need missing.
		/// Empty on a match.</param>
		public static QolVerdict Judge(string[] Offer, QolProfile Profile, out string Tag)
		{
			Tag = "";
			if (Profile == null)
			{
				return QolVerdict.Match;
			}
			if (Profile.Refuses != null)
			{
				for (int i = 0; i < Profile.Refuses.Length; i++)
				{
					if (Has(Offer, Profile.Refuses[i]))
					{
						Tag = Fold(Profile.Refuses[i]);
						return QolVerdict.Refused;
					}
				}
			}
			if (Profile.Needs != null)
			{
				for (int i = 0; i < Profile.Needs.Length; i++)
				{
					if (!Has(Offer, Profile.Needs[i]))
					{
						Tag = Fold(Profile.Needs[i]);
						return QolVerdict.NeedUnmet;
					}
				}
			}
			return QolVerdict.Match;
		}

		/// <summary>Whether a verdict means the match happens.</summary>
		public static bool IsMatch(QolVerdict Verdict)
		{
			return Verdict == QolVerdict.Match;
		}

		/// <summary>Whether a verdict is the STANDARDS 7b kind that owes the founder a sentence.
		/// Both refusals do; a match says nothing, correctly.</summary>
		public static bool IsBlocked(QolVerdict Verdict)
		{
			return Verdict != QolVerdict.Match;
		}

		// --- Prefers: the small, capped, tastes-shaped half -------------------------------------

		/// <summary>
		/// How many met Prefers are ever counted. Two, which is the number of tastes a settling
		/// notable states, so a person with a long authored wish-list cannot out-shade a person
		/// with the ordinary one or two.
		/// </summary>
		public const int MaxPrefersCounted = 2;

		/// <summary>
		/// Which of this resident's Prefers the place meets, as the met/unmet flags
		/// <c>KingdomCeremonyRules.TasteShade</c> already takes. Capped at
		/// <see cref="MaxPrefersCounted"/> entries.
		/// <para>
		/// Deliberately this shape rather than a number of its own: the tastes machinery is the
		/// mod's one and only route from "this person found what they wanted here" to equilibrium,
		/// and a second route would be a second balance to keep.
		/// </para>
		/// </summary>
		/// <returns>Never null; empty for a resident who prefers nothing.</returns>
		public static List<bool> PreferFlags(string[] Offer, QolProfile Profile)
		{
			List<bool> flags = new List<bool>();
			if (Profile == null || Profile.Prefers == null)
			{
				return flags;
			}
			for (int i = 0; i < Profile.Prefers.Length && flags.Count < MaxPrefersCounted; i++)
			{
				flags.Add(Has(Offer, Profile.Prefers[i]));
			}
			return flags;
		}

		/// <summary>
		/// Equilibrium points this resident's met Prefers are worth, through the tastes machinery
		/// and capped by it: <c>KingdomCeremonyRules.TasteShadeAmount</c> a piece, never more than
		/// <see cref="MaxPrefersCounted"/> pieces.
		/// <para>
		/// There is no matching negative anywhere in this file. An unmet Prefers subtracts nothing
		/// and is not recorded; it simply means this person lives the way they would have lived
		/// anywhere.
		/// </para>
		/// </summary>
		public static int PreferShade(string[] Offer, QolProfile Profile)
		{
			return KingdomCeremonyRules.TasteShade(PreferFlags(Offer, Profile));
		}

		/// <summary>The most any one resident's Prefers can ever be worth.</summary>
		public static int MaxPreferShade
		{
			get
			{
				return MaxPrefersCounted * KingdomCeremonyRules.TasteShadeAmount;
			}
		}

		// --- Cohabitation ---------------------------------------------------------------------

		/// <summary>
		/// <b>Superseded</b> by <c>KingdomLodgingRules.RefusalHostility</c> (the closeness ladder,
		/// brief Addendum 4c) under the fault-line ceiling (4d), and kept only until its last
		/// caller is moved. Do not write new callers against it. What the hundred means is still
		/// exactly this:
		/// <para>
		/// Hostility at which two creeds will not share a roof, on the 0..100 scale
		/// <c>KingdomCreedRules.Hostility</c> returns. A hundred: only the game's own flat -100
		/// fault lines &mdash; the Templar and the Girsh, the Barathrumites and the Templar &mdash;
		/// and never the standing -50 several factions hold toward everyone they have not troubled
		/// to name, which would otherwise stop half a mixed settlement from sharing a wall.
		/// Everything milder is texture, and texture is what <c>Refuses</c> tags are for.
		/// </para>
		/// </summary>
		[System.Obsolete("Retired before public release; use KingdomLodgingRules.RefusalHostility(quarters).", true)]
		public const int CohabitHostility = 100;

		/// <summary>
		/// Whether two people share a roof <b>under the superseded flat floor</b>. The live answer
		/// is <c>KingdomLodgingRules</c>, which scales the same two sources by the quarters
		/// (Addendum 4c) under the fault-line ceiling (4d); this remains only until its last caller
		/// is moved. One rule, two sources: the engine's faction feelings for the ideological case,
		/// and the tag vocabulary for everything else &mdash; the neighbour's household is judged
		/// by exactly the <see cref="Judge"/> a building is.
		/// </summary>
		/// <param name="Profile">The person being moved in. Null refuses nobody.</param>
		/// <param name="TheirHousehold">What the household already there provides, from
		/// <see cref="HouseholdProvides"/>.</param>
		/// <param name="CreedHostility">From <c>KingdomCreed.HostilityBetween</c>. Zero for
		/// creedless people, people of one creed, and creeds that get on.</param>
		/// <param name="Tag">The tag refused, or empty when the creed decided it or nothing did.
		/// </param>
		/// <returns><see cref="QolVerdict.Refused"/> for a creed clash, so the caller can tell the
		/// two cases apart by <paramref name="Tag"/> being empty.</returns>
	}
}
