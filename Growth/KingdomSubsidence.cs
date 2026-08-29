using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	/// <summary>
	/// The consumer of the equilibrium arithmetic, and the slide that follows from it.
	/// <para>
	/// One reckoning per attended pass, run from <c>KingdomGrowth.UpdateStage</c> after the
	/// staffing pass has said which works are actually running. It does four things in order:
	/// sums what the finished works carry, converts that to a level at this settlement's own
	/// stage (<see cref="KingdomSubsidenceRules.SupportedLevel"/>), runs the slide forward over
	/// however much world time has passed, and tells the founder about it once.
	/// </para>
	/// <para>
	/// <b>What the sum is made of.</b> Three things, and only the first is a building's
	/// <c>Carries</c>. A household's yard trade shades the same pools beside the house it belongs
	/// to (<see cref="Supports"/>); a work's LIFT lands only in proportion to the settlement's
	/// roofs it reaches (<see cref="ScopedSupports"/>, Addendum 6); and the settlement's named
	/// settlement is worth a small shade of its own (<c>KingdomSystem.Shade</c>) &mdash; its named
	/// notable's met tastes, virtue net of flaw and met <c>Prefers</c>, plus whatever the last
	/// day's eating left behind (<c>KingdomRules.MealShadeFor</c>, Addendum 11(b): a settlement
	/// that ate its own favourite dish is well fed for exactly one day). All of them ride the
	/// one lift term inside <c>KingdomCatalogueRules.LiftCapPercent</c>, so none of them can carry
	/// a settlement past its own water.
	/// </para>
	/// <para>
	/// <b>The clock.</b> World time, uncapped, through <c>KingdomRules.ElapsedDays</c> and a
	/// checkpoint that advances by exactly the steps it cashed. The settlement lives whether the
	/// founder is there or not (Addendum 8 clause 1), so the slide runs the same length whether
	/// it is watched or not; what changes at a homecoming is only that somebody is told. The
	/// stamp is planted on the first pass before any days are counted &mdash; the same lesson
	/// <c>LastFetchTick</c> learned, where an unplanted stamp read as the age of the world.
	/// </para>
	/// <para>
	/// <b>The protection law.</b> Nothing here deletes or moves anything. Works are ruined by
	/// wear on the part the mending system already owns, capped at
	/// <c>KingdomMaterialRules.MaxWearPercent</c>, and every point of it is mendable. People
	/// leave through <c>KingdomGrowth.Emigrate</c>, which is the settlement's one departure path
	/// and floors at <c>KingdomRules.LoyalCoreSettlers</c> &mdash; and the level itself floors at
	/// <c>KingdomCatalogueRules.FloorLevel</c>, so the floor that actually binds is Camp's own
	/// equilibrium and nobody subsides out of existence.
	/// </para>
	/// <para>
	/// <b>A city, not a zone.</b> The ground under the pass's feet is counted from the survey,
	/// which is the zone the founder is standing in &mdash; and then every OTHER zone the city
	/// claims is folded in as it was last seen (<see cref="OtherZones"/>). Before that, a
	/// two-zone city's level swung with which way the founder walked in: entering through the
	/// mine overwrote the city's supported level with the mine's cisterns and the granary
	/// vanished. Nothing here simulates an unvisited zone forward &mdash; a sighting is dated,
	/// stays exactly as old as it is, and a zone nobody has ever stood in contributes nothing.
	/// </para>
	/// </summary>
	public static partial class KingdomSubsidence
	{
		public static bool Enabled => Options.GetOption("r_TAF_OptionSubsidence") != "No";

		/// <summary>Per-settlement bounded option observation. Kept in the already-serialized
		/// game-state store because subsidence is citywide while a city may own several zones;
		/// putting this on whichever zone was visited would reinitialize the city clock at every
		/// boundary.</summary>
		public const string OptionStatePrefix = "r_TAF_SubsidenceOption_v1:";

		private static KingdomElapsedOptionDecision ObserveOption(KingdomSystem System,
			long Now)
		{
			string settlementId = KingdomChronicle.SettlementId(System);
			if (The.Game == null || !KingdomIdentityRules.IsSettlementId(settlementId))
			{
				return KingdomElapsedOptionRules.Observe(
					KingdomElapsedOptionRecord.Unobserved, Enabled,
					System?.MasterAppliedResumeToken ?? 0L, Now);
			}
			string key = OptionStatePrefix + settlementId;
			string encoded = The.Game.GetStringGameState(key, "");
			KingdomElapsedOptionRecord prior;
			bool decoded = KingdomElapsedOptionRules.TryDecode(encoded, out prior);
			if (!decoded) prior = KingdomElapsedOptionRecord.Unobserved;
			KingdomElapsedOptionDecision decision = KingdomElapsedOptionRules.Observe(prior,
				Enabled, System.MasterAppliedResumeToken, Now);
			if (!decision.Valid)
			{
				decision = KingdomElapsedOptionRules.Observe(
					KingdomElapsedOptionRecord.Unobserved, Enabled,
					System.MasterAppliedResumeToken, Now);
			}
			return decision;
		}

		private static void CommitOption(KingdomSystem System,
			KingdomElapsedOptionRecord Record)
		{
			string settlementId = KingdomChronicle.SettlementId(System);
			if (The.Game == null || !KingdomIdentityRules.IsSettlementId(settlementId)) return;
			string next = KingdomElapsedOptionRules.Encode(Record);
			if (next != null)
				The.Game.SetStringGameState(OptionStatePrefix + settlementId, next);
		}

		/// <summary>
		/// What this settlement's finished works carry between them.
		/// <para>
		/// A work that asks for crew carries at what the staffing pass gave it, reduced again by
		/// its own condition, so an unmanned field feeds nobody. That is Addendum 8 clause 2
		/// applied to the level: infrastructure times labour, never infrastructure alone.
		/// </para>
		/// <para>
		/// <b>And a work that asks for nobody carries at its CONDITION</b> (Addendum 10(b)). This
		/// used to be a flat 100 &mdash; wear reached the level only through the
		/// <c>KingdomStaffNeeded</c> gate, so a half-wrecked reservoir carried its full
		/// twenty-six drams and only the food lane, which never automates, could be hurt by ruin
		/// at all. The ruling overturned it: a ruined reservoir does not carry its full drams.
		/// Both arms are <see cref="KingdomWearRules.WorkEffectiveness"/>, which is also what
		/// <c>KingdomPower</c> asks, so the rule lives in exactly one place.
		/// </para>
		/// <para>
		/// <b>Why the condition is read off the work rather than off the stamp.</b>
		/// <c>KingdomEffectiveness</c> is the staffing pass's own crew stretch and nothing else;
		/// nobody folds wear into it any more. This function is called twice per pass from two
		/// different points in <c>KingdomGrowth</c> (the water works' daily make, at the top, and
		/// the level, after <c>AssignWork</c>), and reading condition from the part rather than
		/// from a property somebody else may or may not have already folded is what makes both
		/// answers the same arithmetic.
		/// </para>
		/// </summary>
		/// <para>
		/// <b>And a household's yard trade carries with the house it belongs to.</b> A
		/// <c>&lt;yardwork&gt;</c>'s <c>Shades</c> is denominated in exactly the same
		/// <c>support:amount</c> language a design's <c>Carries</c> is, and is capped small
		/// (<c>KingdomYardRules.MaxShadePerWork</c>) precisely because it lands here. It is folded
		/// through <c>KingdomCatalogueRules.FoldShade</c> rather than <c>FoldWork</c>, so a vine
		/// lattice feeds the settlement without pretending to be a second thing standing.
		/// </para>
		/// <param name="Survey">The pass's survey. Null carries nothing.</param>
		public static KingdomCatalogueRules.SupportTally Supports(KingdomSurvey Survey)
		{
			KingdomCatalogueRules.SupportTally tally = default(KingdomCatalogueRules.SupportTally);
			if (Survey == null)
			{
				return tally;
			}
			for (int i = 0; i < Survey.Built.Count; i++)
			{
				GameObject work = Survey.Built[i];
				if (!GameObject.Validate(work))
				{
					continue;
				}
				string key = KingdomUpgrade.DesignKeyOf(work);
				KingdomRules.BuildEntry entry;
				if (string.IsNullOrEmpty(key) || !KingdomData.TryGetBuilding(key, out entry))
				{
					continue;
				}
				// A malformed Carries is already reported by the catalogue validator, and whatever
				// parsed before the bad pair still counts, so the verdict is deliberately unread.
				List<KindAmount> carries;
				KingdomCatalogueRules.TryParseTally(entry.Carries, out carries, out _);
				carries = KingdomHostedArcology.HostedCarries(work, carries,
					Survey.StoredWater > 0);
				// Addendum 11(b): a farm starts producing only once seeds are committed, so a field
				// nobody has sown carries no food - to the level or to the day. Everything else the
				// design carries is untouched, because a home farm's mill and its yard are built and
				// real whether or not a row is in the ground. The rule lives in KingdomCrops so the
				// level and KingdomGrowth.FoodMadePerDay cannot disagree about which fields count.
				carries = KingdomCrops.WithoutUnsownFood(work, carries);
				int effectiveness = KingdomWear.EffectivenessOf(work);
				tally = KingdomCatalogueRules.FoldWork(tally, carries, effectiveness);
				tally = KingdomCatalogueRules.FoldShade(tally, YardShadesOf(work), effectiveness);
			}
			return tally;
		}

		/// <summary>What the household living in this work has turned its yard to, or null for a
		/// house that has taken up no trade and for every work that is not a house.</summary>
		private static List<KindAmount> YardShadesOf(GameObject Work)
		{
			string key = Work.GetStringProperty(KingdomYards.YardKeyProperty);
			KingdomYardRules.YardWorkSpec spec;
			return (!string.IsNullOrEmpty(key) && KingdomYards.TryGetSpec(key, out spec)) ? spec.Shades : null;
		}

		/// <summary>The lifting half of one parsed <c>support:amount</c> list, scaled the way a
		/// lift is scaled (<c>KingdomReachRules.Scaled</c>, which keeps a point of anything still
		/// being worked). The binding half is left to <see cref="Supports"/>, which has already
		/// folded it into the citywide pools.</summary>
		private static int LiftOf(List<KindAmount> Shades, int EffectivenessPercent)
		{
			int lift = 0;
			for (int i = 0; (Shades != null) && i < Shades.Count; i++)
			{
				if (!KingdomCatalogueRules.IsBindingSupport(Shades[i].Kind))
				{
					lift += KingdomReachRules.Scaled(Shades[i].Amount, EffectivenessPercent);
				}
			}
			return lift;
		}

	}
}
