#if TAF_TESTS
using System;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The operative partial-marker census, executed rather than searched for.
	/// <para>
	/// A source test that greps a file for a marker name passes while the predicate that actually
	/// runs omits it. These mutants run the predicate, so a marker dropped from the census fails a
	/// test instead of passing one.
	/// </para>
	/// </summary>
	[TestFixture]
	public sealed class KingdomRealizedAuthorityShapeTests
	{
		/// <summary>An ordinary settler standing on the finished build: no claim of any kind.</summary>
		private static KingdomRealizedMarkerObservation Bystander()
		{
			return new KingdomRealizedMarkerObservation { InsideRect = true };
		}

		/// <summary>A lawful plot object elsewhere on the same plot, outside the lot rect.</summary>
		private static KingdomRealizedMarkerObservation PlotNeighbour()
		{
			return new KingdomRealizedMarkerObservation
			{
				PlotIdString = true,
				ClaimsLot = true,
				PlotPart = true,
				InsideRect = false
			};
		}

		private static KingdomRealizedAuthorityVerdict Judge(
			KingdomRealizedMarkerObservation observed)
		{
			return KingdomRealizedAuthorityShape.Judge(observed);
		}

		// ----- exclusion: refusing everything would be as wrong as refusing nothing --------------

		[Test]
		public void AnUnmarkedBystanderIsUnrelated()
		{
			Assert.AreEqual(KingdomRealizedAuthorityVerdict.Unrelated, Judge(Bystander()));
		}

		/// <summary>
		/// The lot relationship is why this is a predicate and not a blanket refusal: a crop or a
		/// yard elsewhere on the same plot carries plot-part custody lawfully.
		/// </summary>
		[Test]
		public void APlotPartOutsideTheLotRectIsUnrelated()
		{
			Assert.AreEqual(KingdomRealizedAuthorityVerdict.Unrelated, Judge(PlotNeighbour()));
		}

		[Test]
		public void APlotPartOnAnotherLotInsideTheRectIsUnrelated()
		{
			KingdomRealizedMarkerObservation foreign = PlotNeighbour();
			foreign.ClaimsLot = false;
			foreign.InsideRect = true;
			Assert.AreEqual(KingdomRealizedAuthorityVerdict.Unrelated, Judge(foreign));
		}

		// ----- the census: a partial marker is still a claim --------------------------------------

		/// <summary>
		/// The false-green this fixture exists for. Plot-part custody is written as part of stamping
		/// a component, so an object holding only that custody inside the lot rect is either a
		/// half-stamped component or foreign matter on the build. Skipping it narrows the world.
		/// </summary>
		[Test]
		public void PlotPartAloneInsideTheLotRectIsUnreceipted()
		{
			KingdomRealizedMarkerObservation observed = PlotNeighbour();
			observed.InsideRect = true;
			Assert.IsTrue(KingdomRealizedAuthorityShape.ClaimsComponentAuthority(observed));
			Assert.AreEqual(KingdomRealizedAuthorityVerdict.Unreceipted, Judge(observed));
		}

		[Test]
		public void AComponentMarkerOnThisLotIsUnreceipted()
		{
			KingdomRealizedMarkerObservation observed = new KingdomRealizedMarkerObservation
			{
				ComponentMarker = true,
				PlotIdString = true,
				ClaimsLot = true,
				InsideRect = false
			};
			Assert.AreEqual(KingdomRealizedAuthorityVerdict.Unreceipted, Judge(observed),
				"a component marker is a claim wherever it stands");
		}

		[Test]
		public void ASecondLayoutOwnerOnThisLotRefusesFirst()
		{
			KingdomRealizedMarkerObservation observed = new KingdomRealizedMarkerObservation
			{
				PlotIdString = true,
				ClaimsLot = true,
				CarriesLayoutOwnerSchema = true
			};
			Assert.AreEqual(KingdomRealizedAuthorityVerdict.SecondOwner, Judge(observed));
		}

		[Test]
		public void ACopiedSnapshotAuthorityOutsideThisLotRefuses()
		{
			KingdomRealizedMarkerObservation observed = new KingdomRealizedMarkerObservation
			{
				ComponentMarker = true,
				PlotIdString = true,
				ClaimsLot = false,
				CarriesThisSnapshotHash = true,
				InsideRect = true
			};
			Assert.AreEqual(KingdomRealizedAuthorityVerdict.CopiedAuthority, Judge(observed));
		}

		// ----- plot-part-only custody: no component marker smuggled in ---------------------------

		/// <summary>
		/// The escape this fixture was written to close and did not. An object inside the rect with
		/// only plot-part custody and an INT-typed plot id has no readable lot, so asking whether it
		/// claims THIS lot answers no for the same reason it answers no for a bystander - and the
		/// claim used to walk out of the census through that door. Raw authority is classified
		/// before any value relationship is possible.
		/// </summary>
		[Test]
		public void PlotPartOnlyWithIntTypedCustodyIsUnreadable()
		{
			KingdomRealizedMarkerObservation observed = new KingdomRealizedMarkerObservation
			{
				ComponentMarker = false,
				PlotPart = true,
				InsideRect = true,
				PlotIdInt = true,
				PlotIdString = false,
				ClaimsLot = false
			};
			Assert.IsTrue(KingdomRealizedAuthorityShape.ClaimsComponentAuthority(observed));
			Assert.AreEqual(KingdomRealizedAuthorityVerdict.UnreadableCustody, Judge(observed));
		}

		[Test]
		public void PlotPartOnlyWithDualTypedCustodyIsUnreadable()
		{
			KingdomRealizedMarkerObservation observed = new KingdomRealizedMarkerObservation
			{
				ComponentMarker = false,
				PlotPart = true,
				InsideRect = true,
				PlotIdInt = true,
				PlotIdString = true,
				ClaimsLot = true
			};
			Assert.AreEqual(KingdomRealizedAuthorityVerdict.UnreadableCustody, Judge(observed));
		}

		[Test]
		public void PlotPartOnlyWithNoCustodyKeyIsUnreadable()
		{
			KingdomRealizedMarkerObservation observed = new KingdomRealizedMarkerObservation
			{
				ComponentMarker = false,
				PlotPart = true,
				InsideRect = true,
				PlotIdInt = false,
				PlotIdString = false,
				ClaimsLot = false
			};
			Assert.AreEqual(KingdomRealizedAuthorityVerdict.UnreadableCustody, Judge(observed),
				"plot-part authority with no custody key at all cannot be dismissed as another "
					+ "lot's business");
		}

		/// <summary>
		/// A component marker with no custody key is partial authority for the same reason: the
		/// stamper never writes one without the other.
		/// </summary>
		[Test]
		public void AComponentMarkerWithNoCustodyKeyIsUnreadable()
		{
			KingdomRealizedMarkerObservation observed = new KingdomRealizedMarkerObservation
			{
				ComponentMarker = true,
				PlotIdString = false,
				PlotIdInt = false,
				InsideRect = false
			};
			Assert.AreEqual(KingdomRealizedAuthorityVerdict.UnreadableCustody, Judge(observed));
		}

		// ----- plot custody type presence ---------------------------------------------------------

		/// <summary>
		/// A custody key under the int table answers a text read with nothing, so an object claiming
		/// component authority through one can never be judged by value.
		/// </summary>
		[Test]
		public void AnIntTypedPlotCustodyKeyIsUnreadable()
		{
			KingdomRealizedMarkerObservation observed = new KingdomRealizedMarkerObservation
			{
				ComponentMarker = true,
				PlotIdInt = true,
				InsideRect = true
			};
			Assert.AreEqual(KingdomRealizedAuthorityVerdict.UnreadableCustody, Judge(observed));
		}

		[Test]
		public void ADualTypedPlotCustodyKeyIsUnreadable()
		{
			KingdomRealizedMarkerObservation observed = new KingdomRealizedMarkerObservation
			{
				ComponentMarker = true,
				PlotIdString = true,
				PlotIdInt = true,
				ClaimsLot = true,
				InsideRect = true
			};
			Assert.AreEqual(KingdomRealizedAuthorityVerdict.UnreadableCustody, Judge(observed),
				"unreadable custody outranks the unreceipted verdict; it cannot be judged by value");
		}

		[Test]
		public void AnUnobservedObjectIsUnreadableRatherThanUnrelated()
		{
			Assert.AreEqual(KingdomRealizedAuthorityVerdict.UnreadableCustody, Judge(null));
			Assert.IsFalse(KingdomRealizedAuthorityShape.ClaimsComponentAuthority(null));
		}

		// ----- every refusal names itself ---------------------------------------------------------

		[TestCase(KingdomRealizedAuthorityVerdict.SecondOwner)]
		[TestCase(KingdomRealizedAuthorityVerdict.UnreadableCustody)]
		[TestCase(KingdomRealizedAuthorityVerdict.Unreceipted)]
		[TestCase(KingdomRealizedAuthorityVerdict.CopiedAuthority)]
		public void EveryRefusalCarriesAnOperatorReason(KingdomRealizedAuthorityVerdict verdict)
		{
			string reason = KingdomRealizedAuthorityShape.Describe(verdict);
			Assert.IsNotNull(reason, verdict.ToString());
			Assert.IsNotEmpty(reason, verdict.ToString());
		}

		[Test]
		public void AnUnrelatedObjectHasNoRefusalReason()
		{
			Assert.IsNull(KingdomRealizedAuthorityShape.Describe(
				KingdomRealizedAuthorityVerdict.Unrelated));
		}
	}
}
#endif
