#if TAF_TESTS
using NUnit.Framework;
using ThousandAndFirst.Api;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomExternalOwnershipDigestTests
	{
		[Test]
		public void ExternalDecisionIsInsideVersionTwoFoundingDigest()
		{
			string none = KingdomExternalOwnershipRules.Encode(
				KingdomExternalOwnershipRules.None());
			string bind = KingdomExternalOwnershipRules.Encode(
				KingdomExternalOwnershipRules.Bind(new KingdomExternalOwnershipObservation
				{
					ProviderId = "Hearthpyre", ProviderVersion = "2.2.3",
					OwnerGuid = "11111111-2222-3333-4444-555555555555",
					SectorGuid = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
					Evidence = "settlement+sector", ZoneId = "JoppaWorld.1.2.0.0.10",
					ParasangId = "JoppaWorld.1.2"
				}));
			string openDigest = Digest(none);
			string boundDigest = Digest(bind);
			Assert.AreEqual(64, openDigest.Length);
			Assert.AreEqual(64, boundDigest.Length);
			Assert.AreNotEqual(openDigest, boundDigest);
			Assert.AreNotEqual(openDigest,
				KingdomFoundingTransactionRules.PayloadDigest(
					KingdomFoundingKind.FirstCity, "Ada", null, null, null,
					64, 64, 0, 64, "water:64", ""));
		}

		private static string Digest(string external)
		{
			return KingdomFoundingTransactionRules.PayloadDigestWithExternalBinding(
				KingdomFoundingKind.FirstCity, "Ada", null, null, null,
				64, 64, 0, 64, "water:64", "", external);
		}
	}
}
#endif
