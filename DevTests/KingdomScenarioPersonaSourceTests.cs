#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

using ThousandAndFirst.Harness;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Source contracts binding the persona matrix to the runtime it asserts against.
	/// <para>
	/// The grammar itself executes in Tools/tests/persona_matrix_test.py; these hold the things
	/// that suite cannot see, because they live on the other side of the language boundary: that
	/// every reason code a persona binds to is a code the C# actually emits, that the terminal rows
	/// a persona may declare are rows the runtime actually writes, and that the sealable and
	/// reserved verb sets have not drifted between the runtime and the two tools.
	/// </para>
	/// <para>
	/// This is what makes "bind to reason codes, never to prose" enforceable rather than advisory:
	/// deleting a code from the runtime fails here, not silently in a run nobody can retry.
	/// </para>
	/// </summary>
	[TestFixture]
	public sealed class KingdomScenarioPersonaSourceTests
	{
		private static string Read(string path)
		{
			return TestMain.ReadRepositoryText(path);
		}

		private static IList<string> Personas()
		{
			string directory = Path.Combine(TestMain.RepositoryRoot, "Tools", "personas");
			Assert.IsTrue(Directory.Exists(directory), "Tools/personas is missing");
			string[] found = Directory.GetFiles(directory, "*.persona");
			Array.Sort(found, StringComparer.Ordinal);
			List<string> rows = new List<string>(found);
			Assert.Greater(rows.Count, 5, "the persona matrix authors more than a token case");
			return rows;
		}

		private static string Field(string Text, string Key)
		{
			string[] lines = Text.Replace("\r\n", "\n").Split('\n');
			for (int i = 0; i < lines.Length; i++)
			{
				string line = lines[i].Trim();
				if (line.StartsWith(Key + "=", StringComparison.Ordinal))
					return line.Substring(Key.Length + 1).Trim();
			}
			return null;
		}

		/// <summary>
		/// Every reason code a persona asserts on must be emitted by the runtime. A persona binding
		/// to a code nobody writes would go red forever, and one binding to a code that was later
		/// renamed would go green forever - the worse of the two.
		/// </summary>
		[Test]
		public void EveryAssertedReasonCodeIsEmittedByTheRuntime()
		{
			string transaction = Read("Harness/KingdomScenarioTransaction.cs");
			string realizer = Read("Harness/KingdomScenarioRealizer.cs");
			string advance = Read("Harness/KingdomScenarioAdvance.cs");
			string providers = Read("Harness/KingdomScenarioVerbProviderRules.cs");
			string sources = transaction + "\n" + realizer + "\n" + advance + "\n" + providers;
			int asserted = 0;
			foreach (string path in Personas())
			{
				string expect = Field(File.ReadAllText(path), "EXPECT");
				Assert.IsNotNull(expect, path);
				foreach (string item in expect.Split(','))
				{
					int tilde = item.IndexOf('~');
					if (tilde < 0) continue;
					string wanted = item.Substring(tilde + 1).Trim();
					if (!wanted.StartsWith("taf-", StringComparison.Ordinal)) continue;
					StringAssert.Contains(wanted, sources);
					asserted++;
				}
			}
			Assert.Greater(asserted, 0, "no persona binds to a reason code");
		}

		/// <summary>Every terminal a persona may declare is a row some runtime shard writes.</summary>
		[Test]
		public void EveryTerminalRowIsWrittenByTheRuntime()
		{
			StringAssert.Contains("\"SCRIPT-COMPLETE\"", Read("Harness/KingdomScenarioAutoRunner.cs"));
			StringAssert.Contains("\"SCRIPT-STOPPED\"", Read("Harness/KingdomScenarioAutoRunner.cs"));
			StringAssert.Contains("\"GATE-REFUSED\"", Read("Harness/KingdomScenarioRealizer.cs"));
			string matrix = Read("Tools/personas/persona_matrix.py");
			StringAssert.Contains("\"COMPLETE\": \"SCRIPT-COMPLETE\"", matrix);
			StringAssert.Contains("\"STOPPED\": \"SCRIPT-STOPPED\"", matrix);
			StringAssert.Contains("\"GATE-REFUSED\": \"GATE-REFUSED\"", matrix);
		}

		/// <summary>
		/// The gate journals BEFORE it throws. A refusal at the embark player-mutator step happens
		/// long before the runner's first action opportunity, so without this row an unattended run
		/// the gate refused is indistinguishable from one that hung.
		/// </summary>
		[Test]
		public void TheGateJournalsItsRefusalBeforeThrowing()
		{
			string realizer = Read("Harness/KingdomScenarioRealizer.cs");
			int journalled = realizer.IndexOf("KingdomScenarioJournal.Append(RefusedRow",
				StringComparison.Ordinal);
			int thrown = realizer.IndexOf("ThousandAndFirst scenario harness refused to open",
				StringComparison.Ordinal);
			Assert.Greater(journalled, -1);
			Assert.Greater(thrown, journalled);
		}

		/// <summary>
		/// The reserved verb set is restated in three places - the runtime, the profile tool, and
		/// the persona engine - and all three must name the same words, or a persona seals a verb
		/// the runtime refuses and spends a whole non-retryable profile finding out.
		/// </summary>
		[Test]
		public void TheReservedVerbSetIsIdenticalInAllThreePlaces()
		{
			string profile = Read("Tools/scenario_profile.py");
			string matrix = Read("Tools/personas/persona_matrix.py");
			for (int i = 0; i < KingdomScenarioVerbApi.Reserved.Length; i++)
			{
				string quoted = "\"" + KingdomScenarioVerbApi.Reserved[i] + "\"";
				StringAssert.Contains(quoted, profile);
				StringAssert.Contains(quoted, matrix);
			}
			StringAssert.Contains("RESERVED_VERBS", profile);
			StringAssert.Contains("RESERVED_VERBS", matrix);
		}

		/// <summary>
		/// The matrix runner's own invariants: it stops the game, wipes every scenario root, runs
		/// one persona at a time, and exits nonzero on any non-PASS verdict.
		/// </summary>
		[Test]
		public void TheMatrixRunnerIsSerialIdempotentAndFailsLoudly()
		{
			string runner = Read("Tools/run-personas.sh");
			StringAssert.Contains("Get-Process -Name CoQ", runner);
			StringAssert.Contains("/mnt/c/taf-scenario.*", runner);
			StringAssert.Contains("TAF_SCENARIO_EXTRA_VERBS", runner);
			StringAssert.Contains("exit \"$failed\"", runner);
			int stop = runner.IndexOf("\tstop_game\n\twipe_profiles", StringComparison.Ordinal);
			int prepare = runner.IndexOf("\"$PREPARE\" \"$root\"", StringComparison.Ordinal);
			Assert.Greater(stop, -1, "a persona must kill the game and wipe profiles first");
			Assert.Greater(prepare, stop);
		}

		/// <summary>
		/// TAF_REQUEST selects the request; the SEED stays the launcher's. A caller that could name
		/// both could point two runs at one world and the gate would prove only the last.
		/// </summary>
		[Test]
		public void PrepareTakesTheRequestFromTheEnvironmentButNeverTheSeed()
		{
			string prepare = Read("Tools/prepare-scenario.sh");
			StringAssert.Contains("REQUEST_BASE=\"${TAF_REQUEST:-$DEFAULT_REQUEST}\"", prepare);
			StringAssert.Contains("refusing TAF_REQUEST that names its own seed", prepare);
			StringAssert.Contains("REQUEST=\"$REQUEST_BASE;seed=$SEED\"", prepare);
		}

		/// <summary>
		/// The roster loads through the engine's merged XML streams, so another mod's scenarios
		/// merge for free - and each row is attributed to the stream's own ModInfo rather than to
		/// an attribute the file could set to anybody's name.
		/// </summary>
		[Test]
		public void TheRosterMergesEveryModsStreamAndRecordsWhoAuthoredEachRow()
		{
			string registry = Read("Harness/KingdomScenarioRegistry.cs");
			StringAssert.Contains("DataManager.YieldXMLStreamsWithRoot(", registry);
			StringAssert.Contains("Xml.modInfo == null ? \"\" : (Xml.modInfo.ID ?? \"\")", registry);
			string digests = Read("Harness/KingdomScenarioDigests.cs");
			// Provenance is inside the canonical row, and the rows are ordinal-sorted, so a merged
			// roster digests deterministically regardless of mod load order.
			StringAssert.Contains("string owner = Definition.Owner ?? \"\";", digests);
			int owner = digests.IndexOf("sb.Append(owner)", StringComparison.Ordinal);
			int sort = digests.IndexOf("rows.Sort(StringComparer.Ordinal)", StringComparison.Ordinal);
			Assert.Greater(owner, -1);
			Assert.Greater(sort, -1);
		}

		/// <summary>
		/// Verb discovery uses the engine's cached attribute scan but constructs each type inside a
		/// guard, so one third-party provider with no parameterless constructor cannot take every
		/// other mod's verbs down with it.
		/// </summary>
		[Test]
		public void VerbDiscoveryScansWithTheEngineAndConstructsUnderGuard()
		{
			string registry = Read("Harness/KingdomScenarioVerbRegistry.cs");
			StringAssert.Contains("ModManager.GetTypesWithAttribute(", registry);
			int guard = registry.IndexOf("KingdomSystem.Guard(\"scenario verb provider \"",
				StringComparison.Ordinal);
			int construct = registry.IndexOf("Activator.CreateInstance(type)",
				StringComparison.Ordinal);
			Assert.Greater(guard, -1);
			Assert.Greater(construct, guard);
			// One dispatch path: extensions are asked only after the closed built-in set declines.
			string verbs = Read("Harness/KingdomScenarioVerbs.cs");
			int builtin = verbs.IndexOf("case \"flatten\":", StringComparison.Ordinal);
			int extension = verbs.IndexOf("KingdomScenarioVerbRegistry.TryRun(",
				StringComparison.Ordinal);
			Assert.Greater(builtin, -1);
			Assert.Greater(extension, builtin);
		}
	}
}
#endif
