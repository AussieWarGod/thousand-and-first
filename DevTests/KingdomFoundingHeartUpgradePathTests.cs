#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomFoundingHeartUpgradePathTests
	{
		private static KingdomConstructionJob Edge(int From, bool Complete = true)
			=> new KingdomConstructionJob
			{
				Id = "job" + From, SubjectId = "root" + From, SourceId = "root" + From,
				OutputId = "root" + (From + 1), OwnerKey = "realm", ZoneId = "zone", X = 41, Y = 12,
				Route = KingdomConstructionRoute.Improvement,
				Phase = Complete ? KingdomConstructionPhase.Complete : KingdomConstructionPhase.Working,
				PhysicalPhase = Complete ? KingdomPhysicalPhase.EffectsSettled : KingdomPhysicalPhase.None,
				TargetKey = From == 1 ? "heartwaterstone" : From == 2 ? "heartmoot" : "heartcourt"
			};

		private static bool Read(IList<KingdomConstructionJob> Jobs)
			=> KingdomFoundingHeartUpgradePathRules.TryRead(Jobs, "root1", "works", "realm", "zone",
				out _, out _);

		[Test]
		public void HistoryStartsFromItsRecordedFoundingRung()
		{
			ClassicAssert.IsTrue(KingdomFoundingHeartUpgradePathRules.TryRead(new[] { Edge(2), Edge(3) },
				"root2", "works", "realm", "zone", out var path, out _, OriginRung: 2));
			ClassicAssert.AreEqual(2, path.Count);
		}

		[TestCase(0)]
		[TestCase(5)]
		public void InvalidOrFinalFoundingRungCannotStartAnotherChain(int Rung)
		{
			ClassicAssert.IsFalse(KingdomFoundingHeartUpgradePathRules.TryRead(new[] { Edge(1) },
				"root1", "works", "realm", "zone", out _, out _, OriginRung: Rung));
		}

		[Test]
		public void ThreeCompletedUpgradesResolveInIdentityOrderWithoutChangingRegistry()
		{
			var first = Edge(1); var second = Edge(2); var third = Edge(3);
			var jobs = new[] { third, first, second };
			ClassicAssert.IsTrue(KingdomFoundingHeartUpgradePathRules.TryRead(jobs, "root1", "works",
				"realm", "zone", out var path, out var pending));
			CollectionAssert.AreEqual(new[] { first, second, third }, path);
			CollectionAssert.AreEqual(new[] { third, first, second }, jobs);
			ClassicAssert.IsNull(pending);
		}

		[Test]
		public void NextPaidReceiptCanMarkStandingOutputBeforeNextHandover()
		{
			var first = Edge(1); var next = Edge(2, false);
			ClassicAssert.IsTrue(KingdomFoundingHeartUpgradePathRules.TryRead(new[] { first, next },
				"root1", "works", "realm", "zone", out var path, out var pending));
			ClassicAssert.AreEqual(1, path.Count);
			ClassicAssert.AreSame(next, pending);
			ClassicAssert.IsTrue(KingdomFoundingHeartUpgradePathRules.OutputReceiptMatches(
				first, pending, "root2", next.Id));
			ClassicAssert.IsTrue(KingdomFoundingHeartUpgradePathRules.OutputReceiptMatches(
				first, pending, "root2", first.Id), "publishing next intent cannot invalidate prior receipt");
			ClassicAssert.IsFalse(KingdomFoundingHeartUpgradePathRules.OutputReceiptMatches(
				first, null, "root2", next.Id), "a new marker alone proves nothing");
		}

		[TestCase("branch")]
		[TestCase("cycle")]
		[TestCase("works")]
		[TestCase("foreign-owner")]
		[TestCase("foreign-zone")]
		[TestCase("foreign-source")]
		[TestCase("moved-anchor")]
		[TestCase("skipped-rung")]
		[TestCase("wrong-design")]
		[TestCase("unsettled")]
		[TestCase("failure")]
		[TestCase("cancelled")]
		[TestCase("missing-output")]
		public void AmbiguousOrUnprovedHistoryRefuses(string Mutation)
		{
			var first = Edge(1); var second = Edge(2);
			var jobs = new List<KingdomConstructionJob> { first, second };
			switch (Mutation)
			{
				case "branch": jobs.Add(Edge(1)); break;
				case "cycle": second.OutputId = "root1"; break;
				case "works": second.OutputId = "works"; break;
				case "foreign-owner": second.OwnerKey = "other"; break;
				case "foreign-zone": second.ZoneId = "other"; break;
				case "foreign-source": second.SourceId = "other"; break;
				case "moved-anchor": second.X++; break;
				case "skipped-rung": second.TargetKey = "heartcourt"; break;
				case "wrong-design": second.TargetKey = "tentrow"; break;
				case "unsettled": second.PhysicalPhase = KingdomPhysicalPhase.None; break;
				case "failure": second.Failure = "unfinished"; break;
				case "cancelled": second.Phase = KingdomConstructionPhase.Cancelled; break;
				case "missing-output": second.OutputId = ""; break;
			}
			ClassicAssert.IsFalse(Read(jobs), Mutation);
		}

		[TestCase("object")]
		[TestCase("receipt")]
		[TestCase("subject")]
		[TestCase("owner")]
		[TestCase("zone")]
		[TestCase("anchor")]
		[TestCase("cancelled")]
		[TestCase("completed")]
		public void ForeignNextReceiptCannotReplaceCompletedOutputProof(string Mutation)
		{
			var first = Edge(1); var next = Edge(2, false);
			string objectId = "root2", receipt = next.Id;
			switch (Mutation)
			{
				case "object": objectId = "other"; break;
				case "receipt": receipt = "other"; break;
				case "subject": next.SubjectId = "other"; break;
				case "owner": next.OwnerKey = "other"; break;
				case "zone": next.ZoneId = "other"; break;
				case "anchor": next.Y++; break;
				case "cancelled": next.Phase = KingdomConstructionPhase.Cancelled; break;
				case "completed": next.Phase = KingdomConstructionPhase.Complete; break;
			}
			ClassicAssert.IsFalse(KingdomFoundingHeartUpgradePathRules.OutputReceiptMatches(
				first, next, objectId, receipt), Mutation);
		}

		[Test]
		public void EmptyAndOversizedHistoryCannotEstablishFoundingSuccession()
		{
			ClassicAssert.IsFalse(Read(null));
			ClassicAssert.IsFalse(Read(new KingdomConstructionJob[0]));
			ClassicAssert.IsFalse(Read(new KingdomConstructionJob[KingdomConstructionRules.MaxRows + 1]));
		}
	}
}
#endif
