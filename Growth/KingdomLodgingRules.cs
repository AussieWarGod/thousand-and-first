using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>
	/// Engine-free arithmetic and prose for cohabitation (Addendum 4, the quality-of-life tag
	/// vocabulary): who is allowed to sleep under which roof, and who is never put beside whom.
	/// <see cref="KingdomLodging"/> is the engine-coupled shell &mdash; it reads real settlers,
	/// real buildings, and the engine's own creed feelings, and calls down into the pure
	/// functions here for every decision that has one right answer given the facts.
	/// <para>
	/// Three hard gates, all placement constraints and never meters (VISION.md, STANDARDS 7b):
	/// a resident is never assigned a home whose <c>Provides</c> does not cover every one of
	/// their <c>Needs</c> (<see cref="MeetsNeeds"/>); a resident is never assigned a home that has
	/// no free bed (<see cref="HasFreeBed"/>); and two residents who refuse each other &mdash;
	/// by creed or by an authored <c>Refuses</c> tag &mdash; are never assigned the same building
	/// (<see cref="Conflicts"/>). Nothing here shades an equilibrium number: <c>Prefers</c> is
	/// out of scope for this file entirely, exactly as Addendum 4 splits it out ("never a penalty
	/// unmet") from the two hard gates and the one hard negative this file owns.
	/// </para>
	/// <para>
	/// <b>Feelings scale with closeness (Addendum 4c).</b> The third gate is not one threshold but
	/// a ladder of four: <see cref="Closeness"/>, derived from a design's beds-per-footprint and
	/// overridable by its <c>Closeness</c> attribute, decides how much of a quarrel these
	/// particular quarters will hold (<see cref="RefusalHostility"/>). An authored
	/// <c>Refuses</c> tag is unscaled and absolute at every rung. The consequence is the ruling's
	/// own: a diverse city must build better housing to exist at all.
	/// </para>
	/// <para>
	/// <b>Refuses, read against what.</b> The brief pairs <c>Refuses</c> with "no cohabitation"
	/// rather than with a building quality, so this file tests it resident-against-resident: a
	/// refusal names a tag, and it fires when the OTHER resident carries that tag among their own
	/// <c>Needs</c> or <c>Prefers</c> &mdash; the only two self-declared surfaces the vocabulary
	/// gives a resident. No third tag category is invented to hold "what a resident presents to
	/// others"; the two that already exist do that job.
	/// </para>
	/// <para>
	/// <b>Where the tags come from.</b> Nothing here parses a creature: every list handed to these
	/// functions is assembled by <see cref="KingdomLodging"/> from resident profiles and the
	/// benefit index's exact physical-provider reading for the assigned root. A designation or
	/// catalogue row contributes no offer by itself. A robot needs charge here because the engine
	/// says it is a robot; a home meets that need only while an actual provider supplies it.
	/// </para>
	/// <para>
	/// <b>Determinism without dice.</b> Which of several eligible homes a resident is assigned to
	/// is decided by <see cref="ChooseIndex"/>: fewest free beds first (fill a household before
	/// opening an empty one), the plot's own id as a stable tiebreak. Nothing here calls the
	/// kernel's counter-random draw, on purpose &mdash; that idiom exists for choices that are
	/// genuinely a matter of taste (which of ten taste categories, which virtue, which pattern-
	/// book design), and dressing a logistics decision in dice would make it LESS honest, not
	/// more. The result is still exactly as reproducible: the same city state always assigns the
	/// same way, and a real decision, once made, is persisted on the resident rather than
	/// re-drawn.
	/// </para>
	/// </summary>
	public static partial class KingdomLodgingRules
	{
		/// <summary>
		/// Hostility (0-100, from <c>KingdomCreed.HostilityBetween</c>) above which two creeds
		/// will not share the tightest quarters there are. Zero: in one open room any real enmity
		/// at all is enough to refuse cohabitation, because a household is closer quarters than a
		/// shared city. Named rather than inlined so a playtest that wants a softer floor changes
		/// one constant, not a comparison buried in a loop.
		/// <para>
		/// This is the bottom rung of Addendum 4c's ladder and no longer the whole of the rule:
		/// <see cref="PackedRefusalHostility"/> is defined as one past it, and better quarters
		/// hold worse feelings (<see cref="RefusalHostility"/>).
		/// </para>
		/// </summary>
		public const int CreedRefusalHostilityFloor = 0;

		/// <summary>Splits a comma list of namespaced tags (<c>Provides</c>, <c>Needs</c>,
		/// <c>Prefers</c>, <c>Refuses</c>) into trimmed, non-empty tokens, in the order
		/// written.</summary>
		/// <param name="Raw">The raw attribute or tag value. Null and empty both read as no
		/// tags.</param>
		/// <returns>Never null. Empty when <paramref name="Raw"/> named nothing.</returns>
		public static List<string> ParseTags(string Raw)
		{
			List<string> tags = new List<string>();
			if (string.IsNullOrEmpty(Raw))
			{
				return tags;
			}
			string[] parts = Raw.Split(',');
			for (int i = 0; i < parts.Length; i++)
			{
				string token = parts[i].Trim();
				if (token.Length > 0)
				{
					tags.Add(token);
				}
			}
			return tags;
		}

		/// <summary>Whether any tag in <paramref name="A"/> also appears in
		/// <paramref name="B"/>, case-insensitively. Tag order never matters, only membership.
		/// </summary>
		public static bool Intersects(IReadOnlyList<string> A, IReadOnlyList<string> B)
		{
			if (A == null || B == null || A.Count == 0 || B.Count == 0)
			{
				return false;
			}
			for (int i = 0; i < A.Count; i++)
			{
				for (int j = 0; j < B.Count; j++)
				{
					if (string.Equals(A[i], B[j], StringComparison.OrdinalIgnoreCase))
					{
						return true;
					}
				}
			}
			return false;
		}

		/// <summary>
		/// The hard placement gate: every one of a resident's <c>Needs</c> must appear among a
		/// building's <c>Provides</c>. No needs is trivially met by anything, including a
		/// building that provides nothing &mdash; which is every building in the base catalogue
		/// until a design author writes a <c>Provides</c> string, and is why an unauthored
		/// catalogue behaves exactly as it did before this vocabulary existed.
		/// </summary>
		public static bool MeetsNeeds(IReadOnlyList<string> Needs, IReadOnlyList<string> Provides)
		{
			if (Needs == null || Needs.Count == 0)
			{
				return true;
			}
			if (Provides == null || Provides.Count == 0)
			{
				return false;
			}
			for (int i = 0; i < Needs.Count; i++)
			{
				bool found = false;
				for (int j = 0; j < Provides.Count; j++)
				{
					if (string.Equals(Needs[i], Provides[j], StringComparison.OrdinalIgnoreCase))
					{
						found = true;
						break;
					}
				}
				if (!found)
				{
					return false;
				}
			}
			return true;
		}

		/// <summary>Whether a building has a bed free for one more resident. Strict: a building
		/// at capacity takes nobody else, even the resident who would refuse nobody in it.
		/// </summary>
		public static bool HasFreeBed(int Capacity, int Occupants)
		{
			return Occupants < Capacity;
		}

	}
}
