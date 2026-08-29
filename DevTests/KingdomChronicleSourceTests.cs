#if TAF_TESTS
using System;
using System.IO;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	public class KingdomChronicleSourceTests
	{
		private static string Source(string relative)
		{
			if (relative == Path.Combine("Chronicle", "KingdomChronicle.cs"))
				return KingdomChronicleLogicalSource.Read();
			if (relative == Path.Combine("Chronicle", "KingdomChronicleReceiptRules.cs"))
				return KingdomChronicleReceiptRulesLogicalSource.Read();
			return TestMain.ReadRepositoryText(relative);
		}

		[Test]
		public void ParserCapsRawRowsAndFieldsBeforeAllocation()
		{
			string rules = Source(Path.Combine("Chronicle",
				"KingdomChronicleReceiptRules.cs"));
			int parser = rules.IndexOf("TryParseRegistry(string Text", StringComparison.Ordinal);
			int rawCap = rules.IndexOf("Text.Length > MaxRegistryChars", parser,
				StringComparison.Ordinal);
			int firstSplit = rules.IndexOf("Text.Split('\\n')", parser,
				StringComparison.Ordinal);
			Assert.Greater(rawCap, parser);
			Assert.Greater(firstSplit, rawCap,
				"raw state must be bounded before any row-array allocation");

			int rowParser = rules.IndexOf("TryParseV3Row", firstSplit,
				StringComparison.Ordinal);
			int separatorCap = rules.IndexOf("Count(Line, '|')", rowParser,
				StringComparison.Ordinal);
			int fieldSplit = rules.IndexOf("Line.Split('|')", separatorCap,
				StringComparison.Ordinal);
			Assert.Greater(separatorCap, rowParser);
			Assert.Greater(fieldSplit, separatorCap);

			int decoder = rules.IndexOf("private static bool Decode", StringComparison.Ordinal);
			int encodedCap = rules.IndexOf("Value.Length > MaxEncodedChars", decoder,
				StringComparison.Ordinal);
			int base64 = rules.IndexOf("Convert.FromBase64String", decoder,
				StringComparison.Ordinal);
			Assert.Greater(encodedCap, decoder);
			Assert.Greater(base64, encodedCap,
				"encoded text must be bounded before base64 allocation");
		}

		[Test]
		public void RegistryNeverEvictsReplayProofAndCapacityIsVisible()
		{
			string shell = Source(Path.Combine("Chronicle", "KingdomChronicle.cs"));
			string rules = Source(Path.Combine("Chronicle",
				"KingdomChronicleReceiptRules.cs"));
			StringAssert.Contains("public const int MaxReceipts = 4096;", rules);
			StringAssert.Contains("rows.Count >= KingdomChronicleReceiptRules.MaxReceipts", shell);
			StringAssert.Contains("ReportFault(KingdomChronicleRegistryFault.TooManyRows, \"capacity\", true)",
				shell);
			StringAssert.Contains("no replay receipt was discarded", shell);
			Assert.IsFalse(shell.Contains("rows.RemoveAt("));
			Assert.IsFalse(shell.Contains("Rows.RemoveAt("));
			StringAssert.Contains("if (separators > MaxReceipts)", rules);
		}

		[Test]
		public void BothRegisterPathsUseTheRootPreservingBoundedAppend()
		{
			string telling = Source(Path.Combine("Chronicle", "KingdomChronicle.Telling.cs"));
			string rules = Source(Path.Combine("Chronicle",
				"KingdomChronicleReceiptRules.cs"));
			Assert.AreEqual(2, Count(telling,
				"KingdomChronicleReceiptRules.AppendBounded("));
			StringAssert.DoesNotContain("RemoveAt(0)", telling);
			StringAssert.Contains("Values.RemoveAt(Values.Count > 1 ? 1 : 0)", rules);
		}

		[Test]
		public void HashesAreCanonicalLengthPrefixedSha256Only()
		{
			string shell = Source(Path.Combine("Chronicle", "KingdomChronicle.cs"));
			string rules = Source(Path.Combine("Chronicle",
				"KingdomChronicleReceiptRules.cs"));
			StringAssert.Contains("using System.Security.Cryptography;", rules);
			StringAssert.Contains("SHA256.Create()", rules);
			StringAssert.Contains("WriteUInt32(bytes, (uint)Fields.Count)", rules);
			StringAssert.Contains("WriteField(bytes, Fields[i])", rules);
			StringAssert.Contains("taf-chronicle-fingerprint-v3", rules);
			StringAssert.Contains("taf-chronicle-list-v3:", rules);
			Assert.IsFalse(rules.Contains("14695981039346656037"));
			Assert.IsFalse(shell.Contains("14695981039346656037"));
			Assert.IsFalse(shell.Contains("private static void Fold"));
		}

		[Test]
		public void ListRecoveryUsesExactAfterBeforeOrLostOrder()
		{
			string shell = Source(Path.Combine("Chronicle", "KingdomChronicle.cs"));
			int delivery = shell.IndexOf("private static bool DeliverList",
				StringComparison.Ordinal);
			int action = shell.IndexOf("KingdomChronicleReceiptRules.ListAction", delivery,
				StringComparison.Ordinal);
			int confirm = shell.IndexOf("KingdomChronicleListAction.ConfirmDelivered", action,
				StringComparison.Ordinal);
			int intent = shell.IndexOf("KingdomChronicleSinkDisposition.Attempting", confirm,
				StringComparison.Ordinal);
			int persistIntent = shell.IndexOf("register + \"-intent\"", intent,
				StringComparison.Ordinal);
			int append = shell.IndexOf("AppendBounded(Values, value)", persistIntent,
				StringComparison.Ordinal);
			Assert.Greater(action, delivery);
			Assert.Greater(confirm, action, "exact after confirms first");
			Assert.Greater(persistIntent, intent, "intent persists before append");
			Assert.Greater(append, persistIntent);
			StringAssert.Contains("KingdomChronicleListAction.MarkLost",
				Source(Path.Combine("Chronicle", "KingdomChronicleReceiptRules.cs")));
			StringAssert.Contains("return LoseList", shell.Substring(delivery,
				append - delivery));
		}

		[Test]
		public void JournalAttemptRecoversByExactIdWithoutRepeatingAndOptionOffSkips()
		{
			string shell = Source(Path.Combine("Chronicle", "KingdomChronicle.cs"));
			int journal = shell.IndexOf("private static bool DeliverJournal",
				StringComparison.Ordinal);
			int attempting = shell.IndexOf(
				"state == KingdomChronicleSinkDisposition.Attempting", journal,
				StringComparison.Ordinal);
			int observed = shell.IndexOf(
				"CountJournalAccomplishments(Receipt.EventId)", attempting,
				StringComparison.Ordinal);
			int recovered = shell.IndexOf(
				"? KingdomChronicleSinkDisposition.Delivered", observed,
				StringComparison.Ordinal);
			int option = shell.IndexOf("GetOption(\"r_TAF_OptionChronicle\")", recovered,
				StringComparison.Ordinal);
			int skipped = shell.IndexOf(
				"Receipt.JournalState = KingdomChronicleSinkDisposition.Skipped", option,
				StringComparison.Ordinal);
			int callback = shell.IndexOf("JournalAPI.AddAccomplishment", skipped,
				StringComparison.Ordinal);
			Assert.Greater(observed, attempting);
			Assert.Greater(recovered, observed);
			Assert.Greater(option, recovered,
				"reloaded Attempting must settle from exact ID before option/callback path");
			Assert.Greater(skipped, option);
			Assert.Greater(callback, skipped,
				"option-off Skipped must persist without calling journal API");
			StringAssert.Contains("journal-intent", shell.Substring(skipped,
				callback - skipped));
		}

		[Test]
		public void JournalProjectionUsesExactIdentityGospelAndBoundedCodaShare()
		{
			string shell = Source(Path.Combine("Chronicle", "KingdomChronicle.cs"));
			StringAssert.Contains("internal const int MaxCodaEligibleAccomplishments = 3;", shell);
			StringAssert.Contains("row.ID.StartsWith(\"taf:\", StringComparison.Ordinal)", shell);
			StringAssert.Contains("GospelText = \"In =year=, =name= \" + clause + \".\";", shell);
			StringAssert.Contains("projectedMural, gospelText, null, \"general\"", shell);
			StringAssert.Contains("weight, Receipt.EventId, -1L", shell);
			StringAssert.Contains("CountJournalAccomplishments(Receipt.EventId) == 1", shell);
		}

		[Test]
		public void ConstructionTombstoneIsExactAndLegacyUnpinsOnlyConstruction()
		{
			string shell = Source(Path.Combine("Chronicle", "KingdomChronicle.cs"));
			string rules = Source(Path.Combine("Chronicle",
				"KingdomChronicleReceiptRules.cs"));
			StringAssert.Contains("return \"tc|\" + job + \"|\" + Encode(coordinate)", rules);
			StringAssert.Contains("TryConstructionIdentity", rules);
			Assert.IsFalse(rules.Contains("Bloom"));
			Assert.IsFalse(rules.Contains("BitArray"));
			int legacy = shell.IndexOf("receipt != null && receipt.LegacyBlocked",
				StringComparison.Ordinal);
			int construction = shell.IndexOf("TryConstructionIdentity(EventId", legacy,
				StringComparison.Ordinal);
			int settle = shell.IndexOf("return true;", construction,
				StringComparison.Ordinal);
			int genericRefusal = shell.IndexOf("legacy-replay-blocked", settle,
				StringComparison.Ordinal);
			int refuse = shell.IndexOf("return false;", genericRefusal,
				StringComparison.Ordinal);
			Assert.Greater(settle, construction);
			Assert.Greater(genericRefusal, settle);
			Assert.Greater(refuse, genericRefusal);
		}

		[Test]
		public void UnknownVersionAndMalformedLegacyCannotBeOverwritten()
		{
			string shell = Source(Path.Combine("Chronicle", "KingdomChronicle.cs"));
			string rules = Source(Path.Combine("Chronicle",
				"KingdomChronicleReceiptRules.cs"));
			StringAssert.Contains("KingdomChronicleRegistryFault.UnknownVersion", rules);
			StringAssert.Contains("LegacyBlocked = true", rules);
			StringAssert.Contains("Fingerprint = Fingerprint == \"-\" ? null", rules);
			int parse = shell.IndexOf("TryParseRegistry(raw", StringComparison.Ordinal);
			int refusal = shell.IndexOf("return false;", parse, StringComparison.Ordinal);
			int migrationWrite = shell.IndexOf("legacy-migration", refusal,
				StringComparison.Ordinal);
			Assert.Greater(refusal, parse);
			Assert.Greater(migrationWrite, refusal,
				"parse failure returns before any registry migration write");
		}

		private static int Count(string value, string token)
		{
			int count = 0;
			for (int at = 0; (at = value.IndexOf(token, at,
				StringComparison.Ordinal)) >= 0; at += token.Length) count++;
			return count;
		}
	}
}
#endif
