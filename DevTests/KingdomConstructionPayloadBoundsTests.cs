#if TAF_TESTS
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	public class KingdomConstructionPayloadBoundsTests
	{
		[TestCase(8192, true)]
		[TestCase(8193, true)]
		[TestCase(32768, true)]
		[TestCase(32769, false)]
		public void LargeAuthoredWorkKeepsABoundedConstructionPayload(int length, bool accepted)
		{
			var job = new KingdomConstructionJob
			{
				Id = "10000000000000000000000000000001", OwnerKey = "realm", ZoneId = "zone",
				Route = KingdomConstructionRoute.Improvement, Phase = KingdomConstructionPhase.Published,
				Projection = KingdomConstructionRules.ProjectionFor(KingdomConstructionRoute.Improvement),
				X = 12, Y = 9, SubjectId = "heart", TargetKey = "heartcourt", Payload = new string('x', length),
				CreatedTick = 10, StartedTick = 10, DueTick = 20, UpdatedTick = 10, Revision = 1,
				Claims = KingdomConstructionRules.NewClaims(0, new KingdomMaterialDebitCost())
			};
			ClassicAssert.AreEqual(accepted, KingdomConstructionRules.ValidJob(job));
		}
	}
}
#endif
