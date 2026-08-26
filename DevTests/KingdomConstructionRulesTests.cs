#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>Pure proof of durable construction routing, claims, phases and registry reloads.</summary>
	public class KingdomConstructionRulesTests
	{
		private static KingdomMaterialDebitCost MaterialCost(int Timber = 0, int Stone = 0,
			int Bit0 = 0)
		{
			KingdomMaterialTally materials = new KingdomMaterialTally();
			materials.Set(KingdomMaterial.Timber, Timber);
			materials.Set(KingdomMaterial.Stone, Stone);
			KingdomBitTally bits = new KingdomBitTally();
			bits.Set(0, Bit0);
			return new KingdomMaterialDebitCost(materials, bits, null);
		}

		private static KingdomConstructionJob Job(KingdomConstructionRoute Route,
			string Id = "00000000000000000000000000000001",
			KingdomConstructionPhase Phase = KingdomConstructionPhase.Published,
			int Water = 0, KingdomMaterialDebitCost Material = null)
		{
			return new KingdomConstructionJob
			{
				Id = Id,
				OwnerKey = KingdomConstructionRules.OwnerKey("realm", 7L, "settlement"),
				ZoneId = "JoppaWorld.11.22.1.1.10",
				Route = Route,
				Phase = Phase,
				Projection = KingdomConstructionRules.ProjectionFor(Route),
				X = 12,
				Y = 9,
				SubjectId = "subject-1",
				TargetKey = "target",
				Payload = "payload",
				CreatedTick = 10L,
				StartedTick = 10L,
				DueTick = 20L,
				UpdatedTick = 10L,
				Revision = 1,
				Claims = KingdomConstructionRules.NewClaims(Water,
					Material ?? new KingdomMaterialDebitCost())
			};
		}

		private static KingdomConstructionJob FullyFunded(KingdomConstructionRoute Route,
			KingdomConstructionPhase Phase)
		{
			KingdomConstructionJob job = Job(Route, Phase: Phase);
			return job;
		}

		private static KingdomConstructionOutbox SettledOutbox(string Id,
			string Suffix = "redressed")
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

		private static void AssertByteEnum(Type Type, string Expected)
		{
			Assert.AreEqual(typeof(byte), Enum.GetUnderlyingType(Type), Type.Name);
			Array values = Enum.GetValues(Type);
			List<string> actual = new List<string>();
			foreach (object value in values)
			{
				actual.Add(Convert.ToByte(value) + ":" + Enum.GetName(Type, value));
			}
			Assert.AreEqual(Expected, string.Join(",", actual.ToArray()), Type.Name);
		}

		private static List<KingdomConstructionJob> CanonicalRoundTrip(string Fixture)
		{
			List<KingdomConstructionJob> decoded;
			Assert.IsTrue(KingdomConstructionRules.TryDecode(Fixture, out decoded));
			string canonical;
			Assert.IsTrue(KingdomConstructionRules.TryEncode(decoded, out canonical));
			StringAssert.StartsWith(KingdomConstructionRules.FormatHeader + "\n", canonical);
			Assert.IsTrue(KingdomConstructionRules.TryDecode(canonical, out decoded));
			string repeated;
			Assert.IsTrue(KingdomConstructionRules.TryEncode(decoded, out repeated));
			Assert.AreEqual(canonical, repeated);
			return decoded;
		}

		[Test]
		public void ConstructionWireEnumsKeepExactByteValues()
		{
			AssertByteEnum(typeof(KingdomConstructionRoute),
				"0:None,1:CommissionScaffold,2:PlanScaffold,3:PlotCommission,4:PlotPlan,5:SocketBuild,6:SocketConvert,7:SocketRedress,8:Improvement,9:RoadPaving,10:WearRepair,11:Strike,12:PurposeConsignment");
			AssertByteEnum(typeof(KingdomConstructionProjection),
				"0:None,1:Scaffold,2:PlotWorks,3:StrikeOrder,4:Redress,5:Improvement,6:Paving,7:Repair,8:PurposeConsignment");
			AssertByteEnum(typeof(KingdomConstructionPhase),
				"0:Invalid,1:Published,2:WaterPending,3:WaterSettled,4:MaterialPending,5:Funded,6:ProjectionPending,7:Projected,8:Working,9:Outstanding,10:CompensationPending,11:Compensated,12:Complete,13:Cancelled,14:InspectionRequired");
			AssertByteEnum(typeof(KingdomConstructionResumeAction),
				"0:None,1:ResumeFunding,2:RetryProjection,3:AdvanceWork,4:Inspect");
			AssertByteEnum(typeof(KingdomConstructionStartResult),
				"0:Refused,1:Funded,2:Outstanding");
			AssertByteEnum(typeof(KingdomScaffoldContinuationAction),
				"0:None,1:AdvanceWork,2:CreateSuccessor,3:RemovePredecessor,4:CompleteReceipt,5:TellCompletion,6:Quarantine");
			AssertByteEnum(typeof(KingdomPhysicalPhase),
				"0:None,1:OutputIntent,2:StrikeOrdered,3:PlotPartRemovalPending,4:PredecessorRemovalPending,5:PredecessorRemoved,6:SalvageAddPending,7:SalvageSettled,8:SuccessorPending,9:SuccessorSettled,10:TellingsPending,11:Settled,12:Quarantined,13:StrikeStampPending,14:StrikeWorking,15:StrikeWorkComplete,16:StrikeCancellationPending,17:FinalOutputPending,18:FinalOutputSettled,19:FurnishingPending,20:FurnishingSettled,21:FinalRemovalPending,22:FinalRemoved,23:EffectsPending,24:EffectsSettled,25:RoadPlanFrozen,26:RoadOutputPending,27:RoadOutputSettled,28:RoadRemovalPending,29:RoadTallyPending,30:RoadTallySettled,31:CargoOutputPending,32:CargoOutputSettled,33:CargoTransferPending,34:CargoDelivered");
			AssertByteEnum(typeof(KingdomConstructionSinkDisposition),
				"0:None,1:Pending,2:Attempting,3:Delivered,4:Skipped,5:Lost");
			AssertByteEnum(typeof(KingdomExactRemovalAction),
				"1:InvokeOnce,2:ProvedAbsent,3:Quarantine");
			AssertByteEnum(typeof(KingdomConstructionCasAction),
				"1:Apply,2:Confirm,3:Quarantine");
			AssertByteEnum(typeof(KingdomPhysicalLookupState),
				"0:Absent,1:Exact,2:Ambiguous");
			AssertByteEnum(typeof(KingdomHandoverItemTopology),
				"0:Invalid,1:Source,2:Loose,3:EnteringCell,4:DestinationInventory,5:DestinationCell");
		}

		[Test]
		public void EveryRouteMapsToOneRequiredPhysicalProjection()
		{
			Dictionary<KingdomConstructionRoute, KingdomConstructionProjection> expected =
				new Dictionary<KingdomConstructionRoute, KingdomConstructionProjection>
				{
					{ KingdomConstructionRoute.CommissionScaffold, KingdomConstructionProjection.Scaffold },
					{ KingdomConstructionRoute.PlanScaffold, KingdomConstructionProjection.Scaffold },
					{ KingdomConstructionRoute.PlotCommission, KingdomConstructionProjection.PlotWorks },
					{ KingdomConstructionRoute.PlotPlan, KingdomConstructionProjection.PlotWorks },
					{ KingdomConstructionRoute.SocketBuild, KingdomConstructionProjection.PlotWorks },
					{ KingdomConstructionRoute.SocketConvert, KingdomConstructionProjection.StrikeOrder },
					{ KingdomConstructionRoute.SocketRedress, KingdomConstructionProjection.Redress },
					{ KingdomConstructionRoute.Improvement, KingdomConstructionProjection.Improvement },
					{ KingdomConstructionRoute.RoadPaving, KingdomConstructionProjection.Paving },
					{ KingdomConstructionRoute.WearRepair, KingdomConstructionProjection.Repair },
					{ KingdomConstructionRoute.Strike, KingdomConstructionProjection.StrikeOrder },
					{ KingdomConstructionRoute.PurposeConsignment,
						KingdomConstructionProjection.PurposeConsignment }
				};
			foreach (KingdomConstructionRoute route in Enum.GetValues(typeof(KingdomConstructionRoute)))
			{
				if (route == KingdomConstructionRoute.None)
				{
					Assert.AreEqual(KingdomConstructionProjection.None,
						KingdomConstructionRules.ProjectionFor(route));
					continue;
				}
				Assert.IsTrue(expected.ContainsKey(route), route.ToString());
				Assert.AreEqual(expected[route], KingdomConstructionRules.ProjectionFor(route),
					route.ToString());
				Assert.IsTrue(KingdomConstructionRules.ValidJob(Job(route)), route.ToString());
			}
		}

		[Test]
		public void LongRunningLawNamesOnlyRoutesWithPersistedWorkProgress()
		{
			KingdomConstructionRoute[] longRunning = new KingdomConstructionRoute[]
			{
				KingdomConstructionRoute.CommissionScaffold,
				KingdomConstructionRoute.PlanScaffold,
				KingdomConstructionRoute.PlotCommission,
				KingdomConstructionRoute.PlotPlan,
				KingdomConstructionRoute.SocketBuild,
				KingdomConstructionRoute.SocketConvert,
				KingdomConstructionRoute.Improvement,
				KingdomConstructionRoute.WearRepair,
				KingdomConstructionRoute.Strike
			};
			foreach (KingdomConstructionRoute route in Enum.GetValues(typeof(KingdomConstructionRoute)))
			{
				bool expected = Array.IndexOf(longRunning, route) >= 0;
				Assert.AreEqual(expected, KingdomConstructionRules.IsLongRunning(route), route.ToString());
			}
		}

		[Test]
		public void ScaffoldContinuationFaultsNeverDuplicateOrCompleteWithoutRemovalProof()
		{
			Assert.AreEqual(KingdomScaffoldContinuationAction.AdvanceWork,
				KingdomConstructionRules.ScaffoldContinuation(
					KingdomConstructionPhase.Working, true, 0, false, false));
			Assert.AreEqual(KingdomScaffoldContinuationAction.Quarantine,
				KingdomConstructionRules.ScaffoldContinuation(
					KingdomConstructionPhase.ProjectionPending, true, 0, false, false),
				"reload after Pending, before an observed create/Add result, may not guess");
			Assert.AreEqual(KingdomScaffoldContinuationAction.CreateSuccessor,
				KingdomConstructionRules.ScaffoldContinuation(
					KingdomConstructionPhase.Outstanding, true, 0, false, false),
				"a clean create failure or exactly-cleaned Add failure is retryable");
			Assert.AreEqual(KingdomScaffoldContinuationAction.RemovePredecessor,
				KingdomConstructionRules.ScaffoldContinuation(
					KingdomConstructionPhase.ProjectionPending, true, 1, false, false),
				"reload after Add retries only predecessor removal");
			Assert.AreEqual(KingdomScaffoldContinuationAction.RemovePredecessor,
				KingdomConstructionRules.ScaffoldContinuation(
					KingdomConstructionPhase.Outstanding, true, 1, false, false),
				"a destroy veto preserves one successor and retries no creation");
			Assert.AreEqual(KingdomScaffoldContinuationAction.Quarantine,
				KingdomConstructionRules.ScaffoldContinuation(
					KingdomConstructionPhase.InspectionRequired, false, 1, false, false),
				"a moved predecessor is not absence proof");
			Assert.AreEqual(KingdomScaffoldContinuationAction.Quarantine,
				KingdomConstructionRules.ScaffoldContinuation(
					KingdomConstructionPhase.ProjectionPending, false, 1, false, false),
				"successor presence alone cannot complete");
			Assert.AreEqual(KingdomScaffoldContinuationAction.CompleteReceipt,
				KingdomConstructionRules.ScaffoldContinuation(
					KingdomConstructionPhase.ProjectionPending, false, 1, true, false),
				"reload after removal, before Complete write, retries only the receipt write");
			Assert.AreEqual(KingdomScaffoldContinuationAction.Quarantine,
				KingdomConstructionRules.ScaffoldContinuation(
					KingdomConstructionPhase.ProjectionPending, true, 2, false, false),
				"two exact successors always fail closed");
		}

		[Test]
		public void ScaffoldCompletionTellingIsPostCompleteAndIdempotentAcrossReload()
		{
			Assert.AreEqual(KingdomScaffoldContinuationAction.TellCompletion,
				KingdomConstructionRules.ScaffoldContinuation(
					KingdomConstructionPhase.Complete, false, 1, true, false));
			Assert.AreEqual(KingdomScaffoldContinuationAction.Quarantine,
				KingdomConstructionRules.ScaffoldContinuation(
					KingdomConstructionPhase.Complete, false, 1, false, false),
				"a terminal row still cannot tell without exact predecessor-removal proof");
			Assert.AreEqual(KingdomScaffoldContinuationAction.None,
				KingdomConstructionRules.ScaffoldContinuation(
					KingdomConstructionPhase.Complete, false, 1, true, true));
			Assert.AreEqual(KingdomScaffoldContinuationAction.None,
				KingdomConstructionRules.ScaffoldContinuation(
					KingdomConstructionPhase.Complete, false, 0, true, false),
				"telling never runs without the exact receipt-bearing successor");
		}

		[Test]
		public void PrematureTerminalScaffoldMigrationRequiresExactFullyFundedClaims()
		{
			KingdomConstructionJob old = FullyFunded(
				KingdomConstructionRoute.CommissionScaffold, KingdomConstructionPhase.Complete);
			Assert.IsTrue(KingdomConstructionRules.FullyFundedExact(old));
			old.Claims.Exact = false;
			Assert.IsFalse(KingdomConstructionRules.FullyFundedExact(old));
			old.Claims.Exact = true;
			old.Claims.WaterOutstanding = 1;
			Assert.IsFalse(KingdomConstructionRules.FullyFundedExact(old));
		}

		[Test]
		public void PaidBuildReceiptFreezesExactPriceAndAccumulatesImprovement()
		{
			KingdomConstructionJob first = Job(KingdomConstructionRoute.PlotCommission,
				Water: 6, Material: MaterialCost(Timber: 4, Stone: 2));
			first.Claims.WaterSpent = 6;
			first.Claims.WaterOutstanding = 0;
			first.Claims.WaterLost = 6;
			first.Claims.MaterialSpent = first.Claims.MaterialRequested;
			first.Claims.MaterialOutstanding = new KingdomMaterialDebitCost().ToClaimString();
			first.Claims.MaterialLost = first.Claims.MaterialRequested;
			KingdomPaidBuildReceipt baseReceipt;
			Assert.IsTrue(KingdomConstructionRules.TryPaidBuildReceipt(first, null,
				out baseReceipt));
			Assert.AreEqual(6, baseReceipt.Water);
			Assert.AreEqual(10L, baseReceipt.WorkTicks);
			Assert.AreEqual(4, baseReceipt.Material.Materials.Get(KingdomMaterial.Timber));

			KingdomConstructionJob improvement = Job(KingdomConstructionRoute.Improvement,
				Water: 3, Material: MaterialCost(Stone: 5));
			improvement.DueTick = 25L;
			improvement.Claims.WaterSpent = 3;
			improvement.Claims.WaterOutstanding = 0;
			improvement.Claims.WaterLost = 3;
			improvement.Claims.MaterialSpent = improvement.Claims.MaterialRequested;
			improvement.Claims.MaterialOutstanding = new KingdomMaterialDebitCost().ToClaimString();
			improvement.Claims.MaterialLost = improvement.Claims.MaterialRequested;
			KingdomPaidBuildReceipt grown;
			Assert.IsTrue(KingdomConstructionRules.TryPaidBuildReceipt(improvement,
				baseReceipt, out grown));
			Assert.AreEqual(9, grown.Water);
			Assert.AreEqual(25L, grown.WorkTicks);
			Assert.AreEqual(4, grown.Material.Materials.Get(KingdomMaterial.Timber));
			Assert.AreEqual(7, grown.Material.Materials.Get(KingdomMaterial.Stone));
		}

		[Test]
		public void PaidBuildReceiptRejectsOutstandingInexactAndOverflow()
		{
			KingdomConstructionJob outstanding = Job(KingdomConstructionRoute.PlotCommission,
				Water: 1, Material: MaterialCost(Timber: 1));
			KingdomPaidBuildReceipt ignored;
			Assert.IsFalse(KingdomConstructionRules.TryPaidBuildReceipt(outstanding, null,
				out ignored));
			outstanding.Claims.Exact = false;
			Assert.IsFalse(KingdomConstructionRules.TryPaidBuildReceipt(outstanding, null,
				out ignored));

			KingdomConstructionJob funded = Job(KingdomConstructionRoute.Improvement,
				Water: 1);
			funded.Claims.WaterSpent = 1;
			funded.Claims.WaterOutstanding = 0;
			funded.Claims.WaterLost = 1;
			Assert.IsFalse(KingdomConstructionRules.TryPaidBuildReceipt(funded,
				new KingdomPaidBuildReceipt(int.MaxValue, 0L, new KingdomMaterialDebitCost()),
				out ignored), "water accumulation must refuse rather than wrap");
		}

		[Test]
		public void OwnerKeySeparatesRealmFoundingAndSettlementWithoutDelimiterCollisions()
		{
			string owner = KingdomConstructionRules.OwnerKey("a:b", 42L, "c:d");
			Assert.AreEqual(owner, KingdomConstructionRules.OwnerKey("a:b", 42L, "c:d"));
			Assert.AreNotEqual(owner, KingdomConstructionRules.OwnerKey("a", 42L, "b:c:d"));
			Assert.AreNotEqual(owner, KingdomConstructionRules.OwnerKey("a:b", 43L, "c:d"));
			Assert.AreNotEqual(owner, KingdomConstructionRules.OwnerKey("a:b", 42L, "c:e"));
			Assert.IsNull(KingdomConstructionRules.OwnerKey(null, 1L, "seat"));
			Assert.IsNull(KingdomConstructionRules.OwnerKey("realm", -1L, "seat"));
			Assert.IsNull(KingdomConstructionRules.OwnerKey("realm", 1L, " "));
		}

		[Test]
		public void EveryPhaseHasOneReloadActionAndPendingMutationsAlwaysInspect()
		{
			KingdomConstructionPhase[] pending = new KingdomConstructionPhase[]
			{
				KingdomConstructionPhase.WaterPending,
				KingdomConstructionPhase.MaterialPending,
				KingdomConstructionPhase.ProjectionPending,
				KingdomConstructionPhase.CompensationPending
			};
			for (int i = 0; i < pending.Length; i++)
			{
				KingdomConstructionJob job = FullyFunded(
					KingdomConstructionRoute.PlotCommission, pending[i]);
				Assert.IsTrue(KingdomConstructionRules.IsMutationPending(pending[i]));
				Assert.AreEqual(KingdomConstructionResumeAction.Inspect,
					KingdomConstructionRules.ResumeAction(job), pending[i].ToString());
			}

			Assert.AreEqual(KingdomConstructionResumeAction.ResumeFunding,
				KingdomConstructionRules.ResumeAction(FullyFunded(
					KingdomConstructionRoute.PlotCommission, KingdomConstructionPhase.Published)));
			Assert.AreEqual(KingdomConstructionResumeAction.ResumeFunding,
				KingdomConstructionRules.ResumeAction(FullyFunded(
					KingdomConstructionRoute.PlotCommission, KingdomConstructionPhase.WaterSettled)));
			Assert.AreEqual(KingdomConstructionResumeAction.RetryProjection,
				KingdomConstructionRules.ResumeAction(FullyFunded(
					KingdomConstructionRoute.PlotCommission, KingdomConstructionPhase.Funded)));
			Assert.AreEqual(KingdomConstructionResumeAction.RetryProjection,
				KingdomConstructionRules.ResumeAction(FullyFunded(
					KingdomConstructionRoute.PlotCommission, KingdomConstructionPhase.Outstanding)));
			Assert.AreEqual(KingdomConstructionResumeAction.AdvanceWork,
				KingdomConstructionRules.ResumeAction(FullyFunded(
					KingdomConstructionRoute.PlotCommission, KingdomConstructionPhase.Working)));
			Assert.AreEqual(KingdomConstructionResumeAction.AdvanceWork,
				KingdomConstructionRules.ResumeAction(FullyFunded(
					KingdomConstructionRoute.PlotCommission, KingdomConstructionPhase.Projected)));
			Assert.AreEqual(KingdomConstructionResumeAction.Inspect,
				KingdomConstructionRules.ResumeAction(FullyFunded(
					KingdomConstructionRoute.PlotCommission, KingdomConstructionPhase.InspectionRequired)));
			Assert.AreEqual(KingdomConstructionResumeAction.None,
				KingdomConstructionRules.ResumeAction(FullyFunded(
					KingdomConstructionRoute.PlotCommission, KingdomConstructionPhase.Complete)));
			Assert.AreEqual(KingdomConstructionResumeAction.None,
				KingdomConstructionRules.ResumeAction(FullyFunded(
					KingdomConstructionRoute.PlotCommission, KingdomConstructionPhase.Compensated)));
			Assert.AreEqual(KingdomConstructionResumeAction.None,
				KingdomConstructionRules.ResumeAction(FullyFunded(
					KingdomConstructionRoute.PlotCommission, KingdomConstructionPhase.Cancelled)));
		}

		[Test]
		public void OutstandingClaimsAlwaysResumeFundingBeforeProjection()
		{
			KingdomConstructionJob water = Job(KingdomConstructionRoute.RoadPaving,
				Phase: KingdomConstructionPhase.Outstanding, Water: 5);
			Assert.AreEqual(KingdomConstructionResumeAction.ResumeFunding,
				KingdomConstructionRules.ResumeAction(water));

			KingdomConstructionJob material = Job(KingdomConstructionRoute.RoadPaving,
				Phase: KingdomConstructionPhase.Outstanding, Material: MaterialCost(Timber: 2));
			Assert.AreEqual(KingdomConstructionResumeAction.ResumeFunding,
				KingdomConstructionRules.ResumeAction(material));

			water.Claims.Exact = false;
			Assert.AreEqual(KingdomConstructionResumeAction.Inspect,
				KingdomConstructionRules.ResumeAction(water));
		}

		[Test]
		public void WaterAttemptsMergeOnlyExactOutstandingSlicesAcrossReloads()
		{
			KingdomConstructionClaims claims = KingdomConstructionRules.NewClaims(10,
				new KingdomMaterialDebitCost());
			KingdomConstructionClaims first;
			Assert.IsTrue(KingdomConstructionRules.TryApplyWaterAttempt(claims,
				10, 4, 6, 5, true, out first));
			Assert.AreEqual(4, first.WaterSpent);
			Assert.AreEqual(6, first.WaterOutstanding);
			Assert.AreEqual(5, first.WaterLost);

			KingdomConstructionClaims reloaded = first.Copy();
			KingdomConstructionClaims second;
			Assert.IsTrue(KingdomConstructionRules.TryApplyWaterAttempt(reloaded,
				6, 6, 0, 7, true, out second));
			Assert.AreEqual(10, second.WaterSpent);
			Assert.AreEqual(0, second.WaterOutstanding);
			Assert.AreEqual(12, second.WaterLost);
			Assert.IsTrue(second.Exact);

			KingdomConstructionClaims rejected;
			Assert.IsFalse(KingdomConstructionRules.TryApplyWaterAttempt(first,
				10, 6, 4, 6, true, out rejected), "retry may request only persisted outstanding");
			Assert.IsFalse(KingdomConstructionRules.TryApplyWaterAttempt(first,
				6, 4, 1, 4, true, out rejected), "spent plus outstanding must equal request");
			Assert.IsFalse(KingdomConstructionRules.TryApplyWaterAttempt(first,
				6, 4, 2, 3, true, out rejected), "lost must cover proved spent");
		}

		[Test]
		public void InexactWaterAttemptPersistsClaimButQuarantinesRetry()
		{
			KingdomConstructionJob job = Job(KingdomConstructionRoute.CommissionScaffold,
				Phase: KingdomConstructionPhase.Outstanding, Water: 8);
			KingdomConstructionClaims measured;
			Assert.IsTrue(KingdomConstructionRules.TryApplyWaterAttempt(job.Claims,
				8, 3, 5, 9, false, out measured));
			job.Claims = measured;
			Assert.AreEqual(3, measured.WaterSpent);
			Assert.AreEqual(5, measured.WaterOutstanding);
			Assert.AreEqual(9, measured.WaterLost);
			Assert.IsFalse(measured.Exact);
			Assert.AreEqual(KingdomConstructionResumeAction.Inspect,
				KingdomConstructionRules.ResumeAction(job));
		}

		[Test]
		public void MaterialPartialThenReloadedRetryNeverCreditsOneUnitTwice()
		{
			KingdomMaterialDebitCost requested = MaterialCost(Stone: 3);
			KingdomMaterialDebitPlan firstPlan;
			KingdomMaterialDebitFault fault;
			Assert.IsTrue(KingdomMaterialDebitRules.TryPlan(requested,
				new KingdomMaterialDebitSource[]
				{
					new KingdomMaterialDebitSource(0, KingdomMaterialDebitSourceKind.Material,
						(int)KingdomMaterial.Stone, 1),
					new KingdomMaterialDebitSource(1, KingdomMaterialDebitSourceKind.Material,
						(int)KingdomMaterial.Stone, 1),
					new KingdomMaterialDebitSource(2, KingdomMaterialDebitSourceKind.Material,
						(int)KingdomMaterial.Stone, 1)
				}, out firstPlan, out fault));
			KingdomMaterialDebitResult partial = KingdomMaterialDebitRules.Classify(firstPlan,
				new int[] { 1, 0, 0 }, new bool[] { false, true, true },
				KingdomMaterialDebitFault.OperationRefused, "second source refused");
			Assert.AreEqual(KingdomMaterialDebitOutcome.IrreversiblePartial, partial.Outcome);

			KingdomConstructionClaims claims = KingdomConstructionRules.NewClaims(0, requested);
			KingdomConstructionClaims afterPartial;
			Assert.IsTrue(KingdomConstructionRules.TryApplyMaterial(claims, partial,
				out afterPartial));
			KingdomMaterialDebitCost spent;
			KingdomMaterialDebitCost outstanding;
			Assert.IsTrue(KingdomMaterialDebitCost.TryParseClaim(afterPartial.MaterialSpent,
				out spent));
			Assert.IsTrue(KingdomMaterialDebitCost.TryParseClaim(afterPartial.MaterialOutstanding,
				out outstanding));
			Assert.AreEqual(1, spent.Materials.Get(KingdomMaterial.Stone));
			Assert.AreEqual(2, outstanding.Materials.Get(KingdomMaterial.Stone));
			KingdomConstructionJob retryable = Job(KingdomConstructionRoute.WearRepair,
				Phase: KingdomConstructionPhase.Outstanding);
			retryable.Claims = afterPartial.Copy();
			Assert.AreEqual(KingdomConstructionResumeAction.ResumeFunding,
				KingdomConstructionRules.ResumeAction(retryable));

			KingdomMaterialDebitPlan retryPlan;
			Assert.IsTrue(KingdomMaterialDebitRules.TryPlan(outstanding,
				new KingdomMaterialDebitSource[]
				{
					new KingdomMaterialDebitSource(3, KingdomMaterialDebitSourceKind.Material,
						(int)KingdomMaterial.Stone, 2)
				}, out retryPlan, out fault));
			KingdomMaterialDebitResult exact = KingdomMaterialDebitRules.Classify(retryPlan,
				new int[] { 2 }, new bool[] { false }, KingdomMaterialDebitFault.None, null);
			KingdomConstructionClaims complete;
			Assert.IsTrue(KingdomConstructionRules.TryApplyMaterial(afterPartial.Copy(), exact,
				out complete));
			Assert.IsTrue(KingdomMaterialDebitCost.TryParseClaim(complete.MaterialSpent, out spent));
			Assert.IsTrue(KingdomMaterialDebitCost.TryParseClaim(complete.MaterialOutstanding,
				out outstanding));
			Assert.AreEqual(3, spent.Materials.Get(KingdomMaterial.Stone));
			Assert.IsTrue(outstanding.IsEmpty);
		}

		[Test]
		public void TransitionIsCopyOnWriteAndNeverMovesTimeBackward()
		{
			KingdomConstructionJob original = Job(KingdomConstructionRoute.WearRepair,
				Water: 2, Material: MaterialCost(Bit0: 1));
			KingdomConstructionJob next = KingdomConstructionRules.Transition(original,
				KingdomConstructionPhase.WaterPending, 5L, "pending");
			Assert.AreNotSame(original, next);
			Assert.AreNotSame(original.Claims, next.Claims);
			Assert.AreEqual(KingdomConstructionPhase.Published, original.Phase);
			Assert.AreEqual(KingdomConstructionPhase.WaterPending, next.Phase);
			Assert.AreEqual(original.CreatedTick, next.UpdatedTick);
			Assert.AreEqual(original.Revision + 1, next.Revision);
			next.Claims.WaterSpent = 2;
			Assert.AreEqual(0, original.Claims.WaterSpent);

			KingdomConstructionJob later = original.Copy();
			later.UpdatedTick = 100L;
			KingdomConstructionJob clamped = KingdomConstructionRules.Transition(later,
				KingdomConstructionPhase.WaterPending, 20L);
			Assert.AreEqual(100L, clamped.UpdatedTick);
			Assert.IsTrue(KingdomConstructionRules.ValidRegistryUpdate(later, clamped));
		}

		[Test]
		public void RegistryRoundTripIsCanonicalIndependentAndIdempotent()
		{
			KingdomConstructionJob later = Job(KingdomConstructionRoute.RoadPaving,
				"00000000000000000000000000000002");
			later.CreatedTick = 30L;
			later.StartedTick = 30L;
			later.DueTick = 30L;
			later.UpdatedTick = 30L;
			later.SubjectId = null;
			later.Payload = "v1;4,5;6,7";
			KingdomConstructionJob earlier = Job(KingdomConstructionRoute.WearRepair,
				"00000000000000000000000000000001", Material: MaterialCost(Timber: 1, Bit0: 2));
			List<KingdomConstructionJob> unordered = new List<KingdomConstructionJob>
			{
				later, earlier
			};
			string encoded;
			Assert.IsTrue(KingdomConstructionRules.TryEncode(unordered, out encoded));
			List<KingdomConstructionJob> decoded;
			Assert.IsTrue(KingdomConstructionRules.TryDecode(encoded, out decoded));
			Assert.AreEqual(2, decoded.Count);
			Assert.AreEqual(earlier.Id, decoded[0].Id);
			Assert.AreEqual(later.Id, decoded[1].Id);
			string encodedAgain;
			Assert.IsTrue(KingdomConstructionRules.TryEncode(decoded, out encodedAgain));
			Assert.AreEqual(encoded, encodedAgain);

			decoded[0].Claims.WaterOutstanding = 99;
			Assert.AreNotEqual(99, earlier.Claims.WaterOutstanding,
				"reload row must own a deep copy of claims");
		}

		[Test]
		public void BuildTruthFreezesRoundTripsAndRefusesContradictions()
		{
			KingdomConstructionJob plot = Job(KingdomConstructionRoute.PlotCommission);
			Assert.IsTrue(KingdomConstructionRules.FreezeBuildTruth(plot, true, 6));
			Assert.IsFalse(KingdomConstructionRules.FreezeBuildTruth(plot, true, 6),
				"published truth is write-once");
			Assert.IsTrue(KingdomConstructionRules.TryReadBuildTruth(plot,
				out bool hasPlot, out bool frontier, out int defence));
			Assert.IsTrue(hasPlot);
			Assert.IsFalse(frontier);
			Assert.AreEqual(6, defence);
			string encoded;
			Assert.IsTrue(KingdomConstructionRules.TryEncode(
				new List<KingdomConstructionJob> { plot }, out encoded));
			List<KingdomConstructionJob> decoded;
			Assert.IsTrue(KingdomConstructionRules.TryDecode(encoded, out decoded));
			Assert.IsTrue(KingdomConstructionRules.TryReadBuildTruth(decoded[0],
				out hasPlot, out frontier, out defence));
			Assert.AreEqual(6, defence);

			KingdomConstructionJob wall = Job(KingdomConstructionRoute.CommissionScaffold);
			Assert.IsTrue(KingdomConstructionRules.FreezeBuildTruth(wall, false, 9));
			Assert.IsTrue(KingdomConstructionRules.TryReadBuildTruth(wall,
				out hasPlot, out frontier, out defence));
			Assert.IsFalse(hasPlot);
			Assert.IsTrue(frontier);
			KingdomConstructionJob contradictory = wall.Copy();
			contradictory.BuildHasPlot = true;
			Assert.IsFalse(KingdomConstructionRules.ValidJob(contradictory));
			contradictory = plot.Copy();
			contradictory.BuildFrontier = true;
			Assert.IsFalse(KingdomConstructionRules.ValidJob(contradictory));
			KingdomConstructionJob wrongShape = Job(KingdomConstructionRoute.SocketConvert);
			Assert.IsFalse(KingdomConstructionRules.FreezeBuildTruth(wrongShape, false, 5));
			Assert.AreEqual(0, wrongShape.BuildTruthSchema,
				"a refused freeze must be atomic");
			KingdomConstructionJob unrelated = Job(KingdomConstructionRoute.WearRepair);
			Assert.IsFalse(KingdomConstructionRules.FreezeBuildTruth(unrelated, false, 5));
			Assert.AreEqual(0, unrelated.BuildTruthSchema);
			KingdomConstructionJob legacy = Job(KingdomConstructionRoute.Improvement);
			Assert.IsFalse(KingdomConstructionRules.TryReadBuildTruth(legacy,
				out hasPlot, out frontier, out defence));
			legacy.BuildDefence = 1;
			Assert.IsFalse(KingdomConstructionRules.ValidJob(legacy));

			string[] lines = encoded.Split('\n');
			string[] fields = lines[1].Split('|');
			fields[52] = "0";
			Assert.IsFalse(KingdomConstructionRules.TryDecode(lines[0] + "\n"
				+ string.Join("|", fields), out decoded),
				"wire-level route/shape contradiction must fail closed");
		}

		[Test]
		public void BuildTruthRequirementNamesOnlyEffectBearingRoutes()
		{
			HashSet<KingdomConstructionRoute> required = new HashSet<KingdomConstructionRoute>
			{
				KingdomConstructionRoute.CommissionScaffold,
				KingdomConstructionRoute.PlanScaffold,
				KingdomConstructionRoute.PlotCommission,
				KingdomConstructionRoute.PlotPlan,
				KingdomConstructionRoute.SocketBuild,
				KingdomConstructionRoute.SocketConvert,
				KingdomConstructionRoute.Improvement
			};
			foreach (KingdomConstructionRoute route in Enum.GetValues(
				typeof(KingdomConstructionRoute)))
			{
				if (route == KingdomConstructionRoute.None) continue;
				Assert.AreEqual(required.Contains(route),
					KingdomConstructionRules.RequiresBuildTruth(route), route.ToString());
			}
		}

		[Test]
		public void RegistryRejectsMalformedDuplicateAndOverActiveStateWhole()
		{
			KingdomConstructionJob one = Job(KingdomConstructionRoute.CommissionScaffold);
			string encoded;
			Assert.IsTrue(KingdomConstructionRules.TryEncode(
				new List<KingdomConstructionJob> { one }, out encoded));
			string row = encoded.Substring(encoded.IndexOf('\n') + 1);
			List<KingdomConstructionJob> ignored;
			Assert.IsFalse(KingdomConstructionRules.TryDecode("wrong\n" + row, out ignored));
			Assert.IsFalse(KingdomConstructionRules.TryDecode(encoded + "\n" + row, out ignored));
			Assert.IsFalse(KingdomConstructionRules.TryDecode(encoded + "\n", out ignored));
			Assert.IsFalse(KingdomConstructionRules.TryDecode(
				KingdomConstructionRules.FormatHeader + "\nnot-a-row", out ignored));

			List<KingdomConstructionJob> active = new List<KingdomConstructionJob>();
			for (int i = 1; i <= KingdomConstructionRules.MaxActiveRows + 1; i++)
			{
				active.Add(Job(KingdomConstructionRoute.CommissionScaffold,
					i.ToString("x32")));
			}
			Assert.IsFalse(KingdomConstructionRules.TryEncode(active, out encoded));
		}

		[Test]
		public void TerminalHistoryCompactsWithoutDroppingReplayProof()
		{
			List<KingdomConstructionJob> rows = new List<KingdomConstructionJob>();
			for (int i = 1; i <= 80; i++)
			{
				KingdomConstructionJob row = Job(KingdomConstructionRoute.SocketRedress,
					i.ToString("x32"), KingdomConstructionPhase.Complete);
				row.UpdatedTick = 100L + i;
				row.Outbox = SettledOutbox(row.Id);
				row.PhysicalPhase = KingdomPhysicalPhase.Settled;
				rows.Add(row);
			}
			List<KingdomConstructionJob> normalized;
			Assert.IsTrue(KingdomConstructionRules.TryNormalize(rows, out normalized));
			Assert.AreEqual(rows.Count, normalized.Count);
			for (int i = 1; i <= rows.Count; i++)
			{
				KingdomConstructionJob proof = normalized.Find(j => j.Id == i.ToString("x32"));
				Assert.IsNotNull(proof, "terminal replay ID must never be dropped");
				Assert.IsTrue(proof.Compacted);
				Assert.AreEqual(64, proof.CompactHash.Length);
				Assert.IsNull(proof.Outbox);
			}
			string once;
			string twice;
			Assert.IsTrue(KingdomConstructionRules.TryEncode(normalized, out once));
			List<KingdomConstructionJob> reloaded;
			Assert.IsTrue(KingdomConstructionRules.TryDecode(once, out reloaded));
			Assert.IsTrue(KingdomConstructionRules.TryEncode(reloaded, out twice));
			Assert.AreEqual(once, twice);

			normalized[0].CompactHash = new string('0', 64);
			Assert.IsFalse(KingdomConstructionRules.ValidJob(normalized[0]),
				"tampered compact proof must fail closed");
		}

		[Test]
		public void CurrentCompactProofAuthenticatesBuildTruthWhileLegacyProofStillLoads()
		{
			KingdomConstructionJob current = Job(KingdomConstructionRoute.PlotCommission,
				Phase: KingdomConstructionPhase.Complete);
			current.Outbox = SettledOutbox(current.Id, "raised");
			current.PhysicalPhase = KingdomPhysicalPhase.EffectsSettled;
			Assert.IsTrue(KingdomConstructionRules.FreezeBuildTruth(current, true, 7));
			List<KingdomConstructionJob> normalized;
			Assert.IsTrue(KingdomConstructionRules.TryNormalize(
				new List<KingdomConstructionJob> { current }, out normalized));
			Assert.IsTrue(normalized[0].Compacted);
			normalized[0].BuildDefence = 8;
			Assert.IsFalse(KingdomConstructionRules.ValidJob(normalized[0]));

			KingdomConstructionJob legacy = Job(KingdomConstructionRoute.PlotCommission,
				Phase: KingdomConstructionPhase.Complete);
			legacy.Outbox = SettledOutbox(legacy.Id, "raised");
			legacy.PhysicalPhase = KingdomPhysicalPhase.EffectsSettled;
			Assert.IsTrue(KingdomConstructionRules.TryNormalize(
				new List<KingdomConstructionJob> { legacy }, out normalized));
			string encoded;
			Assert.IsTrue(KingdomConstructionRules.TryEncode(normalized, out encoded));
			List<KingdomConstructionJob> decoded;
			Assert.IsTrue(KingdomConstructionRules.TryDecode(encoded, out decoded));
			Assert.AreEqual(0, decoded[0].BuildTruthSchema);
		}

		[Test]
		public void IntermediateOutboxesAndUnsettledEffectsNeverCompact()
		{
			KingdomConstructionJob socket = Job(KingdomConstructionRoute.SocketBuild,
				Phase: KingdomConstructionPhase.Complete);
			socket.Outbox = SettledOutbox(socket.Id, "socket-staked");
			socket.PhysicalPhase = KingdomPhysicalPhase.EffectsSettled;
			List<KingdomConstructionJob> normalized;
			Assert.IsTrue(KingdomConstructionRules.TryNormalize(
				new List<KingdomConstructionJob> { socket }, out normalized));
			Assert.IsFalse(normalized[0].Compacted);

			KingdomConstructionJob plot = Job(KingdomConstructionRoute.PlotCommission,
				Phase: KingdomConstructionPhase.Complete);
			plot.Outbox = SettledOutbox(plot.Id, "raised");
			plot.PhysicalPhase = KingdomPhysicalPhase.EffectsPending;
			Assert.IsTrue(KingdomConstructionRules.TryNormalize(
				new List<KingdomConstructionJob> { plot }, out normalized));
			Assert.IsFalse(normalized[0].Compacted);
			plot.PhysicalPhase = KingdomPhysicalPhase.EffectsSettled;
			Assert.IsTrue(KingdomConstructionRules.TryNormalize(
				new List<KingdomConstructionJob> { plot }, out normalized));
			Assert.IsTrue(normalized[0].Compacted);
			Assert.AreEqual(64, normalized[0].CompactHash.Length);
		}

		[Test]
		public void StrikeV2FreezesSortedExactTargetsAndLegacyCannotInventThem()
		{
			KingdomStrikeIntent intent = new KingdomStrikeIntent
			{
				DisplayName = "mill", BuildKey = "mill-key", TargetDisplayName = null,
				SalvageClaim = new KingdomMaterialDebitCost().ToClaimString(),
				HasPlot = true, X1 = 1, Y1 = 2, X2 = 8, Y2 = 9,
				PlotId = "plot-1", Effort = 17,
				Targets = new List<KingdomStrikeTarget>
				{
					new KingdomStrikeTarget { Id = "part-b", Blueprint = "Wall B", X = 4, Y = 5 },
					new KingdomStrikeTarget { Id = "part-a", Blueprint = "Wall A", X = 3, Y = 5 }
				}
			};
			string encoded;
			Assert.IsTrue(KingdomConstructionRules.TryEncodeStrikeIntent(intent, out encoded));
			StringAssert.StartsWith("v2|", encoded);
			KingdomStrikeIntent decoded;
			Assert.IsTrue(KingdomConstructionRules.TryDecodeStrikeIntent(encoded, out decoded));
			Assert.AreEqual(17, decoded.Effort);
			Assert.AreEqual("part-a", decoded.Targets[0].Id);
			Assert.AreEqual("part-b", decoded.Targets[1].Id);
			string[] fields = encoded.Split('|');
			string legacy = "v1|" + string.Join("|", fields, 1, 10);
			Assert.IsTrue(KingdomConstructionRules.TryDecodeStrikeIntent(legacy, out decoded));
			Assert.IsNull(decoded.Targets, "v1 may be inspected but never adopts current plot parts");
			Assert.AreEqual(0, decoded.Effort);

			intent.Targets.Clear();
			for (int i = 0; i < KingdomConstructionRules.MaxStrikeTargets; i++)
				intent.Targets.Add(new KingdomStrikeTarget { Id = "part-" + i,
					Blueprint = "Wall", X = 1 + i % 8, Y = 2 + i / 8 });
			intent.Y2 = 40;
			Assert.IsTrue(KingdomConstructionRules.TryEncodeStrikeIntent(intent, out encoded));
			intent.Targets.Add(new KingdomStrikeTarget
				{ Id = "over-cap", Blueprint = "Wall", X = 1, Y = 2 });
			Assert.IsFalse(KingdomConstructionRules.TryEncodeStrikeIntent(intent, out encoded));
		}

		[Test]
		public void RegistryV1V2V3V4FixturesMigrateToCanonicalV4()
		{
			KingdomConstructionJob original = Job(KingdomConstructionRoute.PlotCommission);
			Assert.IsTrue(KingdomConstructionRules.FreezeBuildTruth(original, true, 4));
			string v4;
			Assert.IsTrue(KingdomConstructionRules.TryEncode(
				new List<KingdomConstructionJob> { original }, out v4));
			List<KingdomConstructionJob> decoded = CanonicalRoundTrip(v4);
			Assert.AreEqual(KingdomConstructionRules.BuildTruthSchema,
				decoded[0].BuildTruthSchema);
			string[] lines = v4.Split('\n');
			string[] field = lines[1].Split('|');
			Assert.AreEqual(55, field.Length);
			string v3 = KingdomConstructionRules.PriorFormatHeader + "\n"
				+ string.Join("|", field, 0, 51);
			decoded = CanonicalRoundTrip(v3);
			Assert.AreEqual(0, decoded[0].BuildTruthSchema);
			string v2 = KingdomConstructionRules.OlderFormatHeader + "\n"
				+ string.Join("|", field, 0, 45);
			decoded = CanonicalRoundTrip(v2);
			Assert.AreEqual(original.Id, decoded[0].Id);
			string[] legacy = new string[26];
			for (int i = 0; i <= 8; i++) legacy[i] = field[i];
			legacy[9] = field[18]; legacy[10] = field[19];
			for (int i = 20; i <= 34; i++) legacy[11 + i - 20] = field[i];
			string v1 = KingdomConstructionRules.LegacyFormatHeader + "\n"
				+ string.Join("|", legacy);
			decoded = CanonicalRoundTrip(v1);
			Assert.AreEqual(0, decoded[0].BuildTruthSchema,
				"migration must preserve unknown truth rather than infer it");
		}

		[Test]
		public void PurposeConsignmentWireIsAppendOnlyAndCurrentFormatOnly()
		{
			KingdomConstructionJob purpose = Job(KingdomConstructionRoute.PurposeConsignment);
			purpose.PhysicalDestinationId = "destination-stockpile";
			purpose.PhysicalReceipt = "frozen-purpose-manifest";
			string current;
			Assert.IsTrue(KingdomConstructionRules.TryEncode(
				new List<KingdomConstructionJob> { purpose }, out current));
			List<KingdomConstructionJob> decoded;
			Assert.IsTrue(KingdomConstructionRules.TryDecode(current, out decoded));
			Assert.AreEqual(KingdomConstructionRoute.PurposeConsignment, decoded[0].Route);
			Assert.AreEqual(KingdomConstructionProjection.PurposeConsignment,
				decoded[0].Projection);

			string[] field = current.Split('\n')[1].Split('|');
			string priorRoute = KingdomConstructionRules.OlderFormatHeader + "\n"
				+ string.Join("|", field, 0, 45);
			Assert.IsFalse(KingdomConstructionRules.TryDecode(priorRoute, out decoded),
				"v2 cannot reinterpret the append-only purpose route");

			field[3] = ((int)KingdomConstructionRoute.WearRepair).ToString();
			field[5] = ((int)KingdomConstructionProjection.Repair).ToString();
			field[11] = ((int)KingdomPhysicalPhase.CargoDelivered).ToString();
			string priorPhase = KingdomConstructionRules.OlderFormatHeader + "\n"
				+ string.Join("|", field, 0, 45);
			Assert.IsFalse(KingdomConstructionRules.TryDecode(priorPhase, out decoded),
				"v2 cannot reinterpret an append-only cargo phase");
		}

		[Test]
		public void CounterReceiptsConfirmOnlyFrozenEndpoints()
		{
			int after;
			Assert.IsTrue(KingdomConstructionRules.TryCounterAfter(7, 3, out after));
			Assert.AreEqual(10, after);
			Assert.AreEqual(KingdomConstructionCasAction.Apply,
				KingdomConstructionRules.CounterCasAction(7, 7, 10));
			Assert.AreEqual(KingdomConstructionCasAction.Confirm,
				KingdomConstructionRules.CounterCasAction(10, 7, 10));
			Assert.AreEqual(KingdomConstructionCasAction.Quarantine,
				KingdomConstructionRules.CounterCasAction(8, 7, 10));
			Assert.IsFalse(KingdomConstructionRules.TryCounterAfter(int.MaxValue, 1, out after));
		}

		[Test]
		public void LedgerReceiptUsesCountAndHashNotTextMembership()
		{
			List<string> before = new List<string> { "same text" };
			int beforeCount, afterCount;
			string beforeHash, afterHash;
			Assert.IsTrue(KingdomConstructionRules.TryFreezeLedger(before, "same text",
				out beforeCount, out beforeHash, out afterCount, out afterHash));
			Assert.AreEqual(KingdomConstructionCasAction.Apply,
				KingdomConstructionRules.LedgerCasAction(before, beforeCount, beforeHash,
					afterCount, afterHash), "pre-existing equal text is not this event");
			before.Add("same text");
			Assert.AreEqual(KingdomConstructionCasAction.Confirm,
				KingdomConstructionRules.LedgerCasAction(before, beforeCount, beforeHash,
					afterCount, afterHash));
			before[0] = "interleaved";
			Assert.AreEqual(KingdomConstructionCasAction.Quarantine,
				KingdomConstructionRules.LedgerCasAction(before, beforeCount, beforeHash,
					afterCount, afterHash));
		}

		[Test]
		public void InterruptedAggregateFundingNeverAuthorizesAutomaticRecharge()
		{
			StringAssert.Contains("exact vessel bindings were not persisted",
				KingdomConstructionRules.InterruptedFundingDiagnostic(
					KingdomConstructionPhase.WaterPending));
			StringAssert.Contains("exact source bindings were not persisted",
				KingdomConstructionRules.InterruptedFundingDiagnostic(
					KingdomConstructionPhase.MaterialPending));
			Assert.IsNull(KingdomConstructionRules.InterruptedFundingDiagnostic(
				KingdomConstructionPhase.Outstanding));
			Assert.IsFalse(KingdomConstructionRules.CapacityInspectionRequired(
				KingdomConstructionRules.MaxRows - 2, 0));
			Assert.IsTrue(KingdomConstructionRules.CapacityInspectionRequired(
				KingdomConstructionRules.MaxRows - 1, 0));
			Assert.IsTrue(KingdomConstructionRules.CapacityInspectionRequired(
				KingdomConstructionRules.MaxRows, 0));
			Assert.IsTrue(KingdomConstructionRules.CapacityInspectionRequired(0,
				KingdomConstructionRules.MaxActiveRows - 1));
		}

		[Test]
		public void TerminalSupersessionRequiresExactOwnerZoneReceiptAndObject()
		{
			KingdomConstructionJob terminal = Job(KingdomConstructionRoute.PlotCommission,
				Phase: KingdomConstructionPhase.Complete);
			terminal.SourceId = "old-works";
			terminal.SubjectId = "old-works";
			terminal.OutputId = "building-1";
			terminal.Outbox = SettledOutbox(terminal.Id, "raised");
			terminal.PhysicalPhase = KingdomPhysicalPhase.EffectsSettled;
			Assert.IsTrue(KingdomConstructionRules.CanSupersedeTerminal(terminal,
				terminal.OwnerKey, terminal.ZoneId, terminal.Id, "building-1"));
			Assert.IsFalse(KingdomConstructionRules.CanSupersedeTerminal(terminal,
				terminal.OwnerKey, terminal.ZoneId, terminal.Id, "replacement"));
			Assert.IsFalse(KingdomConstructionRules.CanSupersedeTerminal(terminal,
				terminal.OwnerKey + "x", terminal.ZoneId, terminal.Id, "building-1"));
			terminal.Outbox = null;
			Assert.IsFalse(KingdomConstructionRules.CanSupersedeTerminal(terminal,
				terminal.OwnerKey, terminal.ZoneId, terminal.Id, "building-1"));
		}

		[Test]
		public void ConstructionSourceOrdersSocketRemovalAndContainsNoLegacyClearPath()
		{
			string root = LocateRepository();
			string socket = File.ReadAllText(Path.Combine(root, "Growth", "KingdomSocket.cs"));
			int continuation = socket.IndexOf("private static void ContinueSocketBuild",
				StringComparison.Ordinal);
			int intent = socket.IndexOf("KingdomPhysicalPhase.PredecessorRemovalPending",
				continuation, StringComparison.Ordinal);
			int remove = socket.IndexOf("source.Obliterate", intent, StringComparison.Ordinal);
			int project = socket.IndexOf("KingdomPlots.ProjectOnRect", remove,
				StringComparison.Ordinal);
			Assert.Greater(intent, continuation);
			Assert.Greater(remove, intent);
			Assert.Greater(project, remove);
			int legacy = socket.IndexOf("public static bool OnCleared", StringComparison.Ordinal);
			int legacyEnd = socket.IndexOf("private static bool TrySweepLegacyPlotParts", legacy,
				StringComparison.Ordinal);
			string body = socket.Substring(legacy, legacyEnd - legacy);
			Assert.IsFalse(body.Contains("SweepPlotParts"));
			Assert.IsFalse(body.Contains("LeaveSocket"));
			StringAssert.Contains("KingdomMaterials.InspectConstruction", body);
		}

		private sealed class GraveyardDestroyFake
		{
			public bool Valid = true;
			public bool MarkerPartRetained = true;

			public bool Destroy()
			{
				Valid = false;
				return true;
			}
		}

		[Test]
		public void GraveyardRemovalProofUsesCallbackAndIdentityNotRetainedParts()
		{
			GraveyardDestroyFake fake = new GraveyardDestroyFake();
			bool callback = fake.Destroy();
			Assert.IsTrue(fake.MarkerPartRetained, "engine graveyards retain serialized parts");
			Assert.AreEqual(KingdomExactRemovalAction.ProvedAbsent,
				KingdomConstructionRules.ExactRemovalAction(true, callback,
					fake.Valid, false, fake.MarkerPartRetained));
			Assert.AreEqual(KingdomExactRemovalAction.Quarantine,
				KingdomConstructionRules.ExactRemovalAction(true, false,
					false, false, true), "reload has no durable callback-success tombstone");
			Assert.AreEqual(KingdomExactRemovalAction.Quarantine,
				KingdomConstructionRules.ExactRemovalAction(true, true,
					false, true, true), "same-ID replacement defeats exact absence");
			Assert.AreEqual(KingdomExactRemovalAction.InvokeOnce,
				KingdomConstructionRules.ExactRemovalAction(false, false,
					true, true, true));
		}

		[Test]
		public void PlotOutputsAndFurnishingsPublishExactIdsBeforeAddCallbacks()
		{
			string root = LocateRepository();
			string plot = KingdomPlot2LogicalSource.Read();
			AssertOrdered(plot, "works = GameObject.Create", "UpdateOutput(ref Job, works.ID",
				"cell.AddObject(works)");
			AssertOrdered(plot, "UpdateFinalOutput(ref construction", "FinalOutputPending",
				"cell.AddObject(building)");
			AssertOrdered(plot, "row.Id = placed.ID", "FurnishingPending",
				"cell.AddObject(placed)");
			StringAssert.Contains("A replacement furnishing carries the construction receipt", plot);
			StringAssert.Contains("MaxFurnishItems", plot);
			StringAssert.Contains("FinalRemovalPending", plot);
			StringAssert.Contains("EffectsSettled", plot);
		}

		[Test]
		public void ConstructionLogicalAuthorityKeepsReceiptAbiAndRecoveryOrder()
		{
			string source = KingdomConstructionLogicalSource.Read();
			Assert.AreEqual(7, CountOf(source,
				"public static partial class KingdomConstruction"));
			AssertOrdered(source,
				"public const string RegistryStateKey = \"r_TAF_ConstructionJobs\";",
				"public const string ReceiptProperty = \"KingdomConstructionReceipt\";",
				"public const string PaidBuildSchemaProperty = \"r_TAF_PaidBuildSchema\";",
				"public const string PaidBuildWaterProperty = \"r_TAF_PaidBuildWater\";",
				"public const string PaidBuildMaterialProperty = \"r_TAF_PaidBuildMaterial\";",
				"public const string PaidBuildWorkProperty = \"r_TAF_PaidBuildWork\";",
				"public const int PaidBuildSchema = 1;",
				"private const int MaxLoadedLookupObjects = 4096;",
				"private static bool Resolving;");
			AssertOrdered(source, "public static bool FreezeBuildTruth(",
				"public static string OwnerOf(", "public static bool TryRead(",
				"public static KingdomConstructionStartResult TryFundNew(",
				"public static bool BeginProjection(", "public static void Bind(",
				"public static void OnSettlementPass(",
				"private static void RetryProjection(",
				"private static void InspectProjection(");
		}

		[Test]
		public void NewPlotWorksFreezeLabourBeforeProjectionAndLegacyWorksKeepTheirClock()
		{
			string root = LocateRepository();
			string plot = KingdomPlot2LogicalSource.Read();
			AssertOrdered(plot, "works.SetIntProperty(PlotWorkSchemaProperty, PlotWorkSchema)",
				"SetPlotWorkLong(works, PlotWorkRequiredProperty, part.TotalTicks)",
				"SetPlotWorkLong(works, PlotWorkRemainingProperty, part.TotalTicks)",
				"SetPlotWorkLong(works, PlotWorkLastTickProperty, The.Game.TimeTicks)",
				"KingdomConstruction.UpdateOutput(ref Job, works.ID)", "cell.AddObject(works)");
			StringAssert.Contains("if (schema == 0)", plot);
			StringAssert.Contains("StageAt(TimeTick - Works.StartTick, Works.TotalTicks)", plot);
			AssertOrdered(plot, "KingdomConstructionPresence.EffectivenessOf(parent, System",
				"if (selected) SayPlotWorkShortfall",
				"KingdomArchitectureRules.AdvanceLabour(",
				"SetPlotWorkLong(parent, PlotWorkLastTickProperty, progress.NextTick)",
				"SetPlotWorkLong(parent, PlotWorkRemainingProperty, remaining)");
			StringAssert.Contains("r_KingdomPlotWorks' positional save layout ends at DoorY", plot);
		}

		[Test]
		public void StandingWorksFreezePaidBillsAndStrikeReadsThemBeforeLegacyCatalogueFallback()
		{
			string root = LocateRepository();
			string construction = KingdomConstructionLogicalSource.Read();
			AssertOrdered(construction, "SetStringProperty(PaidBuildMaterialProperty, material)",
				"SetIntProperty(PaidBuildSchemaProperty, PaidBuildSchema)");
			StringAssert.Contains("TryPaidBuildReceipt(Job, previous", construction);

			string scaffold = File.ReadAllText(Path.Combine(root, "Growth", "KingdomScaffold.cs"));
			AssertOrdered(scaffold, "KingdomConstruction.Bind(Successor, Job)",
				"KingdomConstruction.FreezePaidBuild(Successor, Job",
				"KingdomDesign.ApplyRenderOverrides");

			string plot = KingdomPlot2LogicalSource.Read();
			AssertOrdered(plot, "PrepareFinalBuilding(building, entry",
				"KingdomConstruction.FreezePaidBuild(building, construction)",
				"KingdomPhysicalPhase.FinalOutputPending");

			string strike = KingdomMaterialsLogicalSource.Read();
			int schema = strike.IndexOf("int paidSchema = Building.GetIntProperty",
				StringComparison.Ordinal);
			int legacy = strike.IndexOf("cost = CostFor(key)", schema,
				StringComparison.Ordinal);
			int frozen = strike.IndexOf("cost = paid.Material.Materials", legacy,
				StringComparison.Ordinal);
			Assert.Greater(schema, 0);
			Assert.Greater(legacy, schema);
			Assert.Greater(frozen, legacy);
			StringAssert.Contains("building's paid construction receipt cannot be read",
				strike.Substring(schema, frozen - schema));
		}

		[Test]
		public void AuthoredMinimumStageIsCheckedByCommitPathsNotOnlyMenus()
		{
			string root = LocateRepository();
			string commission = KingdomCommissionLogicalSource.Read();
			AssertOrdered(commission, "Failure = StageRefusal(System, entry)",
				"KingdomZoning.Permits(System, zone.ZoneID, entry");

			string plot = KingdomPlot2LogicalSource.Read();
			AssertOrdered(plot, "Failure = KingdomCommission.StageRefusal(System, Entry)",
				"KingdomPlotRules.PlotSize staked = StakedSize");
			int plan = plot.IndexOf("public static bool PlanBlocked", StringComparison.Ordinal);
			int prepare = plot.IndexOf("internal static bool TryPreparePlan", plan,
				StringComparison.Ordinal);
			string planBody = plot.Substring(plan, prepare - plan);
			AssertOrdered(planBody, "KingdomCommission.StageRefusal(System, Entry)",
				"TryGetSpec(Entry.Key, out var spec)");
			StringAssert.Contains("System.Stage < Entry.MinStage", plot.Substring(prepare));

			string socket = File.ReadAllText(Path.Combine(root, "Growth", "KingdomSocket.cs"));
			Assert.GreaterOrEqual(CountOf(socket, "KingdomCommission.StageRefusal(System,"), 2,
				"conversion and vacant-socket builds both need the authored stage gate");
		}

		private static int CountOf(string Source, string Needle)
		{
			int count = 0;
			int offset = 0;
			while ((offset = Source.IndexOf(Needle, offset, StringComparison.Ordinal)) >= 0)
			{
				count++;
				offset += Needle.Length;
			}
			return count;
		}

		[Test]
		public void StrikeAndRoadSourcesFreezePhasesBeforePhysicalCallbacksAndCounters()
		{
			string root = LocateRepository();
			string strike = KingdomMaterialsLogicalSource.Read();
			AssertOrdered(strike, "KingdomPhysicalPhase.StrikeStampPending",
				"SetIntProperty(StrikeEffortProperty", "KingdomPhysicalPhase.StrikeWorking");
			AssertOrdered(strike, "KingdomPhysicalPhase.StrikeWorkComplete",
				"KingdomPhysicalPhase.PlotPartRemovalPending", "Part.Obliterate");
			StringAssert.Contains("interrupted before exact callback-success proof", strike);
			StringAssert.Contains("Intent.Targets", strike);

			string road = File.ReadAllText(Path.Combine(root, "Growth", "KingdomRoads.cs"));
			AssertOrdered(road, "if (!FreezeRoadReceipt", "KingdomPhysicalPhase.RoadPlanFrozen");
			int create = road.IndexOf("try { floor = GameObject.Create", StringComparison.Ordinal);
			int remove = road.IndexOf("bool removed;", create, StringComparison.Ordinal);
			Assert.Greater(create, 0);
			Assert.Greater(remove, create);
			AssertOrdered(road.Substring(create, remove - create), "floor.ID = row.NewId",
				"KingdomPhysicalPhase.RoadOutputPending", "cell.AddObject(floor)");
			int freeze = road.IndexOf("private static bool FreezeRoadReceipt",
				StringComparison.Ordinal);
			int encode = road.IndexOf("private static string EncodeRoadReceipt", freeze,
				StringComparison.Ordinal);
			AssertOrdered(road.Substring(freeze, encode - freeze),
				"outputId = System.Guid.NewGuid", "NewId = outputId");
			AssertOrdered(road, "KingdomPhysicalPhase.RoadRemovalPending",
				"old.Obliterate", "row.Settled = true");
			AssertOrdered(road, "KingdomPhysicalPhase.RoadTallyPending",
				"Z.SetZoneProperty(TallyProperty", "KingdomPhysicalPhase.RoadTallySettled");
			StringAssert.Contains("RoadTerminalExact", road);
		}

		private static void AssertOrdered(string Source, params string[] Needles)
		{
			int offset = 0;
			for (int i = 0; i < Needles.Length; i++)
			{
				int found = Source.IndexOf(Needles[i], offset, StringComparison.Ordinal);
				Assert.GreaterOrEqual(found, 0, "missing ordered source token: " + Needles[i]);
				offset = found + Needles[i].Length;
			}
		}

		private static string LocateRepository()
		{
			return TestMain.RepositoryRoot;
		}

		[Test]
		public void ShippedPositionalPartLayoutsMatchOldBinaryFixtures()
		{
			string root = LocateRepository();
			AssertDeclaredPositionalFields(
				KingdomPlot2LogicalSource.Read(),
				"public class r_KingdomPlotWorks", "public KingdomPlotRules.PlotRect Rect()",
				new string[] { "DesignKey", "DisplayName", "X1", "Y1", "X2", "Y2",
					"StartTick", "TotalTicks", "StageApplied", "Open", "Carved",
					"WallBlueprint", "ContentsTable", "StaffNeeded", "ThresholdManning",
					"DefencePending", "HasDoor", "DoorX", "DoorY" });
			AssertDeclaredPositionalFields(
				File.ReadAllText(Path.Combine(root, "Growth", "KingdomScaffold.cs")),
				"public class r_KingdomScaffold", "public override bool WantTurnTick()",
				new string[] { "TargetBlueprint", "TargetDisplayName", "CompleteTick",
					"RemainingTicks", "LastWorkedTick", "ShortfallSaid", "StaffNeeded",
					"ThresholdManning" });
			AssertDeclaredPositionalFields(
				KingdomUpgradeLogicalSource.Read(),
				"public partial class r_KingdomImprovement", "public override bool WantEvent",
				new string[] { "SuccessorKey", "SuccessorBlueprint", "Held", "Working",
					"Scaffold", "WorkCompleteTick", "AnnouncedReason" });

			// Literal bytes written in the shipped positional order. Read and rewrite every
			// value so this test also fixes the old field types/order, not only their names.
			byte[] plot = Convert.FromBase64String(
				"A2tleQRuYW1lAQAAAAIAAAADAAAABAAAAAUAAAAAAAAABgAAAAAAAAAHAAAAAQAEd2FsbAV0YWJsZQgAAAABCQAAAAEKAAAACwAAAA==");
			Assert.AreEqual(plot, RoundTripPlotFixture(plot));
			byte[] scaffold = Convert.FromBase64String(
				"AmJwB2Rpc3BsYXkMAAAAAAAAAA0AAAAAAAAADgAAAAAAAAABDwAAAAA=");
			Assert.AreEqual(scaffold, RoundTripScaffoldFixture(scaffold));
		}

		private static void AssertDeclaredPositionalFields(string Source, string Start,
			string End, string[] Expected)
		{
			int first = Source.IndexOf(Start, StringComparison.Ordinal);
			int last = Source.IndexOf(End, first, StringComparison.Ordinal);
			Assert.GreaterOrEqual(first, 0);
			Assert.Greater(last, first);
			MatchCollection matches = Regex.Matches(Source.Substring(first, last - first),
				@"(?m)^\s*public\s+(?!const\b)[\w.<>]+\s+(\w+)\s*;\s*$");
			List<string> actual = new List<string>();
			foreach (Match match in matches) actual.Add(match.Groups[1].Value);
			CollectionAssert.AreEqual(Expected, actual,
				"IComponent.Write/Read reflects these non-static fields positionally");
		}

		private static byte[] RoundTripPlotFixture(byte[] Bytes)
		{
			using (MemoryStream input = new MemoryStream(Bytes))
			using (BinaryReader reader = new BinaryReader(input))
			{
				string design = reader.ReadString(), name = reader.ReadString();
				int x1 = reader.ReadInt32(), y1 = reader.ReadInt32();
				int x2 = reader.ReadInt32(), y2 = reader.ReadInt32();
				long start = reader.ReadInt64(), total = reader.ReadInt64();
				int stage = reader.ReadInt32(); bool open = reader.ReadBoolean();
				bool carved = reader.ReadBoolean(); string wall = reader.ReadString();
				string table = reader.ReadString(); int staff = reader.ReadInt32();
				bool threshold = reader.ReadBoolean(); int defence = reader.ReadInt32();
				bool door = reader.ReadBoolean(); int doorX = reader.ReadInt32();
				int doorY = reader.ReadInt32();
				Assert.AreEqual(input.Length, input.Position);
				using (MemoryStream output = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(output))
				{
					writer.Write(design); writer.Write(name); writer.Write(x1); writer.Write(y1);
					writer.Write(x2); writer.Write(y2); writer.Write(start); writer.Write(total);
					writer.Write(stage); writer.Write(open); writer.Write(carved); writer.Write(wall);
					writer.Write(table); writer.Write(staff); writer.Write(threshold);
					writer.Write(defence); writer.Write(door); writer.Write(doorX); writer.Write(doorY);
					writer.Flush(); return output.ToArray();
				}
			}
		}

		private static byte[] RoundTripScaffoldFixture(byte[] Bytes)
		{
			using (MemoryStream input = new MemoryStream(Bytes))
			using (BinaryReader reader = new BinaryReader(input))
			{
				string blueprint = reader.ReadString(), display = reader.ReadString();
				long complete = reader.ReadInt64(), remaining = reader.ReadInt64();
				long worked = reader.ReadInt64(); bool said = reader.ReadBoolean();
				int staff = reader.ReadInt32(); bool threshold = reader.ReadBoolean();
				Assert.AreEqual(input.Length, input.Position);
				using (MemoryStream output = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(output))
				{
					writer.Write(blueprint); writer.Write(display); writer.Write(complete);
					writer.Write(remaining); writer.Write(worked); writer.Write(said);
					writer.Write(staff); writer.Write(threshold); writer.Flush();
					return output.ToArray();
				}
			}
		}

		[Test]
		public void AutomaticPlotClearancePaysExactPhysicalStockNotDeadCounters()
		{
			string root = LocateRepository();
			string source = KingdomPlot2LogicalSource.Read();
			StringAssert.Contains("ResumeClearPayout", source);
			StringAssert.Contains("ClearOutputIdProperty = \"r_TAF_PlotClearOutputId\"", source);
			StringAssert.Contains("ClearOutputMarkerProperty = \"r_TAF_PlotClearOutputMarker\"", source);
			StringAssert.Contains("ClearDestinationKindProperty", source);
			StringAssert.Contains("KingdomMaterials.MaterialStock stock = KingdomMaterials.Stock(Z)",
				source);
			StringAssert.Contains("KingdomMaterials.BlueprintFor(stockMaterial)", source);
			StringAssert.Contains("destination.Inventory.AddObject(item, null, Silent: true, NoStack: true)",
				source);
			StringAssert.Contains("cell.AddObject(item, NoStack: true, Silent: true)", source);
			StringAssert.Contains("ReferenceEquals(accepted, item)", source);
			StringAssert.Contains("ExactClearOutput", source);
			StringAssert.Contains("KingdomConstructionRules.CounterCasAction", source);
			StringAssert.Contains("[Obsolete(\"Use KingdomMaterials.Stock(zone).Tally", source);
			StringAssert.DoesNotContain("MaterialStatePrefix", source);
			StringAssert.DoesNotContain("ModIntGameState(\"r_TAF_Material_", source);

			int removal = source.IndexOf("ClearInt(Works, ClearRemovedProperty, 1);",
				StringComparison.Ordinal);
			int payout = source.IndexOf("PrepareClearOutput(Works, Z, material, amount)",
				removal, StringComparison.Ordinal);
			int tally = source.IndexOf("SetClearTally(Works, material", payout,
				StringComparison.Ordinal);
			Assert.GreaterOrEqual(removal, 0);
			Assert.Greater(payout, removal, "source removal must be proved before payout");
			Assert.Greater(tally, payout, "summary tally must follow exact physical payout");
		}

		[Test]
		public void ImprovementHandoverUsesNamedReceiptsExactNoStackAndDrainFirstPhases()
		{
			string root = LocateRepository();
			string source = KingdomUpgradeLogicalSource.Read();
			StringAssert.Contains("HandoverPrefix = \"r_TAF_ImprovementHandover:\"", source);
			StringAssert.Contains("Explicit accessors have no backing fields", source);
			StringAssert.Contains("NoStack: true", source);
			StringAssert.Contains("ReferenceEquals(accepted, Item)", source);
			StringAssert.Contains("CellKey(Where) == Receipt.HandoverItemDestinationId", source);
			StringAssert.Contains("ReferenceCount(Owner.Inventory.Objects, Item) == 1", source);
			StringAssert.Contains("ReferenceCount(Where.GetObjects(), Item) == 1", source);
			StringAssert.Contains("HandoverItemMovedBefore", source);
			StringAssert.Contains("HandoverItemMovedAfter", source);
			StringAssert.Contains("HandoverItemDestinationKind < 1", source);
			StringAssert.Contains("HandoverItemMovedBefore == int.MaxValue", source);
			StringAssert.Contains("Inventory moved count has a third value", source);
			StringAssert.Contains("Inventory removal changed an endpoint before throwing", source);
			StringAssert.Contains("ExactLiquidReceiptShape", source);
			StringAssert.Contains("Exact compensation could not be proved", source);

			int liquid = source.IndexOf("internal static bool CarryLiquidDurable",
				StringComparison.Ordinal);
			int inventory = source.IndexOf("internal static bool CarryInventoryDurable",
				liquid, StringComparison.Ordinal);
			string liquidBody = source.Substring(liquid, inventory - liquid);
			AssertOrdered(liquidBody, "HandoverPhase = 1", "KingdomLiquids.Drain",
				"HandoverPhase = 2", "Target.MixWith", "HandoverPhase = 3");
			StringAssert.Contains("CompensateLiquid", liquidBody);

			int handover = source.IndexOf("public static void HandOver", StringComparison.Ordinal);
			int carryLiquid = source.IndexOf("CarryLiquidDurable", handover,
				StringComparison.Ordinal);
			int carryInventory = source.IndexOf("CarryInventoryDurable", carryLiquid,
				StringComparison.Ordinal);
			int grow = source.IndexOf("KingdomPlots.GrowInPlace", carryInventory,
				StringComparison.Ordinal);
			int marks = source.IndexOf("CarryMarks(Predecessor", grow, StringComparison.Ordinal);
			int removalIntent = source.IndexOf("KingdomPhysicalPhase.FinalRemovalPending", marks,
				StringComparison.Ordinal);
			int destroy = source.IndexOf("Predecessor.Destroy", removalIntent,
				StringComparison.Ordinal);
			int removed = source.IndexOf("KingdomPhysicalPhase.FinalRemoved", destroy,
				StringComparison.Ordinal);
			Assert.Greater(carryLiquid, handover);
			Assert.Greater(carryInventory, carryLiquid);
			Assert.Greater(grow, carryInventory);
			Assert.Greater(marks, grow, "plot state must be read before marks publish closure");
			Assert.Greater(removalIntent, marks);
			Assert.Greater(destroy, removalIntent);
			Assert.Greater(removed, destroy);
			StringAssert.Contains("FailHandover(intent,", source.Substring(handover));
			StringAssert.DoesNotContain("FinishProjection(ref job, false, false,\n"
				+ "\t\t\t\t\t\"The improved successor could not be verified before handover.\"",
				source.Substring(handover));
			StringAssert.Contains("requires inspection", source);
		}

		[Test]
		public void RegistryUpdatesRequireExactNextRevisionAndLegalRawStates()
		{
			KingdomConstructionJob current = Job(KingdomConstructionRoute.Improvement);
			KingdomConstructionJob next = KingdomConstructionRules.Transition(current,
				KingdomConstructionPhase.WaterPending, 11L);
			Assert.IsTrue(KingdomConstructionRules.ValidRegistryUpdate(current, next));

			KingdomConstructionJob skipped = next.Copy();
			skipped.Revision++;
			Assert.IsFalse(KingdomConstructionRules.ValidRegistryUpdate(current, skipped));
			KingdomConstructionJob wrongOwner = next.Copy();
			wrongOwner.OwnerKey += "-replacement";
			Assert.IsFalse(KingdomConstructionRules.ValidRegistryUpdate(current, wrongOwner));
			KingdomConstructionJob illegal = KingdomConstructionRules.Transition(current,
				KingdomConstructionPhase.Working, 11L);
			Assert.IsFalse(KingdomConstructionRules.ValidRegistryUpdate(current, illegal));
			KingdomConstructionJob waterSettled = Job(KingdomConstructionRoute.Improvement,
				Phase: KingdomConstructionPhase.WaterSettled);
			KingdomConstructionJob waterRetry = KingdomConstructionRules.Transition(waterSettled,
				KingdomConstructionPhase.WaterPending, 11L);
			Assert.IsTrue(KingdomConstructionRules.ValidRegistryUpdate(waterSettled, waterRetry));
			KingdomConstructionJob older = next.Copy();
			current.UpdatedTick = 100L;
			older.UpdatedTick = 99L;
			Assert.IsFalse(KingdomConstructionRules.ValidRegistryUpdate(current, older));

			KingdomConstructionJob saturated = current.Copy();
			saturated.Revision = int.MaxValue;
			KingdomConstructionJob noNextRevision = KingdomConstructionRules.Transition(saturated,
				KingdomConstructionPhase.WaterPending, 11L);
			Assert.AreEqual(int.MaxValue, noNextRevision.Revision);
			Assert.IsFalse(KingdomConstructionRules.ValidRegistryUpdate(saturated,
				noNextRevision));

			KingdomConstructionJob futurePhase = next.Copy();
			futurePhase.Phase = (KingdomConstructionPhase)255;
			Assert.IsFalse(KingdomConstructionRules.ValidRegistryUpdate(current, futurePhase));
			KingdomConstructionJob futurePhysical = next.Copy();
			futurePhysical.PhysicalPhase = (KingdomPhysicalPhase)255;
			Assert.IsFalse(KingdomConstructionRules.ValidRegistryUpdate(current, futurePhysical));

			KingdomConstructionJob underfunded = Job(KingdomConstructionRoute.Improvement,
				Phase: KingdomConstructionPhase.MaterialPending, Water: 1);
			KingdomConstructionJob falseFunded = KingdomConstructionRules.Transition(underfunded,
				KingdomConstructionPhase.Funded, 11L);
			Assert.IsFalse(KingdomConstructionRules.ValidRegistryUpdate(underfunded, falseFunded));
			underfunded.Phase = KingdomConstructionPhase.Outstanding;
			KingdomConstructionJob falseProjection = KingdomConstructionRules.Transition(underfunded,
				KingdomConstructionPhase.ProjectionPending, 11L);
			Assert.IsFalse(KingdomConstructionRules.ValidRegistryUpdate(underfunded,
				falseProjection));

			KingdomConstructionJob frozen = Job(KingdomConstructionRoute.Improvement);
			Assert.IsTrue(KingdomConstructionRules.FreezeBuildTruth(frozen, false, 5));
			KingdomConstructionJob changedTruth = KingdomConstructionRules.Transition(frozen,
				KingdomConstructionPhase.WaterPending, 11L);
			changedTruth.BuildDefence = 6;
			Assert.IsFalse(KingdomConstructionRules.ValidRegistryUpdate(frozen, changedTruth));
		}

		[Test]
		public void PhysicalIdentityAndHandoverTopologiesFailClosedOnEveryDuplicate()
		{
			Assert.AreEqual(KingdomPhysicalLookupState.Absent,
				KingdomConstructionRules.PhysicalLookupState(0, false));
			Assert.AreEqual(KingdomPhysicalLookupState.Exact,
				KingdomConstructionRules.PhysicalLookupState(1, true));
			Assert.AreEqual(KingdomPhysicalLookupState.Ambiguous,
				KingdomConstructionRules.PhysicalLookupState(1, false));
			Assert.AreEqual(KingdomPhysicalLookupState.Ambiguous,
				KingdomConstructionRules.PhysicalLookupState(2, true));

			Assert.AreEqual(KingdomHandoverItemTopology.Source,
				KingdomConstructionRules.HandoverItemTopology(1, 0, 0, 1, 1, 1, 0));
			Assert.AreEqual(KingdomHandoverItemTopology.DestinationInventory,
				KingdomConstructionRules.HandoverItemTopology(0, 1, 0, 1, 1, 2, 0));
			Assert.AreEqual(KingdomHandoverItemTopology.DestinationCell,
				KingdomConstructionRules.HandoverItemTopology(0, 0, 1, 1, 1, 0, 1));
			Assert.AreEqual(KingdomHandoverItemTopology.Loose,
				KingdomConstructionRules.HandoverItemTopology(0, 0, 0, 0, 0, 0, 0));
			Assert.AreEqual(KingdomHandoverItemTopology.EnteringCell,
				KingdomConstructionRules.HandoverItemTopology(0, 0, 0, 0, 0, 0, 1));

			Assert.AreEqual(KingdomHandoverItemTopology.Invalid,
				KingdomConstructionRules.HandoverItemTopology(1, 0, 0, 2, 1, 1, 0));
			Assert.AreEqual(KingdomHandoverItemTopology.Invalid,
				KingdomConstructionRules.HandoverItemTopology(1, 1, 0, 1, 1, 1, 0));
			Assert.AreEqual(KingdomHandoverItemTopology.Invalid,
				KingdomConstructionRules.HandoverItemTopology(0, 0, 0, 1, 0, 0, 0));
			Assert.AreEqual(KingdomHandoverItemTopology.Invalid,
				KingdomConstructionRules.HandoverItemTopology(1, 0, 0, 1, 1, 3, 0));
		}

		[Test]
		public void ImprovementItemEscrowRootsBeforeRemovalAndSurvivesEveryAddCut()
		{
			string root = LocateRepository();
			string source = KingdomUpgradeLogicalSource.Read();
			StringAssert.DoesNotContain("GameObject.FindByID", source);
			StringAssert.Contains("HandoverEscrowPrefix = \"r_TAF_ImprovementItemEscrow:\"", source);
			StringAssert.Contains("HandoverConstructionReceipt", source);
			StringAssert.Contains("ExactHandoverAuthority", source);
			StringAssert.Contains("if (string.IsNullOrEmpty(frozen)) return false", source);
			StringAssert.Contains("Legacy improvement handover lacks a current exact construction receipt",
				source);

			int carry = source.IndexOf("internal static bool CarryInventoryDurable",
				StringComparison.Ordinal);
			int resume = source.IndexOf("private static bool ResumePendingItem", carry,
				StringComparison.Ordinal);
			string carryBody = source.Substring(carry, resume - carry);
			AssertOrdered(carryBody, "HandoverItemEscrowKey = EscrowKeyFor",
				"RootEscrowItem", "HandoverItemPhase = 1", "RemoveObjectFromInventory");

			int rootEscrow = source.IndexOf("private static bool RootEscrowItem",
				StringComparison.Ordinal);
			int tryEscrow = source.IndexOf("private static bool TryEscrowItem", rootEscrow,
				StringComparison.Ordinal);
			string rootBody = source.Substring(rootEscrow, tryEscrow - rootEscrow);
			AssertOrdered(rootBody, "ObjectGameState.TryGetValue", "SetObjectGameState",
				"TryEscrowItem");

			int place = source.IndexOf("private static bool PlacePendingItem",
				StringComparison.Ordinal);
			int restore = source.IndexOf("private static bool RestoreItem", place,
				StringComparison.Ordinal);
			string placeBody = source.Substring(place, restore - place);
			StringAssert.Contains("destination.AddObject(Item", placeBody);
			StringAssert.Contains("Where.AddObject(Item, NoStack: true", placeBody);
			StringAssert.Contains("ReproveEscrowItem", placeBody);
			StringAssert.Contains("ReferenceEquals(accepted, Item)", placeBody);

			int settle = source.IndexOf("private static bool SettlePendingItem",
				StringComparison.Ordinal);
			int retire = source.IndexOf("private static bool RetirePendingItem", settle,
				StringComparison.Ordinal);
			AssertOrdered(source.Substring(settle, retire - settle), "HandoverItemPhase = 3",
				"HandoverMovedItems = Receipt.HandoverItemMovedAfter",
				"HandoverItemPhase = 4", "RetirePendingItem");
			int clear = source.IndexOf("private static void ClearPendingItem", retire,
				StringComparison.Ordinal);
			AssertOrdered(source.Substring(retire, clear - retire),
				"ObjectGameState.Remove", "ClearPendingItem");

			string restoreBody = source.Substring(restore, source.IndexOf(
				"private static bool ExactHandoverObjects", restore, StringComparison.Ordinal) - restore);
			AssertOrdered(restoreBody, "ExactEnteringCell", "Item.Physics.CurrentCell = null",
				"ExactLooseItem", "Source.Inventory.AddObject");
		}

		[Test]
		public void ConstructionAuthorityFlagsAndGrowOutputsUseExactDurableProofs()
		{
			string root = LocateRepository();
			string construction = KingdomConstructionLogicalSource.Read();
			StringAssert.Contains("ReferenceEquals(The.Game.RequireSystem<KingdomSystem>(), System)",
				construction);
			StringAssert.Contains("System.ClaimedZones.Contains(Z.ZoneID)", construction);
			StringAssert.Contains("KingdomConstructionRules.PhysicalLookupState", construction);

			string scaffold = File.ReadAllText(Path.Combine(root, "Growth", "KingdomScaffold.cs"));
			StringAssert.Contains("finalPending != 0 && finalPending != 1", scaffold);
			StringAssert.Contains("told != 0 && told != 1", scaffold);
			string roads = File.ReadAllText(Path.Combine(root, "Growth", "KingdomRoads.cs"));
			StringAssert.Contains("KingdomPhysicalLookupState FindOurFloor", roads);
			StringAssert.Contains("floorState == KingdomPhysicalLookupState.Ambiguous", roads);
			string socket = File.ReadAllText(Path.Combine(root, "Growth", "KingdomSocket.cs"));
			StringAssert.Contains("ReferenceEquals(accepted, marker)", socket);

			string plot = KingdomPlot2LogicalSource.Read();
			StringAssert.Contains("if (string.IsNullOrEmpty(receipt)) return false", plot);
			int grow = plot.IndexOf("public static bool GrowInPlace", StringComparison.Ordinal);
			int build = plot.IndexOf("private static bool TryBuildGrowthPlan", grow,
				StringComparison.Ordinal);
			AssertOrdered(plot.Substring(grow, build - grow), "TryBuildGrowthPlan",
				"SetStringProperty(GrowthReceiptProperty", "RequirePart<r_KingdomYielding>",
				"ApplyGrowthPlan");
			int apply = plot.IndexOf("private static bool ApplyGrowthPlan", build,
				StringComparison.Ordinal);
			AssertOrdered(plot.Substring(build, apply - build), "Guid.NewGuid().ToString(\"N\")",
				"Id = outputId", "Plan = new GrowthPlan");
			int settleAdd = plot.IndexOf("private static bool TrySettleGrowthAddAfterCallback",
				apply, StringComparison.Ordinal);
			string applyBody = plot.Substring(apply, settleAdd - apply);
			AssertOrdered(applyBody, "row.State = 1", "PublishGrowthPlan", "exact.Destroy",
				"KingdomPhysicalLookupState.Absent", "row.State = 2");
			AssertOrdered(applyBody, "GameObject.Create(row.Blueprint)", "ValidateGrowthWorld",
				"placed.ID = row.Id", "RootGrowthOutput", "row.State = 1", "PublishGrowthPlan",
				"AddObject(placed)", "ReferenceEquals(accepted, placed)");
			string settleBody = plot.Substring(settleAdd, plot.IndexOf(
				"private static bool ValidateGrowthWorld", settleAdd,
				StringComparison.Ordinal) - settleAdd);
			AssertOrdered(settleBody, "ReferenceEquals(rooted, Expected)",
				"ExactGrowthOutput", "Row.State = 2", "RetireSettledGrowthRoot");
			int exactOutput = plot.IndexOf("private static bool ExactGrowthOutput",
				settleAdd, StringComparison.Ordinal);
			int referenceCount = plot.IndexOf("private static int ReferenceCountInCell",
				exactOutput, StringComparison.Ordinal);
			string exactOutputBody = plot.Substring(exactOutput, referenceCount - exactOutput);
			StringAssert.Contains("Item.Physics.InInventory != null", exactOutputBody);
			AssertOrdered(exactOutputBody, "FindExactId(Z, Row.Id, out global)",
				"KingdomPhysicalLookupState.Exact", "ReferenceEquals(global, Item)");
		}

		[Test]
		public void CellPayloadRoundTripsExactlyAndRejectsEveryUnsafeShape()
		{
			List<KingdomConstructionCell> cells = new List<KingdomConstructionCell>
			{
				new KingdomConstructionCell(1, 2),
				new KingdomConstructionCell(80, 24),
				new KingdomConstructionCell(1023, 1023)
			};
			string payload;
			Assert.IsTrue(KingdomConstructionRules.TryEncodeCells(cells, out payload));
			Assert.AreEqual("v1;1,2;80,24;1023,1023", payload);
			List<KingdomConstructionCell> decoded;
			Assert.IsTrue(KingdomConstructionRules.TryDecodeCells(payload, out decoded));
			Assert.AreEqual(3, decoded.Count);
			Assert.AreEqual(80, decoded[1].X);
			Assert.AreEqual(24, decoded[1].Y);

			Assert.IsFalse(KingdomConstructionRules.TryEncodeCells(null, out payload));
			Assert.IsFalse(KingdomConstructionRules.TryEncodeCells(
				new List<KingdomConstructionCell>(), out payload));
			Assert.IsFalse(KingdomConstructionRules.TryEncodeCells(
				new List<KingdomConstructionCell>
				{
					new KingdomConstructionCell(1, 2),
					new KingdomConstructionCell(1, 2)
				}, out payload));
			Assert.IsFalse(KingdomConstructionRules.TryDecodeCells("v1;1,2;1,2", out decoded));
			Assert.IsFalse(KingdomConstructionRules.TryDecodeCells("v1;-1,2", out decoded));
			Assert.IsFalse(KingdomConstructionRules.TryDecodeCells("v1;01,2", out decoded));
			Assert.IsFalse(KingdomConstructionRules.TryDecodeCells("v2;1,2", out decoded));
		}

		[Test]
		public void InvalidRowsFailClosedInsteadOfBeingRepaired()
		{
			KingdomConstructionJob job = Job(KingdomConstructionRoute.SocketConvert);
			Assert.IsTrue(KingdomConstructionRules.ValidJob(job));
			job.Projection = KingdomConstructionProjection.Redress;
			Assert.IsFalse(KingdomConstructionRules.ValidJob(job));
			job.Projection = KingdomConstructionRules.ProjectionFor(job.Route);
			job.Claims.WaterOutstanding++;
			Assert.IsFalse(KingdomConstructionRules.ValidJob(job));
			string ignored;
			Assert.IsFalse(KingdomConstructionRules.TryEncode(
				new List<KingdomConstructionJob> { job }, out ignored));

			KingdomConstructionJob backward = Job(KingdomConstructionRoute.PlotCommission);
			backward.DueTick = backward.StartedTick - 1L;
			Assert.IsFalse(KingdomConstructionRules.ValidJob(backward));
			backward = Job(KingdomConstructionRoute.PlotCommission);
			backward.StartedTick = backward.CreatedTick - 1L;
			Assert.IsFalse(KingdomConstructionRules.ValidJob(backward));
		}

		[Test]
		public void PaidBuildEffectsFreezeBeforeFundingAndProjectionReadsReceiptOnly()
		{
			string commission = KingdomCommissionLogicalSource.Read();
			string plan = TestMain.ReadRepositoryText("Growth/KingdomPlanMarker.cs");
			string upgrade = KingdomUpgradeLogicalSource.Read();
			string plot = KingdomPlot2LogicalSource.Read();
			string socket = TestMain.ReadRepositoryText("Growth/KingdomSocket.cs");
			AssertOrdered(commission, "KingdomConstruction.FreezeBuildTruth(job",
				"KingdomConstruction.TryFundNew(job");
			AssertOrdered(plan, "KingdomConstruction.FreezeBuildTruth(job",
				"KingdomConstruction.TryFundNew(job");
			AssertOrdered(upgrade, "KingdomConstruction.FreezeBuildTruth(job",
				"KingdomConstruction.TryFundNew(job");
			AssertOrdered(plot, "KingdomConstruction.FreezeBuildTruth(job",
				"KingdomConstruction.TryFundNew(job");
			AssertOrdered(socket, "KingdomConstruction.FreezeBuildTruth(job",
				"KingdomConstruction.TryFundNew(job");

			string commissionProjection = Between(commission,
				"private static bool ProjectScaffold(", "private static GameObject FindExpectedScaffold(");
			string improvementProjection = Between(upgrade,
				"private static bool ProjectImprovement(",
				"private static bool ExpectedImprovementScaffold(");
			string planProjection = Between(plan, "private static bool Realize(",
				"internal static void RetryConstruction(");
			foreach (string body in new[] { commissionProjection, improvementProjection,
				planProjection })
			{
				StringAssert.Contains("ApplyBuildTruth", body);
				StringAssert.DoesNotContain("BuiltDefence(", body);
				StringAssert.DoesNotContain("HasSkill(", body);
				StringAssert.DoesNotContain("IsPlotDesign(", body);
			}
			string construction = KingdomConstructionLogicalSource.Read();
			AssertOrdered(construction, "RequiresBuildTruth(job.Route)",
				"TryResumeFunding(job, Z, Survey");
			StringAssert.Contains("LegacyProjectedBuildTruthMatches", commission);
			StringAssert.Contains("LegacyProjectedBuildTruthMatches", plan);
			StringAssert.Contains("LegacyProjectedBuildTruthMatchesUnknownPlot", upgrade);
			StringAssert.Contains("part.DefencePending = defence;", plot);
			string convertProjection = Between(socket,
				"private static bool ProjectConvertOrder(",
				"internal static bool ResumeStrikeSuccessor(");
			AssertOrdered(convertProjection, "TryReadBuildTruth(Job",
				"KingdomConstruction.BeginProjection(ref Updated");
			StringAssert.Contains("The unprojected legacy plot plan predates frozen build effects.",
				plot);
		}

		private static string Between(string Source, string Start, string End)
		{
			int start = Source.IndexOf(Start, StringComparison.Ordinal);
			int end = Source.IndexOf(End, start + Start.Length, StringComparison.Ordinal);
			Assert.GreaterOrEqual(start, 0, Start);
			Assert.Greater(end, start, End);
			return Source.Substring(start, end - start);
		}

	}
}
#endif
