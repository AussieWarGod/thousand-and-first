#if TAF_TESTS
using System;
using System.IO;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomResidentTransitionAuthoritySourceTests
	{
		private static string Read(string path)
		{
			return TestMain.ReadRepositoryText(path);
		}

		[Test]
		public void SharedAuthorityJoinsMarkersAndDurableSourceReceipts()
		{
			string body = Read("Simulation/City/KingdomResidentTransitionAuthority.cs");
			string durable = Read(
				"Simulation/City/KingdomResidentTransitionAuthority.Durable.cs");
			string lifecycle = Read(
				"Simulation/City/KingdomResidentTransitionAuthority.Lifecycle.cs");
			string jobs = Read(
				"Simulation/City/KingdomJobRegistry.z16.ResidentTransitionProjection.cs");
			string loaded = Read(
				"Simulation/City/KingdomResidentTransitionAuthority.Loaded.cs");
			string office = Read(
				"Simulation/City/KingdomResidentTransitionAuthority.Office.cs");
			string source = body + durable + lifecycle + loaded + office;

			StringAssert.Contains("GetPart<r_KingdomNamedCook>()", body);
			StringAssert.Contains("GetPart<r_KingdomResidentDeparture>()", body);
			StringAssert.Contains("Book.NamedCook", durable);
			StringAssert.Contains("GetPart<r_KingdomAssentingMootMember>()", body);
			StringAssert.Contains("Book.AssentingMoot", durable);
			StringAssert.Contains("KingdomPhysicalHappenings.IsStaged(Body)", body);
			StringAssert.Contains("happening.Active", durable);
			StringAssert.Contains("active.Participants", durable);
			StringAssert.Contains("LodgeReceiptProperty", body);
			StringAssert.Contains("Book.NotableGuest", lifecycle);
			StringAssert.Contains("receipt.ResidentId == ResidentId", lifecycle);
			StringAssert.Contains("KingdomExpeditions.ResidentJobProperty", body);
			StringAssert.Contains("TryProjectResidentTransition", lifecycle);
			StringAssert.Contains("KingdomJobKind.Expedition", jobs);
			StringAssert.Contains("JobColumnsSquare", jobs);
			StringAssert.Contains("notice.WorkerResidentId != ResidentId", loaded);
			StringAssert.Contains("GetIntProperty(\"KingdomKeeper\")", body);
			StringAssert.Contains("ambient.KeeperResidentId == ResidentId", loaded);
			StringAssert.Contains("GetPart<r_KingdomStasisCustody>()", body);
			StringAssert.Contains("vault.Slots", loaded);
			StringAssert.Contains("System.Experience.Offices", office);
			StringAssert.Contains("KingdomMarketHandoffGlobalIndex.TryLoaded", loaded);
			StringAssert.Contains("r_KingdomMarketHandoffSourceProjection", source);
		}

		[Test]
		public void TerminalAndUnrelatedReceiptsDoNotBecomeOpenClaims()
		{
			string durable = Read(
				"Simulation/City/KingdomResidentTransitionAuthority.Durable.cs");
			string lifecycle = Read(
				"Simulation/City/KingdomResidentTransitionAuthority.Lifecycle.cs");
			string loaded = Read(
				"Simulation/City/KingdomResidentTransitionAuthority.Loaded.cs");

			StringAssert.Contains("!KingdomNamedCookRules.IsVacant(cook.Phase)", durable);
			StringAssert.Contains("participant.ResidentId == ResidentId", durable);
			StringAssert.Contains("participant.ObjectId, objectId", durable);
			StringAssert.Contains("operation.Phase != KingdomLifecyclePhase.Terminal", lifecycle);
			StringAssert.Contains("receipt?.MarketSourcePrepared", lifecycle);
			StringAssert.Contains("MarketNone", lifecycle);
			StringAssert.Contains("notice.Done", loaded);
			StringAssert.Contains("receipt.Phase != KingdomStasisCustodyPhase.Released", loaded);
			StringAssert.Contains("KingdomStasisVaultRules.Validate", loaded);
		}

		[Test]
		public void EveryAccessionBoundaryUsesTheSharedExactGate()
		{
			string heirs = Read("Experience/KingdomSuccession.HeirsAndNews.cs");
			string death = Read("Experience/KingdomSuccession.DeathExecution.cs");
			string recovery = Read("Experience/KingdomSuccession.RiteRecovery.cs");
			string accede = Read(
				"Simulation/City/KingdomResidents.04.ResidentTransitionsAndAccession.cs");
			string repair = Read("Simulation/City/KingdomResidents.05.AccessionRepair.cs");

			StringAssert.Contains("KingdomResidentTransitionAuthority.CanAccede(System, body",
				heirs);
			Assert.GreaterOrEqual(Occurrences(death,
				"KingdomResidentTransitionAuthority.CanAccede"), 2);
			Assert.GreaterOrEqual(Occurrences(recovery,
				"KingdomResidentTransitionAuthority.CanAccede"), 2);
			StringAssert.Contains("KingdomResidentTransitionAuthority.CanAccede(System, Body, residentId)",
				accede);
			StringAssert.Contains("KingdomResidentTransitionAuthority.CanAccede(System, Body, ResidentId)",
				repair);
			AssertGateImmediatelyBeforeTransfer(death, "chosen.Rule.ResidentId",
				"SetPlayerBodyAndRebindAll(game, founder");
			AssertGateImmediatelyBeforeTransfer(recovery, "PendingHeirResidentId",
				"SetPlayerBodyAndRebindAll(game, founder");
		}

		[Test]
		public void EmigrationReprovesSelectionPreviewCommitAndInnerMutation()
		{
			string growth = Read("Growth/KingdomGrowth.z16.Emigration.cs");
			string begin = Read("Growth/KingdomResidentDepartureRuntime.Begin.cs");
			string recovery = Read("Growth/KingdomResidentDepartureRuntime.Recovery.cs");
			string residents = Read(
				"Simulation/City/KingdomResidents.04.ResidentTransitionsAndAccession.cs");
			Assert.GreaterOrEqual(Occurrences(growth, "CanPrepareGenericEmigrate(System,"), 2,
				"named and roster selection require read-only closable-role preflight");
			int chosen = growth.IndexOf("if (leaver == null)", StringComparison.Ordinal);
			int owner = growth.IndexOf("KingdomResidentDepartureRuntime.TryBegin", chosen,
				StringComparison.Ordinal);
			Assert.Greater(owner, chosen);
			int capture = begin.IndexOf("System.ResidentDeparture = operation",
				StringComparison.Ordinal);
			int marker = begin.IndexOf("Body.AddPart(marker)", capture,
				StringComparison.Ordinal);
			int rolePrepare = begin.IndexOf("KingdomResidentDeparturePreparation.TryPrepare",
				marker, StringComparison.Ordinal);
			int preview = begin.IndexOf("KingdomCitizenship.CanRemove", rolePrepare,
				StringComparison.Ordinal);
			int finalGate = begin.IndexOf("CanPrepareJournaledRoles", preview,
				StringComparison.Ordinal);
			int commit = begin.IndexOf("KingdomCitizenship.TryRemove", finalGate,
				StringComparison.Ordinal);
			Assert.Greater(capture, 0); Assert.Greater(marker, capture);
			Assert.Greater(rolePrepare, marker); Assert.Greater(preview, rolePrepare);
			Assert.Greater(finalGate, preview); Assert.Greater(commit, finalGate);
			StringAssert.Contains("CanPrepareJournaledRoles(",
				begin);
			int innerGate = residents.IndexOf(
				"KingdomResidentTransitionAuthority.CanContinueJournaledCarrierRemoval",
				StringComparison.Ordinal);
			int rowMutation = residents.IndexOf("KingdomResidentRules.TryRemove", innerGate,
				StringComparison.Ordinal);
			Assert.Greater(innerGate, 0); Assert.Greater(rowMutation, innerGate);
			StringAssert.Contains("CanCompleteJournaledBodyDestruction", recovery);
			StringAssert.Contains("return TryDestroyBody", recovery);
		}

		[Test]
		public void ExactIdentityRejectsDuplicateMissingOrDivergentCarriersBeforeMutation()
		{
			string authority = Read(
				"Simulation/City/KingdomResidentTransitionAuthority.Identity.cs");
			string rows = Read(
				"Simulation/City/KingdomResidents.06.ResidentTransitionProof.cs");
			string carriers = Read(
				"Simulation/City/KingdomResidents.07.DepartureRecovery.cs");
			StringAssert.Contains("HashSet<KingdomCityBook>", rows);
			StringAssert.Contains("HashSet<string> settlements", rows);
			StringAssert.Contains("if (book.ResidentIds[i] != ResidentId) continue", rows);
			StringAssert.Contains("ResidentColumnsSquareForTransition", rows);
			StringAssert.Contains("Registry.ObjectIds[i], ObjectId", authority);
			StringAssert.Contains("Registry.ZoneIds[i], ZoneId", authority);
			StringAssert.Contains("HashSet<long> seen", authority);
			StringAssert.Contains("ExactCarrierMultiplicity(rows, bindings", authority);
			StringAssert.Contains("KingdomCitizenship.BelongsTo(System, Body)", authority);
			StringAssert.Contains("KingdomCitizenshipRemovalReason.Accession", authority);
			StringAssert.Contains("KingdomCitizenshipRemovalReason.Emigration", authority);
			StringAssert.Contains("rowMatches > 1", carriers);
			StringAssert.Contains("held.ObjectId != Operation.BodyObjectId", carriers);
			StringAssert.Contains("held.ZoneId != Operation.ZoneId", carriers);
			int proof = carriers.IndexOf("CanContinueJournaledCarrierRemoval",
				StringComparison.Ordinal);
			int row = carriers.IndexOf("TryDepart(System, Body", proof,
				StringComparison.Ordinal);
			Assert.Greater(proof, 0); Assert.Greater(row, proof);
		}

		[Test]
		public void CookOfficeAndDeedRolesUseExactReversibleDepartureClosure()
		{
			string preparation = Read(
				"Simulation/City/KingdomResidentDeparturePreparation.cs");
			string cook = Read("Experience/KingdomNamedCook.Lifecycle.cs");
			string office = Read("Experience/KingdomOfficeRuntime.Departure.cs");
			string officeRules = Read("Experience/KingdomExperienceRules.OfficeDeparture.cs");
			string polity = Read("Polity/KingdomPolityRules.ResidentTransition.cs");
			StringAssert.Contains("CanPrepareJournaledRoles", preparation);
			StringAssert.Contains("PrepareCookLoss", preparation);
			StringAssert.Contains("TryPrepareHolderDeparture", preparation);
			StringAssert.Contains("KingdomPolityResidentTransitionCause.Departure", preparation);
			Assert.GreaterOrEqual(Occurrences(preparation,
				"CanPrepareJournaledRoles"), 2);
			StringAssert.Contains("TryRollback", preparation);
			StringAssert.Contains("DepartureVacancyPrepared", cook);
			StringAssert.Contains("TryCancelOfficeDeparture", office);
			StringAssert.Contains("prepared office departure lost its rollback CAS", officeRules);
			StringAssert.Contains("TryConcludeDeedResident", polity);
			StringAssert.Contains("TryRollbackDeedResident", polity);
		}

		[Test]
		public void CitizenHostAndPolityCloseOnAccessionAndDeathRecoveryPaths()
		{
			string authority = Read("Simulation/City/KingdomResidentTransitionAuthority.cs");
			string accede = Read(
				"Simulation/City/KingdomResidents.04.ResidentTransitionsAndAccession.cs");
			string repair = Read("Simulation/City/KingdomResidents.05.AccessionRepair.cs");
			string succession = Read("Experience/KingdomSuccession.Accession.cs");
			string death = Read("Experience/KingdomOffices.cs");
			StringAssert.Contains("CanRetireAccedingHost", authority);
			StringAssert.Contains("TryRetireAccedingHost", accede);
			StringAssert.Contains("KingdomPolityResidentTransitionCause.Accession", accede);
			StringAssert.Contains("TryRetireAccedingHost", repair);
			StringAssert.Contains("KingdomPolityResidentTransitionCause.Accession", repair);
			StringAssert.Contains("TryRetireAccedingHost", succession);
			StringAssert.Contains("KingdomPolityResidentTransitionCause.Death", death);
		}

		[Test]
		public void SuccessionProjectsEveryResidentScopedPendingIdentity()
		{
			string source = Read(
				"Experience/KingdomSuccession.ResidentTransitionAuthority.cs");
			StringAssert.Contains("PendingAccessionRepairResidentId", source);
			StringAssert.Contains("PendingAccessionRepairSettlementId", source);
			StringAssert.Contains("PendingHeirResidentId", source);
			StringAssert.Contains("PendingHeirObjectId", source);
			StringAssert.Contains("PendingHeirZoneId", source);
			StringAssert.Contains("KingdomSuccessionSelectionReceipt.TryDecode", source);
			StringAssert.Contains("Selection.LawHeirResidentId", source);
			StringAssert.Contains("ActiveSeatKeeperResidentId", source);
			StringAssert.Contains("BodyMatches(player", source);
			StringAssert.Contains("Protected = true", source);
		}

		[Test]
		public void LabDepartureRequiresItsExactTypedOwnerAtEveryBoundary()
		{
			string lab = Read("Growth/KingdomLab.CivicDepartureAuthority.cs");
			string loaded = Read(
				"Simulation/City/KingdomResidentTransitionAuthority.Loaded.cs");
			string growth = Read("Growth/KingdomGrowth.z16.Emigration.cs");
			string lodging = Read("Growth/KingdomLodging.BrinkAndObservation.cs");
			StringAssert.Contains("TryAuthorizeDeparture", lodging);
			StringAssert.Contains("EmigrateAuthorized", lodging);
			StringAssert.Contains("KingdomResidentDestructionAuthorization", lab);
			StringAssert.Contains("TryReadOwners", lab);
			StringAssert.Contains("ReadOnlyDepartureAuthorizationMatches", loaded);
			StringAssert.Contains("receipt.Phase == KingdomLabCivicPhase.Closed", loaded);
			StringAssert.Contains("LabRefusalDeparture", loaded);
			StringAssert.Contains("Authorization", growth);
			StringAssert.Contains("TryCompleteAuthorizedDeparture", lab);
			StringAssert.Contains("KingdomResidentDepartureRuntime.TryBegin", growth);
		}

		[Test]
		public void DepartureJournalIsExactResumableAndRunsBeforeAnySemanticMutation()
		{
			string operation = Read("Growth/KingdomResidentDepartureOperation.cs");
			string rules = Read("Growth/KingdomResidentDepartureRules.cs");
			string begin = Read("Growth/KingdomResidentDepartureRuntime.Begin.cs");
			string recovery = Read("Growth/KingdomResidentDepartureRuntime.Recovery.cs");
			string carriers = Read(
				"Simulation/City/KingdomResidents.07.DepartureRecovery.cs");
			string effects = Read("Growth/KingdomResidentDepartureRuntime.Effects.cs");
			string marker = Read("Growth/r_KingdomResidentDeparture.cs");
			string semantic = Read("Core/KingdomSystem.z21.SemanticPass.cs");
			string normalize = Read("Core/KingdomSystem.z23.Normalization.cs");

			StringAssert.Contains("RealmId", operation);
			StringAssert.Contains("SettlementId", operation);
			StringAssert.Contains("BodyObjectId", operation);
			StringAssert.Contains("DeparturesBefore", operation);
			StringAssert.Contains("AuthorizationCauseDigest", operation);
			StringAssert.Contains("Expected + 1", rules);
			StringAssert.Contains("NormalizeOldDefault", normalize);
			StringAssert.Contains("WriteNamedFields", marker);
			StringAssert.Contains("ReadNamedFields", marker);
			StringAssert.Contains("FinalizeCopy", marker);
			StringAssert.Contains("mixed or foreign journal marker", recovery);
			StringAssert.Contains("CanPrepareJournaledRoles", Read(
				"Simulation/City/KingdomResidentTransitionAuthority.Journal.cs"));
			StringAssert.Contains("bodies != 1", recovery);
			StringAssert.Contains("rowMatches > 1", carriers);
			StringAssert.Contains("DepartureCarriersAbsent", carriers);
			StringAssert.Contains("System.Ledger.Departures == Operation.DeparturesBefore",
				effects);
			StringAssert.Contains("KingdomChronicle.RecordOnce", effects);
			Assert.Greater(begin.IndexOf("System.ResidentDeparture = operation",
				StringComparison.Ordinal), 0);
			int recover = semantic.IndexOf("TryRecoverPending(this, Z",
				StringComparison.Ordinal);
			int checkIn = semantic.IndexOf("KingdomCity.CheckIn", StringComparison.Ordinal);
			Assert.Greater(recover, 0); Assert.Greater(checkIn, recover);
		}

		[Test]
		public void DeedTransitionCopiesTheV8SemanticSummary()
		{
			string polity = Read("Polity/KingdomPolityRules.ResidentTransition.cs");
			StringAssert.Contains("DeedSummary = Row.DeedSummary", polity);
		}

		[Test]
		public void DestructiveGraphPreservesNestedEquippedAndForeignCustody()
		{
			string graph = Read(
				"Simulation/City/KingdomResidentTransitionAuthority.ObjectGraph.cs");
			Assert.GreaterOrEqual(Occurrences(graph,
				"GetInventoryDirectAndEquipment()"), 2);
			StringAssert.Contains("HashSet<GameObject>", graph);
			StringAssert.Contains("!seen.Add(item)", graph);
			StringAssert.Contains("MaximumDestructiveObjectGraph", graph);
			StringAssert.Contains("catch { return false; }", graph);
			StringAssert.Contains("KingdomMarketStockProtection.HasProjection(item)", graph);
			StringAssert.Contains("MarketTransferTargetProperty", graph);
			StringAssert.Contains("StockTransferTargetProperty", graph);
			StringAssert.Contains("merchant && item.GetIntProperty(\"_stock\") == 1", graph);
			StringAssert.Contains("if (children == null) continue", graph);
		}

		[Test]
		public void ResidentTransitionProductionShardsStayBounded()
		{
			string[] files =
			{
				"Simulation/City/KingdomResidentTransitionRules.cs",
				"Simulation/City/KingdomResidentTransitionAuthority.cs",
				"Simulation/City/KingdomResidentTransitionAuthority.Identity.cs",
				"Simulation/City/KingdomResidentTransitionAuthority.Durable.cs",
				"Simulation/City/KingdomResidentTransitionAuthority.Office.cs",
				"Simulation/City/KingdomResidentTransitionAuthority.Lifecycle.cs",
				"Simulation/City/KingdomResidentTransitionAuthority.Loaded.cs",
				"Simulation/City/KingdomResidentTransitionAuthority.ObjectGraph.cs",
				"Simulation/City/KingdomResidentTransitionAuthority.Journal.cs",
				"Simulation/City/KingdomResidentDeparturePreparation.cs",
				"Simulation/City/KingdomResidents.06.ResidentTransitionProof.cs",
				"Simulation/City/KingdomResidents.07.DepartureRecovery.cs",
				"Simulation/City/KingdomJobRegistry.z16.ResidentTransitionProjection.cs",
				"Experience/KingdomSuccession.ResidentTransitionAuthority.cs",
				"Experience/KingdomOfficeRuntime.Departure.cs",
				"Experience/KingdomExperienceRules.OfficeDeparture.cs",
				"Experience/KingdomCitizenRite.Projection.cs",
				"Experience/KingdomCitizenRiteProjectionRules.cs",
				"Experience/r_KingdomCitizenRiteProjection.cs",
				"Polity/KingdomPolityRules.ResidentTransition.cs",
				"Polity/KingdomPolityResidentTransition.cs",
				"Growth/KingdomLab.CivicDepartureAuthority.cs",
				"Growth/KingdomResidentDepartureOperation.cs",
				"Growth/KingdomResidentDepartureRules.cs",
				"Growth/KingdomResidentDepartureRuntime.Authority.cs",
				"Growth/KingdomResidentDepartureRuntime.Begin.cs",
				"Growth/KingdomResidentDepartureRuntime.Effects.cs",
				"Growth/KingdomResidentDepartureRuntime.Recovery.cs",
				"Growth/KingdomResidentDepartureRuntime.Rollback.cs",
				"Growth/r_KingdomResidentDeparture.cs"
			};
			for (int i = 0; i < files.Length; i++)
				Assert.Less(File.ReadAllLines(Path.Combine(TestMain.RepositoryRoot,
					files[i])).Length, 300, files[i]);
		}

		private static void AssertGateImmediatelyBeforeTransfer(string source,
			string residentToken, string transferToken)
		{
			int transfer = source.IndexOf(transferToken, StringComparison.Ordinal);
			int gate = source.LastIndexOf("KingdomResidentTransitionAuthority.CanAccede",
				transfer, StringComparison.Ordinal);
			Assert.Greater(transfer, 0); Assert.Greater(gate, 0);
			StringAssert.Contains(residentToken, source.Substring(gate, transfer - gate));
		}

		private static int Occurrences(string source, string token)
		{
			int count = 0;
			for (int at = 0; (at = source.IndexOf(token, at,
				StringComparison.Ordinal)) >= 0; at += token.Length) count++;
			return count;
		}
	}
}
#endif
