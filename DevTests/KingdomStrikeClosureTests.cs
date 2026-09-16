using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomStrikeClosureTests
	{
		private static KingdomConstructionJob Job(KingdomConstructionRoute Route,
			KingdomConstructionPhase Phase)
		{
			return new KingdomConstructionJob
			{
				Id = "00000000000000000000000000000001",
				OwnerKey = KingdomConstructionRules.OwnerKey("realm", 7L, "settlement"),
				ZoneId = "JoppaWorld.11.22.1.1.10", Route = Route, Phase = Phase,
				Projection = KingdomConstructionRules.ProjectionFor(Route),
				X = 12, Y = 9, SubjectId = "subject-1", TargetKey = "target", Payload = "payload",
				CreatedTick = 10L, StartedTick = 10L, DueTick = 20L, UpdatedTick = 10L,
				Revision = 1, Claims = KingdomConstructionRules.NewClaims(0, new KingdomMaterialDebitCost())
			};
		}

		private static KingdomConstructionOutbox SettledOutbox(string Id, string Suffix)
		{
			return new KingdomConstructionOutbox
			{
				EventId = "construction:" + Id + ":" + Suffix, Mode = 1,
				ChronicleState = KingdomConstructionSinkDisposition.Skipped,
				LedgerState = KingdomConstructionSinkDisposition.Skipped,
				MessageState = KingdomConstructionSinkDisposition.Skipped,
				DeedState = KingdomConstructionSinkDisposition.Skipped
			};
		}

		[TestCase("exact", true)]
		[TestCase("null", false)]
		[TestCase("owner", false)]
		[TestCase("zone", false)]
		[TestCase("receipt", false)]
		[TestCase("object", false)]
		[TestCase("empty-object", false)]
		[TestCase("working", false)]
		[TestCase("quarantined", false)]
		[TestCase("cancelled", false)]
		[TestCase("settled", false)]
		[TestCase("compacted", false)]
		[TestCase("malformed", false)]
		[TestCase("in-place", true)]
		[TestCase("wrong-subject", false)]
		public void PendingStrikeClosureFeedbackRequiresExactHealthyCompletedWork(string Change, bool Expected)
		{
			KingdomConstructionJob job = Job(KingdomConstructionRoute.PlotCommission,
				Phase: KingdomConstructionPhase.Complete);
			job.SourceId = job.SubjectId = "old-works";
			job.OutputId = "building-1";
			string owner = job.OwnerKey, zone = job.ZoneId, receipt = job.Id, body = job.OutputId;
			switch (Change)
			{
				case "null": job = null; break;
				case "owner": owner += "x"; break;
				case "zone": zone += "x"; break;
				case "receipt": receipt = "00000000000000000000000000000002"; break;
				case "object": body = "other-building"; break;
				case "empty-object": body = null; break;
				case "working": job.Phase = KingdomConstructionPhase.Working; break;
				case "quarantined": job.Phase = KingdomConstructionPhase.InspectionRequired; break;
				case "cancelled": job.Phase = KingdomConstructionPhase.Cancelled; break;
				case "settled":
					job.Outbox = SettledOutbox(job.Id, "raised");
					job.PhysicalPhase = KingdomPhysicalPhase.EffectsSettled; break;
				case "compacted": job.Compacted = true; break;
				case "malformed": job.Claims = null; break;
				case "in-place": job.OutputId = null; job.SourceId = job.SubjectId = body; break;
				case "wrong-subject": job.OutputId = null; job.SourceId = body; break;
			}
			ClassicAssert.AreEqual(Expected, KingdomConstructionRules.HasPendingOwnTerminalClosure(
				job, owner, zone, receipt, body));
		}

	}
}
