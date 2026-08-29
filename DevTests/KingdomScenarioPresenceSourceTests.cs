#if TAF_TESTS
using System;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Source contracts for the durable-key presence law and the sealed-profile cross-checks.
	/// <para>
	/// The pure classifier is executed elsewhere. What cannot execute is the runtime edge: whether
	/// the harness asks the engine the RIGHT question, and whether it hands the answer to the
	/// classifier instead of deciding for itself. Those two facts are held here in text until a
	/// live-engine pass can run them.
	/// </para>
	/// </summary>
	[TestFixture]
	public sealed class KingdomScenarioPresenceSourceTests
	{
		private static string Read(string path)
		{
			return TestMain.ReadRepositoryText(path);
		}

		// ----- RED 7: presence means key presence -------------------------------------------------

		/// <summary>
		/// One reader asks the engine about presence, and it asks with the Has* family. A default
		/// getter answers the same for a key that was never written and for a key holding zero.
		/// </summary>
		[Test]
		public void OnlyTheDurableStateReaderAsksTheEngineAboutPresence()
		{
			string reader = Read("Harness/KingdomScenarioDurableState.cs");
			foreach (string required in new string[]
				{ "HasStringGameState", "HasIntGameState", "HasInt64GameState",
					"HasObjectGameState", "HasBooleanGameState" })
				StringAssert.Contains(required, reader);
			foreach (string shard in new string[]
				{ "Harness/KingdomScenarioTransaction.cs", "Harness/KingdomScenarioRealizer.cs",
					"Harness/KingdomScenarioStampAuthority.cs" })
			{
				string source = Read(shard);
				StringAssert.DoesNotContain("GetIntGameState(", source, shard);
				StringAssert.DoesNotContain("HasIntGameState(", source, shard);
			}
		}

		/// <summary>The runtime reader takes no verdict of its own; the pure classifier does.</summary>
		[Test]
		public void TheRuntimeReaderIsPinnedToThePureClassifier()
		{
			string marker = Read("Harness/KingdomScenarioTransaction.cs");
			StringAssert.Contains("KingdomScenarioStateShape.Transaction(", marker);
			StringAssert.Contains("KingdomScenarioDurableState.Observe(TransactionState)", marker);
			StringAssert.Contains("KingdomScenarioDurableState.Observe(RealizedState)", marker);
			string realizer = Read("Harness/KingdomScenarioRealizer.cs");
			StringAssert.Contains("KingdomScenarioStateShape.TryAuthorityText(", realizer);
			// The stamp shape classification moved to the publication authority when the measured
			// republication landed; it is still the pure classifier that decides.
			string authority = Read("Harness/KingdomScenarioStampAuthority.cs");
			StringAssert.Contains("KingdomScenarioStateShape.Stamp(", authority);
		}

		/// <summary>The exact table shape is reproved after every write, not just the value.</summary>
		[Test]
		public void EveryWriteReprovesItsExactTableShape()
		{
			string marker = Read("Harness/KingdomScenarioTransaction.cs");
			StringAssert.Contains("ProvesExactInt(TransactionState", marker);
			StringAssert.Contains("ProvesExactInt(RealizedState", marker);
			StringAssert.Contains("did not read back as exactly one int key", marker);
			StringAssert.Contains("did not read back as exactly the two int keys", marker);
			string realizer = Read("Harness/KingdomScenarioRealizer.cs");
			StringAssert.Contains("ProvesExactInt(StampedState", realizer);
			// The provenance write moved behind the one shared authority; the canonical presence-law
			// fixture follows it rather than describing where it used to live.
			string authority = Read("Harness/KingdomScenarioStampAuthority.cs");
			StringAssert.Contains("ProvesExactText(", authority);
		}

		/// <summary>
		/// The engine's own seed key follows the engine's contract and is the ONE documented
		/// exception; every harness-owned authority key goes through the presence law.
		/// </summary>
		[Test]
		public void OnlyTheEngineOwnedSeedKeyUsesADefaultGetter()
		{
			string realizer = Read("Harness/KingdomScenarioRealizer.cs");
			StringAssert.Contains("Engine-owned, so it follows the engine's contract", realizer);
			int reads = 0;
			int cursor = realizer.IndexOf("GetStringGameState(", StringComparison.Ordinal);
			while (cursor >= 0)
			{
				reads++;
				cursor = realizer.IndexOf("GetStringGameState(", cursor + 1,
					StringComparison.Ordinal);
			}
			Assert.AreEqual(1, reads,
				"one raw text read in the realizer: the engine seed, which follows the engine's "
					+ "own contract");
			// The stamp read moved with the presence reader; it is still exactly one, and still
			// only on a pair the classifier already proved Readable.
			string authority = Read("Harness/KingdomScenarioStampAuthority.cs");
			int authorityReads = 0;
			cursor = authority.IndexOf("GetStringGameState(", StringComparison.Ordinal);
			while (cursor >= 0)
			{
				authorityReads++;
				cursor = authority.IndexOf("GetStringGameState(", cursor + 1,
					StringComparison.Ordinal);
			}
			Assert.AreEqual(1, authorityReads,
				"one raw text read in the authority: the stamp already proved present");
		}
	}
}
#endif
