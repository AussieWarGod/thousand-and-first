#if TAF_TESTS
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class KingdomDelveLinkRulesTests
	{
		private const string Head = "JoppaWorld.11.22.1.1.10";
		private const string Foot = "JoppaWorld.11.22.1.1.11";
		private static readonly string Snapshot = new string('a', 64);

		private static KingdomDelveLinkReceipt Receipt()
		{
			KingdomDelveLinkReceipt receipt;
			string failure;
			Assert.IsTrue(KingdomDelveLinkRules.TryCreate(Head, Foot, 17, 9,
				"root-1", "lot-1", Snapshot, "down@2,2", "down-1", "up-1",
				out receipt, out failure), failure);
			return receipt;
		}

		[Test]
		public void PhysicalPairReceiptRoundTripsCanonically()
		{
			KingdomDelveLinkReceipt receipt = Receipt();
			string encoded;
			string failure;
			Assert.IsTrue(KingdomDelveLinkRules.TryEncode(receipt, out encoded, out failure), failure);
			KingdomDelveLinkReceipt read;
			Assert.IsTrue(KingdomDelveLinkRules.TryDecode(encoded, out read, out failure), failure);
			Assert.AreEqual(Head, read.HeadZoneId);
			Assert.AreEqual(Foot, read.FootZoneId);
			Assert.AreEqual("down-1", read.HeadEndpointId);
			Assert.AreEqual("up-1", read.FootEndpointId);
			Assert.AreEqual(receipt.Token, read.Token);
			string second;
			Assert.IsTrue(KingdomDelveLinkRules.TryEncode(read, out second, out failure), failure);
			Assert.AreEqual(encoded, second);
		}

		[Test]
		public void TamperedReceiptFailsClosed()
		{
			string encoded;
			string failure;
			Assert.IsTrue(KingdomDelveLinkRules.TryEncode(Receipt(), out encoded, out failure), failure);
			KingdomDelveLinkReceipt ignored;
			Assert.IsFalse(KingdomDelveLinkRules.TryDecode(encoded.Replace("|17|", "|18|"),
				out ignored, out failure));
			StringAssert.Contains("digest", failure);
		}

		[Test]
		public void WrongColumnOrDirectionNeverMakesAReceipt()
		{
			KingdomDelveLinkReceipt ignored;
			string failure;
			Assert.IsFalse(KingdomDelveLinkRules.TryCreate(Head,
				"JoppaWorld.11.22.2.1.11", 17, 9, "root", "lot", Snapshot,
				"down@2,2", "down", "up", out ignored, out failure));
			Assert.IsFalse(KingdomDelveLinkRules.TryCreate(Foot, Head, 17, 9,
				"root", "lot", Snapshot, "down@2,2", "down", "up",
				out ignored, out failure));
		}

		[Test]
		public void EndpointIdentityCannotAlias()
		{
			KingdomDelveLinkReceipt ignored;
			string failure;
			Assert.IsFalse(KingdomDelveLinkRules.TryCreate(Head, Foot, 17, 9,
				"root", "lot", Snapshot, "down@2,2", "same", "same",
				out ignored, out failure));
			StringAssert.Contains("endpoint", failure);
		}

		[Test]
		public void IdentityAndCoordinatesAreBounded()
		{
			KingdomDelveLinkReceipt ignored;
			string failure;
			Assert.IsFalse(KingdomDelveLinkRules.TryCreate(Head, Foot, 512, 9,
				"root", "lot", Snapshot, "down@2,2", "down", "up",
				out ignored, out failure));
			Assert.IsFalse(KingdomDelveLinkRules.TryCreate(Head, Foot, 17, 9,
				new string('r', 257), "lot", Snapshot, "down@2,2", "down", "up",
				out ignored, out failure));
		}
	}
}
#endif
