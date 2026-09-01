#if TAF_TESTS
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	public class KingdomInheritanceSpatialSourceTests
	{
		[Test]
		public void CaptureWitnessesOnlyLoadedGroundAndFrozenReceipts()
		{
			string capture = KingdomInheritanceSpatialLogicalSource.Read();
			StringAssert.Contains("Active.ZoneID != Record.GroundZoneId", capture);
			StringAssert.Contains("KingdomArchitectureRuntime.TryRead(root", capture);
			StringAssert.Contains("KingdomArchitectureStamper.TryVerifyComplete(root, Active", capture);
			StringAssert.Contains("KingdomArchitectureRules.IsCurrentSnapshotEncoding", capture);
			StringAssert.Contains("KingdomRoadRules.TryDecode", capture);
			StringAssert.Contains("KingdomPhysicalLookupState.Ambiguous", capture);
			StringAssert.DoesNotContain("KingdomArchitecture.TryResolve", capture);
			StringAssert.DoesNotContain("GetZone(", capture);
			StringAssert.Contains("TryLegacyRect", capture);
			StringAssert.Contains("legacy authored work cannot be represented", capture);
		}

		[Test]
		public void AwayFromSeatReusesOnlySameGenerationAndSpatialBasis()
		{
			string seal = KingdomSealLogicalSource.Read();
			StringAssert.Contains("SameSpatialBasis(prior, Record)", seal);
			StringAssert.Contains("Earlier.LegacyId != Current.LegacyId", seal);
			StringAssert.Contains("Earlier.GroundZoneId != Current.GroundZoneId", seal);
			StringAssert.Contains("KingdomInheritanceSpatial.CopyEvidence", seal);
		}

		[Test]
		public void ReconstructionUsesSameStamperAndStripsEveryCreatedLayer()
		{
			string engine = KingdomInheritEngineLogicalSource.Read();
			StringAssert.Contains("KingdomArchitectureStamper.TryInitializeOwner", engine);
			StringAssert.Contains("KingdomArchitectureStamper.TryStageLayer(obj, Zone", engine);
			StringAssert.Contains("ArchitectureLayer.Ground", engine);
			StringAssert.Contains("ArchitectureLayer.Structure", engine);
			StringAssert.Contains("ArchitectureLayer.Object", engine);
			StringAssert.Contains("ScrubArchitecture(obj", engine);
			StringAssert.Contains("KingdomArchitectureStamper.TryVerifyComplete", engine);
			StringAssert.Contains("KingdomInheritanceFabricRules.WearFor", engine);
			StringAssert.Contains("KingdomInheritanceFabricRules.MarksComponent", engine);
			StringAssert.Contains("item.RequirePart<r_KingdomInheritedFabric>()", engine);
			StringAssert.Contains("item.hitpoints = Math.Max(1, item.baseHitpoints * condition / 100)",
				engine);
			StringAssert.DoesNotContain("Ruiner", engine);
		}

		[Test]
		public void PreflightUsesFrozenOffCenterRectAndMissingFabricDegradesLocally()
		{
			string engine = KingdomInheritEngineLogicalSource.Read();
			StringAssert.Contains("left = rect.X1", engine);
			StringAssert.Contains("top = rect.Y1", engine);
			StringAssert.Contains("int left = spec.FootprintX", engine);
			StringAssert.Contains("missingArchitecture", engine);
			StringAssert.Contains("ObjectDegradedHashProperty", engine);
			StringAssert.Contains("resolved = \"r_KingdomCairn\"", engine);
			StringAssert.Contains("degraded || ExactArchitecture(obj, Spec)", engine);
			StringAssert.Contains("!blueprint.HasPart(\"Brain\")", engine);
			StringAssert.Contains("GetPartParameter(\"Physics\", \"Takeable\", false)", engine);
			StringAssert.Contains("ObjectAuthorityMemoryProperty", engine);
			StringAssert.Contains("remembers the old settlement's founding heart", engine);
		}

		[Test]
		public void QuarantinedStamperRollbackUsesRawExactLotAndHash()
		{
			string engine = KingdomInheritEngineLogicalSource.Read();
			StringAssert.Contains("TryStageLayer quarantines a failed owner", engine);
			StringAssert.Contains("KingdomArchitectureStamper.LotIdProperty", engine);
			StringAssert.Contains("KingdomArchitectureStamper.HashProperty", engine);
			StringAssert.Contains("ComponentHashProperty) == hash", engine);
		}

		[Test]
		public void CurrentRecordIsExternalSchemaNotSaveAbiAppend()
		{
			string record = KingdomSealRecordLogicalSource.Read();
			StringAssert.Contains("CurrentSchema = 6", record);
			StringAssert.Contains("KeyWorkSnapshot", record);
			StringAssert.Contains("KeyStreetX", record);
			string system = KingdomSystemLogicalSource.Read();
			string settlement = KingdomSettlementLogicalSource.Read();
			StringAssert.DoesNotContain("WorkSnapshots", system);
			StringAssert.DoesNotContain("WorkSnapshots", settlement);
			StringAssert.DoesNotContain("SpatialVersion", system);
			StringAssert.DoesNotContain("SpatialVersion", settlement);
		}

		[Test]
		public void LegacyProxyMarkerVersionIsBoundToImmutableShapeCompatibility()
		{
			string engine = KingdomInheritEngineLogicalSource.Read();
			StringAssert.Contains("LegacyProxyShapeVersion = 1", engine);
			StringAssert.Contains("LegacyReconstructionVersion = 1", engine);
			StringAssert.Contains("ReconstructionVersionFor(canonical)", engine);
			StringAssert.Contains("TryValidateLegacyProxyShape(Legacy.WorkKeys", engine);
			StringAssert.Contains("case \"heartbasin\": Width = 3; Height = 3", engine);
			AssertOrdered(engine, "int reconstruction = ReconstructionVersionFor(canonical);",
				"KingdomInheritRules.TryPrepare(canonical,",
				"TryComposeApplicationMarker(canonical, Receipt,");
		}

		[Test]
		public void PersistedInheritanceArtifactsDeriveTheExactRecordsShapeVersion()
		{
			string state = KingdomInheritanceStateLogicalSource.Read();
			StringAssert.Contains("ReconstructionVersionForText(", state);
			StringAssert.Contains("ReconstructionVersionFor(legacy)", state);
			StringAssert.Contains("ReconstructionVersionFor(Legacy)", state);
			StringAssert.DoesNotContain("KingdomInheritEngine.ReconstructionVersion,", state);
			StringAssert.DoesNotContain("KingdomInheritEngine.ReconstructionVersion)", state);
		}

		private static void AssertOrdered(string Source, params string[] Terms)
		{
			int offset = 0;
			for (int i = 0; i < Terms.Length; i++)
			{
				int found = Source.IndexOf(Terms[i], offset, System.StringComparison.Ordinal);
				Assert.GreaterOrEqual(found, 0, "missing ordered term: " + Terms[i]);
				offset = found + Terms[i].Length;
			}
		}
	}
}
#endif
