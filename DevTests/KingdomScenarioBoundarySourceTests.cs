#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Containment proof for the developer scenario harness. Every test here must fail if the
	/// harness becomes reachable from an ordinary build, and none may pass vacuously when the
	/// harness tree is absent.
	/// </summary>
	[TestFixture]
	public sealed class KingdomScenarioBoundarySourceTests
	{
		private const string HarnessDirectory = "Harness";
		private const string HarnessNamespace = "ThousandAndFirst.Harness";

		private static string Read(string path)
		{
			return TestMain.ReadRepositoryText(path);
		}

		private static string HarnessRoot
		{
			get { return Path.Combine(TestMain.RepositoryRoot, HarnessDirectory); }
		}

		/// <summary>Sources outside DevTests and the harness tree. Production for containment.</summary>
		private static List<string> ProductionSources()
		{
			List<string> result = new List<string>();
			string root = TestMain.RepositoryRoot;
			string devTests = Path.Combine(root, "DevTests") + Path.DirectorySeparatorChar;
			string harness = HarnessRoot + Path.DirectorySeparatorChar;
			foreach (string path in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
			{
				if (path.StartsWith(devTests, StringComparison.Ordinal)
					|| path.StartsWith(harness, StringComparison.Ordinal)) continue;
				if (path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
					|| path.Contains(Path.DirectorySeparatorChar + ".nuget" + Path.DirectorySeparatorChar))
					continue;
				result.Add(path);
			}
			return result;
		}

		private static List<string> HarnessSources()
		{
			return Directory.Exists(HarnessRoot)
				? new List<string>(Directory.EnumerateFiles(HarnessRoot, "*.cs",
					SearchOption.AllDirectories))
				: new List<string>();
		}

		/// <summary>
		/// Vacuity guard. The sweeps below prove nothing if the harness tree is empty, so an empty
		/// tree is itself a failure rather than a silent pass.
		/// </summary>
		[Test]
		public void HarnessTreeExistsSoTheContainmentSweepsAreNotVacuous()
		{
			Assert.IsTrue(Directory.Exists(HarnessRoot),
				"the harness tree is absent; containment sweeps would pass vacuously");
			Assert.Greater(HarnessSources().Count, 0,
				"the harness tree holds no C# source; containment sweeps would pass vacuously");
			Assert.Greater(ProductionSources().Count, 0,
				"no production source was found; the production sweep would pass vacuously");
		}

		/// <summary>
		/// Qud compiles only the directories named in manifest Directories. An unlisted tree is
		/// never compiled, which is the capability gate the softer option/attribute gates cannot be.
		/// </summary>
		[Test]
		public void ShippedManifestNeverSelectsTheHarnessTree()
		{
			string manifest = Read("manifest.json");
			StringAssert.DoesNotContain(HarnessDirectory, manifest);
		}

		[Test]
		public void StageInventoryExcludesTheHarnessTree()
		{
			string stage = Read("Tools/stage.sh");
			StringAssert.Contains("EXCLUDE_DIRS=(", stage);
			int start = stage.IndexOf("EXCLUDE_DIRS=(", StringComparison.Ordinal);
			int end = stage.IndexOf(')', start);
			Assert.Greater(end, start, "EXCLUDE_DIRS assignment is unterminated");
			string list = stage.Substring(start, end - start);
			StringAssert.Contains(" " + HarnessDirectory + " ", list);
		}

		[Test]
		public void PortableAuditRejectsHarnessPathsInTheRuntimeInventory()
		{
			string audit = Read("Tools/portable-check.sh");
			StringAssert.Contains("\"" + HarnessDirectory + "/\"", audit);
			StringAssert.Contains("development-only path entered runtime inventory", audit);
			int tuple = audit.IndexOf("relative.startswith((", StringComparison.Ordinal);
			Assert.Greater(tuple, -1, "the dev-path guard tuple is missing");
			int close = audit.IndexOf("))", tuple);
			Assert.Greater(close, tuple, "the dev-path guard tuple is unterminated");
			StringAssert.Contains("\"" + HarnessDirectory + "/\"",
				audit.Substring(tuple, close - tuple));
		}

		/// <summary>
		/// Production must never name the harness. Absence of the directory has to be absence of
		/// the feature, which fails the moment a production file depends on a harness type.
		/// </summary>
		[Test]
		public void NoProductionSourceReferencesTheHarnessNamespace()
		{
			List<string> offenders = new List<string>();
			foreach (string path in ProductionSources())
				if (File.ReadAllText(path).Contains(HarnessNamespace)) offenders.Add(path);
			Assert.IsEmpty(offenders,
				"production source references the harness namespace: " + string.Join(", ", offenders));
		}

		/// <summary>
		/// Engine registration attributes are reflection entry points. Any harness-owned entry point
		/// must live inside the excluded tree, never beside production.
		/// </summary>
		[Test]
		public void HarnessOwnedRegistrationAttributesStayInsideTheExcludedTree()
		{
			string[] markers = new string[] { "KingdomScenario", "ScenarioHarness" };
			List<string> offenders = new List<string>();
			foreach (string path in ProductionSources())
			{
				string text = File.ReadAllText(path);
				bool registers = text.Contains("[HasWishCommand]") || text.Contains("[PlayerMutator]");
				if (!registers) continue;
				foreach (string marker in markers)
					if (text.Contains(marker) && !path.EndsWith("KingdomScenarioProvenance.cs",
						StringComparison.Ordinal)) offenders.Add(path + " (" + marker + ")");
			}
			Assert.IsEmpty(offenders,
				"a scenario registration attribute lives outside the harness tree: "
				+ string.Join(", ", offenders));
		}

		/// <summary>House law: ASCII source, and no production file at or past the line cap.</summary>
		[Test]
		public void HarnessSourcesAreAsciiAndUnderTheLineCap()
		{
			List<string> offenders = new List<string>();
			foreach (string path in HarnessSources())
			{
				string text = File.ReadAllText(path);
				for (int i = 0; i < text.Length; i++)
					if (text[i] > 0x7E && text[i] != '\r' && text[i] != '\n' && text[i] != '\t')
					{
						offenders.Add(path + " (non-ascii at " + i + ")");
						break;
					}
				int lines = text.Split('\n').Length;
				if (lines >= 300) offenders.Add(path + " (" + lines + " lines)");
			}
			Assert.IsEmpty(offenders, "harness source violates house law: "
				+ string.Join(", ", offenders));
		}
	}
}
#endif
