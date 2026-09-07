#if TAF_TESTS
using System;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst.Harness;

namespace ThousandAndFirst.Tests
{
	public class KingdomScenarioLoadRulesTests
	{
		private const string GameId = "0f6a1c2d-9b3e-4a71-8c05-2ff9a6e1d834";
		private const string AltGameId = "abcdef01-2345-6789-abcd-ef0123456789";
		private const string Primary = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
		private const string Info = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
		private const string Cache = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
		private const string Snapshot = "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";
		private const string AltHash = "111111111111111111111111111111111111111111111111111111111111111a";

		private static string Wire(string gameId, string primary, string info, string cache,
			string snapshot)
		{
			return KingdomScenarioLoadRules.Header + "\n" + gameId + "\n" + primary + "\n" + info
				+ "\n" + cache + "\n" + snapshot + "\n";
		}

		private static KingdomScenarioLoadRequest Request(string gameId, string primary,
			string info, string cache, string snapshot)
		{
			return new KingdomScenarioLoadRequest(gameId, primary, info, cache, snapshot);
		}

		private static string BadGuid(string kind)
		{
			switch (kind)
			{
				case "uppercase": return GameId.ToUpperInvariant();
				case "braced": return "{" + GameId + "}";
				case "nform": return GameId.Replace("-", "");
				case "whitespace": return GameId.Substring(0, 8) + " " + GameId.Substring(9);
				default: throw new ArgumentException(kind);
			}
		}

		private static string BadHash(string kind)
		{
			switch (kind)
			{
				case "uppercase": return Primary.ToUpperInvariant();
				case "tooShort": return Primary.Substring(0, 63);
				case "tooLong": return Primary + "a";
				case "signedPlus": return "+" + Primary.Substring(1);
				case "signedMinus": return "-" + Primary.Substring(1);
				case "whitespaceInside": return Primary.Substring(0, 32) + " " + Primary.Substring(33);
				case "leadingSpace": return " " + Primary.Substring(1);
				case "trailingSpace": return Primary.Substring(0, 63) + " ";
				case "forwardSlash": return Primary.Substring(0, 32) + "/" + Primary.Substring(33);
				case "backslash": return Primary.Substring(0, 32) + "\\" + Primary.Substring(33);
				case "controlChar": return Primary.Substring(0, 32) + "\a" + Primary.Substring(33);
				case "unpairedSurrogate":
					return Primary.Substring(0, 32) + "\uD800" + Primary.Substring(33);
				default: throw new ArgumentException(kind);
			}
		}

		[Test]
		public void ValidRequestIsValid()
		{
			foreach (string hash in new[] { Primary, Info, Cache, Snapshot, AltHash })
				ClassicAssert.AreEqual(64, hash.Length, "positive SHA-256 fixture length");
			ClassicAssert.IsTrue(KingdomScenarioLoadRules.Valid(Request(GameId, Primary, Info, Cache,
				Snapshot)));
		}

		[Test]
		public void ValidRefusesNullRequest()
		{
			ClassicAssert.IsFalse(KingdomScenarioLoadRules.Valid(null));
		}

		[Test]
		public void RoundTripFromRequestThroughEncodeAndParseReproducesTheWireAndEveryField()
		{
			KingdomScenarioLoadRequest original = Request(GameId, Primary, Info, Cache, Snapshot);
			ClassicAssert.IsTrue(KingdomScenarioLoadRules.TryEncode(original, out string wire));
			ClassicAssert.IsNotNull(wire);
			ClassicAssert.AreEqual(Wire(GameId, Primary, Info, Cache, Snapshot), wire);
			ClassicAssert.IsTrue(KingdomScenarioLoadRules.TryParse(wire, out KingdomScenarioLoadRequest parsed));
			ClassicAssert.IsNotNull(parsed);
			ClassicAssert.AreNotSame(original, parsed);
			ClassicAssert.AreEqual(GameId, parsed.GameId);
			ClassicAssert.AreEqual(Primary, parsed.PrimarySha256);
			ClassicAssert.AreEqual(Info, parsed.InfoSha256);
			ClassicAssert.AreEqual(Cache, parsed.CacheSha256);
			ClassicAssert.AreEqual(Snapshot, parsed.SnapshotSha256);
			ClassicAssert.IsTrue(KingdomScenarioLoadRules.TryEncode(parsed, out string reEncoded));
			ClassicAssert.AreEqual(wire, reEncoded);
		}

		[Test]
		public void GameIdIsPreservedThroughParse()
		{
			ClassicAssert.IsTrue(KingdomScenarioLoadRules.TryParse(
				Wire(AltGameId, Primary, Info, Cache, Snapshot),
				out KingdomScenarioLoadRequest parsed));
			ClassicAssert.AreEqual(AltGameId, parsed.GameId);
			ClassicAssert.AreEqual(Primary, parsed.PrimarySha256);
			ClassicAssert.AreEqual(Info, parsed.InfoSha256);
			ClassicAssert.AreEqual(Cache, parsed.CacheSha256);
			ClassicAssert.AreEqual(Snapshot, parsed.SnapshotSha256);
		}

