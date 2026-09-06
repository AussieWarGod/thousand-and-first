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
			TryReckon(System, Z, Survey, TimeTicks, out _);
		}

		internal static bool TryReckon(KingdomSystem system, Zone zone, KingdomSurvey survey, long now,
			out string refusal)
		{
			refusal = "Subsidence waits for an exact attended settlement survey.";
			try
			{
				if (system == null || !system.Founded || zone == null || survey == null
					|| The.Game == null || now != The.Game.TimeTicks || now < 0
					|| KingdomSurvey.ActiveFor(zone) != survey || survey.Ground != zone) return false;
				if (!KingdomResidentDeathRuntime.TryRecoverPending(system, out refusal)) return false;
				if (!KingdomSubsidenceStepRuntime.TryResumeAnnouncement(system, now, out refusal)) return false;
				bool pending = KingdomSubsidenceStepRuntime.HasPending(system);
				if (!pending)
				{
					if (!KingdomSubsidenceStepRuntime.TryOption(system, Enabled, now,
						out KingdomElapsedOptionAction action, out refusal)) return false;
					if (action != KingdomElapsedOptionAction.Run) { refusal = null; return true; }
				}
				if (!KingdomSubsidenceStepRuntime.TryPassGuard(system, zone, survey,
					out System.Func<bool> exact, out System.Func<bool> sameSeat, out refusal)) return false;
				KingdomCatalogueRules.SupportTally ordinary = OrdinarySupports(survey);
				if (!exact()) return false;
				RecordZone(system, zone, survey, ordinary, survey.StorageCapacity, now);
				if (!exact()) return false;
				int storage = CityStorageCapacity(system, zone, survey.StorageCapacity);
				System.Func<KingdomCatalogueRules.SupportTally> readSupports = () =>
				{
					KingdomCatalogueRules.SupportTally here = ScopedSupports(system, zone, survey);
					KingdomCatalogueRules.SupportTally tally = KingdomSubsidenceRules.CityTally(here, OtherZones(system, zone));
					KingdomHostedArcology.AddBindingProjection(system, zone, ref tally);
					return tally;
				};
				if (!pending)
				{
					KingdomCatalogueRules.SupportTally supports = readSupports();
					if (!exact()) return false;
					int level = KingdomSubsidenceRules.SupportedLevel(supports, system.Stage, system.Shade);
					system.SupportedLevel = level;
					system.SubsidenceBinding = KingdomSubsidenceRules.BindingSupportFor(supports, system.Stage);
					int elapsed = KingdomRules.ElapsedDays(now - system.LastSubsidenceTick);
					if (KingdomSubsidenceRules.HasArrived(system.Population, level)
						|| !system.SubsidenceAnnounced && !KingdomSubsidenceRules.IsSubsiding(system.Population, level))
					{
						if (!KingdomSubsidenceStepRuntime.TryTell(system, zone, survey, now, false,
							system.SubsidenceBinding, level, out refusal)) return false;
						if (!sameSeat() || !KingdomSubsidenceStepRuntime.TryPassGuard(system, zone, survey, out exact, out _))
						{ refusal = "The settlement changed while telling its subsidence arrest."; return false; }
						system.LastSubsidenceTick = Checkpoint(system.LastSubsidenceTick, elapsed / KingdomSubsidenceRules.StepDays);
						refusal = null; return true;
					}
					if (elapsed <= 0) { refusal = null; return true; }
					if (!KingdomSubsidenceStepRuntime.TryTell(system, zone, survey, now, true,
						system.SubsidenceBinding, level, out refusal)) return false;
					if (!sameSeat() || !KingdomSubsidenceStepRuntime.TryPassGuard(system, zone, survey, out exact, out _))
					{ refusal = "The settlement changed while telling its subsidence beginning."; return false; }
				}
				if (!KingdomSubsidenceStepRuntime.TryDrive(system, zone, survey, now, readSupports, storage, out refusal))
					return false;
				if (!sameSeat() || !KingdomSubsidenceStepRuntime.TryPassGuard(system, zone, survey, out exact, out _)) return false;
				KingdomCatalogueRules.SupportTally final = readSupports();
				if (!exact()) return false;
				system.SupportedLevel = KingdomSubsidenceRules.SupportedLevel(final, system.Stage, system.Shade);
				system.SubsidenceBinding = KingdomSubsidenceRules.BindingSupportFor(final, system.Stage);
				if (KingdomSubsidenceRules.HasArrived(system.Population, system.SupportedLevel))
				{
					if (!KingdomSubsidenceStepRuntime.TryTell(system, zone, survey, now, false,
						system.SubsidenceBinding, system.SupportedLevel, out refusal)) return false;
					if (!sameSeat() || !KingdomSubsidenceStepRuntime.TryPassGuard(system, zone, survey, out exact, out _))
					{ refusal = "The settlement changed while telling its subsidence arrest."; return false; }
				}
				if (!exact()) return false;
				refusal = null; return true;
			}
			catch (System.Exception)
			{ refusal = "Subsidence stopped while reading its settlement; saved evidence is retained."; return false; }
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

	}
}
