#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The semantic layer of the migration-port census. Where
	/// <see cref="KingdomMigrationPortCoverageTests"/> proves the manifest still describes the
	/// tree, this suite proves each claim against the exact declaration or the exact bytes it
	/// names rather than against a substring of a whole file.
	/// <para>
	/// It exists because the whole-file check it replaces was false-green: that check passed
	/// while the polity row declared currentVersion 5 and Polity/KingdomPolityCodec.Envelope.cs:10
	/// declared CurrentWireVersion = 6, because the needle "= 5" matched the unrelated
	/// ImmediatePriorWireVersion = 5 on the next line.
	/// </para>
	/// </summary>
	[TestFixture]
	public sealed class KingdomMigrationPortSemanticCensusTests
	{
		/// <summary>
		/// Ports whose versionConstant names an expression or a symbolic alias rather than a
		/// parseable constant declaration, so they cannot carry a versionConstantIdentifier and
		/// fall back to the weaker whole-file presence check. Pinned so the weak set cannot grow
		/// silently and so no port can escape the semantic check by dropping its identifier.
		/// </summary>
		/// <summary>
		/// A version constant may wrap, but not far. Anything wider is a stale-but-wide pointer,
		/// which is the same staleness class the audit found in migrationAuthority and
		/// opaqueFutureSite: containment alone would let ":1-207" stand in for ":10".
		/// </summary>
		private const int MaximumConstantWindowLines = 4;

		private static readonly string[] PortsWithoutASemanticVersionConstant =
		{
			"KingdomArchivedSettlementCodec", "KingdomPurposePortfolioRules", "KingdomTradeState"
		};

		/// <summary>
		/// The declared current version must equal the value bound by the exact declaration the
		/// manifest names. The previous check searched the WHOLE FILE for "= N", which a sibling
		/// constant satisfies: it passed while the polity row declared 5 and the file declared
		/// CurrentWireVersion = 6, because ImmediatePriorWireVersion = 5 sat on the next line.
		/// Ports that cannot name a declaration keep the old check and are pinned above.
		/// </summary>
		[Test]
		public void EveryPortsDeclaredCurrentVersionMatchesItsNamedConstantDeclaration()
		{
			List<string> offenders = new List<string>();
			foreach (JsonElement port in Ports())
			{
				string codec = Text(port, "codec");
				string constant = Text(port, "versionConstant");
				string identifier = Text(port, "versionConstantIdentifier");
				if (string.IsNullOrEmpty(constant)) continue;
				if (!Exists(FilePart(constant)))
				{ offenders.Add(codec + " names versionConstant " + constant
					+ " but that file does not exist"); continue; }
				int current = port.GetProperty("currentVersion").GetInt32();
				if (current < 1) { offenders.Add(codec + " declares version " + current); continue; }
				string source = File.ReadAllText(Path.Combine(TestMain.RepositoryRoot,
					FilePart(constant).Replace('/', Path.DirectorySeparatorChar)));
				if (string.IsNullOrEmpty(identifier))
				{
					string needle = "= " + current.ToString(CultureInfo.InvariantCulture);
					string quoted = "\"" + current.ToString(CultureInfo.InvariantCulture) + "\"";
					if (!source.Contains(needle) && !source.Contains(quoted))
						offenders.Add(codec + " declares version " + current
							+ " but that value does not appear in " + FilePart(constant));
					continue;
				}
				int bound;
				string fault;
				if (!TryReadNamedConstant(source, LineRange(constant), identifier, out bound, out fault))
					offenders.Add(codec + " names " + identifier + " at " + constant + " but " + fault);
				else if (bound != current)
					offenders.Add(codec + " declares version " + current + " while " + identifier
						+ " at " + constant + " is declared " + bound);
			}
			Assert.IsEmpty(offenders, string.Join("; ", offenders));
		}

		/// <summary>
		/// The weak set is pinned by name, so a port cannot escape the declaration check above by
		/// quietly dropping its versionConstantIdentifier.
		/// </summary>
		[Test]
		public void PortsWithoutASemanticVersionConstantAreExactlyTheDeclaredWeakSet()
		{
			List<string> weak = new List<string>();
			foreach (JsonElement port in Ports())
				if (string.IsNullOrEmpty(Text(port, "versionConstantIdentifier")))
					weak.Add(Text(port, "codec"));
			weak.Sort(StringComparer.Ordinal);
			CollectionAssert.AreEqual(PortsWithoutASemanticVersionConstant, weak,
				"the set of ports falling back to the weaker whole-file version check changed; give "
				+ "the port a versionConstantIdentifier or move this pin deliberately");
		}

		/// <summary>
		/// A hostileFixtures entry is the opposite of coverage: hand-frozen bytes the exact
		/// historical writer cannot produce, checked in to prove a refusal. Its SHA-256 is
		/// recomputed here from the literal it names, so the manifest cannot drift from the bytes
		/// in either direction, and it never reduces a hard gap because the gap total counts only
		/// the "fixtures" array.
		/// </summary>
		[Test]
		public void DeclaredHostileFixturesMatchTheCheckedInBytesTheyName()
		{
			List<string> offenders = new List<string>();
			int seen = 0;
			foreach (JsonElement port in Ports())
			{
				string codec = Text(port, "codec");
				JsonElement declared;
				if (!port.TryGetProperty("hostileFixtures", out declared)) continue;
				SortedSet<string> claimed = new SortedSet<string>(StringComparer.Ordinal);
				SortedSet<string> located = new SortedSet<string>(StringComparer.Ordinal);
				foreach (JsonElement fixture in declared.EnumerateArray())
				{
					seen++;
					string hash = Text(fixture, "sha256");
					if (string.IsNullOrEmpty(hash) || hash.Length != 64)
					{ offenders.Add(codec + " has a hostile fixture with no SHA-256"); continue; }
					JsonElement generated;
					if (!fixture.TryGetProperty("generated", out generated)
						|| generated.ValueKind != JsonValueKind.False)
					{ offenders.Add(codec + " has a hostile fixture not marked hand-frozen"); continue; }
					string location = Text(fixture, "location");
					if (string.IsNullOrEmpty(location) || !Exists(location))
					{ offenders.Add(codec + " names a missing hostile fixture file"); continue; }
					Dictionary<string, byte[]> literals = FrozenEnvelopes(location);
					claimed.Add(hash);
					foreach (string actual in literals.Keys) located.Add(actual);
					byte[] bytes;
					if (!literals.TryGetValue(hash, out bytes))
					{ offenders.Add(codec + " declares a hostile fixture SHA-256 with no matching "
						+ "literal in " + location); continue; }
					// The SHA pins the bytes; these pin the manifest's description OF the bytes, each
					// recomputed from the literal rather than trusted.
					int wire = Number(fixture, "wireVersion");
					int size = Number(fixture, "envelopeBytes");
					int offset = Number(fixture, "phaseByteOffset");
					if (size != bytes.Length)
						offenders.Add(codec + " declares envelopeBytes " + size + " but the literal is "
							+ bytes.Length + " bytes");
					if (bytes.Length < 8 || wire != BitConverter.ToInt32(bytes, 4))
						offenders.Add(codec + " declares wireVersion " + wire + " but the literal frames "
							+ (bytes.Length < 8 ? "no version" : BitConverter.ToInt32(bytes, 4).ToString(
								CultureInfo.InvariantCulture)));
					if (offset < 0 || 12 + offset >= bytes.Length || bytes[12 + offset] != 6)
						offenders.Add(codec + " declares phaseByteOffset " + offset
							+ ", which is not a phase-6 byte of that literal");
					string testCase = Text(fixture, "testCase");
					if (string.IsNullOrEmpty(testCase) || !File.ReadAllText(Path.Combine(
						TestMain.RepositoryRoot, location.Replace('/', Path.DirectorySeparatorChar)))
						.Contains(testCase))
						offenders.Add(codec + " names testCase \"" + testCase + "\" which is absent from "
							+ location);
				}
				if (claimed.Count > 0 && !claimed.SetEquals(located))
					offenders.Add(codec + " declares " + claimed.Count + " hostile fixture hashes that "
						+ "do not match the " + located.Count + " checked-in literals");
			}
			Assert.IsEmpty(offenders, string.Join("; ", offenders));
			Assert.Greater(seen, 0, "the hostile fixture list must not be silently emptied");
		}

		// Manifest access is owned by the coverage suite; these forward rather than duplicate it.
		private static IList<JsonElement> Ports() => KingdomMigrationPortCoverageTests.Ports();

		private static string Text(JsonElement port, string name) =>
			KingdomMigrationPortCoverageTests.Text(port, name);

		private static string FilePart(string reference) =>
			KingdomMigrationPortCoverageTests.FilePart(reference);

		/// <summary>An integer manifest field, or -1 when absent or not a number.</summary>
		private static int Number(JsonElement element, string name)
		{
			JsonElement value;
			if (!element.TryGetProperty(name, out value)
				|| value.ValueKind != JsonValueKind.Number) return -1;
			int parsed;
			return value.TryGetInt32(out parsed) ? parsed : -1;
		}

		private static bool Exists(string relative) =>
			KingdomMigrationPortCoverageTests.Exists(relative);

		/// <summary>First and last 1-based lines named by a "path:first" or "path:first-last".</summary>
		private static int[] LineRange(string reference)
		{
			// IndexOf, not LastIndexOf, so this agrees with FilePart on where the path ends.
			int colon = reference.IndexOf(':');
			if (colon < 0) return new[] { 0, 0 };
			string span = reference.Substring(colon + 1);
			int dash = span.IndexOf('-'), first, last;
			if (!int.TryParse(dash < 0 ? span : span.Substring(0, dash), NumberStyles.None,
				CultureInfo.InvariantCulture, out first)) return new[] { 0, 0 };
			if (dash < 0) last = first;
			else if (!int.TryParse(span.Substring(dash + 1), NumberStyles.None,
				CultureInfo.InvariantCulture, out last)) return new[] { 0, 0 };
			return new[] { first, last };
		}

		/// <summary>
		/// Reads the integer bound to <paramref name="Identifier"/> by the declaration the manifest
		/// names. The identifier must be bound exactly once in the whole file, that one binding must
		/// sit ON a line inside a range no wider than a declaration can span, and that line must
		/// itself carry <c>const</c>. Containment alone is not enough: a wrong value, a sibling
		/// constant standing in, a stale pointer, and a pointer widened to cover the whole file are
		/// each caught.
		/// </summary>
		private static bool TryReadNamedConstant(string Source, int[] Range, string Identifier,
			out int Value, out string Fault)
		{
			Value = 0; Fault = null;
			string normalized = Source.Replace("\r\n", "\n");
			string[] lines = normalized.Split('\n');
			if (Range[0] < 1 || Range[1] < Range[0] || Range[1] > lines.Length)
			{ Fault = "it names no line range inside that file"; return false; }
			if (Range[1] - Range[0] + 1 > MaximumConstantWindowLines)
			{ Fault = "it names a " + (Range[1] - Range[0] + 1) + "-line window, wider than the "
				+ MaximumConstantWindowLines + " lines a declaration may span"; return false; }
			int line;
			int whole = CountBindings(normalized, Identifier, out Value, out line);
			if (whole != 1)
			{ Value = 0; Fault = "it is bound " + whole + " times in that file, not once";
				return false; }
			if (line < Range[0] || line > Range[1])
			{ Value = 0; Fault = "its one binding is on line " + line + ", outside those lines";
				return false; }
			if (lines[line - 1].IndexOf("const", StringComparison.Ordinal) < 0)
			{ Value = 0; Fault = "line " + line + " is not a const declaration"; return false; }
			return true;
		}

		/// <summary>
		/// Counts `Identifier = &lt;digits&gt;` bindings, yielding the bound value and the 1-based
		/// line the binding sits on. <paramref name="Source"/> must already be newline-normalized.
		/// </summary>
		private static int CountBindings(string Source, string Identifier, out int Value,
			out int Line)
		{
			Value = 0; Line = 0; int count = 0;
			for (int i = Source.IndexOf(Identifier, StringComparison.Ordinal); i >= 0;
				i = Source.IndexOf(Identifier, i + Identifier.Length, StringComparison.Ordinal))
			{
				if (i > 0 && (char.IsLetterOrDigit(Source[i - 1]) || Source[i - 1] == '_' ||
					Source[i - 1] == '.')) continue;
				int j = i + Identifier.Length;
				while (j < Source.Length && (Source[j] == ' ' || Source[j] == '\t')) j++;
				if (j >= Source.Length || Source[j] != '=' ||
					(j + 1 < Source.Length && Source[j + 1] == '=')) continue;
				j++;
				while (j < Source.Length && (Source[j] == ' ' || Source[j] == '\t')) j++;
				int digits = j;
				while (digits < Source.Length && char.IsDigit(Source[digits])) digits++;
				if (digits == j) continue;
				count++;
				Value = int.Parse(Source.Substring(j, digits - j), CultureInfo.InvariantCulture);
				Line = 1;
				for (int k = 0; k < i; k++) if (Source[k] == '\n') Line++;
			}
			return count;
		}

		/// <summary>
		/// Every checked-in base64 polity envelope literal in a source file, keyed by the SHA-256
		/// recomputed from its decoded bytes, so both the hash and the bytes can be asserted on.
		/// </summary>
		private static Dictionary<string, byte[]> FrozenEnvelopes(string Relative)
		{
			const string marker = "[TestCase(\"MlBBVA";
			Dictionary<string, byte[]> literals = new Dictionary<string, byte[]>(
				StringComparer.Ordinal);
			using (SHA256 sha = SHA256.Create())
				foreach (string line in File.ReadAllLines(Path.Combine(TestMain.RepositoryRoot,
					Relative.Replace('/', Path.DirectorySeparatorChar))))
				{
					int start = line.IndexOf(marker, StringComparison.Ordinal);
					if (start < 0) continue;
					start = line.IndexOf('"', start) + 1;
					int end = line.IndexOf('"', start);
					if (end <= start) continue;
					byte[] bytes = Convert.FromBase64String(line.Substring(start, end - start));
					literals[BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-",
						string.Empty).ToLowerInvariant()] = bytes;
				}
			return literals;
		}
	}
}
#endif
