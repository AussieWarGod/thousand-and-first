#if TAF_TESTS
using System;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomPolityConsignmentSourceTests
	{
		[Test]
		public void PolityFreezesNeedAndConsumesOnlyTerminalTradeProof()
		{
			string transaction = Read("Polity/KingdomPolityCorrespondenceRules.Consignment.cs");
			string validation = Read("Polity/KingdomPolityCorrespondenceRules.ConsignmentValidation.cs");
			string relationship = Read(
				"Polity/KingdomPolityCorrespondenceRules.ConsignmentRelationship.cs");
			string request = Read(
				"Polity/KingdomPolityCorrespondenceRules.ConsignmentRequest.cs");
			string recovery = Read("Polity/KingdomPolityCorrespondenceRuntime.cs");
			string acknowledgement = Read(
				"Trade/KingdomTradeRules.PolityConsignmentAcknowledgement.cs");
			string visit = Read("Polity/KingdomPolityVisitRuntime.cs");
			string active = Read("Polity/KingdomPolityActiveRuntime.cs");
			StringAssert.Contains("FirstContactWaterDrams = 8", transaction);
			StringAssert.Contains("ConsignmentStandingPerDram = 1", transaction);
			StringAssert.Contains("TryPlanConsignment", transaction);
			StringAssert.Contains("TryConsumeTradeReceipt", transaction);
			StringAssert.Contains("KingdomPolitySystemicDeltaKind.Standing", relationship +
				Read("Polity/KingdomPolityCorrespondenceRules.ConsignmentHelpers.cs"));
			StringAssert.Contains("TryBuildNewRequest", request);
			StringAssert.Contains("TryBuildFrozenRequest", request);
			AssertBefore(visit, "TryEnsureFirstContact", "TryRecoverTradeReceipts");
			AssertBefore(visit, "ReconcileEnvoy(System", "TryEnsureFirstContact");
			StringAssert.Contains("TryInspectPolityConsignmentReceipt", recovery);
			StringAssert.Contains("TryConsumeTradeReceipt", recovery);
			AssertBefore(recovery, "TryConsumeTradeReceipt(",
				"TryAcknowledgePolityConsignment");
			StringAssert.Contains("TryValidateConsumedTradeReceipt", acknowledgement);
			StringAssert.Contains("TryCompactProofRows", acknowledgement);
			StringAssert.Contains("KingdomTradePolityConsignmentReceiptKind.Invalid", recovery);
			StringAssert.Contains("TryReconcileCommittedCapacity", active);
			StringAssert.Contains("KingdomPolityCorrespondenceRuntime.TryRecoverTradeReceipts",
				active);
			foreach (string forbidden in new[] { "GetZone", "ZoneManager", "GameObject",
				"Reward", "Experience", "Loot", "TryLoadManifest" })
				StringAssert.DoesNotContain(forbidden, transaction + validation + relationship +
					request + recovery);
		}

		[Test]
		public void TradeAloneOwnsLoadedDebitRetentionAndTerminalReceipt()
		{
			string seam = Read("Trade/KingdomTrade.PolityConsignment.cs");
			string water = Read("Trade/KingdomTrade.12.WaterMutation.cs");
			string proof = Read("Trade/KingdomTradeRules.PolityConsignment.cs");
			string receipt = Read("Trade/KingdomTradeRules.PolityConsignmentReceipt.cs");
			string lifecycle = Read("Trade/KingdomTradeRules.Lifecycle.cs");
			string retention = Read("Trade/KingdomTrade.PolityConsignmentRetention.cs");
			string quarantine = Read("Trade/KingdomTrade.21.Quarantine.cs");
			string acknowledgement = Read(
				"Trade/KingdomTradeRules.PolityConsignmentAcknowledgement.cs");
			string compaction = Read("Trade/KingdomTradeRules.ProofCompaction.cs");
			StringAssert.Contains("KingdomTradeOperationKind.PolityConsignmentDelivery", seam);
			AssertBefore(seam, "TryCaptureConsignmentRecipientWitness", "NewOperation(Book");
			AssertBefore(seam, "TryValidatePolityConsignmentPreparation", "NewOperation(Book");
			AssertBefore(seam, "TryPreflightPolityConsignmentGround", "ApplyOption(book");
			AssertBefore(seam, "PreparePolityConsignment(System", "ApplyOption(book");
			AssertBefore(seam, "PreparePolityConsignment(System", "ContinueOperation(System");
			StringAssert.Contains("NewOperation(Book", seam);
			StringAssert.Contains("KingdomLiquids.Drain", water);
			StringAssert.Contains("TryInspectPolityConsignmentReceipt", proof);
			StringAssert.Contains("KingdomTradePolityConsignmentReceiptKind.Landed", receipt);
			StringAssert.Contains("KingdomTradePolityConsignmentReceiptKind.TerminalFailed", receipt);
			StringAssert.Contains("Proof.ProvedWater >= 1", receipt);
			StringAssert.Contains("PolityConsignmentDelivery", lifecycle);
			StringAssert.Contains("TryAcknowledgePolityConsignment", acknowledgement);
			StringAssert.Contains("TryCompactProofRows", compaction);
			StringAssert.DoesNotContain("future explicit acknowledgement", lifecycle);
			StringAssert.Contains("SettleRetainedAccounting", retention);
			StringAssert.Contains("SettlePolityConsignmentRetention", quarantine);
			StringAssert.DoesNotContain("GetZone", seam + retention);
			StringAssert.DoesNotContain("ZoneManager", seam + retention);
			foreach (string forbidden in new[] { "AddStanding", "SetStanding", "GiveXP",
				"Award", "Reward", "Deed =" })
				StringAssert.DoesNotContain(forbidden, seam + retention + proof + receipt);
		}

		[Test]
		public void EveryResumeBoundaryRebindsExactBodyCohortAndProjection()
		{
			string seam = Read("Trade/KingdomTrade.PolityConsignment.cs");
			string bind = Read("Trade/KingdomTrade.PolityRecipientRuntime.cs");
			string continuation = Read("Trade/KingdomTrade.11.OperationAndResources.cs");
			string water = Read("Trade/KingdomTrade.12.WaterMutation.cs");
			string resumed = Read("Trade/KingdomTrade.13.WaterRecovery.cs");
			string failure = Read("Trade/KingdomTrade.13b.PhysicalFailure.cs");
			string domain = Read("Trade/KingdomTrade.18.DomainAccounting.cs");
			string quarantine = Read("Trade/KingdomTrade.21.Quarantine.cs");
			StringAssert.Contains("operation.PolityRecipient =", seam);
			StringAssert.Contains("ResolveLoadedObject(expected.BodyId", bind);
			StringAssert.Contains("LoadedObjectResolution.ExactUnique", bind);
			StringAssert.Contains("TryCaptureConsignmentRecipientWitness", bind);
			StringAssert.Contains("TryValidatePolityConsignmentCheckpoint", bind);
			AssertBefore(bind, "SealUnstartedPolityConsignmentLegs(Operation)",
				"Quarantine(Operation");
			AssertBefore(continuation, "\"physical debit\"", "SettleResources(operation");
			AssertBefore(continuation, "\"domain settlement\"", "SettleDomain(System");
			AssertBefore(continuation, "\"success outbox\"", "BuildOutbox(System");
			AssertBefore(continuation, "\"terminal publication\"", "SettleSchedule(Book");
			AssertBefore(continuation, "\"terminal receipt\"", "KingdomTradeRules.Retire");
			AssertBefore(water, "\"water debit leg\"", "KingdomLiquids.Drain");
			AssertBefore(water, "leg.State = KingdomTradePhysicalState.Proved",
				"\"post-debit landing\"");
			AssertBefore(resumed, "\"resumed water debit leg\"", "KingdomLiquids.Drain");
			AssertBefore(resumed, "leg.State = KingdomTradePhysicalState.Proved",
				"\"resumed post-debit landing\"");
			AssertBefore(domain, "\"DomainSettled publication\"",
				"Operation.Phase = KingdomTradePhase.DomainSettled");
			StringAssert.Contains("SettlePolityConsignmentRetention", quarantine);
			AssertBefore(continuation, "HasPolityWaterIntent(operation)",
				"ContinueOrQuarantinePolityRecipient");
			StringAssert.Contains("ClassifyPolityWaterIntent", resumed);
			StringAssert.Contains("KingdomTradeWaterIntentResolution.Before", resumed);
			StringAssert.Contains("KingdomTradeWaterIntentResolution.After", resumed);
			AssertBefore(resumed, "leg?.State == KingdomTradePhysicalState.Skipped",
				"ResolveLoadedObject(leg.OwnerId");
			StringAssert.Contains("ValidSkippedPolityWaterLeg", resumed);
			StringAssert.Contains("ExactWaterWitness(witness, Z, false)", failure);
			StringAssert.Contains("SealUnstartedPolityConsignmentLegs(Operation)", quarantine);
			foreach (string forbidden in new[] { "GetZone", "SetActiveZone", "LoadZone" })
				StringAssert.DoesNotContain(forbidden, bind + continuation);
		}

		[Test]
		public void WitnessIsCurrentWireEvidenceAndOldWireFailsClosed()
		{
			string declarations = Read(
				"Trade/KingdomTradeState.01.OperationAndAuthorityDeclarations.cs");
			string envelope = Read("Trade/KingdomTradeState.02.CodecEnvelopeAndPayload.cs");
			string rows = Read("Trade/KingdomTradeState.05.CodecOperationProofArchiveRows.cs");
			string oldRows = Read("Trade/KingdomTradeState.05b.CodecWireV4Rows.cs");
			string migration = Read("Trade/KingdomTradeRules.WireV4Migration.cs");
			string normalize = Read("Trade/KingdomTradeRules.NormalizeOperation.cs") +
				Read("Trade/KingdomTradeRules.NormalizeEvidence.cs");
			StringAssert.Contains("KingdomTradePolityRecipientWitness PolityRecipient", declarations);
			StringAssert.Contains("CurrentWireVersion = 5", envelope);
			StringAssert.Contains("ImmediatePriorWireVersion = 4", envelope);
			StringAssert.Contains("WritePolityRecipient", rows);
			StringAssert.Contains("ReadPolityRecipient", rows);
			StringAssert.DoesNotContain("WritePolityRecipient", oldRows);
			StringAssert.Contains("QuarantineLegacyConsignmentWithoutWitness", migration);
			StringAssert.Contains("ValidPolityConsignmentOperation", normalize);
			StringAssert.Contains("ValidPolityRecipientProof", normalize);
			StringAssert.Contains("SealUnstartedPolityConsignmentLegs(operation)", migration);
		}

		[Test]
		public void PlayerReplyRequiresExactLoadedEnvoyAndHasNoFarmableBenefit()
		{
			string ui = Read("Polity/KingdomPolityVisitInteraction.Consignment.cs");
			string authority = Read("Polity/KingdomPolityVisitInteraction.ConsignmentAuthority.cs");
			StringAssert.Contains("TryValidateConsignmentRecipient", authority);
			StringAssert.Contains("cohort.Phase != KingdomPolityCohortPhase.Materialized", authority);
			StringAssert.Contains("MemberOrdinalProperty", authority);
			AssertBefore(ui, "KingdomTrade.TryDeliverPolityConsignment",
				"TryConsumeTradeReceipt");
			AssertBefore(ui, "TryConsumeTradeReceipt",
				"TryAcknowledgePolityConsignment");
			StringAssert.Contains("Decline the consignment", ui);
			StringAssert.Contains("Answer later", ui);
			StringAssert.Contains("one relationship-standing point per dram",
				ui.ToLowerInvariant());
			StringAssert.Contains("no experience or loot", ui.ToLowerInvariant());
			StringAssert.DoesNotContain("GetZone", ui + authority);
			StringAssert.DoesNotContain("ZoneManager", ui + authority);
		}

		private static string Read(string relative)
		{
			return TestMain.ReadRepositoryText(relative);
		}

		private static void AssertBefore(string source, string first, string second)
		{
			int a = source.IndexOf(first, StringComparison.Ordinal);
			int b = source.IndexOf(second, a < 0 ? 0 : a, StringComparison.Ordinal);
			Assert.GreaterOrEqual(a, 0, first); Assert.Greater(b, a, second);
		}
	}
}
#endif