		[Test]
		public void PrimarySha256IsPreservedThroughParse()
		{
			ClassicAssert.IsTrue(KingdomScenarioLoadRules.TryParse(
				Wire(GameId, AltHash, Info, Cache, Snapshot),
				out KingdomScenarioLoadRequest parsed));
			ClassicAssert.AreEqual(GameId, parsed.GameId);
			ClassicAssert.AreEqual(AltHash, parsed.PrimarySha256);
			ClassicAssert.AreEqual(Info, parsed.InfoSha256);
			ClassicAssert.AreEqual(Cache, parsed.CacheSha256);
			ClassicAssert.AreEqual(Snapshot, parsed.SnapshotSha256);
		}

		[Test]
		public void InfoSha256IsPreservedThroughParse()
		{
			ClassicAssert.IsTrue(KingdomScenarioLoadRules.TryParse(
				Wire(GameId, Primary, AltHash, Cache, Snapshot),
				out KingdomScenarioLoadRequest parsed));
			ClassicAssert.AreEqual(GameId, parsed.GameId);
			ClassicAssert.AreEqual(Primary, parsed.PrimarySha256);
			ClassicAssert.AreEqual(AltHash, parsed.InfoSha256);
			ClassicAssert.AreEqual(Cache, parsed.CacheSha256);
			ClassicAssert.AreEqual(Snapshot, parsed.SnapshotSha256);
		}

		[Test]
		public void CacheSha256IsPreservedThroughParse()
		{
			ClassicAssert.IsTrue(KingdomScenarioLoadRules.TryParse(
				Wire(GameId, Primary, Info, AltHash, Snapshot),
				out KingdomScenarioLoadRequest parsed));
			ClassicAssert.AreEqual(GameId, parsed.GameId);
			ClassicAssert.AreEqual(Primary, parsed.PrimarySha256);
			ClassicAssert.AreEqual(Info, parsed.InfoSha256);
			ClassicAssert.AreEqual(AltHash, parsed.CacheSha256);
			ClassicAssert.AreEqual(Snapshot, parsed.SnapshotSha256);
		}

		[Test]
		public void SnapshotSha256IsPreservedThroughParse()
		{
			ClassicAssert.IsTrue(KingdomScenarioLoadRules.TryParse(
				Wire(GameId, Primary, Info, Cache, AltHash),
				out KingdomScenarioLoadRequest parsed));
			ClassicAssert.AreEqual(GameId, parsed.GameId);
			ClassicAssert.AreEqual(Primary, parsed.PrimarySha256);
			ClassicAssert.AreEqual(Info, parsed.InfoSha256);
			ClassicAssert.AreEqual(Cache, parsed.CacheSha256);
			ClassicAssert.AreEqual(AltHash, parsed.SnapshotSha256);
		}

		[TestCase("uppercase")]
		[TestCase("braced")]
		[TestCase("nform")]
		[TestCase("whitespace")]
		public void ValidRefusesMalformedGameId(string kind)
		{
			KingdomScenarioLoadRequest request = Request(BadGuid(kind), Primary, Info, Cache,
				Snapshot);
			ClassicAssert.IsFalse(KingdomScenarioLoadRules.Valid(request));
		}

		[TestCase("uppercase")]
		[TestCase("braced")]
		[TestCase("nform")]
		[TestCase("whitespace")]
		public void ParseRefusesMalformedGameIdLine(string kind)
		{
			ClassicAssert.IsFalse(KingdomScenarioLoadRules.TryParse(
				Wire(BadGuid(kind), Primary, Info, Cache, Snapshot),
				out KingdomScenarioLoadRequest parsed));
			ClassicAssert.IsNull(parsed);
		}

		[TestCase("uppercase")]
		[TestCase("tooShort")]
		[TestCase("tooLong")]
		[TestCase("signedPlus")]
		[TestCase("signedMinus")]
		[TestCase("whitespaceInside")]
		[TestCase("leadingSpace")]
		[TestCase("trailingSpace")]
		[TestCase("forwardSlash")]
		[TestCase("backslash")]
		[TestCase("controlChar")]
		[TestCase("unpairedSurrogate")]
		public void ValidRefusesMalformedPrimaryHash(string kind)
		{
			KingdomScenarioLoadRequest request = Request(GameId, BadHash(kind), Info, Cache,
				Snapshot);
			ClassicAssert.IsFalse(KingdomScenarioLoadRules.Valid(request));
		}

