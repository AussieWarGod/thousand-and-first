#if TAF_TESTS
using System;
using System.IO;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomVisualProofRulesTests
	{
		private const string Digest =
			"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

		[TestCase(2120)]
		[TestCase(14)]
		public void CheckpointRoundTripsBoundedHumanVerdictStates(int total)
		{
			byte[] states = KingdomVisualProofRules.Empty(total);
			states[0] = KingdomVisualProofRules.Pass;
			states[total / 2] = KingdomVisualProofRules.Fail;
			states[total - 1] = KingdomVisualProofRules.Pass;
			string encoded = KingdomVisualProofRules.EncodeCheckpoint(Digest, states);
			Assert.IsNotNull(encoded);
			Assert.Less(encoded.Length, 1024);
			Assert.IsTrue(KingdomVisualProofRules.TryDecodeCheckpoint(encoded, total, Digest,
				out byte[] decoded, out string failure), failure);
			CollectionAssert.AreEqual(states, decoded);
			KingdomVisualProofRules.Counts(decoded, out int passed, out int failed, out int open);
			Assert.AreEqual(2, passed);
			Assert.AreEqual(1, failed);
			Assert.AreEqual(total - 3, open);
			Assert.AreEqual(1, KingdomVisualProofRules.Next(decoded));
		}

		[Test]
		public void CheckpointRejectsCatalogueDriftMalformedPayloadAndUnknownVerdict()
		{
			byte[] states = KingdomVisualProofRules.Empty(14);
			string encoded = KingdomVisualProofRules.EncodeCheckpoint(Digest, states);
			string other = new string('a', 64);
			Assert.IsFalse(KingdomVisualProofRules.TryDecodeCheckpoint(encoded, 14, other,
				out _, out _));
			Assert.IsFalse(KingdomVisualProofRules.TryDecodeCheckpoint("vp1|14|" + Digest + "|!",
				14, Digest, out _, out _));
			states[3] = 3;
			Assert.IsNull(KingdomVisualProofRules.EncodeCheckpoint(Digest, states));
		}

		[Test]
		public void ScreenshotNamesAreDeterministicButNeverClaimFileExistence()
		{
			Assert.AreEqual("taf-architecture-0001.png",
				KingdomVisualProofRules.ExpectedScreenshot("architecture", 1, 2120));
			Assert.AreEqual("taf-architecture-2120.png",
				KingdomVisualProofRules.ExpectedScreenshot("architecture", 2120, 2120));
			Assert.AreEqual("taf-visual-0014.png",
				KingdomVisualProofRules.ExpectedScreenshot("visual", 14, 14));
			Assert.IsTrue(KingdomVisualProofRules.ScreenshotMatches(
				@"C:\evidence\taf-visual-0014.png", "taf-visual-0014.png"));
			Assert.IsFalse(KingdomVisualProofRules.ScreenshotMatches(
				"taf-visual-0013.png", "taf-visual-0014.png"));
		}

		[Test]
		public void EvidenceRowBindsClaimWithoutFabricatingVerdictOrCapture()
		{
			string row = KingdomVisualProofRules.EvidenceRow("visual", 4, 14, "gatehouse",
				"vg1-0123456789abcdef01234567", Digest, "pass",
				@"C:\evidence\taf-visual-0004.png", "human review");
			StringAssert.StartsWith("[TAF visual-evidence]\tschema=1\tsuite=visual", row);
			StringAssert.Contains("\treceipt=vg1-0123456789abcdef01234567", row);
			StringAssert.Contains("\tdigest=" + Digest, row);
			StringAssert.Contains("\tverdict=pass", row);
			StringAssert.Contains("\tcapture=human-asserted", row);
			Assert.IsNull(KingdomVisualProofRules.EvidenceRow("visual", 4, 14, "gatehouse",
				"vg1-0123456789abcdef01234567", Digest, "", "x.png", null));
		}

		[Test]
		public void RuntimeSourcesExposeTraversalAllSupplementalFamiliesAndNoInputAutomation()
		{
			string architecture = Read("Debug/KingdomArchitectureGalleryWishes.cs")
				+ Read("Debug/KingdomArchitectureGalleryWishes.Traversal.cs");
			foreach (string command in new string[] { "list", "status", "next", "resume", "checkpoint" })
				StringAssert.Contains("\"" + command + "\"", architecture);
			StringAssert.Contains("TryWriteArchitectureVerdict", architecture);
			StringAssert.Contains("EvidenceRow(ArchitectureSuite", architecture);

			string cases = Read("Debug/KingdomArchitectureGalleryWishes.VisualCases.cs");
			foreach (string key in new string[] { "palisade", "rampart", "watchtower", "gatehouse",
				"watermain", "brinemain", "liquidcrossing", "watertap", "brinetap", "rubblewall" })
				StringAssert.Contains("\"" + key + "\"", cases);
			foreach (string road in new string[] { "road-worn", "road-trodden", "road-path", "road-paved" })
				StringAssert.Contains("\"" + road + "\"", cases);

			string runtime = ReadVisualRuntime();
			StringAssert.Contains("KingdomRoads.Lay(cell, state, paving)", runtime);
			StringAssert.Contains("KingdomRoadRules.WornTraffic", runtime);
			StringAssert.Contains("KingdomGatehouseRules.SatelliteCount", runtime);
			StringAssert.Contains("capture=human-asserted", runtime);
			StringAssert.DoesNotContain("SendKeys", runtime);
			StringAssert.DoesNotContain("keybd_event", runtime);
			StringAssert.DoesNotContain("CopyFromScreen", runtime);
			StringAssert.DoesNotContain("verdict = \"pass\"", runtime);
		}

		[Test]
		public void EveryVisualRuntimeShardStaysBelowThreeHundredLines()
		{
			string root = TestMain.RepositoryRoot;
			foreach (string path in Directory.GetFiles(Path.Combine(root, "Debug"),
				"KingdomArchitectureGalleryWishes*.cs"))
			{
				int lines = File.ReadAllLines(path).Length;
				Assert.Less(lines, 300, Path.GetFileName(path));
			}
			Assert.Less(File.ReadAllLines(Path.Combine(root, "Debug",
				"KingdomVisualProofRules.cs")).Length, 300);
		}

		private static string ReadVisualRuntime()
		{
			string root = TestMain.RepositoryRoot;
			string result = "";
			foreach (string path in Directory.GetFiles(Path.Combine(root, "Debug"),
				"KingdomArchitectureGalleryWishes.Visual*.cs")) result += File.ReadAllText(path);
			return result + Read("Debug/KingdomVisualProofRules.cs");
		}

		private static string Read(string Path)
		{
			return TestMain.ReadRepositoryText(Path);
		}
	}
}
#endif
