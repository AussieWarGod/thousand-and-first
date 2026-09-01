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
	/// reads what the finished works physically provide, converts that to a level at this settlement's own
	/// stage (<see cref="KingdomSubsidenceRules.SupportedLevel"/>), runs the slide forward over
	/// however much world time has passed, and tells the founder about it once.
	/// </para>
	/// <para>
	/// <b>What the sum is made of.</b> Water and roof are the live population constraints. Food
	/// remains a separately measured physical production/storage lane, but it never binds this
	/// slide: absent food can withhold a chosen meal or industry act and cannot make anyone leave.
	/// Roof and lift come only from live providers inside an exact designation; catalogue
	/// <c>Carries</c> caps those providers and never supplies them. A work's lift lands in
	/// proportion to the settlement's
	/// roofs it reaches (<see cref="ScopedSupports"/>, Addendum 6); and the settlement's named
	/// settlement is worth a small shade of its own (<c>KingdomSystem.Shade</c>) &mdash; its named
	/// notable's met tastes, virtue net of flaw and met <c>Prefers</c>. Meal shade is a retired
	/// wire field and contributes zero. All live lifts ride the one term inside
	/// <c>KingdomCatalogueRules.LiftCapPercent</c>, so none can carry a settlement past its own
	/// water or roofs.
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
		/// What this settlement's finished works physically supply between them.
		/// <para>
		/// A work that asks for crew carries at what the staffing pass gave it, reduced again by
		/// its own condition, so an unmanned field feeds nobody. That is Addendum 8 clause 2
		/// applied to the level: infrastructure times labour, never infrastructure alone.
		/// </para>
		/// <para>
		/// <b>And a work that asks for nobody carries at its CONDITION</b> (Addendum 10(b)). This
		/// used to be a flat 100 &mdash; wear reached the level only through the
		/// <c>KingdomStaffNeeded</c> gate, so a half-wrecked reservoir carried its full
		/// twenty-six drams and only a staffed work could be hurt by ruin
		/// at all. The ruling overturned it: a ruined reservoir does not carry its full drams.
		/// Both arms are <see cref="KingdomWearRules.WorkEffectiveness"/>, which is also what
		/// <c>KingdomPower</c> asks, so the rule lives in exactly one place.
		/// </para>
		/// <para>
		/// <b>Why the condition is read off the work rather than off the stamp.</b>
		/// <c>KingdomEffectiveness</c> is the staffing pass's own crew stretch and nothing else;
		/// nobody folds wear into it any more. This function is called twice per pass from two
		/// different consumers (the water works' daily make and the supported level after
		/// <c>AssignWork</c>), and reading condition from the part rather than
		/// from a property somebody else may or may not have already folded is what makes both
		/// answers the same arithmetic.
		/// </para>
		/// </summary>
		/// <param name="Survey">The pass's survey. Null carries nothing.</param>
		public static KingdomCatalogueRules.SupportTally Supports(KingdomSurvey Survey)
		{
			if (Survey == null || Survey.Ground == null)
				return default(KingdomCatalogueRules.SupportTally);
			KingdomBenefitIndex benefits = null;
			KingdomReach.TryActiveBenefits(Survey.Ground, Survey, "subsidence", out benefits);
			return Supports(Survey, benefits, true);
		}

		internal static KingdomCatalogueRules.SupportTally OrdinarySupports(KingdomSurvey Survey)
		{
			if (Survey == null || Survey.Ground == null)
				return default(KingdomCatalogueRules.SupportTally);
			KingdomBenefitIndex benefits = null;
			KingdomReach.TryActiveBenefits(Survey.Ground, Survey,
				"ordinary subsidence projection", out benefits);
			return Supports(Survey, benefits, false);
		}

		private static KingdomCatalogueRules.SupportTally Supports(KingdomSurvey Survey,
			KingdomBenefitIndex Benefits, bool IncludeHosted)
		{
			KingdomCatalogueRules.SupportTally tally = default(KingdomCatalogueRules.SupportTally);
			if (Survey == null || Survey.Ground == null) return tally;
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
				int effectiveness = KingdomWear.EffectivenessOf(work);
				if (!work.HasStringProperty(KingdomAdopt.AdoptedKeyProperty))
					tally = KingdomCatalogueRules.FoldWork(tally,
						PhysicalFlowContract(work, entry), effectiveness);
				int yardFood = KingdomYardBenefits.PhysicalFoodForHouse(Survey, work);
				if (yardFood > 0)
					tally.Food = KingdomCatalogueRules.SaturatingCounterAdd(tally.Food,
						KingdomCatalogueRules.Carried(yardFood, effectiveness));
				if (IncludeHosted && work.GetPart<r_KingdomArcology>() != null)
				{
					if (KingdomHostedArcology.TryTerracePhysicalFood(work,
						Survey.StoredWater > 0, out int food, out string failure))
						tally.Food = KingdomCatalogueRules.SaturatingCounterAdd(tally.Food,
							KingdomCatalogueRules.Carried(food, effectiveness));
					else KingdomLog.Log("subsidence: hosted terrace failed closed ("
						+ (failure ?? "unknown physical evidence") + ")");
				}
			}
			if (Benefits != null)
			{
				IReadOnlyList<KingdomBenefitReading> readings = Benefits.Readings;
				for (int i = 0; i < readings.Count; i++)
				{
					KingdomBenefitReading reading = readings[i];
					if (!KingdomReach.TryRoot(Survey.Ground, reading, out GameObject work)) continue;
					bool hosted = work.GetPart<r_KingdomArcology>() != null;
					List<KindAmount> carries;
					string failure = null;
					if (hosted && !IncludeHosted)
						carries = new List<KindAmount>(reading.Carries);
					else if (!KingdomObservedBenefitProjection.TryCarries(work, reading,
						out carries, out failure))
					{
						KingdomLog.Log("subsidence: observed benefits failed closed ("
							+ (failure ?? "unknown physical evidence") + ")");
						continue;
					}
					tally.Roof = KingdomCatalogueRules.SaturatingCounterAdd(tally.Roof,
						KingdomObservedBenefitProjectionRules.Amount(carries,
							KingdomCatalogueRules.SupportRoof));
					tally.Lift = KingdomCatalogueRules.SaturatingCounterAdd(tally.Lift,
						KingdomObservedBenefitProjectionRules.PhysicalLift(carries));
				}
			}
			return tally;
		}

		// Food and water are the two exceptional flow contracts. Their existing crop, staffing,
		// wear, container, and landing systems establish live supply; every other catalogue number
		// is only a designation cap and is deliberately removed here.
		private static List<KindAmount> PhysicalFlowContract(GameObject Work,
			KingdomRules.BuildEntry Entry)
		{
			List<KindAmount> declared;
			KingdomCatalogueRules.TryParseTally(Entry.Carries, out declared, out _);
			declared = KingdomCrops.WithoutUnsownFood(Work, declared);
			List<KindAmount> flows = new List<KindAmount>();
			for (int i = 0; declared != null && i < declared.Count; i++)
				if (declared[i].Kind == KingdomCatalogueRules.SupportWater
					|| declared[i].Kind == KingdomCatalogueRules.SupportFood)
					flows.Add(declared[i]);
			return flows;
		}

	}
}
