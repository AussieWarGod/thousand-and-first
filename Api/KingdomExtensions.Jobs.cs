using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Api
{
	public static partial class KingdomExtensions
	{
		// ==================================================================================
		// The jobs. Every one crosses the seam, so each inherits budget, timeout and error isolation
		// from the same contract our own computations do (§2.5).
		// ==================================================================================

		private sealed class AskJob : IKingdomComputation<KingdomCityReading, KingdomAsk[]>
		{
			private readonly IKingdomAskSource source;

			private readonly KingdomExtensionDraws draws;

			private readonly string label;

			internal AskJob(IKingdomAskSource source, KingdomExtensionDraws draws,
				string modName, bool own)
			{
				this.source = source;
				this.draws = draws;
				// The receipt distinguishes the city's own asks from an extension's, because
				// §6.5's whole point is that a regression has an owner.
				label = (own ? "asks:" : "ext:asks:") + KingdomApiRules.Slug(modName);
			}

			public string Label
			{
				get { return label; }
			}

			public KingdomBudgetLane Lane
			{
				get { return KingdomBudgetLane.Reckon; }
			}

			public bool TryRun(KingdomCityReading input, out KingdomAsk[] output, out KingdomComputeCounters counters, out KingdomCityFault fault)
			{
				output = source.Ask(input, draws);
				counters = new KingdomComputeCounters(0, output == null ? 0L : output.Length,
					draws.ReportedDraws, 0, 0L);
				fault = KingdomCityFault.None;
				return true;
			}
		}

		private sealed class HappeningJob : IKingdomComputation<KingdomCityReading, KingdomNotice[]>
		{
			private readonly IKingdomHappeningSource source;

			private readonly long sinceTick;

			private readonly KingdomExtensionDraws draws;

			private readonly string label;

			internal HappeningJob(IKingdomHappeningSource source, long sinceTick,
				KingdomExtensionDraws draws, string modName)
			{
				this.source = source;
				this.sinceTick = sinceTick;
				this.draws = draws;
				label = "ext:happenings:" + KingdomApiRules.Slug(modName);
			}

			public string Label
			{
				get { return label; }
			}

			public KingdomBudgetLane Lane
			{
				get { return KingdomBudgetLane.Reckon; }
			}

			public bool TryRun(KingdomCityReading input, out KingdomNotice[] output, out KingdomComputeCounters counters, out KingdomCityFault fault)
			{
				output = source.Happen(input, sinceTick, draws);
				counters = new KingdomComputeCounters(0, output == null ? 0L : output.Length,
					draws.ReportedDraws, 0, 0L);
				fault = KingdomCityFault.None;
				return true;
			}
		}

		private sealed class IdentityKeysJob : IKingdomComputation<KingdomIdentityReading, string[]>
		{
			private readonly IKingdomIdentitySource source;

			private readonly string label;

			internal IdentityKeysJob(IKingdomIdentitySource source, string modName)
			{
				this.source = source;
				label = "ext:identity-keys:" + KingdomApiRules.Slug(modName);
			}

			public string Label
			{
				get { return label; }
			}

			public KingdomBudgetLane Lane
			{
				get { return KingdomBudgetLane.Reckon; }
			}

			public bool TryRun(KingdomIdentityReading input, out string[] output,
				out KingdomComputeCounters counters, out KingdomCityFault fault)
			{
				output = source.Keys(input);
				counters = KingdomComputeCounters.None;
				fault = KingdomCityFault.None;
				return true;
			}
		}

		private sealed class IdentityAffinityJob
			: IKingdomComputation<KingdomIdentityWorkReading, int>
		{
			private readonly IKingdomIdentitySource source;

			private readonly string label;

			internal IdentityAffinityJob(IKingdomIdentitySource source, string modName)
			{
				this.source = source;
				label = "ext:identity-affinity:" + KingdomApiRules.Slug(modName);
			}

			public string Label
			{
				get { return label; }
			}

			public KingdomBudgetLane Lane
			{
				get { return KingdomBudgetLane.Reckon; }
			}

			public bool TryRun(KingdomIdentityWorkReading input, out int output,
				out KingdomComputeCounters counters, out KingdomCityFault fault)
			{
				output = source.Affinity(input.Identity, input.WorkKind);
				counters = KingdomComputeCounters.None;
				fault = KingdomCityFault.None;
				return true;
			}
		}
	}
}
