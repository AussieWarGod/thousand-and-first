#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The exemption half of the migration-port census.
	/// <para>
	/// <see cref="KingdomMigrationPortCoverageTests.EveryProductionWireVersionConstantIsRepresentedInTheManifest"/>
	/// accepts a production file carrying a wire-version-shaped constant in exactly two ways: a
	/// port row, which asserts the bytes are durable, or a <c>nonDurableVersionSites</c> row, which
	/// asserts they are not. An exemption list that were merely declared would be a hole straight
	/// through the census: any durable codec could be waved past it by adding a line of JSON.
	/// </para>
	/// <para>
	/// So every exemption is checked against the tree it names rather than trusted. The row must
	/// point at a real file, name a constant that file really declares, be a file the census would
	/// otherwise flag, carry a reason someone actually wrote, not also be claimed as durable by a
	/// port, and — the load-bearing one — the file must carry none of the durable-write mechanisms
	/// this repository persists through. The size of the list is pinned as well, so growing it is
	/// a deliberate edit in two places rather than a quiet one in the manifest.
	/// </para>
	/// </summary>
	[TestFixture]
	public sealed class KingdomMigrationPortExemptionCensusTests
	{
		/// <summary>
		/// One exemption today: Core/KingdomXmlSchemaRules.cs. Its CurrentVersion is compared
		/// against the Schema attribute of a public XML registry root read at mod load and is
		/// otherwise only spelled into an operator log line. Nothing writes it anywhere.
		/// </summary>
		private const int ExpectedExemptions = 1;

		/// <summary>A reason short enough to be a shrug is not a reason.</summary>
		private const int MinimumReasonCharacters = 80;

		/// <summary>
		/// Every way this repository moves a value into something that survives a reload: the
		/// engine's own part and composite serialisers, the binary codecs, zone properties, and
		/// game state. A file carrying a wire-version constant and ANY of these is not obviously
		/// non-durable, and must be argued as a port instead of waved through as an exemption.
		/// </summary>
		private static readonly string[] DurableWriteMechanisms =
		{
			"SerializationWriter", "SerializationReader", "IComposite", "IPart",
			"BinaryWriter", "BinaryReader", "SetZoneProperty", "SetStringGameState",
			"SetIntGameState", "WriteNamedFields"
		};

		[Test]
		public void TheExemptionListIsPresentAndItsSizeIsPinned()
		{
			Assert.AreEqual(ExpectedExemptions, Exemptions().Count,
				"the non-durable exemption list changed size; a file was waved past the census or "
				+ "a durable codec was found. Move this pin deliberately, with the reading that "
				+ "justifies it recorded on the row.");
		}

		/// <summary>
		/// A row must name a file that exists, a constant that file declares, and a reason. Without
		/// this the list is a set of strings and the census believes all three.
		/// </summary>
		[Test]
		public void EveryExemptionNamesAFileConstantAndReasonThatExistOnDisk()
		{
			List<string> offenders = new List<string>();
			foreach (JsonElement exemption in Exemptions())
			{
				string file = Text(exemption, "file");
				string constant = Text(exemption, "constant");
				string reason = Text(exemption, "reason");
				if (string.IsNullOrEmpty(file) || !Exists(file))
				{ offenders.Add("an exemption names a missing file: " + file); continue; }
				if (string.IsNullOrEmpty(constant))
				{ offenders.Add(file + " is exempted without naming its constant"); continue; }
				string source = Read(file);
				if (!source.Contains("const int " + constant)
					&& !source.Contains("const byte " + constant))
					offenders.Add(file + " is exempted for " + constant
						+ ", which it does not declare as a wire-version-shaped constant");
				if (string.IsNullOrEmpty(reason) || reason.Length < MinimumReasonCharacters)
					offenders.Add(file + " is exempted with no reason worth the name");
			}
			Assert.IsEmpty(offenders, string.Join("; ", offenders));
		}

		/// <summary>
		/// An exemption is only meaningful for a file the census would otherwise flag. Padding the
		/// list with files it never looks at would make the pinned size meaningless.
		/// </summary>
		[Test]
		public void EveryExemptedFileIsOneTheCensusWouldOtherwiseFlag()
		{
			List<string> offenders = new List<string>();
			foreach (JsonElement exemption in Exemptions())
			{
				string file = Text(exemption, "file");
				if (string.IsNullOrEmpty(file) || !Exists(file)) continue;
				if (!KingdomMigrationPortCoverageTests.CarriesWireVersionMarker(Read(file)))
					offenders.Add(file + " is exempted but declares no wire-version constant at "
						+ "all, so the exemption stands for nothing");
			}
			Assert.IsEmpty(offenders, string.Join("; ", offenders));
		}

		/// <summary>
		/// The two lists are answers to the same question and must not both claim a file. A file
		/// named by a port has been argued durable; it cannot also be argued transient.
		/// </summary>
		[Test]
		public void NoExemptedFileIsAlsoClaimedByAPort()
		{
			HashSet<string> durable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (JsonElement port in KingdomMigrationPortCoverageTests.Ports())
			{
				string reader = Text(port, "reader");
				string constant = Text(port, "versionConstant");
				if (reader != null) durable.Add(Path.GetFileName(reader));
				if (constant != null) durable.Add(Path.GetFileName(
					KingdomMigrationPortCoverageTests.FilePart(constant)));
			}
			List<string> offenders = new List<string>();
			foreach (JsonElement exemption in Exemptions())
			{
				string file = Text(exemption, "file");
				if (!string.IsNullOrEmpty(file) && durable.Contains(Path.GetFileName(file)))
					offenders.Add(file + " is claimed as both a durable port and a non-durable site");
			}
			Assert.IsEmpty(offenders, string.Join("; ", offenders));
		}

		/// <summary>
		/// The load-bearing guard. A file exempted as non-durable must carry none of the mechanisms
		/// this repository persists through, so a durable codec cannot be exempted by assertion.
		/// This is deliberately mechanical and deliberately strict: it does not decide whether a
		/// codec is durable, it refuses to let the question go unasked.
		/// </summary>
		[Test]
		public void NoExemptedFileCarriesADurableWriteMechanism()
		{
			List<string> offenders = new List<string>();
			foreach (JsonElement exemption in Exemptions())
			{
				string file = Text(exemption, "file");
				if (string.IsNullOrEmpty(file) || !Exists(file)) continue;
				string source = Read(file);
				for (int i = 0; i < DurableWriteMechanisms.Length; i++)
					if (source.Contains(DurableWriteMechanisms[i]))
						offenders.Add(file + " is exempted as non-durable but carries "
							+ DurableWriteMechanisms[i]
							+ "; argue it as a port or explain the write site");
			}
			Assert.IsEmpty(offenders, string.Join("; ", offenders));
		}

		// Manifest access is owned by the coverage suite; these forward rather than duplicate it.
		private static IList<JsonElement> Exemptions() =>
			KingdomMigrationPortCoverageTests.Exemptions();

		private static string Text(JsonElement row, string name) =>
			KingdomMigrationPortCoverageTests.Text(row, name);

		private static bool Exists(string relative) =>
			KingdomMigrationPortCoverageTests.Exists(relative);

		private static string Read(string relative)
		{
			return File.ReadAllText(Path.Combine(TestMain.RepositoryRoot,
				relative.Replace('/', Path.DirectorySeparatorChar)));
		}
	}
}
#endif
