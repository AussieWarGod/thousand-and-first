#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	public class KingdomRealmChronicleIntentRulesTests
	{
		private const string Hash =
			"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

		private static KingdomRealmChronicleIntent Current()
		{
			string fingerprint;
			Assert.IsTrue(KingdomChronicleReceiptRules.TryDisputedFingerprint(
				"taf:realm:exile:v1:realm", "On 1 Uulu Ut, the city cast you out.",
				"Some say Kaviir fled the city.", false, null, out fingerprint));
			return new KingdomRealmChronicleIntent
			{
				Version = KingdomRealmChronicleIntentRules.CurrentVersion,
				EventId = "taf:realm:exile:v1:realm",
				OfficialText = "the city cast you out",
				OutsiderText = "Kaviir fled the city",
				Accomplishment = false,
				Fingerprint = fingerprint,
				RegistryHash = Hash,
				OtherRegistryHash = Hash,
				OfficialBefore = Hash,
				OfficialAfter = Hash,
				OutsiderBefore = Hash,
				OutsiderAfter = Hash,
				Official = "On 1 Uulu Ut, the city cast you out.",
				Outsider = "Some say Kaviir fled the city.",
				RegistryFault = ""
			};
		}

		[Test]
		public void DisputedFingerprintBindsBothRenderedAccounts()
		{
			string baseline, officialChanged, outsiderChanged;
			Assert.IsTrue(KingdomChronicleReceiptRules.TryDisputedFingerprint("event",
				"official", "outsider", true, null, out baseline));
			Assert.IsTrue(KingdomChronicleReceiptRules.TryDisputedFingerprint("event",
				"official changed", "outsider", true, null, out officialChanged));
			Assert.IsTrue(KingdomChronicleReceiptRules.TryDisputedFingerprint("event",
				"official", "outsider changed", true, null, out outsiderChanged));
			Assert.AreNotEqual(baseline, officialChanged);
			Assert.AreNotEqual(baseline, outsiderChanged);
			Assert.AreNotEqual(officialChanged, outsiderChanged);
		}

		[Test]
		public void CurrentIntentRoundTripsExactCounterHistory()
		{
			KingdomRealmChronicleIntent source = Current();
			Assert.IsTrue(KingdomRealmChronicleIntentRules.TryEncode(source, out string wire));
			StringAssert.StartsWith(KingdomRealmChronicleIntentRules.CurrentPrefix + "|", wire);
			Assert.IsTrue(KingdomRealmChronicleIntentRules.TryDecodeCurrent(wire,
				source.EventId, out KingdomRealmChronicleIntent restored));
			Assert.AreEqual(source.Fingerprint, restored.Fingerprint);
			Assert.AreEqual(source.OfficialText, restored.OfficialText);
			Assert.AreEqual(source.OutsiderText, restored.OutsiderText);
			Assert.AreEqual(source.Official, restored.Official);
			Assert.AreEqual(source.Outsider, restored.Outsider);
			Assert.IsFalse(restored.Accomplishment,
				"realm disputes belong to TAF Chronicle, not the vanilla Journal");
			Assert.IsNull(restored.MuralText);
		}

		[Test]
		public void CurrentIntentRejectsEitherAccountChangedUnderOldFingerprint()
		{
			KingdomRealmChronicleIntent source = Current();
			source.Outsider = "A different road account.";
			Assert.IsFalse(KingdomRealmChronicleIntentRules.TryEncode(source, out string wire));
			Assert.IsNull(wire);
			source = Current();
			source.Official = "A different official account.";
			Assert.IsFalse(KingdomRealmChronicleIntentRules.TryEncode(source, out wire));
		}

		[Test]
		public void DisputedFingerprintSurvivesRegistryCompactionAndReload()
		{
			KingdomRealmChronicleIntent intent = Current();
			KingdomChronicleReceipt receipt = new KingdomChronicleReceipt
			{
				EventId = intent.EventId,
				Fingerprint = intent.Fingerprint,
				OfficialState = KingdomChronicleSinkDisposition.Delivered,
				OutsiderState = KingdomChronicleSinkDisposition.Delivered,
				JournalState = KingdomChronicleSinkDisposition.Skipped,
				Updated = 17L,
				Compact = true
			};
			Assert.IsTrue(KingdomChronicleReceiptRules.TryWriteRegistry(
				new List<KingdomChronicleReceipt> { receipt }, out string wire,
				out KingdomChronicleRegistryFault fault), fault.ToString());
			Assert.IsTrue(KingdomChronicleReceiptRules.TryParseRegistry(wire,
				out List<KingdomChronicleReceipt> restored, out bool migrated, out fault),
				fault.ToString());
			Assert.IsFalse(migrated);
			Assert.AreEqual(1, restored.Count);
			Assert.IsTrue(restored[0].Compact);
			Assert.AreEqual(intent.EventId, restored[0].EventId);
			Assert.AreEqual(intent.Fingerprint, restored[0].Fingerprint);
		}

		[Test]
		public void LegacyV2IntentDecodesOnlyAgainstItsOldOfficialFingerprint()
		{
			const string id = "taf:realm:return:v1:realm";
			const string text = "you returned to Kavvat";
			Assert.IsTrue(KingdomChronicleReceiptRules.TryFingerprint(id, text, true, null,
				out string fingerprint));
			string wire = KingdomRealmChronicleIntentRules.LegacyPrefix + "|" + B64(id) + "|" +
				fingerprint + "|" + Hash + "|" + Hash + "|" + Hash + "|" + Hash + "|" +
				Hash + "|" + Hash + "|" + B64("dated official") + "|" +
				B64("derived outsider") + "|" + B64("");
			Assert.IsTrue(KingdomRealmChronicleIntentRules.TryDecodeLegacy(wire, id, text,
				true, null, out KingdomRealmChronicleIntent restored));
			Assert.AreEqual(KingdomRealmChronicleIntentRules.LegacyVersion, restored.Version);
			Assert.IsNull(restored.OutsiderText,
				"legacy intent must not invent authored counter-history evidence");
			Assert.AreEqual("derived outsider", restored.Outsider);
			Assert.IsFalse(KingdomRealmChronicleIntentRules.TryDecodeLegacy(wire, id,
				"changed official", true, null, out restored));
		}

		private static string B64(string Value)
		{
			return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(Value));
		}
	}
}
#endif
