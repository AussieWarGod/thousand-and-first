#if TAF_TESTS
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Value tests for <see cref="KingdomTeardownStrikeRowClaims.IsExpectedStrikeRow"/> -- a
	/// pure, engine-free predicate, so every case here is a real behavioural proof, not a
	/// source pin. The positive fixture is the row's state AFTER OrderStrikeDurable actually
	/// returns true (Phase==Working, PhysicalPhase==StrikeWorking), never NewJob's initial
	/// Published/pre-stamp state. Covers that positive row and each named negative (wrong
	/// route, subject, owner, zone, pre-stamp Published phase, missing StrikeWorking physical
	/// phase, and the old paid-construction id supplied by mistake).
	/// </summary>
	public class KingdomTeardownStrikeRowClaimsTests
	{
		private const string WorksId = "works-1";
		private const string OwnerKey = "owner-1";
		private const string ZoneId = "zone-1";

		private static KingdomConstructionJob ExactRow()
		{
			return new KingdomConstructionJob
			{
				Id = "strike-row-1",
				Route = KingdomConstructionRoute.Strike,
				SubjectId = WorksId,
				SourceId = WorksId,
				OwnerKey = OwnerKey,
				ZoneId = ZoneId,
				Phase = KingdomConstructionPhase.Working,
				PhysicalPhase = KingdomPhysicalPhase.StrikeWorking,
			};
		}

		private static bool Check(KingdomConstructionJob row, out string failure)
		{
			return KingdomTeardownStrikeRowClaims.IsExpectedStrikeRow(
				row, WorksId, OwnerKey, ZoneId, out failure);
		}

		[Test]
		public void ExactPostStampRowIsAccepted()
		{
			Assert.That(Check(ExactRow(), out string failure), Is.True);
			Assert.That(failure, Is.Null);
		}

		[Test]
		public void NullRowIsRejected()
		{
			Assert.That(Check(null, out string failure), Is.False);
			Assert.That(failure, Does.Contain("no registry row"));
		}

		[Test]
		public void WrongRouteIsRejected()
		{
			KingdomConstructionJob row = ExactRow();
			row.Route = KingdomConstructionRoute.PlotCommission;
			Assert.That(Check(row, out string failure), Is.False);
			Assert.That(failure, Does.Contain("route"));
		}

		[Test]
		public void WrongSubjectIdIsRejected()
		{
			KingdomConstructionJob row = ExactRow();
			row.SubjectId = "someone-else";
			Assert.That(Check(row, out string failure), Is.False);
			Assert.That(failure, Does.Contain("subject/source"));
		}

		[Test]
		public void WrongSourceIdIsRejected()
		{
			KingdomConstructionJob row = ExactRow();
			row.SourceId = "someone-else";
			Assert.That(Check(row, out string failure), Is.False);
			Assert.That(failure, Does.Contain("subject/source"));
		}

		[Test]
		public void WrongOwnerKeyIsRejected()
		{
			KingdomConstructionJob row = ExactRow();
			row.OwnerKey = "foreign-owner";
			Assert.That(Check(row, out string failure), Is.False);
			Assert.That(failure, Does.Contain("owner key"));
		}

		[Test]
		public void WrongZoneIdIsRejected()
		{
			KingdomConstructionJob row = ExactRow();
			row.ZoneId = "foreign-zone";
			Assert.That(Check(row, out string failure), Is.False);
			Assert.That(failure, Does.Contain("zone id"));
		}

		[Test]
		public void PublishedPreStampPhaseIsRejected()
		{
			// NewJob's own initial state, before ResumeStrikeStamp ever ran -- OrderStrike has
			// not actually returned true yet at this state, so it must never pass as "expected".
			KingdomConstructionJob row = ExactRow();
			row.Phase = KingdomConstructionPhase.Published;
			row.PhysicalPhase = KingdomPhysicalPhase.StrikeOrdered;
			Assert.That(Check(row, out string failure), Is.False);
			Assert.That(failure, Does.Contain("Working"));
		}

		[Test]
		public void WrongPhaseIsRejected()
		{
			KingdomConstructionJob row = ExactRow();
			row.Phase = KingdomConstructionPhase.Complete;
			Assert.That(Check(row, out string failure), Is.False);
			Assert.That(failure, Does.Contain("Working"));
		}

		[Test]
		public void MissingStrikeWorkingPhysicalPhaseIsRejected()
		{
			// Phase reached Working but the physical stamp itself is not StrikeWorking -- a
			// mismatched pair must still refuse, not pass on Phase alone.
			KingdomConstructionJob row = ExactRow();
			row.PhysicalPhase = KingdomPhysicalPhase.StrikeOrdered;
			Assert.That(Check(row, out string failure), Is.False);
			Assert.That(failure, Does.Contain("StrikeWorking"));
		}

		[Test]
		public void OldConstructionRouteSuppliedInsteadOfStrikeIsRejected()
		{
			// The exact failure mode this predicate exists to catch: a caller mistakenly
			// resolving the OLD paid-construction job (route PlotCommission, not Strike), even
			// though that old job may also legitimately read Phase==Working.
			KingdomConstructionJob row = ExactRow();
			row.Route = KingdomConstructionRoute.PlotCommission;
			Assert.That(Check(row, out string failure), Is.False);
			Assert.That(failure, Does.Contain("route"));
		}
	}
}
#endif
