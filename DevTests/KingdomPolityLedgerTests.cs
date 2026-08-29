using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace ThousandAndFirst.DevTests
{
	internal static class KingdomPolityTestData
	{
		internal const string Realm = "taf:realm:v1:current";
		internal const string Rival = "taf:polity:rival";
		internal const string CurrentProfile = "taf:polity-profile:current";
		internal const string RivalProfile = "taf:polity-profile:rival";
		internal const string Route = "taf:route:current-rival";
		internal const string Front = "taf:front:crossing";
		internal const string Grievance = "taf:grievance:crossing";
		internal const string Cohort = "taf:cohort:rival-envoy";
		internal const string Incident = "taf:incident:crossing";
		internal const string Plan = "taf:incident-plan:crossing";
		internal const string Settlement =
			"taf:settlement:v1:cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
		internal const string DigestA =
			"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
		internal const string DigestB =
			"bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

		internal static KingdomPolityLedger Full()
		{
			Assert.IsTrue(KingdomPolityRules.TryCreate(Realm, KingdomPolityImportPolicy.Off,
				out KingdomPolityLedger l, out string failure), failure);
			l.Polities.Add(Polity(Rival, "The Returned Brass", KingdomPolitySource.ImportedLegacy,
				RivalProfile, "taf:faction:rival"));
			l.Polities.Add(Polity(Realm, "Our Thousandth Home", KingdomPolitySource.CurrentRealm,
				CurrentProfile, Realm));
			l.Profiles.Add(Profile(CurrentProfile, Realm, "taf:fact:current-covenant", "settler",
				"warden", KingdomPolityLoadoutPolicyKind.StockPreserve));
			l.Profiles.Add(Profile(RivalProfile, Rival, "taf:fact:legacy-brass", "goatfolk",
				"envoy", KingdomPolityLoadoutPolicyKind.BoundedAdd));
			l.Relations.Add(Relation("taf:relation:current-rival", Realm, Rival,
				KingdomPolityRelationBand.Rival, "taf:event:crossing-claim"));
			l.Relations.Add(Relation("taf:relation:rival-current", Rival, Realm,
				KingdomPolityRelationBand.Contact, "taf:event:rival-arrival"));
			l.Routes.Add(new KingdomPolityRouteRecord
			{
				RouteId = Route, EventStreamId = "taf:stream:route-current-rival",
				OriginId = "taf:site:current", DestinationId = "taf:site:rival",
				OrderedPath = new List<string> { "taf:site:current", "taf:site:rival" },
				Mode = KingdomPolityRouteMode.Caravan, Purpose = KingdomPolityRoutePurpose.Delegation,
				Phase = KingdomPolityRoutePhase.Preparing, SegmentIndex = 0, NextDueTick = 1200L,
				ManifestOrErrandId = "taf:errand:terms", CounterpartyRef = Rival, FrontId = Front
			});
			l.Grievances.Add(new KingdomPolityGrievanceRecord
			{
				GrievanceId = Grievance, IssuerPolityId = Rival, TargetPolityId = Realm,
				Cause = KingdomPolityGrievanceCause.Claim, SourceEventId = "taf:event:crossing-claim",
				Severity = 2, EvidenceRefs = new List<string> { "taf:evidence:posted-claim" },
				Phase = KingdomPolityGrievancePhase.Consumed, ConsumedByIncidentId = Incident
			});
			l.Fronts.Add(new KingdomPolityFrontRecord
			{
				FrontId = Front, TargetKind = KingdomPolityFrontTarget.Route, TargetRef = Route,
				PressureBand = 2, NextDueEventTick = 2400L,
				GrievanceRefs = new List<string> { Grievance }, Phase = KingdomPolityFrontPhase.Friction
			});
			l.Cohorts.Add(CohortPlan());
			l.NamedFigures.Add(new KingdomPolityNamedFigureRecord
			{
				FigureId = "taf:figure:current-successor", PolityId = Realm,
				DisplayName = "Ira of the First Well", RoleKey = "successor",
				Origin = KingdomPolityFigureOrigin.Successor,
				Phase = KingdomPolityFigurePhase.Active, CauseRef = "taf:event:successor-seated",
				ChronicleRef = "taf:chronicle:current-successor", ResidentId = 17,
				ResidentSettlementId = Settlement
			});
			l.NamedFigures.Add(new KingdomPolityNamedFigureRecord
			{
				FigureId = "taf:figure:rival-envoy", PolityId = Rival, DisplayName = "Nara of Brass",
				RoleKey = "envoy", Origin = KingdomPolityFigureOrigin.LegacyEnvoy,
				Phase = KingdomPolityFigurePhase.Active, CauseRef = "taf:event:rival-arrival",
				ChronicleRef = "taf:chronicle:rival-envoy"
			});
			l.Incidents.Add(IncidentPlan());
			l.Projections.Add(Projection("taf:projection:faction-rival",
				KingdomPolityProjectionKind.Faction, Rival));
			l.Projections.Add(Projection("taf:projection:incident-view",
				KingdomPolityProjectionKind.IncidentView, Plan));
			Assert.IsTrue(KingdomPolityRules.TryValidate(l, out failure), failure);
			return l;
		}

		internal static void ClearResidentBridges(KingdomPolityLedger Ledger)
		{
			for (int i = 0; i < Ledger.NamedFigures.Count; i++)
			{
				Ledger.NamedFigures[i].ResidentId = 0;
				Ledger.NamedFigures[i].ResidentSettlementId = null;
			}
		}

		private static KingdomPolityRecord Polity(string Id, string Name, KingdomPolitySource Source,
			string Profile, string Faction)
		{
			return new KingdomPolityRecord { PolityId = Id, DisplayName = Name, NameRevision = 1,
				Source = Source, Lifecycle = KingdomPolityLifecycle.Active, ProfileId = Profile,
				ProfileRevision = 1, ProjectedFactionId = Faction };
		}

		internal static KingdomPolityProfileRevision Profile(string Id, string Owner, string Fact,
			string Body, string Role, KingdomPolityLoadoutPolicyKind Policy)
		{
			KingdomPolityProfileRevision p = new KingdomPolityProfileRevision
			{
				ProfileId = Id, Revision = 1, PolityId = Owner, EffectiveTick = 10L, RulesVersion = 1,
				DerivedFromFactIds = new List<string> { Fact }, FactsDigest = DigestA,
				TechnologyBand = 2, PracticeTags = new List<string> { "water-covenant" },
				BodyKeys = new List<string> { Body }, RoleKeys = new List<string> { Role },
				GearKeys = new List<string> { "bronze" },
				Loadout = new KingdomPolityLoadoutPolicy { Kind = Policy, ExpectedValueBudget = 200 }
			};
			if (Policy != KingdomPolityLoadoutPolicyKind.StockPreserve)
				p.Loadout.SelectedKeys.Add("bronze");
			return p;
		}

		private static KingdomPolityRelation Relation(string Id, string From, string To,
			KingdomPolityRelationBand Band, string Source)
		{
			return new KingdomPolityRelation { RelationId = Id, FromPolityId = From, ToPolityId = To,
				Band = Band, SourceRefs = new List<string> { Source }, ChangedTick = 20L };
		}

		private static KingdomPolityCohortPlan CohortPlan()
		{
			return new KingdomPolityCohortPlan
			{
				CohortId = Cohort, Purpose = KingdomPolityCohortPurpose.Envoy, SourceRef = Route,
				PolityId = Rival, ProfileId = RivalProfile, ProfileRevision = 1,
				MinimumLevel = 8, MaximumLevel = 12, SurfaceRef = "taf:site:current", ScaleBudget = 2,
				RoleSlots = new List<string> { "envoy", "guard" },
				ResolvedMembers = new List<KingdomPolityCohortMember>
				{
					new KingdomPolityCohortMember { Ordinal = 0, MemberKey = "taf:cohort-member:envoy",
						BlueprintKey = "Goatfolk", LoadoutKey = "envoy-bronze", SignatureKey = "brass" },
					new KingdomPolityCohortMember { Ordinal = 1, MemberKey = "taf:cohort-member:guard",
						BlueprintKey = "Goatfolk", LoadoutKey = "guard-bronze", SignatureKey = "brass" }
				},
				NamedRepresentativeAllowance = 1, EventStreamId = "taf:stream:rival-envoy",
				RulesVersion = 1, EventOrdinal = 0UL,
				PresentationOptionKind = KingdomExperienceOptionKind.CivicStory,
				PresentationEnableEpoch = 1L, PresentationReservedTick = 20L,
				Phase = KingdomPolityCohortPhase.Planned
			};
		}

		private static KingdomPolityIncidentRecord IncidentPlan()
		{
			return new KingdomPolityIncidentRecord
			{
				IncidentPlanId = Plan, IncidentId = Incident,
				GrievanceRefs = new List<string> { Grievance },
				ParticipantCohortRefs = new List<string> { Cohort },
				DisclosedStakeRefs = new List<string> { "taf:stake:route-access" },
				MaxSystemicWound = 1, Purpose = KingdomPolityCohortPurpose.Envoy,
				EventStreamId = "taf:stream:crossing-incident", RulesVersion = 1,
				EligibleSurfaceRefs = new List<string> { "taf:site:current", "taf:site:rival" },
				InterventionOptionKeys = new List<string> { "arbitrate", "refuse" }
			};
		}

		private static KingdomPolityProjectionReceipt Projection(string Id,
			KingdomPolityProjectionKind Kind, string Source)
		{
			return new KingdomPolityProjectionReceipt { ProjectionId = Id, Kind = Kind,
				SourceRef = Source, Phase = KingdomPolityProjectionPhase.Prepared,
				PriorDigest = DigestA, AppliedDigest = DigestB, PreparedTick = 30L };
		}
	}

	[TestFixture]
	public sealed class KingdomPolityLedgerTests
	{
		[Test]
		public void FullSemanticGraphRoundTripsCanonically()
		{
			KingdomPolityLedger source = KingdomPolityTestData.Full();
			byte[] first = KingdomPolityCodec.EncodeEnvelope(source);
			Assert.AreEqual(KingdomPolityCodec.CurrentWireVersion,
				BitConverter.ToInt32(first, 4));
			KingdomPolityLedger decoded = KingdomPolityCodec.DecodeEnvelope(first);
			Assert.IsTrue(KingdomPolityRules.TryValidate(decoded, out string failure), failure);
			Assert.AreEqual(17, decoded.NamedFigures[0].ResidentId);
			Assert.AreEqual(KingdomPolityTestData.Settlement,
				decoded.NamedFigures[0].ResidentSettlementId);
			CollectionAssert.AreEqual(first, KingdomPolityCodec.EncodeEnvelope(decoded));
		}

		[Test]
		public void WireV2MigratesWithoutInventingBridgeAndPreservesV2Authority()
		{
			KingdomPolityLedger source = KingdomPolityTestData.Full();
			KingdomPolityTestData.ClearResidentBridges(source);
			Assert.IsTrue(KingdomPolityRules.TryObservePresentation(source,
				KingdomPolityPresentationState.Enabled, 80L, out string failure), failure);
			byte[] prior = KingdomPolityCodec.EncodeEnvelopeV2Fixture(source);
			Assert.AreEqual(KingdomPolityCodec.OldestWireVersion,
				BitConverter.ToInt32(prior, 4));

			KingdomPolityLedger migrated = KingdomPolityCodec.DecodeEnvelope(prior);
			Assert.AreEqual(KingdomPolityRules.CurrentFormatVersion, migrated.FormatVersion);
			Assert.AreEqual(KingdomPolityRules.OldestFormatVersion,
				migrated.MigratedFromVersion);
			Assert.AreEqual(KingdomPolityPresentationState.Enabled,
				migrated.Options.Presentation);
			Assert.AreEqual(source.Projections.Count, migrated.Projections.Count);
			Assert.AreEqual(0, migrated.NamedFigures[0].ResidentId);
			Assert.IsNull(migrated.NamedFigures[0].ResidentSettlementId);
			Assert.IsTrue(KingdomPolityRules.TryValidate(migrated, out failure), failure);
			CollectionAssert.AreNotEqual(prior, KingdomPolityCodec.EncodeEnvelope(migrated));
		}

		[Test]
		public void WireV3MigrationPreservesAmbiguityWithoutInventingCapacityProof()
		{
			KingdomPolityLedger source = KingdomPolityTestData.Full();
			byte[] prior = KingdomPolityCodec.EncodeEnvelopeV3Fixture(source);
			Assert.AreEqual(KingdomPolityCodec.OlderWireVersion,
				BitConverter.ToInt32(prior, 4));
			KingdomPolityLedger migrated = KingdomPolityCodec.DecodeEnvelope(prior);
			Assert.AreEqual(KingdomPolityRules.OlderFormatVersion,
				migrated.MigratedFromVersion);
			Assert.AreEqual(KingdomExperienceOptionKind.None,
				migrated.Cohorts[0].PresentationOptionKind);
			Assert.AreEqual(0L, migrated.Cohorts[0].PresentationEnableEpoch);
			Assert.AreEqual(0L, migrated.Cohorts[0].PresentationReservedTick);
			Assert.IsTrue(KingdomPolityRules.TryValidate(migrated, out string failure), failure);
			CollectionAssert.AreNotEqual(prior, KingdomPolityCodec.EncodeEnvelope(migrated));
		}

		[Test]
		public void WireV4MigrationAddsNoHospitalityTransaction()
		{
			KingdomPolityLedger source = KingdomPolityTestData.Full();
			byte[] prior = KingdomPolityCodec.EncodeEnvelopeV4Fixture(source);
			Assert.AreEqual(KingdomPolityCodec.PriorWireVersion,
				BitConverter.ToInt32(prior, 4));
			KingdomPolityLedger migrated = KingdomPolityCodec.DecodeEnvelope(prior);
			Assert.AreEqual(KingdomPolityRules.PriorFormatVersion,
				migrated.MigratedFromVersion);
			for (int i = 0; i < migrated.Incidents.Count; i++)
				Assert.IsNull(migrated.Incidents[i].Hospitality);
			Assert.IsTrue(KingdomPolityRules.TryValidate(migrated,
				out string failure), failure);
		}

		[Test]
		public void WireV5MigratesAndRewritesStableV6()
		{
			KingdomPolityLedger source = KingdomPolityTestData.Full();
			byte[] prior = KingdomPolityCodec.EncodeEnvelopeV5Fixture(source);
			Assert.AreEqual(KingdomPolityCodec.ImmediatePriorWireVersion,
				BitConverter.ToInt32(prior, 4));
			KingdomPolityLedger migrated = KingdomPolityCodec.DecodeEnvelope(prior);
			Assert.AreEqual(KingdomPolityRules.CurrentFormatVersion, migrated.FormatVersion);
			Assert.AreEqual(KingdomPolityRules.ImmediatePriorFormatVersion,
				migrated.MigratedFromVersion);
			byte[] current = KingdomPolityCodec.EncodeEnvelope(migrated);
			Assert.AreEqual(KingdomPolityCodec.CurrentWireVersion,
				BitConverter.ToInt32(current, 4));
			CollectionAssert.AreEqual(current, KingdomPolityCodec.EncodeEnvelope(
				KingdomPolityCodec.DecodeEnvelope(current)));
		}

		/// <summary>
		/// Every historical fixture writer must refuse phase 6, and must refuse it BECAUSE of the
		/// phase. A bare Assert.Throws proved nothing here: this ledger with an Abandoned cohort
		/// also fails semantic validation ("manifested cohort lacks receipt") and, for v2, the
		/// resident-bridge gate, so the write guard was never reached. The message assertions and
		/// the unmodified-ledger controls pin the refusal to the phase-6 guard itself.
		/// </summary>
		[Test]
		public void HistoricalFixtureWritersRejectAbandonedPhaseSix()
		{
			Assert.DoesNotThrow(() => KingdomPolityCodec.EncodeEnvelopeV5Fixture(
				KingdomPolityTestData.Full()), "negative control: the unflipped ledger encodes");
			KingdomPolityLedger source = KingdomPolityTestData.Full();
			source.Cohorts[0].Phase = KingdomPolityCohortPhase.Abandoned;
			source.Cohorts[0].RewardEventId = null;
			StringAssert.Contains("Wire-v2 fixture cannot carry phase 6.",
				Assert.Throws<InvalidDataException>(
					() => KingdomPolityCodec.EncodeEnvelopeV2Fixture(source)).Message);
			StringAssert.Contains("Wire-v3 fixture cannot carry phase 6.",
				Assert.Throws<InvalidDataException>(
					() => KingdomPolityCodec.EncodeEnvelopeV3Fixture(source)).Message);
			StringAssert.Contains("Wire-v4 fixture cannot carry phase 6.",
				Assert.Throws<InvalidDataException>(
					() => KingdomPolityCodec.EncodeEnvelopeV4Fixture(source)).Message);
			StringAssert.Contains("Wire-v5 fixture cannot carry phase 6.",
				Assert.Throws<InvalidDataException>(
					() => KingdomPolityCodec.EncodeEnvelopeV5Fixture(source)).Message);

			// v1 forbids projection rows outright while a valid Abandoned cohort requires a
			// committed manifestation projection, so this refusal exists only because the phase
			// guard is asked before validation. Its control is the same ledger unflipped.
			KingdomPolityLedger legacy = KingdomPolityTestData.Full();
			KingdomPolityTestData.ClearResidentBridges(legacy);
			legacy.Projections.Clear();
			Assert.DoesNotThrow(() => KingdomPolityCodec.EncodeEnvelopeV1Fixture(legacy),
				"negative control: the unflipped v1-shaped ledger encodes");
			legacy.Cohorts[0].Phase = KingdomPolityCohortPhase.Abandoned;
			legacy.Cohorts[0].RewardEventId = null;
			StringAssert.Contains("Wire-v1 fixture cannot carry phase 6.",
				Assert.Throws<InvalidDataException>(
					() => KingdomPolityCodec.EncodeEnvelopeV1Fixture(legacy)).Message);
		}

		/// <summary>
		/// The phase guard runs before validation, so it must not dereference a shape validation
		/// has not refused yet. A null cohort row is validation's to reject: the writer must still
		/// answer with its typed refusal, never a NullReferenceException.
		/// </summary>
		[Test]
		public void HistoricalFixtureWritersRefuseANullCohortRowWithoutDereferencingIt()
		{
			KingdomPolityLedger source = KingdomPolityTestData.Full();
			source.Cohorts[0] = null;
			foreach (TestDelegate write in new TestDelegate[]
			{
				() => KingdomPolityCodec.EncodeEnvelopeV1Fixture(source),
				() => KingdomPolityCodec.EncodeEnvelopeV2Fixture(source),
				() => KingdomPolityCodec.EncodeEnvelopeV3Fixture(source),
				() => KingdomPolityCodec.EncodeEnvelopeV4Fixture(source),
				() => KingdomPolityCodec.EncodeEnvelopeV5Fixture(source)
			})
				StringAssert.Contains("cohort plan is invalid or noncanonical",
					Assert.Throws<InvalidDataException>(write).Message);
		}

		[TestCase("MlBBVAEAAACPAAAAAQAAAAH/////AAAAAAEAAAByAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAQAAAAEAAABjAQEAAABzAQAAAHABAAAAZgEAAAABAAAAAQAAAAEAAAB4AQAAAAAAAAAAAAAAAAAAAAEAAABlAQAAAAAAAAAAAAAABv//////////AAAAAAAAAAA=", 1, 126)]
		[TestCase("MlBBVAIAAAC+AAAAAgAAAAH/////AAAAAAEAAAByAAAAAAAAAAAAAQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAQAAAAEAAABjAQEAAABzAQAAAHABAAAAZgEAAAABAAAAAQAAAAEAAAB4AQAAAAAAAAAAAAAAAAAAAAEAAABlAQAAAAAAAAAAAAAABv//////////AAAAAAAAAAAAAAAAAAAAAAAAAAD/////AAAAAA==", 2, 153)]
		[TestCase("MlBBVAMAAAC+AAAAAwAAAAH/////AAAAAAEAAAByAAAAAAAAAAAAAQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAQAAAAEAAABjAQEAAABzAQAAAHABAAAAZgEAAAABAAAAAQAAAAEAAAB4AQAAAAAAAAAAAAAAAAAAAAEAAABlAQAAAAAAAAAAAAAABv//////////AAAAAAAAAAAAAAAAAAAAAAAAAAD/////AAAAAA==", 3, 153)]
		[TestCase("MlBBVAQAAADPAAAABAAAAAH/////AAAAAAEAAAByAAAAAAAAAAAAAQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAQAAAAEAAABjAQEAAABzAQAAAHABAAAAZgEAAAABAAAAAQAAAAEAAAB4AQAAAAAAAAAAAAAAAAAAAAEAAABlAQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAG//////////8AAAAAAAAAAAAAAAAAAAAAAAAAAP////8AAAAA", 4, 170)]
		[TestCase("MlBBVAUAAADPAAAABQAAAAH/////AAAAAAEAAAByAAAAAAAAAAAAAQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAQAAAAEAAABjAQEAAABzAQAAAHABAAAAZgEAAAABAAAAAQAAAAEAAAB4AQAAAAAAAAAAAAAAAAAAAAEAAABlAQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAG//////////8AAAAAAAAAAAAAAAAAAAAAAAAAAP////8AAAAA", 5, 170)]
		public void IndependentlyFrozenHistoricalPhaseSixEnvelopeIsRejected(string Base64,
			int Wire, int PhaseOffset)
		{
			byte[] hostile = Convert.FromBase64String(Base64);
			Assert.AreEqual(Wire, BitConverter.ToInt32(hostile, 4), "frozen fixture wire version");
			Assert.AreEqual(6, hostile[12 + PhaseOffset],
				"the frozen fixture no longer carries phase 6 in its cohort phase slot");
			Assert.Throws<InvalidDataException>(() => KingdomPolityCodec.DecodeEnvelopeRaw(hostile));

			// Negative control. The identical envelope with that one byte lowered to a
			// historically admitted phase decodes, so the refusal above is caused by phase 6
			// and not by framing, length, nested format, or any other property of these bytes.
			byte[] admitted = (byte[])hostile.Clone();
			admitted[12 + PhaseOffset] = (byte)KingdomPolityCohortPhase.Cancelled;
			KingdomPolityLedger decoded = KingdomPolityCodec.DecodeEnvelopeRaw(admitted);
			Assert.AreEqual(KingdomPolityCohortPhase.Cancelled, decoded.Cohorts[0].Phase);
		}

		[Test]
		public void WireV1MigratesFailClosedPresentationAndRewritesCurrent()
		{
			KingdomPolityLedger source = KingdomPolityTestData.Full();
			KingdomPolityTestData.ClearResidentBridges(source);
			source.Projections.Clear();
			byte[] prior = KingdomPolityCodec.EncodeEnvelopeV1Fixture(source);
			KingdomPolityLedger migrated = KingdomPolityCodec.DecodeEnvelope(prior);
			Assert.AreEqual(KingdomPolityRules.CurrentFormatVersion, migrated.FormatVersion);
			Assert.AreEqual(KingdomPolityRules.LegacyFormatVersion, migrated.MigratedFromVersion);
			Assert.AreEqual(KingdomPolityPresentationState.Unobserved, migrated.Options.Presentation);
			Assert.IsFalse(KingdomPolityRules.CanEmitOptionalProjection(migrated, long.MaxValue));
			Assert.IsTrue(KingdomPolityRules.TryValidate(migrated, out string failure), failure);
			CollectionAssert.AreNotEqual(prior, KingdomPolityCodec.EncodeEnvelope(migrated));
		}

		[Test]
		public void LegacyFixtureRefusesResidentBridgeInsteadOfDroppingIt()
		{
			KingdomPolityLedger source = KingdomPolityTestData.Full();
			Assert.Throws<InvalidDataException>(() =>
				KingdomPolityCodec.EncodeEnvelopeV2Fixture(source));
			source.Projections.Clear();
			Assert.Throws<InvalidDataException>(() =>
				KingdomPolityCodec.EncodeEnvelopeV1Fixture(source));

			KingdomPolityTestData.ClearResidentBridges(source);
			source.MigratedFromVersion = KingdomPolityRules.PriorFormatVersion;
			Assert.Throws<InvalidDataException>(() =>
				KingdomPolityCodec.EncodeEnvelopeV1Fixture(source),
				"wire-v1 cannot claim provenance from a later schema");
		}

		[Test]
		public void MigrationPreservesEarliestValidProvenanceAndQuarantinesImpossibleEvidence()
		{
			KingdomPolityLedger source = KingdomPolityTestData.Full();
			KingdomPolityTestData.ClearResidentBridges(source);
			source.MigratedFromVersion = KingdomPolityRules.LegacyFormatVersion;
			KingdomPolityLedger migrated = KingdomPolityCodec.DecodeEnvelope(
				KingdomPolityCodec.EncodeEnvelopeV2Fixture(source));
			Assert.AreEqual(KingdomPolityRules.LegacyFormatVersion,
				migrated.MigratedFromVersion);
			Assert.IsTrue(KingdomPolityRules.TryValidate(migrated, out string failure), failure);

			KingdomPolityLedger impossible = KingdomPolityTestData.Full();
			impossible.FormatVersion = KingdomPolityRules.PriorFormatVersion;
			impossible.MigratedFromVersion = KingdomPolityRules.CurrentFormatVersion;
			KingdomPolityRules.Normalize(impossible);
			Assert.AreEqual(KingdomPolitySchemaState.Quarantined, impossible.SchemaState);
			StringAssert.Contains("provenance", impossible.SchemaFault);

			KingdomPolityLedger blankV2 = new KingdomPolityLedger
			{
				FormatVersion = KingdomPolityRules.PriorFormatVersion
			};
			KingdomPolityRules.Normalize(blankV2);
			Assert.AreEqual(KingdomPolitySchemaState.Compatible, blankV2.SchemaState);
			Assert.AreEqual(KingdomPolityRules.CurrentFormatVersion, blankV2.FormatVersion);
			Assert.AreEqual(long.MaxValue, blankV2.Options.FutureCauseFloorTick);
			Assert.IsTrue(KingdomPolityRules.TryValidate(blankV2, out failure), failure);
		}

		[Test]
		public void FutureWireIsOpaqueInertAndByteStable()
		{
			byte[] future = KingdomPolityCodec.EncodeEnvelope(KingdomPolityTestData.Full());
			future[4] = 77; future[5] = future[6] = future[7] = 0;
			KingdomPolityLedger decoded = KingdomPolityCodec.DecodeEnvelope(future);
			Assert.AreEqual(KingdomPolitySchemaState.Unknown, decoded.SchemaState);
			Assert.IsFalse(KingdomPolityRules.Usable(decoded));
			CollectionAssert.AreEqual(future, KingdomPolityCodec.EncodeEnvelope(decoded));
		}
	}
}
