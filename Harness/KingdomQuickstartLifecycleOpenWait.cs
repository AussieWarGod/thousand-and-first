namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// The bounded-wait DECISION for lifecycle-open, engine-free so it is a value test rather than
	/// a native one.
	///
	/// <para>WHY. `lifecycle-open`'s CanPay reads a production predicate -- a stockpile the founder
	/// has dedicated (Growth/KingdomMaterials.03.StockClassification.cs:19-21,
	/// KingdomQuickstartLifecycleSteps.TryStockpile) -- and the ordinary founding transaction
	/// `realize` drives (Core/KingdomFoundingTransaction.11Run.cs) claims ground and faction only;
	/// it places no camp kit and dedicates nothing. The only production writer of that predicate,
	/// Growth/KingdomMaterials.05.StockpileAndPaymentGates.cs:166 (`DedicateStockpile`), is reached
	/// from exactly one caller, Core/KingdomCharterPart.Vessels.cs:182 -- an interactive founder
	/// popup a sealed script never drives. The Quickstart boot path's own writer
	/// (World/KingdomQuickstartBootstrap.Materials.cs:34) belongs to the OTHER road
	/// (Harness/KingdomQuickstartLifecycleProvider.cs's own docstring explains why this chain does
	/// not use it) and never runs here. An instant refusal the moment `realize` finishes therefore
	/// proves nothing about whether the predicate could ever become true; this class makes the
	/// harness ask honestly, in bounded real turns, before it gives up.</para>
	///
	/// <para>NO MINTING, NO DIRECT WRITE. This class never dedicates anything and never invents a
	/// stockpile; it only decides whether to keep waiting, journaling the exact reading each time
	/// (see KingdomQuickstartLifecycleSteps.Open), or to refuse once the budget is spent.</para>
	/// </summary>
	internal static class KingdomQuickstartLifecycleOpenWait
	{
		/// <summary>Ordinary engine turns one wait chunk spends, via the same bounded, yielding
		/// <c>advance</c> mechanism every other scripted wait in this harness uses
		/// (KingdomScenarioAdvance) -- never a synchronous spin.</summary>
		internal const int ChunkTurns = 100;

		/// <summary>How many chunks lifecycle-open may ask for before it gives up. Finite: at most
		/// <see cref="ChunkTurns"/> * <see cref="MaxChunks"/> ordinary turns pass before an honest
		/// refusal, never an unbounded wait.</summary>
		internal const int MaxChunks = 5;

		/// <summary>What lifecycle-open should do this attempt, given what it just observed.</summary>
		internal enum Decision
		{
			/// <summary>The predicate already holds; proceed with the ordinary startup census now.</summary>
			Proceed,

			/// <summary>The predicate does not hold yet, and the budget is not spent; wait one more
			/// chunk and journal the reading, but do not refuse.</summary>
			WaitMore,

			/// <summary>The predicate never became true within the budget; refuse honestly, naming
			/// the last reading.</summary>
			Exhausted,
		}

		/// <summary>
		/// The decision for this attempt. Pure: no engine type crosses this boundary, so it is
		/// tested by value alone (DevTests/KingdomQuickstartLifecycleOpenWaitTests.cs).
		/// </summary>
		/// <param name="StockpileDedicated">Whatever <c>TryStockpile</c> read THIS attempt --
		/// never cached, never assumed from an earlier attempt.</param>
		/// <param name="ChunksAlreadyWaited">How many chunks this game has already spent waiting,
		/// read back from the durable counter lifecycle-open keeps
		/// (KingdomQuickstartLifecycleSteps.WaitChunksKey). Zero on the very first attempt.</param>
		internal static Decision Evaluate(bool StockpileDedicated, int ChunksAlreadyWaited)
		{
			if (StockpileDedicated)
			{
				return Decision.Proceed;
			}
			return ChunksAlreadyWaited < MaxChunks ? Decision.WaitMore : Decision.Exhausted;
		}

		/// <summary>The total ordinary-turn budget the wait is bounded to, for the refusal message
		/// and for the persona's own advance line count.</summary>
		internal static int TotalBudgetTurns
		{
			get { return ChunkTurns * MaxChunks; }
		}
	}
}
