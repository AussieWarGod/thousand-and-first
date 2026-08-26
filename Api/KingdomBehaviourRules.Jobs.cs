using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace ThousandAndFirst.Api
{
	internal static partial class KingdomBehaviourRules
	{
		internal static bool TryCompleteJobs(KingdomBehaviourState state, long nowTick,
			out KingdomBehaviourState next, out int completed, out int failed)
		{
			state = state ?? KingdomBehaviourState.Empty;
			next = state; completed = 0; failed = 0;
			if (nowTick < 0L) return false;
			KingdomResourceReading[] resources = state.Resources();
			KingdomBehaviourJobRow[] jobs = state.Jobs();
			bool changed = false;
			for (int i = 0; i < jobs.Length; i++)
			{
				KingdomBehaviourJobRow job = jobs[i];
				if (job.Status != KingdomExtensionJobStatus.Open || job.DueTick > nowTick) continue;
				KingdomResourceChange[] changes = JobChanges(job);
				KingdomResourceReading[] posted;
				if (TryApplyOwnedChanges(resources, OwnerOf(job.Key), changes, out posted))
				{
					resources = posted;
					jobs[i] = job.WithStatus(KingdomExtensionJobStatus.Completed);
					completed++;
				}
				else
				{
					// Completion is all-or-nothing. A failed carrier gives its reserved cargo back if
					// capacity still permits; inability to restore is represented by the unchanged
					// bounded stock, never arithmetic overflow.
					int cargo = ResourceIndex(resources, job.CargoResourceKey);
					if (cargo >= 0 && resources[cargo].Level <= resources[cargo].Capacity - job.CargoAmount)
						resources[cargo] = WithLevel(resources[cargo], resources[cargo].Level + job.CargoAmount);
					jobs[i] = job.WithStatus(KingdomExtensionJobStatus.Failed);
					failed++;
				}
				changed = true;
			}
			KingdomBehaviourJobRow[] retained = TrimTerminalJobs(jobs);
			if (retained.Length != jobs.Length)
			{
				jobs = retained;
				changed = true;
			}
			if (changed) next = new KingdomBehaviourState(resources, jobs, state.Networks(), state.Works());
			return true;
		}

		internal static bool TryApplyJobs(KingdomBehaviourState state, string owner,
			KingdomJobPlan[] candidates, KingdomCarrierKindRow[] carriers, KingdomCityReading city,
			long nowTick, out KingdomBehaviourState next, out int kept)
		{
			state = state ?? KingdomBehaviourState.Empty;
			next = state; kept = 0;
			if (city == null || nowTick < 0L) return false;
			KingdomResourceReading[] resources = state.Resources();
			KingdomBehaviourJobRow[] originalJobs = state.Jobs();
			KingdomBehaviourJobRow[] jobs = TrimTerminalJobs(originalJobs);
			int owned = CountOpenOwner(jobs, owner);
			int open = CountOpen(jobs);
			List<string> seen = new List<string>();
			for (int i = 0; candidates != null && i < candidates.Length
				&& i < KingdomApiRules.MaxBehaviourCandidatesPerCall
				&& kept < KingdomApiRules.MaxJobsPerOwner; i++)
			{
				KingdomJobPlan plan = candidates[i];
				if (plan == null || plan.StartTick != nowTick || plan.CargoAmount <= 0
					|| plan.LegCount <= 0 || plan.LegCount > KingdomApiRules.MaxLegsPerJob
					|| plan.CompletionChangeCount > KingdomApiRules.MaxChangesPerResult) continue;
				string key = KingdomApiRules.ExtensionKey(owner, plan.Key);
				string carrierKey = KingdomApiRules.ExtensionKey(owner, plan.CarrierKey);
				string cargoKey = KingdomApiRules.ExtensionKey(owner, plan.CargoResourceKey);
				if (key == null || carrierKey == null || cargoKey == null || seen.Contains(key)
					|| JobIndex(jobs, key) >= 0) continue;
				seen.Add(key);
				int carrierAt = CarrierIndex(carriers, carrierKey);
				int resourceAt = ResourceIndex(resources, cargoKey);
				if (carrierAt < 0 || resourceAt < 0 || plan.CargoAmount > carriers[carrierAt].Capacity
					|| resources[resourceAt].Level < plan.CargoAmount
					|| owned >= KingdomApiRules.MaxJobsPerOwner
					|| open >= KingdomApiRules.MaxJobsPerCity
					|| jobs.Length >= KingdomApiRules.MaxStoredJobsPerCity) continue;
				KingdomExtensionLeg[] legs;
				long due;
				if (!TryLegs(plan, city, carriers[carrierAt].WalkTicksPerCell, nowTick,
					out legs, out due)) continue;
				KingdomResourceChange[] completion;
				if (!TryNormalizeChanges(owner, plan, out completion)) continue;
				resources[resourceAt] = WithLevel(resources[resourceAt],
					resources[resourceAt].Level - plan.CargoAmount);
				jobs = Append(jobs, new KingdomBehaviourJobRow(key, carrierKey,
					carriers[carrierAt].Blueprint, carriers[carrierAt].WalkTicksPerCell, cargoKey,
					plan.CargoAmount, nowTick, due, KingdomExtensionJobStatus.Open, legs, completion));
				owned++; open++; kept++;
			}
			if (kept > 0 || jobs.Length != originalJobs.Length)
				next = new KingdomBehaviourState(resources, jobs, state.Networks(), state.Works());
			return true;
		}

	}
}
