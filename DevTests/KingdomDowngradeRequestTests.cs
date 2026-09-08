#if TAF_TESTS
using System;
using System.Globalization;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst.Harness;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomDowngradeRequestTests
	{
		private const string Root = @"C:\taf-scenario.Test01";
		private const string Origin = "origin-game_01";
		private const string Source = "d2097d086f65e81c4d84c1a35d7d20b6f606c489";
		private static readonly string Hash = new string('a', 64);
		private static string Present(char name, int count = 100) => name + " " + count.ToString(CultureInfo.InvariantCulture) + " " + Hash;
		private static string Wire(string a = null, string b = "b absent") =>
			KingdomDowngradeRequest.Header + "\n" + Root + "\n" + Origin + "\n" + Source + "\n"
			+ KingdomDowngradeRequest.OldPin + "\n" + (a ?? Present('a')) + "\n" + b + "\n";

		[TestCase("a")]
		[TestCase("b")]
		[TestCase("both")]
		public void PresentAndAbsentSlotsRemainExactlyAsDeclared(string which)
		{
			string wire = Wire(which == "b" ? "a absent" : Present('a'), which == "a" ? "b absent" : Present('b'));
			ClassicAssert.IsTrue(KingdomDowngradeRequest.TryParse(wire, out var request));
			ClassicAssert.AreEqual(wire, request.Compose());
			ClassicAssert.AreEqual(Root, request.Root); ClassicAssert.AreEqual(Origin, request.Origin);
			ClassicAssert.AreEqual(Source, request.SourcePin);
			for (int i = 0; i < 2; i++)
			{
				var slot = request.At(i); bool present = which == "both" || which == (i == 0 ? "a" : "b");
				ClassicAssert.AreEqual(i == 0 ? 'a' : 'b', slot.Name);
				ClassicAssert.AreEqual(present, slot.Present);
				ClassicAssert.AreEqual(present ? 100 : 0, slot.Count);
				ClassicAssert.AreEqual(present ? Hash : null, slot.Sha256);
			}
		}
		[TestCase(1)]
		[TestCase(262144)]
		public void SlotCountBoundsAreInclusive(int count)
		{
			ClassicAssert.IsTrue(KingdomDowngradeRequest.TryParse(Wire(Present('a', count)), out var request));
			ClassicAssert.AreEqual(count, request.At(0).Count);
		}
		[Test]
		public void SourcePinIsExactHostDeclarationNotHardcodedCandidateAuthority()
		{
			string later = new string('b', 40), wire = Wire().Replace(Source, later);
			ClassicAssert.IsTrue(KingdomDowngradeRequest.TryParse(wire, out var request));
			ClassicAssert.AreEqual(later, request.SourcePin); ClassicAssert.AreEqual(wire, request.Compose());
		}
		[TestCase(-1)]
		[TestCase(2)]
		public void UnknownSlotIndexDoesNotAliasExistingSlot(int index)
		{
			ClassicAssert.IsTrue(KingdomDowngradeRequest.TryParse(Wire(), out var request));
			Assert.Throws<ArgumentOutOfRangeException>(() => request.At(index));
		}

		[TestCase("null")]
		[TestCase("header")]
		[TestCase("drive")]
		[TestCase("lower-drive")]
		[TestCase("nested-root")]
		[TestCase("root-trailing")]
		[TestCase("root-empty-suffix")]
		[TestCase("origin-empty")]
		[TestCase("origin-long")]
		[TestCase("origin-path")]
		[TestCase("source-short")]
		[TestCase("source-upper")]
		[TestCase("old-pin")]
		[TestCase("extra-row")]
		[TestCase("missing-lf")]
		[TestCase("crlf")]
		[TestCase("bom")]
		[TestCase("duplicate-slot")]
		[TestCase("reordered-slots")]
		[TestCase("both-absent")]
		[TestCase("zero-count")]
		[TestCase("negative-count")]
		[TestCase("plus-count")]
		[TestCase("leading-zero")]
		[TestCase("overflow-count")]
		[TestCase("over-cap")]
		[TestCase("hash-short")]
		[TestCase("hash-upper")]
		[TestCase("hash-nonhex")]
		[TestCase("absent-with-hash")]
		[TestCase("extra-space")]
		[TestCase("tab")]
		[TestCase("nul")]
		[TestCase("unicode")]
		[TestCase("overlong")]
		public void MalformedManifestRefusesWithoutPartialRequest(string damage)
		{
			string wire = Wire();
			switch (damage)
			{
				case "null": wire = null; break;
				case "header": wire = wire.Replace(KingdomDowngradeRequest.Header, "taf-downgrade-observe-v2"); break;
				case "drive": wire = wire.Replace(Root, @"D:\taf-scenario.Test01"); break;
				case "lower-drive": wire = wire.Replace(Root, @"c:\taf-scenario.Test01"); break;
				case "nested-root": wire = wire.Replace(Root, Root + @"\other"); break;
				case "root-trailing": wire = wire.Replace(Root, Root + "\\"); break;
				case "root-empty-suffix": wire = wire.Replace(Root, @"C:\taf-scenario."); break;
				case "origin-empty": wire = wire.Replace(Origin, ""); break;
				case "origin-long": wire = wire.Replace(Origin, new string('x', 97)); break;
				case "origin-path": wire = wire.Replace(Origin, "../origin"); break;
				case "source-short": wire = wire.Replace(Source, Source.Substring(1)); break;
				case "source-upper": wire = wire.Replace(Source, Source.ToUpperInvariant()); break;
				case "old-pin": wire = wire.Replace(KingdomDowngradeRequest.OldPin, Source); break;
				case "extra-row": wire += "extra\n"; break;
				case "missing-lf": wire = wire.Substring(0, wire.Length - 1); break;
				case "crlf": wire = wire.Replace("\n", "\r\n"); break;
				case "bom": wire = "\uFEFF" + wire; break;
				case "duplicate-slot": wire = Wire(Present('a'), Present('a')); break;
				case "reordered-slots": wire = Wire(Present('b'), Present('a')); break;
				case "both-absent": wire = Wire("a absent"); break;
				case "zero-count": wire = Wire(Present('a', 0)); break;
				case "negative-count": wire = Wire(Present('a', -1)); break;
				case "plus-count": wire = Wire("a +100 " + Hash); break;
				case "leading-zero": wire = Wire("a 0100 " + Hash); break;
				case "overflow-count": wire = Wire("a 2147483648 " + Hash); break;
				case "over-cap": wire = Wire(Present('a', 262145)); break;
				case "hash-short": wire = Wire("a 100 " + Hash.Substring(1)); break;
				case "hash-upper": wire = Wire("a 100 " + Hash.ToUpperInvariant()); break;
				case "hash-nonhex": wire = Wire("a 100 " + new string('g', 64)); break;
				case "absent-with-hash": wire = Wire("a absent " + Hash); break;
				case "extra-space": wire = Wire("a  100 " + Hash); break;
				case "tab": wire = wire.Replace("a 100", "a\t100"); break;
				case "nul": wire += "\0"; break;
				case "unicode": wire = wire.Replace(Origin, "origin-é"); break;
				case "overlong": wire = new string('a', KingdomDowngradeRequest.MaximumRequestBytes + 1); break;
				default: throw new ArgumentException(damage);
			}
			ClassicAssert.IsFalse(KingdomDowngradeRequest.TryParse(wire, out var request), damage);
			ClassicAssert.IsNull(request, damage);
		}
		[Test]
		public void NativeEntryIsMainMenuOnlyAndCannotCreateLoadOrRepairAGame()
		{
			string source = TestMain.ReadRepositoryText("Harness/KingdomDowngradeProbe.cs");
			foreach (string token in new[] { "[HarmonyPatch(typeof(MainMenu), \"Show\", new Type[] { })]",
				"[HarmonyPostfix]", "Interlocked.Exchange(ref Consumed, 1)", "The.Game == null",
				"KingdomDowngradeRequest.FileName", "old-runtime-authority=host-inventory",
				"scenario-script.txt", "scenario-load.txt", "scenario-load-snapshot.txt",
				"KingdomSealRecord.TryParse(texts[i]", "fault == KingdomSealFault.OutOfBounds",
				"EmptyCampShape(body, report);", "body.Text(\"status\") == \"living\"",
				"body.Number(\"stage\") == 0 && body.Number(\"people\") == 0",
				"bodies.Count == 1 && bodies[0] == \"unresolved\"",
				"KingdomDowngradeRequest.Hex(body.Text(\"source_profile_digest\"), 64)",
				"KingdomDowngradeRequest.Hex(body.Text(\"profile_provenance_digest\"), 64)",
				"'profile_schema' is 2, outside 0 to 1", "store.ReadStage(request.Origin)",
				"Require(rejected > 0", "any == (selected != null)", "selectedText == texts[0]",
				"selectedText == texts[1]", "catch (Exception ex)" }) StringAssert.Contains(token, source);
			foreach (string token in new[] { "LoadGame(", "NewGame(", "SelectedInfo(", "Popup.", "SetOption(",
				"TryStage(", "TryReconcile", "TryReserve", "TryRelease", "RequireSystem<", "HarmonyPrefix",
				"SetStringGameState(", "SetObjectGameState(", "KingdomScenarioVerbs.Invoke" })
				StringAssert.DoesNotContain(token, source);
			int proof = source.IndexOf("VerifyAfterReport(request, requestHash, report);", StringComparison.Ordinal);
			ClassicAssert.GreaterOrEqual(proof, 0);
			ClassicAssert.Greater(source.IndexOf("MetricsManager.LogInfo(", StringComparison.Ordinal), proof);
		}
		[Test]
		public void InputCustodyAndReportReadbackPrecedeNativePositive()
		{
			string files = TestMain.ReadRepositoryText("Harness/KingdomDowngradeFiles.cs");
			foreach (string token in new[] { "FileMode.Open, FileAccess.Read, FileShare.Read", "identity.Links == 1",
				"GetFileInformationByHandle", "0x80000000, 1", "file.Stream.SafeFileHandle",
				"file.Identity, ReadIdentity(named.SafeFileHandle)", "Hash(Bytes(file.Stream)) == file.Hash",
				"FileMode.CreateNew, FileAccess.Write, FileShare.None", "report.Flush(true)", "failure = failure ?? ex" })
				StringAssert.Contains(token, files);
			foreach (string token in new[] { "File.Delete", "File.Move", "File.Copy", "Directory.CreateDirectory", "FileMode.Create," })
				StringAssert.DoesNotContain(token, files);
			string probe = TestMain.ReadRepositoryText("Harness/KingdomDowngradeProbe.cs");
			StringAssert.Contains("echo == report && reportHash == KingdomDowngradeFiles.HashText(report)", probe);
			StringAssert.Contains("Inputs(files, request); files.Reprove(); Inventory(request); Owner(request.Root);", probe);
		}
	}
}
#endif
