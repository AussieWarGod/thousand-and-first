#if TAF_TESTS
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst.Harness;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// VALUE tests for the bounded-wait decision lifecycle-open consults: pure, engine-free, and
	/// therefore able to prove the finite-budget contract by value rather than only by reading
	/// source text. See DevTests/KingdomQuickstartLifecycleContractTests.cs for the source-only
	/// pins over how KingdomQuickstartLifecycleSteps.Open actually calls this class.
	/// </summary>
	[TestFixture]
	public sealed class KingdomQuickstartLifecycleOpenWaitTests
	{
		[Test]
		public void AnAlreadyDedicatedStockpileProceedsRegardlessOfChunksWaited()
		{
			for (int chunks = 0; chunks <= KingdomQuickstartLifecycleOpenWait.MaxChunks + 3; chunks++)
				ClassicAssert.AreEqual(KingdomQuickstartLifecycleOpenWait.Decision.Proceed,
					KingdomQuickstartLifecycleOpenWait.Evaluate(true, chunks),
					"chunks=" + chunks);
		}

		[Test]
		public void AnUndedicatedStockpileWaitsMoreWhileBudgetRemains()
		{
			for (int chunks = 0; chunks < KingdomQuickstartLifecycleOpenWait.MaxChunks; chunks++)
				ClassicAssert.AreEqual(KingdomQuickstartLifecycleOpenWait.Decision.WaitMore,
					KingdomQuickstartLifecycleOpenWait.Evaluate(false, chunks),
					"chunks=" + chunks);
		}

		[Test]
		public void TheBudgetIsFiniteAndExhaustsExactlyAtMaxChunks()
		{
			ClassicAssert.AreEqual(KingdomQuickstartLifecycleOpenWait.Decision.Exhausted,
				KingdomQuickstartLifecycleOpenWait.Evaluate(false, KingdomQuickstartLifecycleOpenWait.MaxChunks));
			// Mutation evidence: one chunk short of the budget must still be WaitMore, never
			// Exhausted -- an off-by-one here would either wait forever or give up one attempt
			// early.
			ClassicAssert.AreEqual(KingdomQuickstartLifecycleOpenWait.Decision.WaitMore,
				KingdomQuickstartLifecycleOpenWait.Evaluate(false, KingdomQuickstartLifecycleOpenWait.MaxChunks - 1));
		}

		[Test]
		public void ExhaustionNeverRegressesOnceThePredicateStaysFalse()
		{
			// Chunks already waited never resets on its own; a chunk count past the budget (a
			// stale durable counter from a longer prior wait) must still refuse, not wrap around
			// into WaitMore.
			ClassicAssert.AreEqual(KingdomQuickstartLifecycleOpenWait.Decision.Exhausted,
				KingdomQuickstartLifecycleOpenWait.Evaluate(false, KingdomQuickstartLifecycleOpenWait.MaxChunks + 1));
		}

		[Test]
		public void TheTotalBudgetIsChunksTimesChunkTurns()
		{
			ClassicAssert.AreEqual(
				KingdomQuickstartLifecycleOpenWait.ChunkTurns * KingdomQuickstartLifecycleOpenWait.MaxChunks,
				KingdomQuickstartLifecycleOpenWait.TotalBudgetTurns);
		}
	}
}
#endif
