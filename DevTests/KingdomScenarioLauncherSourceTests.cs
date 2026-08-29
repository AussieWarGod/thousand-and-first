#if TAF_TESTS
using System;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Source contracts for the throwaway scenario profile: how it is built, sealed, and verified.
	/// <para>
	/// The authoritative seal rule executes in Tools/tests/scenario_profile_test.py. These contracts
	/// hold the two things that suite cannot: that the PowerShell launcher mirrors the same closed
	/// rule on Windows, and that neither side silently narrows the other's sealed inventory.
	/// </para>
	/// </summary>
	[TestFixture]
	public sealed class KingdomScenarioLauncherSourceTests
	{
		private static string Read(string path)
		{
			return TestMain.ReadRepositoryText(path);
		}

		private static void AssertOrder(string source, params string[] terms)
		{
			int cursor = -1;
			for (int i = 0; i < terms.Length; i++)
			{
				int next = source.IndexOf(terms[i], cursor + 1, StringComparison.Ordinal);
				Assert.Greater(next, cursor, terms[i]);
				cursor = next;
			}
		}

		/// <summary>
		/// The Windows launcher mirrors the authoritative closed-seal rule. The rule itself is
		/// executed by Tools/tests/scenario_profile_test.py; this asserts the mirror kept both
		/// directions, its link refusals, and the engine's actual seed range.
		/// </summary>
		[Test]
		public void LauncherMirrorsTheClosedSealRule()
		{
			string launcher = Read("Tools/run-scenario.ps1");
			StringAssert.Contains("Profile carries unsealed extra files", launcher);
			StringAssert.Contains("Profile is missing sealed files", launcher);
			StringAssert.Contains("Profile files differ from the seal", launcher);
			StringAssert.Contains("reparse point", launcher);
			StringAssert.Contains("normalize to one name", launcher);
			StringAssert.Contains("taf-scenario-profile-seal-v1", launcher);
			StringAssert.Contains("2147483647", launcher);
		}

		/// <summary>
		/// GetWorldSeed parses its digits with int.TryParse and returns the parsed value, so exact
		/// '#0' names a world the engine reproduces. Both sealed checks must admit it, and neither
		/// may narrow the other's inventory. The launcher's own hard-link refusal is pinned in
		/// adjacency form - the check and its throw as one contiguous literal - so deleting either
		/// half fails this test instead of leaving a same-file Contains blind to the mutant.
		/// </summary>
		[Test]
		public void BothSealCheckersAgreeOnTheSeedRangeAndOnLinks()
		{
			string launcher = Read("Tools/run-scenario.ps1");
			StringAssert.Contains("[int64]$seedDigits -lt 0", launcher);
			StringAssert.Contains(
				"if ($hardLinkNames.Count -gt 1) {\n"
				+ "            throw \"Profile tree contains a hard-linked file with $($hardLinkNames.Count) names: $($item.FullName)\"",
				launcher);
			string python = Read("Tools/scenario_profile.py");
			StringAssert.Contains("MIN_SEED = 0", python);
			StringAssert.Contains("MAX_SEED = 2147483647", python);
			StringAssert.Contains("st_nlink", python);
			StringAssert.Contains("hard-linked file", python);
			StringAssert.Contains("st_file_attributes", python);
			string tests = Read("Tools/tests/scenario_profile_test.py");
			StringAssert.Contains("test_zero_is_lawful", tests);
			StringAssert.Contains("test_hard_linked_file_is_rejected", tests);
		}

		/// <summary>The profile is completed before it is sealed, or it could never verify.</summary>
		[Test]
		public void ProfileIsCompletedBeforeItIsSealed()
		{
			string prepare = Read("Tools/prepare-scenario.sh");
			AssertOrder(prepare,
				@"stage.sh"" copy",
				@"cp -- {} ""$MOD/Harness/""",
				@"request ""$MOD/Harness/EmbarkModules.xml""",
				@"manifest ""$REPO/manifest.json"" ""$MOD/manifest.json""",
				@"options ""$REPO/Tools/smoke/PlayerOptions.json""",
				@"seal ""$LOCAL"" ""$SEAL_DIR/profile.sha256""",
				@"verify ""$LOCAL"" ""$SEAL_DIR/profile.sha256""");
			StringAssert.Contains("refusing: Harness/ is present in the staged runtime inventory",
				prepare);
			StringAssert.Contains("refusing: shipped manifest.json mentions the harness directory",
				prepare);
		}

	}
}
#endif
