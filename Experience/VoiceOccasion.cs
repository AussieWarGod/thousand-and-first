using System.Collections.Generic;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	/// <summary>
	/// A moment the settlement has something to say about.
	/// <para>
	/// These values are draw identity, not presentation: each one is the
	/// <c>EventKindCode</c> of its own ordinal lane (see <see cref="KingdomVoiceRules"/>), so
	/// renumbering one would re-cast every speaker in every existing save. Add at the end;
	/// never reorder, never reuse a retired number. Zero is absent on purpose &mdash; the
	/// kernel refuses a zero kind code, which would silently cost the first occasion its
	/// deterministic draw.
	/// </para>
	/// </summary>
	public enum VoiceOccasion
	{
		StageUp = 1,
		RaidRepelled = 2,
		ThirstBroken = 3,
		MealShared = 4,
		CitizenLost = 5,

		/// <summary>W4. Two settlers who already shared a roof were married.</summary>
		Wedding = 6,

		/// <summary>W4. A feast kept on a day of Qud's own calendar.</summary>
		Feast = 7,

		/// <summary>W4. What the city's creed makes of the founder's own body. The one occasion
		/// with no per-origin register: what a creed thinks of a mutation is a matter of belief,
		/// not of the country somebody walked out of, so every speaker answers in the plain
		/// one.</summary>
		FounderRegarded = 8
	}
}
