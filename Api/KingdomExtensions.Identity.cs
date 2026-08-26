using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Api
{
	public static partial class KingdomExtensions
	{
		/// <summary>
		/// Extra live roster keys every admitted identity source gives one frozen identity. Each
		/// source crosses the executor independently. Faulted sources contribute nothing; valid keys
		/// are bounded, attributed to their owner, folded, and de-duplicated.
		/// </summary>
		/// <param name="Reading">The frozen identity. No engine object crosses the seam.</param>
		/// <param name="Stalled">Optional distinct mod names whose source faulted or overran.</param>
		/// <returns>Fresh canonical keys in deterministic registry/source order.</returns>
		internal static List<string> IdentityKeys(KingdomIdentityReading Reading, List<string> Stalled = null)
		{
			List<string> keys = new List<string>();
			foreach (Binding binding in Registry())
			{
				IKingdomIdentitySource source = binding.Extension as IKingdomIdentitySource;
				if (source == null)
				{
					continue;
				}
				IdentityKeysJob job = new IdentityKeysJob(source, binding.ModName);
				KingdomComputeResult<string[]> result = KingdomCity.Seam.Submit(Reading, job);
				if (!result.Published)
				{
					Fault(binding.ModName, "identity keys", result.Status.ToString());
					Stall(Stalled, binding.ModName);
					continue;
				}
				KeepIdentityKeys(result.Value, binding.ModName, keys);
			}
			return keys;
		}

		/// <summary>
		/// Composed extension affinity for one frozen identity and existing work kind. Each source
		/// crosses the executor independently; a fault is the neutral 100. Bounded source deltas are
		/// summed before one final clamp, so mixed opinions are independent of registry order.
		/// </summary>
		internal static int IdentityAffinity(KingdomIdentityReading Reading, string WorkKind,
			List<string> Stalled = null)
		{
			long affinityDelta = 0L;
			KingdomIdentityWorkReading request = new KingdomIdentityWorkReading(Reading, WorkKind);
			foreach (Binding binding in Registry())
			{
				IKingdomIdentitySource source = binding.Extension as IKingdomIdentitySource;
				if (source == null)
				{
					continue;
				}
				IdentityAffinityJob job = new IdentityAffinityJob(source, binding.ModName);
				KingdomComputeResult<int> result = KingdomCity.Seam.Submit(request, job);
				if (!result.Published)
				{
					Fault(binding.ModName, "identity affinity", result.Status.ToString());
					Stall(Stalled, binding.ModName);
					continue;
				}
				affinityDelta += KingdomApiRules.IdentityAffinity(result.Value) - 100L;
			}
			return KingdomApiRules.IdentityAffinityFromDelta(affinityDelta);
		}

		private static void Stall(List<string> stalled, string owner)
		{
			if (stalled != null && !stalled.Contains(owner))
			{
				stalled.Add(owner);
			}
		}

		private static void KeepIdentityKeys(string[] source, string owner, List<string> into)
		{
			int kept = 0;
			for (int i = 0; source != null && i < source.Length
				&& i < KingdomApiRules.MaxIdentityKeyCandidatesPerSource
				&& kept < KingdomApiRules.MaxIdentityKeysPerSource; i++)
			{
				string key = KingdomApiRules.IdentityKey(owner, source[i]);
				if (key == null || into.Contains(key))
				{
					continue;
				}
				into.Add(key);
				kept++;
			}
		}

	}
}
