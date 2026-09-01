using System.Collections.Generic;
using System.Text;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	/// <summary>
	/// Hubris subsides (VISION.md: a city stands on water, roofs, and the civic works within them).
	/// <para>
	/// Three things live here and nothing else does. <b>The level</b>: what a settlement's finished
	/// works carry between them, denominated against the stage the settlement has become, through
	/// <c>KingdomCatalogueRules.PopulationEquilibrium</c>. <b>The
	/// ladder</b>: the stage rule, hysteretic both ways, replacing the ratchet that only ever
	/// climbed. <b>The slide</b>: how a settlement standing above its own level converges back
	/// down to it as world time passes, in coarse per-stage steps, and where along the way that
	/// story has breakpoints worth writing down.
	/// </para>
	/// <para>
	/// <b>What subsidence is a punishment for.</b> Building past your works, and only that. It is
	/// not a punishment for absence &mdash; Addendum 8 clause 1 says the settlement lives whether
	/// the founder is there or not, so the slide runs on world time and would run identically
	/// under the founder's nose. It is not a punishment for building: a settlement whose works
	/// carry its people never subsides however much it has raised. What it costs is the gap
	/// between the two, and the gap is closed by raising water/roof/civic works or by losing the
	/// people they could not house and supply with water, whichever the founder chooses first.
	/// Food is not in this gap: crops and meals enable positive transactions only.
	/// </para>
	/// <para>
	/// <b>The floor is Camp's own equilibrium</b> (<c>KingdomCatalogueRules.FloorLevel</c>), not a
	/// special case bolted underneath. Nobody subsides out of existence, and a settlement that
	/// arrives at the floor is a camp, whatever its cisterns still measure.
	/// </para>
	/// <para>
	/// <b>Nothing here destroys anything.</b> The protection law (STANDARDS 7) forbids kingdom
	/// systems from consuming, moving, or deleting what the player placed. Subsidence ruins
	/// through wear &mdash; <see cref="RuinIncrement"/> against
	/// <c>KingdomMaterialRules.MaxWearPercent</c>, which a work never passes and which every
	/// mending undoes &mdash; and through people leaving, which goes through the one departure
	/// path the settlement already has. A subsided city is a damaged city standing in place, and
	/// it is put right by mending it.
	/// </para>
	/// </summary>
	public static partial class KingdomSubsidenceRules
	{
		// ==================================================================================
		// 1. The level: what the works carry, at the rate this settlement drinks.
		// ==================================================================================

		/// <summary>
		/// Settlers the summed <c>water</c> support sustains at a given stage.
		/// <para>
		/// The catalogue denominates water in drams a day, which is one settler's thirst
		/// <i>at camp rates</i> and says so at the attribute. <c>KingdomRules.UpkeepDrams</c> then
		/// scales the real bill by <c>StageUpkeepPercent</c>, so the same cisterns carry fewer
		/// people the grander the place becomes: a camp lives thin and a city drinks like a city.
		/// This is the conversion between the two, and it is the cross-check nothing performed
		/// before &mdash; the catalogue and the upkeep table had never been read against each
		/// other.
		/// </para>
		/// <para>
		/// Note which way it runs when a settlement falls: a City that becomes a Town needs less
		/// water per head, so its level RISES as it subsides. That is what makes the slide
		/// converge on something rather than run to the floor every time.
		/// </para>
		/// </summary>
		/// <param name="Water">Summed <c>water</c> contribution of every finished work.</param>
		/// <param name="Stage">What the settlement is now.</param>
		public static int LevelFromWater(int Water, GrowthStage Stage)
		{
			if (Water <= 0)
			{
				return 0;
			}
			int percent = UpkeepPercent(Stage);
			return (percent <= 100) ? Water : (int)((long)Water * 100L / percent);
		}

		/// <summary>What a settler costs a day at this stage, per hundred. Fails closed onto the
		/// camp rate for a stage this build does not define, which charges the least and so can
		/// never invent a shortfall out of a bad cast.</summary>
		private static int UpkeepPercent(GrowthStage Stage)
		{
			int index = (int)Stage;
			if (index < 0 || index >= KingdomRules.StageUpkeepPercent.Length)
			{
				return 100;
			}
			return KingdomRules.StageUpkeepPercent[index];
		}

		/// <summary>
		/// The population this settlement's works honestly carry: water converted out of drams at
		/// this stage's own rate, roof, and bounded lift. Food is intentionally absent; it enables
		/// explicit physical acts and never creates population pressure.
		/// </summary>
		/// <param name="Supports">Every finished work's <c>Carries</c>, summed, with the lifting
		/// half already scoped to what each work reaches (<c>KingdomSubsidence.Supports</c>).</param>
		/// <param name="Stage">What the settlement is now.</param>
		/// <param name="Shade">What the settlement's named notable is worth to it, from
		/// <c>KingdomCeremonyRules.NotableShade</c>. Defaulted because most callers &mdash; and
		/// every settlement that has never named anybody &mdash; honestly have none, and a
		/// defaulted zero is the same answer this function always gave.</param>
		/// <returns>Never below <c>KingdomCatalogueRules.FloorLevel</c>.</returns>
		public static int SupportedLevel(KingdomCatalogueRules.SupportTally Supports, GrowthStage Stage, int Shade = 0)
		{
			return KingdomCatalogueRules.PopulationEquilibrium(
				LevelFromWater(Supports.Water, Stage), Supports.Roof, Supports.Lift, Shade);
		}

		/// <summary>
		/// Which live population constraint is holding the settlement where it is, asked with the
		/// water already converted &mdash; so a city whose cisterns would be ample at camp rates
		/// is correctly told that it is the water, which is the whole point of the conversion.
		/// </summary>
		/// <returns><c>water</c> or <c>roof</c>. Never food or null.</returns>
		public static string BindingSupportFor(KingdomCatalogueRules.SupportTally Supports, GrowthStage Stage)
		{
			return KingdomCatalogueRules.PopulationBindingSupport(
				LevelFromWater(Supports.Water, Stage), Supports.Roof);
		}

		/// <summary>
		/// A stored live population-binding name, read back safely. Legacy <c>food</c> and unknown
		/// values come back null, so an old slide cannot blame food after migration.
		/// <para>
		/// Read-side rather than a repair in <c>Normalize</c>, deliberately. The seat swap's own
		/// contract is that a field survives a round trip byte for byte
		/// (<c>SettlementSeatTests.CaptureAndRestoreCarryEveryFieldACityHolds</c>), and a
		/// <c>Normalize</c> that rewrote this string would break it for no gain: the thing worth
		/// preventing is a sentence that blames the water for a name this build cannot read, and
		/// that is prevented here, where the sentences are written.
		/// </para>
		/// </summary>
		public static string NormalizedBinding(string Stored)
		{
			if (string.IsNullOrEmpty(Stored))
			{
				return null;
			}
			for (int i = 0; i < KingdomCatalogueRules.PopulationBindingSupports.Length; i++)
			{
				string canonical = KingdomCatalogueRules.PopulationBindingSupports[i];
				if (string.Equals(Stored.Trim(), canonical, System.StringComparison.OrdinalIgnoreCase))
				{
					return canonical;
				}
			}
			return null;
		}

	}
}
