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
	public static class KingdomSubsidence
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

		/// <summary>
		/// The same tally, with its lifting half scoped to what each work actually reaches
		/// (Addendum 6). The <b>only</b> difference from <see cref="Supports"/> is
		/// <c>SupportTally.Lift</c>: water, food and roofs are drawn and carried, so they stay the
		/// citywide pools they have always been, and faith, order, learning, luxury and craft
		/// shade the people in reach of the work giving them.
		/// <para>
		/// Denominated in roofs, which is the level's own currency for a person: a work's lift
		/// lands in proportion to the settlement's housing it covers
		/// (<c>KingdomReachRules.Landed</c>). A shrine standing among the houses is worth its
		/// whole amount; the same shrine out past the fields is worth what it touches; and a
		/// wayside statue that reaches no home lands nothing on the level while still shading the
		/// ground it stands on. That is what makes the temple quarter different ground from the
		/// tanners' rather than a second number nobody can see.
		/// </para>
		/// <para>
		/// The great works of the realm's other claimed zones arrive whole, out of the record
		/// their own attended passes wrote (<c>KingdomReach.CityShadeExcept</c>), because a city
		/// band covers every cell of the city by definition. This zone's own record is deliberately
		/// skipped: what stands here has just been counted from the ground.
		/// </para>
		/// </summary>
		/// <param name="System">The realm. Null falls back to the unscoped tally rather than
		/// dropping every lift &mdash; a caller with no realm to measure against is asking a
		/// different question, not asking this one badly.</param>
		/// <param name="Z">The zone the pass is in.</param>
		/// <param name="Survey">The pass's survey.</param>
		public static KingdomCatalogueRules.SupportTally ScopedSupports(KingdomSystem System, Zone Z, KingdomSurvey Survey)
		{
			KingdomCatalogueRules.SupportTally tally = Supports(Survey);
			if (System == null || Z == null || Survey == null)
			{
				return tally;
			}
			List<Cell> homes = new List<Cell>();
			List<int> housed = new List<int>();
			List<GameObject> lifters = new List<GameObject>();
			List<int> lifted = new List<int>();
			int roofs = 0;
			int trades = 0;
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
				List<KindAmount> carries;
				KingdomCatalogueRules.TryParseTally(entry.Carries, out carries, out _);
				int effectiveness = KingdomWear.EffectivenessOf(work);
				int lift = 0;
				for (int c = 0; c < carries.Count; c++)
				{
					// The kinds come out of TryParseTally already folded, so the comparison
					// against the catalogue's own constant is the whole test.
					if (carries[c].Kind == KingdomCatalogueRules.SupportRoof)
					{
						int people = KingdomCatalogueRules.Carried(carries[c].Amount, effectiveness);
						Cell cell = work.CurrentCell;
						if (people > 0 && cell != null)
						{
							homes.Add(cell);
							housed.Add(people);
							roofs += people;
						}
						continue;
					}
					if (KingdomCatalogueRules.IsBindingSupport(carries[c].Kind))
					{
						continue;
					}
					lift += KingdomReachRules.Scaled(carries[c].Amount, effectiveness);
				}
				if (lift > 0)
				{
					lifters.Add(work);
					lifted.Add(lift);
				}
				// A household's trade is not a work with ground of its own, so it has no band to
				// be scoped by: what it makes goes to the settlement, and its whole ceiling is
				// KingdomYardRules.MaxShadePerWork. Carried straight across, exactly as Supports
				// folded it, so the scoped tally does not quietly lose the yard.
				trades += LiftOf(YardShadesOf(work), effectiveness);
			}
			int scoped = trades;
			for (int i = 0; i < lifters.Count; i++)
			{
				int reached = 0;
				for (int h = 0; h < homes.Count; h++)
				{
					if (KingdomReach.ReachesCell(System, Z, lifters[i], Z, homes[h].X, homes[h].Y))
					{
						reached += housed[h];
					}
				}
				scoped += KingdomReachRules.Landed(lifted[i], reached, roofs);
			}
			for (int i = 0; i < KingdomReachRules.LiftOrder.Length; i++)
			{
				int city = KingdomReach.CityShadeExcept(System, KingdomReachRules.LiftOrder[i], Z.ZoneID);
				if (city > 0)
				{
					scoped += city;
				}
			}
			tally.Lift = scoped;
			return tally;
		}

		// --- The city's own record, one zone at a time --------------------------------------

		/// <summary>
		/// Writes down what this zone was holding, on the pass that stood in it. Rewritten from
		/// the ground every time, including down to zero: a reservoir that was struck stops
		/// counting toward the city the pass the founder sees the empty plot, and never before.
		/// <para>
		/// The discipline is unchanged; where it is written is not. This used to be five
		/// <c>r_TAF_Supports_&lt;zoneID&gt;_*</c> game-state ints, which were the right answer for
		/// five ints that had to be readable without loading a zone and the wrong answer for a
		/// hundred typed rows (LIVING-CITY-ARCHITECTURE &sect;1.3). It is now one row of the
		/// settlement's own city book, and every number downstream is the same number.
		/// </para>
		/// </summary>
		/// <param name="System">The seated settlement, whose book holds the row.</param>
		/// <param name="Z">The zone the pass is in.</param>
		/// <param name="Supports">What was counted here, lifts ignored &mdash; only the binding
		/// half is a citywide pool.</param>
		/// <param name="StorageCapacity">Dedicated storage counted here.</param>
		/// <param name="TimeTicks">Now, which is what dates the sighting.</param>
		public static void RecordZone(KingdomSystem System, Zone Z, KingdomSurvey Survey, KingdomCatalogueRules.SupportTally Supports, int StorageCapacity, long TimeTicks)
		{
			// W7 repair: the RATES are no longer handed over. `Supports.Food` is the raw tally and
			// the model's food carry is KingdomGrowth.FoodMadePerDay, which subtracts the sown
			// fields and the mills because those two deliver physically; passing the raw figure
			// here was the one writer that disagreed with the other two. The survey goes across
			// instead and KingdomCity reads both rates off it through the same expressions every
			// other writer uses.
			Simulation.City.KingdomCity.RecordSupports(System, Z, Survey, Supports.Roof, StorageCapacity, TimeTicks);
		}

		/// <summary>The stamp a sighting tick is dated in: whole DAYS, not ticks, because a day is
		/// the granularity everything downstream reads (<c>KingdomRules.ElapsedDays</c>) and the
		/// staleness clause is written in days. Clamped: a game that somehow outruns it stops
		/// ageing rather than wrapping negative and reading as the future.</summary>
		public static int SeenStamp(long TimeTicks)
		{
			if (TimeTicks <= 0L)
			{
				return 0;
			}
			long days = TimeTicks / KingdomRules.TicksPerDay;
			return (days >= int.MaxValue) ? int.MaxValue : ((days < 1L) ? 1 : (int)days);
		}

		/// <summary>Every claimed zone of the seated city EXCEPT the one the pass is in, as each
		/// was last seen. The exclusion is the whole point: this zone has just been counted from
		/// the ground, and counting it twice would double its cisterns.</summary>
		public static List<KingdomSubsidenceRules.ZoneSighting> OtherZones(KingdomSystem System, Zone Z)
		{
			return Simulation.City.KingdomCity.OtherZones(System, Z);
		}

		/// <summary>
		/// The whole city's dedicated storage: this zone's, counted now, plus every other claimed
		/// zone's as last seen. The stage ladder is read against storage
		/// (<c>KingdomRules.StageFor</c>), so a city whose casks stand in the zone next door must
		/// be measured against all of them or it demotes itself the moment the founder walks in
		/// through the wrong side.
		/// </summary>
		/// <param name="System">The seated city.</param>
		/// <param name="Z">The zone the pass is in.</param>
		/// <param name="Here">Storage counted in this zone this pass
		/// (<c>KingdomSurvey.StorageCapacity</c>).</param>
		public static int CityStorageCapacity(KingdomSystem System, Zone Z, int Here)
		{
			return KingdomSubsidenceRules.CityStorage(Here, OtherZones(System, Z));
		}

		/// <summary>
		/// The clause that dates a city reading for the founder, or null when the reading is
		/// wholly this pass's own. The staleness doctrine said out loud: a two-zone city's level
		/// is partly a memory, and the founder is told how old the memory is.
		/// </summary>
		/// <param name="System">The seated city.</param>
		/// <param name="Z">The zone the pass is in.</param>
		/// <param name="TimeTicks">Now.</param>
		public static string SightingClause(KingdomSystem System, Zone Z, long TimeTicks)
		{
			List<KingdomSubsidenceRules.ZoneSighting> others = OtherZones(System, Z);
			long oldest = KingdomSubsidenceRules.OldestSighting(others);
			int days = (oldest > 0L) ? KingdomRules.ElapsedDays(TimeTicks - oldest) : 0;
			return KingdomSubsidenceRules.SightingClause(KingdomSubsidenceRules.SightedZones(others), days);
		}

		/// <summary>
		/// The whole reckoning. Records the level and what holds it, runs the slide, ruins what
		/// the fall took, and speaks once each way (STANDARDS 7b).
		/// </summary>
		/// <param name="System">The seated settlement.</param>
		/// <param name="Z">The zone the pass is in.</param>
		/// <param name="Survey">The pass's survey.</param>
		/// <param name="TimeTicks">Now.</param>
		public static void Reckon(KingdomSystem System, Zone Z, KingdomSurvey Survey, long TimeTicks)
		{
			if (System == null || !System.Founded || Z == null || Survey == null
				|| TimeTicks < 0L)
			{
				return;
			}
			KingdomElapsedOptionDecision option = ObserveOption(System, TimeTicks);
			if (!option.Valid) return;
			if (option.Action == KingdomElapsedOptionAction.AnchorDisabled
				|| option.Action == KingdomElapsedOptionAction.AnchorEnabled)
			{
				System.LastSubsidenceTick = TimeTicks;
				if (option.Action == KingdomElapsedOptionAction.AnchorDisabled)
				{
					// Turning the consequence off cancels its unpaid slide. Do not call Unsay:
					// disabling is not an earned arrest, reward, chronicle event, or prompt.
					System.SubsidenceAnnounced = false;
				}
				// Commit after the owned clock/cancellation. A cut retries the idempotent
				// transition instead of licensing old elapsed time.
				CommitOption(System, option.Record);
				return;
			}
			if (option.Action != KingdomElapsedOptionAction.Run) return;
			KingdomCatalogueRules.SupportTally here = ScopedSupports(System, Z, Survey);
			// Written down before it is used, so this zone's own sighting is today's on every
			// pass and the fold below never counts this ground out of a memory of it.
			RecordZone(System, Z, Survey, here, Survey.StorageCapacity, TimeTicks);
			List<KingdomSubsidenceRules.ZoneSighting> others = OtherZones(System, Z);
			KingdomCatalogueRules.SupportTally supports = KingdomSubsidenceRules.CityTally(here, others);
			int storage = CityStorageCapacity(System, Z, Survey.StorageCapacity);
			string binding = KingdomSubsidenceRules.BindingSupportFor(supports, System.Stage);
			int level = KingdomSubsidenceRules.SupportedLevel(supports, System.Stage, System.Shade);
			// Recorded on enabled passes before the slide asks whether a consequence is due. An
			// option transition returned above before this survey work and cannot reach the slide.
			System.SupportedLevel = level;
			System.SubsidenceBinding = binding;
			if (System.LastSubsidenceTick <= 0)
			{
				System.LastSubsidenceTick = TimeTicks;
				return;
			}
			int elapsedDays = KingdomRules.ElapsedDays(TimeTicks - System.LastSubsidenceTick);
			if (elapsedDays <= 0)
			{
				return;
			}
			// A settlement inside its band, or already arrived, is not subsiding: unsay whatever
			// was said, spend the days so they cannot be banked against a future overreach, and
			// leave. This is the arrest, and it is why removing the cause stops the slide anywhere
			// along it - the level is re-derived every pass and never remembered.
			if (!KingdomSubsidenceRules.IsSubsiding(System.Population, level) && !System.SubsidenceAnnounced)
			{
				System.LastSubsidenceTick = Checkpoint(System.LastSubsidenceTick, elapsedDays / KingdomSubsidenceRules.StepDays);
				return;
			}
			if (KingdomSubsidenceRules.HasArrived(System.Population, level))
			{
				Unsay(System, level);
				System.LastSubsidenceTick = Checkpoint(System.LastSubsidenceTick, elapsedDays / KingdomSubsidenceRules.StepDays);
				return;
			}
			KingdomSubsidenceRules.Trajectory trajectory = KingdomSubsidenceRules.Slide(
				System.Population, System.Stage, storage, supports, elapsedDays, System.SubsidenceAnnounced,
				System.Shade);
			Say(System, binding, level);
			if (trajectory.Departed <= 0)
			{
				// Announced and standing above the level, but not a whole step of world time has
				// passed yet. Nothing is charged and nothing is banked.
				return;
			}
			long anchor = System.LastSubsidenceTick;
			GrowthStage from = System.Stage;
			string cause = KingdomSubsidenceRules.DepartureCause(binding);
			int departed = 0;
			int named = 0;
			// Told in rungs, sampled in names: the first few and the last of a long slide are
			// chronicled by name and everybody between them rides the summary line below, so a
			// City falling to Camp spends a modest share of the two-hundred-entry register
			// instead of a quarter of it (KingdomSubsidenceRules.ChronicleEntriesFor).
			while (departed < trajectory.Departed)
			{
				bool tell = KingdomSubsidenceRules.TellsDeparture(departed, trajectory.Departed);
				if (!KingdomGrowth.Emigrate(System, Z, Survey, null, cause, tell))
				{
					break;
				}
				departed++;
				if (tell)
				{
					named++;
				}
			}
			string summary = KingdomSubsidenceRules.SlideDepartureSummary(KingdomPresentation.Rich(System.KingdomDisplayName), departed, named, cause);
			if (summary != null)
			{
				System.Ledger.Note("{{r|" + XRL.Language.Grammar.InitCap(summary) + ".}}");
				KingdomChronicle.Record(System, summary);
			}
			// Charged for exactly what was cashed. A settlement whose people are standing in
			// another claimed zone loses fewer than the trajectory called for, and keeps the rest
			// of the elapsed for the pass that can find them.
			int steps = trajectory.Steps * departed / trajectory.Departed;
			System.LastSubsidenceTick = Checkpoint(anchor, steps);
			if (departed <= 0)
			{
				return;
			}
			System.Stage = KingdomSubsidenceRules.SettledStage(from, System.Population, storage);
			// Re-recorded against the rung the slide left, not the one it started from: the water
			// bill per head fell with the stage, so the level the founder is now looking at is a
			// different (higher) number from the one the announcement quoted.
			System.SupportedLevel = KingdomSubsidenceRules.SupportedLevel(supports, System.Stage, System.Shade);
			System.SubsidenceBinding = KingdomSubsidenceRules.BindingSupportFor(supports, System.Stage);
			Chronicle(System, Survey, anchor, TimeTicks, from, trajectory);
			if (KingdomLog.Enabled)
			{
				KingdomLog.Log("subsidence: level=" + level + "->" + System.SupportedLevel + " binding=" + binding
					+ " days=" + elapsedDays + " wanted=" + trajectory.Departed + " left=" + departed
					+ " pop=" + System.Population + " stage=" + System.Stage
					+ " city=" + (SightingClause(System, Z, TimeTicks) ?? "this zone alone"));
			}
			if (KingdomSubsidenceRules.HasArrived(System.Population, System.SupportedLevel))
			{
				Unsay(System, System.SupportedLevel);
			}
		}

		/// <summary>Moves the reckoning's stamp forward by exactly the steps just charged, keeping
		/// the part-step remainder so it counts toward the next one. The same bargain
		/// <c>KingdomRules.AdvanceCheckpoint</c> keeps, at this clock's own coarser granularity.
		/// </summary>
		private static long Checkpoint(long Previous, int Steps)
		{
			if (Steps <= 0)
			{
				return Previous;
			}
			return Previous + (long)Steps * KingdomSubsidenceRules.StepDays * KingdomRules.TicksPerDay;
		}

		// ==================================================================================
		// 7b. Once when it begins, and unsaid the moment it stops.
		// ==================================================================================

		private static void Say(KingdomSystem System, string Binding, int Level)
		{
			if (System.SubsidenceAnnounced)
			{
				return;
			}
			System.SubsidenceAnnounced = true;
			string realm = KingdomPresentation.Rich(System.KingdomDisplayName);
			string line = KingdomSubsidenceRules.BeganNote(realm, Binding, Level, System.Population);
			MessageQueue.AddPlayerMessage("{{r|" + line + "}}");
			System.Ledger.Note("{{r|" + line + "}}");
			KingdomChronicle.Record(System, KingdomSubsidenceRules.BeganChronicle(realm, Binding, Level));
		}

		private static void Unsay(KingdomSystem System, int Level)
		{
			if (!System.SubsidenceAnnounced)
			{
				return;
			}
			System.SubsidenceAnnounced = false;
			string realm = KingdomPresentation.Rich(System.KingdomDisplayName);
			string line = KingdomSubsidenceRules.ArrestedNote(realm, Level, System.Population);
			MessageQueue.AddPlayerMessage("{{G|" + line + "}}");
			System.Ledger.Note("{{G|" + line + "}}");
			KingdomChronicle.Record(System, KingdomSubsidenceRules.ArrestedChronicle(realm, Level));
		}

		// ==================================================================================
		// The breakpoints: the rungs, dated, and the works the fall left the worse for it.
		// ==================================================================================

		private static void Chronicle(KingdomSystem System, KingdomSurvey Survey, long Anchor, long TimeTicks,
			GrowthStage From, KingdomSubsidenceRules.Trajectory Trajectory)
		{
			if (Trajectory.Breakpoints == null)
			{
				return;
			}
			string settlementId = KingdomChronicle.SettlementId(System);
			for (int i = 0; i < Trajectory.Breakpoints.Count; i++)
			{
				KingdomSubsidenceRules.Breakpoint breakpoint = Trajectory.Breakpoints[i];
				// Only the rungs the settlement actually reached. A slide cut short because its
				// people were standing somewhere else did not lose the rungs below where it
				// stopped, and must not claim to have.
				if (breakpoint.To < System.Stage)
				{
					continue;
				}
				long at = Anchor + (long)breakpoint.Day * KingdomRules.TicksPerDay;
				int daysAgo = KingdomRules.ElapsedDays(TimeTicks - at);
				KingdomChronicle.Record(System, KingdomSubsidenceRules.BreakpointChronicle(
					KingdomPresentation.Rich(System.KingdomDisplayName), breakpoint.From, breakpoint.To, daysAgo));
				Ruin(System, Survey, settlementId, (ulong)((at > 0L) ? at : 0L), at, breakpoint.From);
			}
		}

		/// <summary>
		/// What one lost rung does to the works standing in it. Damage and nothing else: the part
		/// is the mending system's own, the ceiling is its own, and a mending puts every point of
		/// it back. Only <c>KingdomBuilt</c> works are candidates, which is the protection law's
		/// own list of what a kingdom system may touch at all.
		/// <para>
		/// <b>The reach</b> (Addendum 10(c)). Every standing work is asked, every rung, and how
		/// far the rung reaches is the rung's own scale
		/// (<see cref="KingdomSubsidenceRules.RuinChanceFor"/>). There is no quota: the flat
		/// two-works-a-rung allowance this replaced meant a City falling all the way to Camp left
		/// eight works scuffed however many dozen were standing, and every other plot pristine.
		/// Because the loop no longer stops at a count, it also cannot stop before a home that
		/// crossed the condemnation line &mdash; every crossing reaches the people under it,
		/// which is a correctness property of having no early exit rather than a check somewhere.
		/// </para>
		/// <para>
		/// <b>The telling is coarsened, the damage is not</b>: a rung that leaves eleven works the
		/// worse for it writes one named line and one that counts the rest
		/// (<see cref="KingdomSubsidenceRules.TellsRuin"/>,
		/// <see cref="KingdomSubsidenceRules.RuinSummary"/>), so the register's share of a whole
		/// collapse did not move when the reach did.
		/// </para>
		/// </summary>
		/// <param name="Ordinal">The breakpoint's own due tick, used as a draw ordinal. It sits on
		/// a fixed lattice from the reckoning's anchor and is never re-anchored, so a reload asks
		/// each work the same question and gets the same answer.</param>
		/// <param name="AtTick">The same tick as a clock rather than an ordinal, for the roof
		/// brink a condemned home owes the people who were living in it.</param>
		/// <param name="From">The rung being lost, which is how far this one reaches.</param>
		private static void Ruin(KingdomSystem System, KingdomSurvey Survey, string SettlementId, ulong Ordinal,
			long AtTick, GrowthStage From)
		{
			int ruined = 0;
			int named = 0;
			int deepest = 0;
			for (int i = 0; i < Survey.Built.Count; i++)
			{
				GameObject work = Survey.Built[i];
				if (!GameObject.Validate(work) || !KingdomSubsidenceRules.RollRuin(SettlementId, work.ID, Ordinal, From))
				{
					continue;
				}
				int increment = KingdomSubsidenceRules.RolledRuinIncrement(SettlementId, work.ID, Ordinal);
				if (increment <= 0)
				{
					continue;
				}
				// Read before the damage lands, so the sentence names the building that stood
				// here rather than the ruin it is about to become: "the granary fell into
				// disrepair", not "the ruined granary fell into disrepair". The adjective the
				// work wears from here on is r_KingdomWear's own.
				string name = KingdomDesign.ReferenceFor(work, work.ShortDisplayName);
				r_KingdomWear wear = work.RequirePart<r_KingdomWear>();
				int before = wear.Wear;
				wear.Wear = KingdomMaterialRules.AddWear(wear.Wear, increment);
				if (wear.Wear == before)
				{
					// Already at the ceiling. A real thing happened and there is nothing new to
					// report, and 7b's "once" would be broken by saying it twice.
					continue;
				}
				ruined++;
				if (wear.Wear > deepest)
				{
					deepest = wear.Wear;
				}
				// A home the fall took past KingdomLodgingRules.CondemnedWearPercent stopped
				// being a roof on THIS day, not on the day somebody finally walked in and looked
				// at it. So the people who were living under it reach their brink here, dated
				// here, and the lodging pass that finds them later announces the honest elapsed.
				// Nothing is announced and no window is spent from inside an absence; a home that
				// was already condemned, one below the line, and one nobody sleeps in all record
				// nothing (KingdomBrink.Record is idempotent, and this only fires on the
				// crossing). It fires for EVERY home that crosses, not for the first couple: the
				// loop above has no count to stop at.
				if (KingdomLodgingRules.IsCondemned(wear.Wear) && !KingdomLodgingRules.IsCondemned(before))
				{
					int stranded = KingdomLodging.RecordCondemnedRoofBrink(work.CurrentZone, work, AtTick);
					if (stranded > 0 && KingdomLog.Enabled)
					{
						KingdomLog.Log("subsidence: condemned " + work.Blueprint + " wear=" + wear.Wear + " stranded=" + stranded);
					}
				}
				if (KingdomSubsidenceRules.TellsRuin(ruined - 1))
				{
					named++;
					string line = KingdomSubsidenceRules.RuinedWorkLine(name, KingdomPresentation.Rich(System.KingdomDisplayName));
					System.Ledger.Note("{{r|" + XRL.Language.Grammar.InitCap(line) + ".}}");
					KingdomChronicle.Record(System, line);
				}
				if (KingdomLog.Enabled)
				{
					KingdomLog.Log("subsidence: ruined " + work.Blueprint + " wear=" + wear.Wear + " rung=" + From);
				}
			}
			string summary = KingdomSubsidenceRules.RuinSummary(KingdomPresentation.Rich(System.KingdomDisplayName), ruined, named, deepest);
			if (summary != null)
			{
				System.Ledger.Note("{{r|" + XRL.Language.Grammar.InitCap(summary) + ".}}");
				KingdomChronicle.Record(System, summary);
			}
			if (KingdomLog.Enabled)
			{
				KingdomLog.Log("subsidence: rung=" + From + " reach=" + KingdomSubsidenceRules.RuinChanceFor(From)
					+ "% ruined=" + ruined + " deepest=" + deepest);
			}
		}
	}
}
