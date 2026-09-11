using System.Collections.Generic;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// The journal row names of ONE construction lifecycle on the Quickstart path, in the order a
	/// native run must land them: startup, then the paid commission with its physical debit, then
	/// engine turns until the job completes into a functional building, then a real save, a cold
	/// load, and a further action on the loaded game.
	///
	/// <para>This shard declares the contract and nothing else: it holds no behaviour, so it can
	/// never be mistaken for evidence. Two of the six links have no rows today, and saying so here
	/// is the point -- Tools/check-quickstart-lifecycle.py reads the same six groups and returns
	/// BLOCKER, never PASS, for a link that landed no rows at all.</para>
	///
	/// <para>WHAT IS ALREADY DRIVEN. Startup (KingdomQuickstartBootTest.cs), the paid commission
	/// with exact timber and water debit and a new paid job (KingdomQuickstartBuildTest.cs:118-155),
	/// the real save (KingdomQuickstartSaveTest.cs) and the cold load with byte-identical restored
	/// identities (KingdomQuickstartLoadTest.cs).</para>
	///
	/// <para>THE OTHER ROAD. A Quickstart profile runs with NO scenario auto-runner --
	/// KingdomQuickstartBootTest.cs:141 asserts its absence and DevTests pin it -- and
	/// Tools/scenario_profile.py refuses a script that mixes a Quickstart verb with the
	/// auto-runner verbs, so nothing in a Quickstart run can spend engine turns. The turn-driven
	/// half of the chain therefore runs on an ordinary founded settlement instead, through
	/// KingdomQuickstartLifecycleProvider's four verbs and the existing bounded <c>advance</c>:
	/// <see cref="Lifecycle" /> names those rows. No Quickstart invariant is relaxed.</para>
	///
	/// <para>WHAT IS STILL OWED. <see cref="Next" /> has no producer on either road: the cold-load
	/// session needs a second profile imported from the lifecycle save, and the further action
	/// after that load needs a verb inside it. Until both exist the chain's verdict is BLOCKER by
	/// design, never a pass.</para>
	/// </summary>
	internal static class KingdomQuickstartLifecycleRows
	{
		internal static readonly IList<string> Boot = new[]
		{
			"QUICKSTART-BOOT-BEGIN", "QUICKSTART-BOOT-OBSERVED", "QUICKSTART-BOOT-COMPLETE"
		};

		internal static readonly IList<string> Build = new[]
		{
			"QUICKSTART-BUILD-BEGIN", "QUICKSTART-BUILD-QUOTE", "QUICKSTART-BUILD-CANPAY",
			"QUICKSTART-BUILD-COMMISSION", "QUICKSTART-BUILD-COMPLETE"
		};

		/// <summary>Owed on the Quickstart road: bounded engine turns carrying the paid job to a
		/// functional building. Driven on the founded road by <see cref="Lifecycle" />.</summary>
		internal static readonly IList<string> Grow = new[]
		{
			"QUICKSTART-GROW-BEGIN", "QUICKSTART-GROW-TURNS", "QUICKSTART-GROW-BUILT"
		};

		/// <summary>The founded road's rows: one per scenario verb the runner executed, in the
		/// order the persona seals them.</summary>
		internal static readonly IList<string> Lifecycle = new[]
		{
			// Literals on purpose: this shard is engine-free and is compiled into the portable
			// test projects, where the engine-coupled provider that registers these verbs is not
			// present. DevTests pins the two spellings against each other instead.
			"realize", "lifecycle-open", "lifecycle-build", "lifecycle-grown", "lifecycle-save"
		};

		internal static readonly IList<string> Save = new[]
		{
			"QUICKSTART-SAVE-BEGIN", "QUICKSTART-SAVE-COMPLETE"
		};

		internal static readonly IList<string> Load = new[]
		{
			"QUICKSTART-LOAD-PREACTIVATION", "QUICKSTART-LOAD-COMPLETE"
		};

		/// <summary>Owed: a second quote and commission attempt on the cold-loaded game.</summary>
		internal static readonly IList<string> Next = new[]
		{
			"QUICKSTART-NEXT-BEGIN", "QUICKSTART-NEXT-QUOTE", "QUICKSTART-NEXT-COMPLETE"
		};
	}
}
