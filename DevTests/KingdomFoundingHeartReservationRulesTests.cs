#if TAF_TESTS
using System;
using System.IO;
using System.Text;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomFoundingHeartReservationRulesTests
	{
		private const string Transaction = "0123456789abcdef0123456789abcdef";
		private const string Zone = "JoppaWorld.2.2.1.1.10";
		private const string FrozenId = "taf-heart-v1-633852846ec8adf57ce732ee20b629911650459a0b9ce2d54c897e5910b93117";
		private const string FrozenSeal = "hs1-04c0a06620a93463691fe9b47b37aa57a2dac753378346ffe3f3d614c6a82d5d";
		// Frozen independently from the documented hr1/h2 field order and SHA-256 domains.
		private const string FrozenWire = "hr1|MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=|Sm9wcGFXb3JsZC4yLjIuMS4xLjEw|c2xvdC0w|"
			+ "taf-heart-v1-633852846ec8adf57ce732ee20b629911650459a0b9ce2d54c897e5910b93117|"
			+ "hs1-04c0a06620a93463691fe9b47b37aa57a2dac753378346ffe3f3d614c6a82d5d";

		[TestCase("slot-0")] [TestCase("slot-1")] [TestCase("slot-2")]
		[TestCase("slot-3")] [TestCase("slot-4")] [TestCase("slot-5")] [TestCase("final")]
		public void EveryGeneratedReservationReadsItsOwnUnchangedWire(string role)
		{
			KingdomFoundingHeartPlan plan = Plan();
			string id = KingdomFoundingHeartRules.StableId(Transaction, Zone, role);
			string wire = KingdomFoundingHeartReservationRules.Encode(plan, id, role);
			Assert.IsNotNull(wire);
			AssertRead(Key(id), wire, Transaction, Zone, id);
			Assert.AreEqual(FrozenSeal, wire.Split('|')[5]);
			Assert.AreEqual(68, wire.Split('|')[5].Length);
		}

		[Test]
		public void FrozenProductionFormatLiteralReadsAndWriterDoesNotChangeItsBytes()
		{
			AssertRead(Key(FrozenId), FrozenWire, Transaction, Zone, FrozenId);
			Assert.AreEqual(FrozenWire, KingdomFoundingHeartReservationRules.Encode(Plan(), FrozenId, "slot-0"));
			Assert.AreEqual("r_TAF_FoundingHeartReserved:" + FrozenId, Key(FrozenId));
		}

		[TestCase("slot-0")] [TestCase("slot-1")] [TestCase("slot-2")]
		[TestCase("slot-3")] [TestCase("slot-4")] [TestCase("slot-5")] [TestCase("final")]
		public void LengthFramedStringReloadPreservesExactTextBeforeReservationRead(string role)
		{
			// BCL string serialization only; this is not a native Qud save/load claim.
			string id = KingdomFoundingHeartRules.StableId(Transaction, Zone, role);
			string key = Key(id), wire = KingdomFoundingHeartReservationRules.Encode(Plan(), id, role);
			using (MemoryStream stream = new MemoryStream())
			{
				using (BinaryWriter writer = new BinaryWriter(stream, new UTF8Encoding(false, true), true))
				{ writer.Write(key); writer.Write(wire); }
				stream.Position = 0;
				using (BinaryReader reader = new BinaryReader(stream, new UTF8Encoding(false, true), true))
				{
					string loadedKey = reader.ReadString(), loadedWire = reader.ReadString();
					Assert.AreEqual(key, loadedKey); Assert.AreEqual(wire, loadedWire);
					CollectionAssert.AreEqual(Encoding.UTF8.GetBytes(wire), Encoding.UTF8.GetBytes(loadedWire));
					Assert.AreEqual(stream.Length, stream.Position);
					AssertRead(loadedKey, loadedWire, Transaction, Zone, id);
				}
			}
		}

		[TestCase(0)] [TestCase(1)] [TestCase(2)]
		public void CompletionProjectionCopiesWithoutAdvancingTheLivePlan(int firstState)
		{
			KingdomFoundingHeartPlan plan = Plan(); plan.States[0] = firstState;
			string before = KingdomFoundingHeartRules.Encode(plan);
			int[] states = (int[])plan.States.Clone();
			Assert.AreEqual(FrozenWire, KingdomFoundingHeartReservationRules.Encode(plan, FrozenId, "slot-0"));
			CollectionAssert.AreEqual(states, plan.States);
			Assert.AreEqual(before, KingdomFoundingHeartRules.Encode(plan));
		}

		[TestCase("bare")] [TestCase("wrong-prefix")] [TestCase("upper-prefix")] [TestCase("upper-hex")]
		[TestCase("short")] [TestCase("long")] [TestCase("nonhex")] [TestCase("space")]
		[TestCase("newline")] [TestCase("empty")] [TestCase("null")]
		public void SealRequiresExactHs1PrefixAndSixtyFourLowerHexDigits(string mutation)
		{
			string seal = FrozenSeal;
			switch (mutation)
			{
				case "bare": seal = seal.Substring(4); break;
				case "wrong-prefix": seal = "hs2-" + seal.Substring(4); break;
				case "upper-prefix": seal = "HS1-" + seal.Substring(4); break;
				case "upper-hex": seal = "hs1-" + seal.Substring(4).ToUpperInvariant(); break;
				case "short": seal = seal.Substring(0, seal.Length - 1); break;
				case "long": seal += "0"; break;
				case "nonhex": seal = seal.Substring(0, seal.Length - 1) + "g"; break;
				case "space": seal = seal.Substring(0, seal.Length - 1) + " "; break;
				case "newline": seal = seal.Substring(0, seal.Length - 1) + "\n"; break;
				case "empty": seal = ""; break;
				case "null": seal = null; break;
				default: Assert.Fail("unknown mutation"); break;
			}
			Refuses(Key(FrozenId), Field(5, seal));
		}

		[TestCase(0, "hr0")] [TestCase(0, "HR1")]
		[TestCase(1, "!")] [TestCase(1, "")] [TestCase(2, "!")] [TestCase(2, "")]
		[TestCase(3, "!")] [TestCase(3, "")] [TestCase(4, "")] [TestCase(4, "foreign")]
		public void MalformedEnvelopeFieldsDoNotAcquireReservationAuthority(int field, string value)
		{
			Refuses(Key(FrozenId), Field(field, value));
		}

		[TestCase("slot-6")] [TestCase("slot--1")] [TestCase("slot-")]
		[TestCase("SLOT-0")] [TestCase("Final")] [TestCase("work")]
		[TestCase("slot-00")] [TestCase("slot-05")] [TestCase("slot-+0")] [TestCase("slot-+5")]
		[TestCase("slot--0")] [TestCase("slot- 0")] [TestCase("slot-0 ")]
		[TestCase("slot-\t0")] [TestCase("slot-0\n")] [TestCase("slot-0\r\n")]
		public void InvalidRolesRefuseEvenWithMatchingDeterministicIdentity(string role)
		{
			string id = KingdomFoundingHeartRules.StableId(Transaction, Zone, role);
			Assert.IsNotNull(id);
			Refuses(Key(id), Raw(Transaction, Zone, role, id));
			Assert.IsNull(KingdomFoundingHeartReservationRules.Encode(Plan(), id, role));
		}

		[TestCase("/w==")] [TestCase("gA==")] [TestCase("wK8=")]
		[TestCase("4oI=")] [TestCase("7aCA")] [TestCase("9JCAgA==")]
		public void InvalidUtf8CannotAcquireTheReplacementTextIdentity(string encodedZone)
		{
			string replacement = Encoding.UTF8.GetString(Convert.FromBase64String(encodedZone));
			Assert.IsTrue(replacement.IndexOf('\uFFFD') >= 0);
			string id = KingdomFoundingHeartRules.StableId(Transaction, replacement, "slot-0");
			Assert.IsNotNull(id);
			string[] fields = Raw(Transaction, replacement, "slot-0", id).Split('|');
			fields[2] = encodedZone;
			Refuses(Key(id), string.Join("|", fields));
		}

		[TestCase(0xD800)] [TestCase(0xDBFF)] [TestCase(0xDC00)] [TestCase(0xDFFF)]
		public void ConstructedUnpairedSurrogatesRefuseEncodingWithoutChangingThePlan(int codeUnit)
		{
			string zone = "zone-" + new string((char)codeUnit, 1) + "-end";
			KingdomFoundingHeartPlan plan = Plan(); plan.ZoneId = zone;
			plan.PlotId = KingdomFoundingHeartRules.StableId(Transaction, zone, "plot");
			string id = KingdomFoundingHeartRules.StableId(Transaction, zone, "slot-0");
			Assert.IsNotNull(id);
			Assert.IsTrue(KingdomFoundingHeartRules.Valid(plan));
			int[] states = plan.States, before = (int[])states.Clone();
			string plot = plan.PlotId;
			Assert.IsNull(KingdomFoundingHeartReservationRules.Encode(plan, id, "slot-0"));
			Assert.AreEqual(zone, plan.ZoneId); Assert.AreEqual(plot, plan.PlotId);
			Assert.AreSame(states, plan.States); CollectionAssert.AreEqual(before, states);
		}

		[TestCase("zone-\u00E9")] [TestCase("zone-\uFFFD")] [TestCase("zone-\U0001F30D")]
		public void ValidUnicodeIncludingLiteralReplacementAndPairedSurrogatesKeepsExactBytes(string zone)
		{
			KingdomFoundingHeartPlan plan = Plan(); plan.ZoneId = zone;
			plan.PlotId = KingdomFoundingHeartRules.StableId(Transaction, zone, "plot");
			string id = KingdomFoundingHeartRules.StableId(Transaction, zone, "slot-0");
			string wire = KingdomFoundingHeartReservationRules.Encode(plan, id, "slot-0");
			Assert.IsNotNull(wire);
			AssertRead(Key(id), wire, Transaction, zone, id);
			Assert.AreEqual(B64(zone), wire.Split('|')[2]);
		}

		[TestCase(null)] [TestCase("")] [TestCase("bad")]
		[TestCase("0123456789ABCDEF0123456789ABCDEF")]
		[TestCase("0123456789abcdef0123456789abcde")]
		[TestCase("0123456789abcdef0123456789abcdef0")]
		public void NoncanonicalFoundingTransactionsRefuse(string transaction)
		{
			Refuses(Key(FrozenId), Raw(transaction, Zone, "slot-0", FrozenId));
			KingdomFoundingHeartPlan plan = Plan(); plan.TransactionId = transaction;
			Assert.IsNull(KingdomFoundingHeartReservationRules.Encode(plan, FrozenId, "slot-0"));
		}

		[Test]
		public void ChangedValidTransactionZoneOrRoleCannotReuseAnExistingIdentity()
		{
			Refuses(Key(FrozenId), Raw(new string('a', 32), Zone, "slot-0", FrozenId));
			Refuses(Key(FrozenId), Raw(Transaction, "another-zone", "slot-0", FrozenId));
			Refuses(Key(FrozenId), Raw(Transaction, Zone, "final", FrozenId));
			Assert.IsNull(KingdomFoundingHeartReservationRules.Encode(Plan(), FrozenId, "final"));
		}

		[TestCase(null)] [TestCase("")] [TestCase("foreign")]
		[TestCase("r_TAF_FoundingHeartReserved:other")]
		public void KeyMustNameTheExactReservedIdentity(string key) { Refuses(key, FrozenWire); }

		[TestCase(0, false)] [TestCase(1, true)] [TestCase(512, true)] [TestCase(513, false)]
		public void ZoneIdentityKeepsItsExistingNonemptyAndMaximumBounds(int length, bool accepted)
		{
			string zone = new string('z', length);
			string id = KingdomFoundingHeartRules.StableId(Transaction, zone, "slot-0") ?? FrozenId;
			Assert.AreEqual(accepted, KingdomFoundingHeartReservationRules.TryRead(Key(id), Raw(Transaction, zone, "slot-0", id),
				out _, out _, out _));
			KingdomFoundingHeartPlan plan = Plan(); plan.ZoneId = zone;
			plan.PlotId = KingdomFoundingHeartRules.StableId(Transaction, zone, "plot");
			Assert.AreEqual(accepted, KingdomFoundingHeartReservationRules.Encode(plan, id, "slot-0") != null);
		}

		[Test]
		public void RawAndKeyBoundsDoNotBroadenWhileExistingBase64WhitespaceBehaviorIsPreserved()
		{
			string[] fields = FrozenWire.Split('|');
			fields[1] += new string(' ', 4096 - FrozenWire.Length);
			string boundary = string.Join("|", fields);
			Assert.AreEqual(4096, boundary.Length);
			AssertRead(Key(FrozenId), boundary, Transaction, Zone, FrozenId);
			Refuses(Key(FrozenId), boundary + " ");
			Refuses(new string('k', 2048), FrozenWire);
			Refuses(new string('k', 2049), FrozenWire);
		}

		[TestCase(1)] [TestCase(2)] [TestCase(3)]
		public void Base64WhitespaceRemainsSupportedForEveryEncodedTextField(int field)
		{
			string[] fields = FrozenWire.Split('|');
			fields[field] = " \t" + fields[field].Substring(0, 4) + "\r\n"
				+ fields[field].Substring(4) + " ";
			AssertRead(Key(FrozenId), string.Join("|", fields), Transaction, Zone, FrozenId);
		}

		[Test]
		public void ExtraMissingOrEmptyEnvelopeNeverBecomesAReservation()
		{
			foreach (string raw in new[] { null, "", FrozenWire + "|tail", "\uFEFF" + FrozenWire,
				FrozenWire.Substring(0, FrozenWire.LastIndexOf('|')), FrozenWire + "\n" }) Refuses(Key(FrozenId), raw);
		}

		[Test]
		public void InvalidEncoderInputsReturnNullWithoutChangingThePlan()
		{
			KingdomFoundingHeartPlan plan = Plan(); string before = KingdomFoundingHeartRules.Encode(plan);
			Assert.IsNull(KingdomFoundingHeartReservationRules.Encode(null, FrozenId, "slot-0"));
			foreach (string value in new[] { null, "", "foreign" })
			{
				Assert.IsNull(KingdomFoundingHeartReservationRules.Encode(plan, value, "slot-0"));
				Assert.IsNull(KingdomFoundingHeartReservationRules.Encode(plan, FrozenId, value));
			}
			Assert.AreEqual(before, KingdomFoundingHeartRules.Encode(plan));
			plan.States[1] = 2;
			Assert.IsNull(KingdomFoundingHeartReservationRules.Encode(plan, FrozenId, "slot-0"));
		}

		private static KingdomFoundingHeartPlan Plan()
		{
			Assert.IsTrue(KingdomFoundingHeartStakeRules.TryCreate("heartbasin", "first basin",
				"r_KingdomPlotWorks", 38, 11, 42, 13, 0, true, false, null,
				"TAF_HeartBasinContents", 2, true, 3, false, 40, 11, false,
				out KingdomFoundingHeartStakeTruth truth));
			Assert.IsTrue(KingdomFoundingHeartRules.TryCreate(Transaction, Zone,
				40, 12, 30, 2, 49, 21, 38, 11, 42, 13, 900L, 600L,
				"p4,frozen-authored-payload", KingdomFoundingHeartStakeRules.Encode(truth), out KingdomFoundingHeartPlan plan));
			return plan;
		}

		private static string Key(string id) { return KingdomFoundingHeartReservationRules.Prefix + id; }
		private static string B64(string value) { return Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? "")); }
		private static string Raw(string transaction, string zone, string role, string id)
		{
			return "hr1|" + B64(transaction) + "|" + B64(zone) + "|" + B64(role) + "|" + id + "|" + FrozenSeal;
		}
		private static string Field(int index, string value)
		{
			string[] fields = FrozenWire.Split('|'); fields[index] = value; return string.Join("|", fields);
		}
		private static void AssertRead(string key, string raw, string transaction, string zone, string id)
		{
			Assert.IsTrue(KingdomFoundingHeartReservationRules.TryRead(key, raw, out string gotTransaction,
				out string gotZone, out string gotId));
			Assert.AreEqual(transaction, gotTransaction); Assert.AreEqual(zone, gotZone); Assert.AreEqual(id, gotId);
		}
		private static void Refuses(string key, string raw)
		{
			Assert.IsFalse(KingdomFoundingHeartReservationRules.TryRead(key, raw, out _, out _, out _));
		}
	}
}
#endif
