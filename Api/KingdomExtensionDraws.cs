using ThousandAndFirst.Simulation.City;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Api
{
	/// <summary>
	/// Engine-free implementation of the public deterministic draw handle. Keeping this outside the
	/// discovery registry makes its actual cap, domain separation, and reload replay directly testable.
	/// </summary>
	internal sealed class KingdomExtensionDraws : IKingdomDraws
	{
		private readonly KernelSeed128 seed;
		private readonly string settlementId;
		private readonly string modName;
		private int attempts;

		/// <summary>Rules version pinned into every extension draw key.</summary>
		private const int ExtensionRulesVersion = 1;

		/// <summary>Stream carries owner/lane separation; this code separates the extension domain.</summary>
		private const uint ExtensionKind = 1u;

		internal KingdomExtensionDraws(KernelSeed128 seed, string settlementId, string modName)
		{
			this.seed = seed;
			this.settlementId = settlementId;
			this.modName = modName;
		}

		/// <summary>Draw count reported to the executor. Attempt 33 becomes an explicit over-budget
		/// receipt even though it never reaches the kernel.</summary>
		internal int ReportedDraws
		{
			get
			{
				return attempts > KingdomApiRules.MaxDrawsPerSourceCall
					? KingdomBudgetRules.MaxDrawsPerCityPass + 1 : attempts;
			}
		}

		public bool TryBetween(string Lane, uint Ordinal, int Low, int High, out int Value)
		{
			Value = Low;
			if (attempts < KingdomApiRules.MaxDrawsPerSourceCall + 1) attempts++;
			if (attempts > KingdomApiRules.MaxDrawsPerSourceCall) return false;
			if (High < Low) return false;
			string stream;
			if (!KingdomApiRules.TryStream(modName, Lane, out stream)) return false;
			SemanticEventKey key;
			KernelFaultCode fault;
			if (!SemanticEventKey.TryCreate(ExtensionRulesVersion, settlementId, stream,
				ExtensionKind, Ordinal, out key, out fault)) return false;
			ulong span = (ulong)((long)High - (long)Low + 1L);
			ulong drawn;
			if (!CounterRandom.TryDrawBelow(seed, key, 0u, span, out drawn, out fault)) return false;
			Value = (int)((long)Low + (long)drawn);
			return true;
		}
	}
}
