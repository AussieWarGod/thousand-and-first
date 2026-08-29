#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Independent proof that the bounded migration-port manifest still describes the tree.
	/// <para>
	/// The manifest is the contract; this suite is the tripwire. It never generates a fixture and
	/// never accepts one without provenance, because a fixture produced by today's writer proves
	/// only that the writer agrees with itself.
	/// </para>
	/// </summary>
	[TestFixture]
	public sealed class KingdomMigrationPortCoverageTests
	{
		/// <summary>
		/// Live wire versions with no provenance-backed historical fixture. Declared so that
		/// closing a gap, opening a new one, or bumping a codec all fail here until the manifest
		/// is updated deliberately.
		/// <para>
		/// NON-FINAL: the polity wire has landed at v6, which widened its own uncovered surface
		/// from five live versions to six. Its five hand-frozen hostile envelopes are refusal
		/// proofs, not coverage, and deliberately do not reduce this total. The manifest stays
		/// non-final until the independent rereview of the polity v6 completion lane is green.
		/// </para>
		/// </summary>
		private const int ExpectedHardGaps = 66;

		private const int ExpectedPorts = 19;

		internal static JsonElement Manifest()
		{
			string path = Path.Combine(TestMain.RepositoryRoot, "DevTests",
				"KingdomMigrationPorts.json");
			Assert.IsTrue(File.Exists(path), "the migration-port manifest is missing: " + path);
			return JsonDocument.Parse(File.ReadAllText(path)).RootElement;
		}

		internal static IList<JsonElement> Ports()
		{
			List<JsonElement> ports = new List<JsonElement>();
			foreach (JsonElement port in Manifest().GetProperty("ports").EnumerateArray())
				ports.Add(port);
			return ports;
		}

		internal static string Text(JsonElement port, string name)
		{
			JsonElement value;
			if (!port.TryGetProperty(name, out value)) return null;
			return value.ValueKind == JsonValueKind.Null ? null : value.GetString();
		}

		[Test]
		public void ManifestParsesAtTheExpectedSchemaAndSize()
		{
			Assert.AreEqual(1, Manifest().GetProperty("schema").GetInt32());
			Assert.AreEqual("NON-FINAL", Manifest().GetProperty("status").GetString(),
				"the census stays marked non-final until the repaired polity freeze posts");
			Assert.AreEqual(ExpectedPorts, Ports().Count,
				"the port count changed; update the manifest and the declared expectation together");
		}

		[Test]
		public void EveryPortNamesAReaderAndVersionConstantThatExistOnDisk()
		{
			List<string> offenders = new List<string>();
			foreach (JsonElement port in Ports())
			{
				string codec = Text(port, "codec");
				string reader = Text(port, "reader");
				string constant = Text(port, "versionConstant");
				if (string.IsNullOrEmpty(codec)) { offenders.Add("a port has no codec name"); continue; }
				if (string.IsNullOrEmpty(reader) || !Exists(reader))
					offenders.Add(codec + " names a missing reader: " + reader);
				if (string.IsNullOrEmpty(constant) || !Exists(FilePart(constant)))
					offenders.Add(codec + " names a missing version constant: " + constant);
			}
			Assert.IsEmpty(offenders, string.Join("; ", offenders));
		}

		/// <summary>
		/// A fixture is admissible only with source-commit and SHA-256 provenance. This suite must
		/// never be able to satisfy itself by writing one.
		/// </summary>
		[Test]
		public void EveryDeclaredFixtureCarriesCommitAndHashProvenance()
		{
			List<string> offenders = new List<string>();
			foreach (JsonElement port in Ports())
			{
				string codec = Text(port, "codec");
				foreach (JsonElement fixture in port.GetProperty("fixtures").EnumerateArray())
				{
					if (string.IsNullOrEmpty(Text(fixture, "sourceCommit")))
						offenders.Add(codec + " has a fixture with no source commit");
					string hash = Text(fixture, "sha256");
					if (string.IsNullOrEmpty(hash) || hash.Length != 64)
						offenders.Add(codec + " has a fixture with no SHA-256 provenance");
					JsonElement generated;
					if (fixture.TryGetProperty("generated", out generated)
						&& generated.ValueKind == JsonValueKind.True)
						offenders.Add(codec + " has a fixture marked generated; "
							+ "a fixture produced by today's writer proves nothing");
				}
			}
			Assert.IsEmpty(offenders, string.Join("; ", offenders));
		}

		/// <summary>
		/// Every live wire version needs a fixture or an explicit gap. The total is pinned so a
		/// silent bump cannot widen the uncovered surface unnoticed.
		/// </summary>
		[Test]
		public void UncoveredWireVersionsAreReportedAsHardGapsAndTheTotalIsPinned()
		{
			int gaps = 0;
			List<string> offenders = new List<string>();
			foreach (JsonElement port in Ports())
			{
				string codec = Text(port, "codec");
				int current = port.GetProperty("currentVersion").GetInt32();
				int fixtures = 0;
				foreach (JsonElement unused in port.GetProperty("fixtures").EnumerateArray()) fixtures++;
				int uncovered = current - fixtures;
				if (uncovered < 0)
				{
					offenders.Add(codec + " declares more fixtures than live versions");
					continue;
				}
				if (uncovered > 0 && string.IsNullOrEmpty(Text(port, "gapReason")))
					offenders.Add(codec + " has uncovered versions but declares no gap reason");
				gaps += uncovered;
			}
			Assert.IsEmpty(offenders, string.Join("; ", offenders));
			Assert.AreEqual(ExpectedHardGaps, gaps,
				"the hard compatibility gap total changed; a codec was bumped, added, or covered. "
				+ "Update the manifest and this expectation together, and never by generating a fixture.");
		}

		/// <summary>
		/// Census completeness. Any production source declaring an in-payload wire version must be
		/// represented, so a new durable codec cannot land outside the manifest.
		/// </summary>
		[Test]
		public void EveryProductionWireVersionConstantIsRepresentedInTheManifest()
		{
			HashSet<string> declared = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (JsonElement port in Ports())
			{
				string reader = Text(port, "reader");
				string constant = Text(port, "versionConstant");
				if (reader != null) declared.Add(Path.GetFileName(reader));
				if (constant != null) declared.Add(Path.GetFileName(FilePart(constant)));
			}
			string[] markers = new string[]
			{
				"CurrentWireVersion", "FirstWireVersion", "CurrentVersion", "WireVersion"
			};
			List<string> missing = new List<string>();
			foreach (string path in Directory.EnumerateFiles(TestMain.RepositoryRoot, "*.cs",
				SearchOption.AllDirectories))
			{
				if (Skip(path)) continue;
				string text = File.ReadAllText(path);
				bool carries = false;
				for (int i = 0; i < markers.Length && !carries; i++)
					carries = text.Contains("const int " + markers[i])
						|| text.Contains("const byte " + markers[i]);
				if (!carries) continue;
				if (!declared.Contains(Path.GetFileName(path)))
					missing.Add(Relative(path));
			}
			Assert.IsEmpty(missing,
				"a production wire-version constant is outside the migration-port manifest: "
				+ string.Join(", ", missing));
		}

		[Test]
		public void PhysicalShellAxesAreDeclaredSeparatelyFromWireVersions()
		{
			int axes = 0;
			foreach (JsonElement axis in Manifest().GetProperty("physicalShellAxes").EnumerateArray())
			{
				Assert.IsNotEmpty(Text(axis, "name"));
				Assert.IsTrue(Exists(FilePart(Text(axis, "constant"))),
					"a shell axis names a missing file: " + Text(axis, "constant"));
				axes++;
			}
			Assert.Greater(axes, 0, "the shell axis list must not be silently emptied");
		}

		private static bool Skip(string path)
		{
			string separator = Path.DirectorySeparatorChar.ToString();
			return path.Contains(separator + "DevTests" + separator)
				|| path.Contains(separator + "Harness" + separator)
				|| path.Contains(separator + "obj" + separator)
				|| path.Contains(separator + ".nuget" + separator);
		}

		private static string Relative(string path)
		{
			return path.Substring(TestMain.RepositoryRoot.Length).TrimStart(
				Path.DirectorySeparatorChar).Replace(Path.DirectorySeparatorChar, '/');
		}

		internal static string FilePart(string reference)
		{
			if (string.IsNullOrEmpty(reference)) return reference;
			int colon = reference.IndexOf(':');
			return colon < 0 ? reference : reference.Substring(0, colon);
		}

		internal static bool Exists(string relative)
		{
			if (string.IsNullOrEmpty(relative)) return false;
			return File.Exists(Path.Combine(TestMain.RepositoryRoot,
				relative.Replace('/', Path.DirectorySeparatorChar)));
		}
	}
}
#endif
