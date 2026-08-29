#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomCivicExperienceSourceTests
	{
		private static string Read(string Relative)
		{
			return TestMain.ReadRepositoryText(Relative);
		}

		private static string Logical(string Prefix)
		{
			string root = Path.Combine(TestMain.RepositoryRoot, "Experience");
			List<string> files = new List<string>(Directory.GetFiles(root,
				Prefix + "*.cs", SearchOption.TopDirectoryOnly));
			files.Sort(StringComparer.Ordinal);
			string source = "";
			for (int i = 0; i < files.Count; i++) source += File.ReadAllText(files[i]);
			return source;
		}

		[Test]
		public void CivicOfficeIsAnExplicitTwoResidentTitleOnlyChoice()
		{
			string context = Read("Experience/KingdomOfficeRuntime.Context.cs");
			string open = Read("Experience/KingdomOfficeRuntime.Open.cs");
			string offer = Read("Experience/KingdomOfficeOfferRules.cs");
			StringAssert.Contains("Exactly two eligible named residents", open);
			StringAssert.Contains("KingdomOfficeOfferRules.TryOffer", context);
			StringAssert.Contains("ReferenceEquals(body.CurrentZone, Context.Zone)", context);
			StringAssert.Contains("!body.IsPlayer() && !body.IsPlayerLed()", context);
			StringAssert.Contains("Eligible = row.Standing == KingdomResidentStanding.Resident && loaded",
				context);
			StringAssert.Contains("System.OwnedZone(Zone.ZoneID)", context);
			StringAssert.Contains("FindNonSeatSettlementByZone", context);
			StringAssert.DoesNotContain("Stand on the seated city's held ground", context);
			StringAssert.Contains("First = null; Second = null; return false", offer);
			StringAssert.Contains("A.ArrivedTick < B.ArrivedTick", offer);
			StringAssert.Contains("Leave the office vacant", open);
			StringAssert.Contains("no service, stock, capability, or succession claim", open);
			StringAssert.DoesNotContain("KingdomResidents.TryHead", Logical("KingdomOfficeRuntime"));
		}

		[Test]
		public void OfficeProjectionOwnsOnlyItsExactRoleAndMarker()
		{
			string projection = Read("Experience/KingdomOfficeRuntime.Projection.cs");
			string commands = Read("Experience/KingdomOfficeRuntime.Commands.cs");
			string combined = Logical("KingdomOfficeRuntime")
				+ Read("Experience/r_KingdomOfficeProjection.cs");
			StringAssert.Contains("OwnsRole = Receipt.OwnsRole", projection);
			StringAssert.Contains("Receipt.OwnsRole == already", projection);
			StringAssert.Contains("foreign same-text title appeared", projection);
			StringAssert.Contains("RequireRole(role)", projection);
			StringAssert.Contains("Receipt.OwnsRole && HasRole(roles, role)", projection);
			StringAssert.Contains("roles.RemoveRole(role)", projection);
			StringAssert.Contains("TryPrepareOfficeVacancy", commands);
			StringAssert.Contains("TryCompleteOfficeVacancy", commands);
			StringAssert.DoesNotContain("TakeOnRoleEvent", combined);
			StringAssert.DoesNotContain("KingdomNotables.Mint", combined);
			StringAssert.DoesNotContain("AddSkill", combined);
			StringAssert.DoesNotContain("AddMutation", combined);
			StringAssert.DoesNotContain("Inventory.AddObject", combined);
			string reconcile = Read("Experience/KingdomOfficeRuntime.Reconcile.cs");
			StringAssert.Contains("bool ownsRole = !HasRole", reconcile);
			StringAssert.Contains("Context.Settlement.OfficeHolderResidentId", reconcile);
			StringAssert.Contains("settlement.OfficeHolderResidentId", reconcile);
			StringAssert.Contains("exact-body office marker diverged", projection);
			StringAssert.DoesNotContain("roles.RemoveRole(marker.RoleText)", projection);
		}

		[Test]
		public void HolderDeathBecomesVacancyBeforeIdentityIsForgotten()
		{
			string offices = Read("Experience/KingdomOffices.cs");
			int death = offices.IndexOf("KingdomResidents.TryMarkDead", StringComparison.Ordinal);
			int vacancy = offices.IndexOf("KingdomOfficeRuntime.ObserveHolderLoss",
				StringComparison.Ordinal);
			int forget = offices.IndexOf("KingdomResidentIdentity.Forget", StringComparison.Ordinal);
			Assert.Greater(death, 0); Assert.Greater(vacancy, death); Assert.Greater(forget, vacancy);
			StringAssert.DoesNotContain("HonourDead(", offices);
			StringAssert.DoesNotContain("UpdateOffice(", offices);
			string reconcile = Read("Experience/KingdomOfficeRuntime.Reconcile.cs");
			StringAssert.Contains("KingdomCivicOfficeVacancyCause.AuthorityLost", reconcile);
			StringAssert.Contains("KingdomCivicOfficeVacancyCause.Departure", reconcile);
			string residents = Read(
				"Simulation/City/KingdomResidents.04.ResidentTransitionsAndAccession.cs");
			int depart = residents.IndexOf("KingdomUnbindCause.Abroad", StringComparison.Ordinal);
			int observe = residents.IndexOf("KingdomOfficeRuntime.ObserveHolderLoss",
				StringComparison.Ordinal);
			int removeId = residents.IndexOf("Body.RemoveIntProperty(ResidentIdProperty)",
				StringComparison.Ordinal);
			Assert.Greater(observe, depart); Assert.Greater(removeId, observe);
			StringAssert.Contains("Predecessor", Read("Experience/KingdomExperienceRules.Office.cs"));
		}

		[Test]
		public void TitleOnlyOfficeCannotBuySuccessionOrLegacyShade()
		{
			string succession = "";
			string root = Path.Combine(TestMain.RepositoryRoot, "Experience");
			foreach (string file in Directory.GetFiles(root, "KingdomSuccession*.cs"))
				succession += File.ReadAllText(file);
			StringAssert.DoesNotContain("OfficeHolder", succession);
			StringAssert.DoesNotContain("HoldsOffice", succession);
			StringAssert.DoesNotContain("RegardForOffice", succession);
			StringAssert.DoesNotContain("KingdomNotables.Mint", succession);
			string reconcile = Read("Experience/KingdomOfficeRuntime.Reconcile.cs");
			StringAssert.Contains("System.NotableShade = 0", reconcile);
			string legacyCeremony = Read(
				"Experience/KingdomCeremony.z03.NotableAndPatternBook.cs");
			StringAssert.Contains("System.NotableShade = 0", legacyCeremony);
			StringAssert.DoesNotContain("System.NotableShade = KingdomCeremonyRules.NotableShade",
				legacyCeremony);
			string seat = Read("Core/KingdomSystem.z01.State.Foundation.cs");
			StringAssert.Contains("return (MealShade < 0) ? 0 : MealShade", seat);
			StringAssert.DoesNotContain("NotableShade < 0) ? 0 : NotableShade", seat);
			StringAssert.Contains("NotableShade = 0",
				Read("Core/KingdomSystem.z23.Normalization.cs"));
			StringAssert.Contains("NotableShade = 0",
				Read("Core/KingdomSettlement.Normalize.cs"));
			StringAssert.DoesNotContain("ShadeClause(System.NotableShade)",
				Read("Core/KingdomReports.cs"));
			StringAssert.Contains("Value.NotableShade = 0",
				Read("Core/KingdomArchivedSettlementCodec.DecodeCloneHash.cs"));
		}

		[Test]
		public void RemembranceUsesOneTerminalRowAndOneLoadedNamedMourner()
		{
			string context = Read("Experience/KingdomRemembranceRuntime.Context.cs");
			string open = Read("Experience/KingdomRemembranceRuntime.Open.cs");
			string death = Read("Experience/KingdomOffices.cs");
			string witness = Read("Experience/KingdomOffices.RemembranceEligibility.cs");
			StringAssert.Contains("KingdomResidentStanding.Dead", context);
			StringAssert.Contains("KingdomResidentStanding.Resident", context);
			StringAssert.Contains("ReferenceEquals(body.CurrentZone, Context.Zone)", context);
			StringAssert.Contains("receipt.SubjectResidentId", open);
			StringAssert.DoesNotContain("Deaths(context.State)", open);
			StringAssert.Contains("TryCaptureRemembranceWitness", death);
			StringAssert.Contains("TryRecordRemembranceEligibility", death);
			Assert.Less(death.IndexOf("TryCaptureRemembranceWitness", StringComparison.Ordinal),
				death.IndexOf("KingdomResidents.TryMarkDead", StringComparison.Ordinal));
			Assert.Greater(death.IndexOf("TryRecordRemembranceEligibility", StringComparison.Ordinal),
				death.IndexOf("KingdomResidents.TryMarkDead", StringComparison.Ordinal));
			StringAssert.Contains("TryCreateRemembranceEligibility", witness);
			StringAssert.DoesNotContain("TryObserveConfiguredOptions", witness);
			StringAssert.Contains("System.TryFindSettlement(book", witness);
			StringAssert.Contains("System.OwnedZone(Zone.ZoneID)", context);
			StringAssert.Contains("FindNonSeatSettlementByZone", context);
			StringAssert.Contains("Deferral never expires", open);
			StringAssert.Contains("Decline this remembrance", open);
			StringAssert.Contains("decline changes no standing or death truth", open);
			StringAssert.DoesNotContain("DeadNames", Logical("KingdomRemembranceRuntime"));
			StringAssert.DoesNotContain("DeadOrigins", Logical("KingdomRemembranceRuntime"));
		}

		[Test]
		public void RemembranceUsesNormalDisclosedCommissionAndFixedFixtureKinds()
		{
			string open = Read("Experience/KingdomRemembranceRuntime.Open.cs");
			string capture = Read("Growth/KingdomSurvey.01.Capture.cs");
			string buildings = Read("RuntimeData/KingdomBuildings.xml");
			StringAssert.Contains("KingdomCommission.Commission(System, key", open);
			StringAssert.Contains("return \"nichetomb\"", open);
			StringAssert.Contains("? \"gravegrove\" : \"cairn\"", open);
			StringAssert.Contains("r_KingdomCairn", capture);
			StringAssert.Contains("r_KingdomGraveGrove", capture);
			StringAssert.Contains("r_KingdomNicheTomb", capture);
			StringAssert.Contains("<building Key=\"cairn\"", buildings);
			StringAssert.Contains("<building Key=\"gravegrove\"", buildings);
			StringAssert.Contains("<building Key=\"nichetomb\"", buildings);
		}

		[Test]
		public void RemembranceProjectionIsReceiptFirstExactAndForeignSafe()
		{
			string commands = Read("Experience/KingdomRemembranceRuntime.Commands.cs");
			string projection = Read("Experience/KingdomRemembranceRuntime.Projection.cs");
			string reconcile = Read("Experience/KingdomRemembranceRuntime.Reconcile.cs");
			Assert.Less(commands.IndexOf("TryPrepareRemembranceProjection", StringComparison.Ordinal),
				commands.IndexOf("EnsureProjection(System", StringComparison.Ordinal));
			StringAssert.Contains("KnownProjectionState", projection);
			StringAssert.Contains("PriorState(Carrier, Description, Marker)", projection);
			StringAssert.Contains("ProjectedState(Carrier, Description, Marker)", projection);
			StringAssert.Contains("MarkerMatchesCarrier", projection);
			StringAssert.Contains("PriorDisplayName", projection);
			StringAssert.Contains("PriorDescription", projection);
			StringAssert.Contains("PriorMemorialFor", projection);
			StringAssert.Contains("TryRestoreProjection", projection);
			StringAssert.Contains("TryMarkRemembranceLost", reconcile);
			StringAssert.Contains("More than one object claims", reconcile);
			StringAssert.Contains("foreign-realm remembrance marker", reconcile);
			StringAssert.Contains("exact-carrier remembrance marker diverged", reconcile);
			string combined = Logical("KingdomRemembranceRuntime");
			StringAssert.DoesNotContain("GetZone", combined);
			StringAssert.DoesNotContain("ZoneManager", combined);
			StringAssert.DoesNotContain("JournalAPI", combined);
			StringAssert.DoesNotContain("AddXP", combined);
			StringAssert.DoesNotContain("Reputation", combined);
		}

		[Test]
		public void RealmRemovalHasReadOnlyPreflightAndExactOwnedRestoration()
		{
			string office = Read("Experience/KingdomOfficeRuntime.Removal.cs");
			StringAssert.Contains("CanRemoveForRealmRemoval", office);
			StringAssert.Contains("GetPart<r_KingdomRemembranceProjection>() != null", office);
			StringAssert.Contains("Marker.BodyObjectId != Body.IDIfAssigned", office);
			StringAssert.Contains("Marker.OwnsRole && HasRole", office);
			StringAssert.Contains("roles.RemoveRole(Marker.RoleText)", office);
			string remembrance = Read("Experience/KingdomRemembranceRuntime.Removal.cs");
			StringAssert.Contains("CanRemoveForRealmRemoval", remembrance);
			StringAssert.Contains("GetPart<r_KingdomOfficeProjection>() != null", remembrance);
			StringAssert.Contains("KnownProjectionState", remembrance);
			StringAssert.Contains("TryRestoreProjection", remembrance);
			string succession = Read("Experience/KingdomSuccession.RemovalAuthority.cs");
			StringAssert.Contains("TryDescribeRealmRemovalBlocker", succession);
			StringAssert.Contains("PendingAccessionRepairResidentId != 0", succession);
			StringAssert.Contains("PendingSealAccessionToken", succession);
			StringAssert.Contains("PendingSealRiteChronicle", succession);
			StringAssert.Contains("PendingSealAccessionReady", succession);
			StringAssert.Contains("PendingDeathToken", succession);
			StringAssert.Contains("PendingRiteStage", succession);
			StringAssert.Contains("PendingPhase != InterregnumPhase.None", succession);
			StringAssert.Contains("PendingAccessionRepairSettlementId", succession);
			StringAssert.DoesNotContain("ClearPending", succession);
		}

		[Test]
		public void CharterExposesBothExplicitCivicVerbs()
		{
			string menu = Read("Core/KingdomCharterMenuRules.cs");
			string charter = Read("Core/KingdomCharterPart.cs");
			StringAssert.Contains("ManageCivicOffice", menu);
			StringAssert.Contains("DedicateRemembrance", menu);
			StringAssert.Contains("KingdomOfficeRuntime.Open(System, ParentObject)", charter);
			StringAssert.Contains("KingdomRemembranceRuntime.Open(System, ParentObject)", charter);
		}

		[Test]
		public void EveryNewCivicProductionShardStaysBelowThreeHundredLines()
		{
			string[] prefixes = { "KingdomOfficeRuntime", "KingdomRemembranceRuntime" };
			for (int p = 0; p < prefixes.Length; p++)
			{
				string root = Path.Combine(TestMain.RepositoryRoot, "Experience");
				foreach (string file in Directory.GetFiles(root, prefixes[p] + "*.cs"))
					Assert.Less(File.ReadAllLines(file).Length, 300, Path.GetFileName(file));
			}
		}
	}
}
#endif
