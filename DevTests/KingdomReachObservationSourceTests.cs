#if TAF_TESTS
using System;
using System.IO;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomReachObservationSourceTests
	{
		private static string Read(string Path) => TestMain.ReadRepositoryText(Path);

		private static string Slice(string Source, string Start, string End)
		{
			int at = Source.IndexOf(Start, StringComparison.Ordinal);
			Assert.That(at, Is.GreaterThanOrEqualTo(0), Start);
			int until = Source.IndexOf(End, at + Start.Length, StringComparison.Ordinal);
			Assert.That(until, Is.GreaterThan(at), End);
			return Source.Substring(at, until - at);
		}

		private static void Ordered(string Source, params string[] Terms)
		{
			int prior = -1;
			for (int i = 0; i < Terms.Length; i++)
			{
				int at = Source.IndexOf(Terms[i], prior + 1, StringComparison.Ordinal);
				Assert.That(at, Is.GreaterThan(prior), Terms[i]); prior = at;
			}
		}

		private static int Count(string Source, string Term)
		{
			int count = 0, at = 0;
			while ((at = Source.IndexOf(Term, at, StringComparison.Ordinal)) >= 0)
			{ count++; at += Term.Length; }
			return count;
		}

		[Test]
		public void NominalReadsUseOnlyExactReceiptsAndNeverCleanState()
		{
			string ground = Read("Growth/KingdomReach.GroundCharacter.cs");
			string query = Slice(ground, "public static int CityShadeExcept(",
				"public static bool CityShaded(");
			StringAssert.Contains("if (!KingdomOffices.Enabled", query);
			StringAssert.Contains("KingdomReachObservationRuntime.Amount", query);
			StringAssert.DoesNotContain("GetIntGameState", query);
			StringAssert.DoesNotContain("SetIntGameState", query);
			StringAssert.DoesNotContain("TryRevoke", query);
			StringAssert.DoesNotContain("RemoveZoneProperty", query);

			string runtime = Read("Growth/KingdomReachObservationRuntime.cs");
			string amount = Slice(runtime, "internal static int Amount(",
				"internal static bool TryWrite(");
			StringAssert.Contains("TryBinding", amount);
			StringAssert.Contains("TryRaw", amount);
			StringAssert.Contains("TryReadReceipt", amount);
			StringAssert.Contains("TryDecodeVersionedPayload", amount);
			StringAssert.Contains("receipt.SourceRevision", amount);
			StringAssert.DoesNotContain("TryRemoveRaw", amount);
			StringAssert.DoesNotContain("SetIntGameState", amount);
			string read = Slice(runtime, "private static bool TryReadReceipt(",
				"private static int[] Values(");
			Assert.That(Count(read, "TryReadExact"), Is.EqualTo(2));
			Ordered(read, "KingdomReachObservationRules.SourceRevision",
				"KingdomReachObservationRules.LegacySourceRevision");
		}

		[Test]
		public void ActivationRevokesBeforeEveryFallibleObservationAndWritesLast()
		{
			string offices = Slice(Read("Growth/KingdomReach.Offices.cs"),
				"public static void OnZoneActivated(", "private static bool TryObservationSourceRow(");
			Ordered(offices,
				"KingdomReachObservationRuntime.TryRevokeZone",
				"if (!KingdomOffices.Enabled) return;",
				"TryActiveBenefits",
				"readings.Count > KingdomReachObservationRules.MaxAuthorityRows",
				"UpdateSeat",
				"TryObservationSourceRow",
				"KingdomReachObservationRuntime.TryWrite");
			StringAssert.DoesNotContain("Record(Z, shaded, realm)", offices);
			StringAssert.DoesNotContain("SetIntGameState", offices);
		}

		[Test]
		public void ReceiptWriteBindsCurrentTopologySourceAndExactRawReadback()
		{
			string runtime = Read("Growth/KingdomReachObservationRuntime.cs");
			string write = Slice(runtime, "internal static bool TryWrite(",
				"internal static bool TryRevokeOwned(");
			Ordered(write, "ReferenceEquals(Zone, The.ZoneManager.ActiveZone)",
				"TryBinding(System, zoneId, settlementId",
				"SameKindOrder", "TryAuthorityDigest", "TryEncodePayload",
				"KingdomZoneObservationRules.TryCreate", "TryRevokeZone(zoneId",
				"Zone.SetZoneProperty", "raw?.GetType() != typeof(string)", "TryReadExact");
			StringAssert.Contains("System.TryExactSettlementIds(true", runtime);
			StringAssert.Contains("System.OwnedZone(ZoneId)", runtime);
			StringAssert.Contains("System.SettlementIdForOwnedZone(ZoneId)", runtime);
			StringAssert.Contains("Receipt.ObservedTick > CurrentTick",
				Read("Growth/KingdomZoneObservationCodec.cs"));
		}

		[Test]
		public void DesignationDigestFreezesAuthorityGeometryAndEffectivePayload()
		{
			string offices = Slice(Read("Growth/KingdomReach.Offices.cs"),
				"private static bool TryObservationSourceRow(",
				"private static void UpdateSeat(");
			foreach (string term in new[] { "d.ProviderId", "d.ProviderVersion", "d.Identity",
				"d.Revision", "d.ZoneId", "d.RootId", "d.BuildingKey", "d.LotId",
				"SeatHolderProperty", "d.Caps", "d.AcceptedTags", "d.Cells",
				"cell.Use", "cell.Cover", "Reading.Carries", "Reading.Provides" })
				StringAssert.Contains(term, offices);
			string rules = Read("Growth/KingdomReachObservationRules.cs");
			StringAssert.Contains("taf.reach.authority/v1", rules);
			StringAssert.Contains("taf.reach.zone/v2", rules);
			StringAssert.Contains("taf.reach.zone/v1", rules);
			StringAssert.Contains("LegacyPayloadPrefix = \"rp1\"", rules);
			StringAssert.Contains("PayloadPrefix = \"rp2\"", rules);
			StringAssert.Contains("City = ExpandLegacy(city)", rules);
			StringAssert.Contains("legacy ? LegacySourceRevision : SourceRevision", rules);
			StringAssert.Contains("sorted.Sort(StringComparer.Ordinal)", rules);
			StringAssert.Contains("string.Equals(sorted[i - 1], sorted[i]", rules);
		}

		[Test]
		public void LegacyIntegersAreWriteZeroOnlyAndNeverPromoted()
		{
			string ground = Read("Growth/KingdomReach.GroundCharacter.cs");
			string runtime = Read("Growth/KingdomReachObservationRuntime.cs");
			StringAssert.Contains("Retired pre-release key prefix", ground);
			StringAssert.DoesNotContain("GetIntGameState", ground);
			StringAssert.Contains("SetIntGameState(city, 0)", runtime);
			StringAssert.Contains("SetIntGameState(realm, 0)", runtime);
			StringAssert.Contains("GetIntGameState(city) != 0", runtime);
			StringAssert.Contains("GetIntGameState(realm) != 0", runtime);
			StringAssert.Contains("SameKindOrder(KingdomReachRules.LiftOrder)", runtime);
			StringAssert.DoesNotContain("GetIntGameState(CityStatePrefix", runtime);
			StringAssert.DoesNotContain("GetIntGameState(RealmStatePrefix", runtime);
		}

		[Test]
		public void OwnershipTransitionsRevokeBeforeTopologyLossOrReset()
		{
			string secession = Read("Core/KingdomCreed.04.SecessionAndRejoin.cs");
			Ordered(secession, "IList<string> leavingClaims",
				"KingdomZoneObservationRevocation.TryRevokeZones(leavingClaims",
				"KingdomRelocation.BeforeOwnershipLoss", "TryRemoveNonSeatSettlement");
			string exile = Slice(Read("Core/KingdomSystem.z09.Exile.Dispatch.cs"),
				"if (archive.Phase == KingdomRealmArchivePhase.Resetting)",
				"private bool DispatchExileChronicle(");
			Ordered(exile, "KingdomZoneObservationRevocation.TryRevokeOwned",
				"KingdomPolityRealmTransitionRuntime.TryAdvanceExile", "ResetCurrentRealmAfterExile");
			string coordinator = Read("Core/KingdomZoneObservationRevocation.cs");
			StringAssert.Contains("KingdomReachObservationRuntime.TryRevokeZones", coordinator);
			StringAssert.Contains("KingdomReachObservationRuntime.TryRevokeOwned", coordinator);
		}

		[Test]
		public void ReceiptHasRemovalCoverageButNoCityBookOrArchiveCodecSurface()
		{
			string coverage = Read("Core/KingdomRemovalCoverage.cs");
			StringAssert.Contains("\"r_TAF_ReachObservation_v1\"", coverage);
			foreach (string path in new[] {
				"Simulation/City/KingdomCityBook.cs",
				"Core/KingdomArchivedSettlementCodec.cs",
				"Core/KingdomRealmArchive.01Capture.cs" })
				StringAssert.DoesNotContain("ReachObservation", Read(path), path);
		}

		[Test]
		public void ReceiptShardsStayUnderProductionSizeCapAndRegisterOnce()
		{
			string[] production = {
				"Growth/KingdomZoneObservationReceipt.cs",
				"Growth/KingdomZoneObservationCodec.cs",
				"Growth/KingdomReachObservationRules.cs",
				"Growth/KingdomReachObservationRuntime.cs",
				"Growth/KingdomReach.GroundCharacter.cs",
				"Growth/KingdomReach.Offices.cs",
				"Core/KingdomCreed.04.SecessionAndRejoin.cs",
				"Core/KingdomSystem.z09.Exile.Dispatch.cs"
			};
			for (int i = 0; i < production.Length; i++)
			{
				int lines = Read(production[i]).Split('\n').Length;
				Assert.That(lines, Is.LessThanOrEqualTo(300), production[i]);
			}
			foreach (string project in new[] { "DevTests/TafTests.csproj",
				"DevTests/PortableTests.csproj" })
			{
				string source = Read(project);
				foreach (string file in new[] { "KingdomZoneObservationReceipt.cs",
					"KingdomZoneObservationCodec.cs", "KingdomReachObservationRules.cs",
					"KingdomZoneObservationReceiptTests.cs",
					"KingdomReachObservationSourceTests.cs" })
					Assert.That(Count(source, file), Is.EqualTo(1), project + ": " + file);
			}
		}
	}
}
#endif
