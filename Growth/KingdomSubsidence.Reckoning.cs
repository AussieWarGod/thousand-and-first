using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomSubsidence
	{
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
			KingdomCatalogueRules.SupportTally ordinary = OrdinarySupports(Survey);
			// Written down before it is used, so this zone's own sighting is today's on every
			// pass and the fold below never counts this ground out of a memory of it.
			RecordZone(System, Z, Survey, ordinary, Survey.StorageCapacity, TimeTicks);
			List<KingdomSubsidenceRules.ZoneSighting> others = OtherZones(System, Z);
			KingdomCatalogueRules.SupportTally supports = KingdomSubsidenceRules.CityTally(here, others);
			KingdomHostedArcology.AddBindingProjection(System, Z, ref supports);
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

	}
}