		[TestCase("uppercase")]
		[TestCase("tooShort")]
		[TestCase("tooLong")]
		[TestCase("signedPlus")]
		[TestCase("signedMinus")]
		[TestCase("whitespaceInside")]
		[TestCase("leadingSpace")]
		[TestCase("trailingSpace")]
		[TestCase("forwardSlash")]
		[TestCase("backslash")]
		[TestCase("controlChar")]
		[TestCase("unpairedSurrogate")]
		public void ParseRefusesMalformedPrimaryHashLine(string kind)
		{
			ClassicAssert.IsFalse(KingdomScenarioLoadRules.TryParse(
				Wire(GameId, BadHash(kind), Info, Cache, Snapshot),
				out KingdomScenarioLoadRequest parsed));
			ClassicAssert.IsNull(parsed);
		}

		[TestCase("info")]
		[TestCase("cache")]
		[TestCase("snapshot")]
		public void ValidRefusesMalformedHashInEveryField(string field)
		{
			string bad = BadHash("uppercase");
			KingdomScenarioLoadRequest request;
			switch (field)
			{
				case "info": request = Request(GameId, Primary, bad, Cache, Snapshot); break;
				case "cache": request = Request(GameId, Primary, Info, bad, Snapshot); break;
				default: request = Request(GameId, Primary, Info, Cache, bad); break;
			}
			ClassicAssert.IsFalse(KingdomScenarioLoadRules.Valid(request));
		}

		[TestCase("null")]
		[TestCase("empty")]
		[TestCase("cr")]
		[TestCase("missingFinalLf")]
		[TestCase("extraTrailingLf")]
		[TestCase("fiveLines")]
		[TestCase("sevenLines")]
		[TestCase("duplicateHeaderLine")]
		[TestCase("wrongHeaderCase")]
		[TestCase("wrongHeaderTypo")]
		[TestCase("bomPrefix")]
		[TestCase("overMaxChars")]
		public void ParseRefusesMalformedWireStructure(string kind)
		{
			string text;
			switch (kind)
			{
				case "null": text = null; break;
				case "empty": text = ""; break;
				case "cr": text = Wire(GameId, Primary, Info, Cache, Snapshot).Replace("\n", "\r\n"); break;
				case "missingFinalLf":
					text = Wire(GameId, Primary, Info, Cache, Snapshot).TrimEnd('\n'); break;
				case "extraTrailingLf": text = Wire(GameId, Primary, Info, Cache, Snapshot) + "\n"; break;
				case "fiveLines":
					text = KingdomScenarioLoadRules.Header + "\n" + GameId + "\n" + Primary + "\n"
						+ Info + "\n" + Cache + "\n";
					break;
				case "sevenLines":
					text = Wire(GameId, Primary, Info, Cache, Snapshot) + Snapshot + "\n"; break;
				case "duplicateHeaderLine":
					text = KingdomScenarioLoadRules.Header + "\n" + KingdomScenarioLoadRules.Header
						+ "\n" + Primary + "\n" + Info + "\n" + Cache + "\n" + Snapshot + "\n";
					break;
				case "wrongHeaderCase":
					text = KingdomScenarioLoadRules.Header.ToUpperInvariant() + "\n" + GameId + "\n"
						+ Primary + "\n" + Info + "\n" + Cache + "\n" + Snapshot + "\n";
					break;
				case "wrongHeaderTypo":
					text = KingdomScenarioLoadRules.Header.Substring(0,
						KingdomScenarioLoadRules.Header.Length - 1) + "2\n" + GameId + "\n" + Primary
						+ "\n" + Info + "\n" + Cache + "\n" + Snapshot + "\n";
					break;
				case "bomPrefix": text = "\uFEFF" + Wire(GameId, Primary, Info, Cache, Snapshot); break;
				case "overMaxChars":
					text = Wire(GameId, Primary, Info, Cache, Snapshot + new string('9', 200)); break;
				default: throw new ArgumentException(kind);
			}
			ClassicAssert.IsFalse(KingdomScenarioLoadRules.TryParse(text, out KingdomScenarioLoadRequest parsed),
				kind);
			ClassicAssert.IsNull(parsed, kind);
		}

		[Test]
		public void EncodeRefusesNullRequest()
		{
			ClassicAssert.IsFalse(KingdomScenarioLoadRules.TryEncode(null, out string wire));
			ClassicAssert.IsNull(wire);
		}

		[Test]
		public void EncodeRefusesRequestWithInvalidField()
		{
			KingdomScenarioLoadRequest request = Request(BadGuid("uppercase"), Primary, Info, Cache,
				Snapshot);
			ClassicAssert.IsFalse(KingdomScenarioLoadRules.TryEncode(request, out string wire));
			ClassicAssert.IsNull(wire);
		}
	}
}
#endif
