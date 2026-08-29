#if TAF_TESTS
using System;
using System.IO;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomPurposeEffectSourceTests
	{
		private static readonly string[] EffectShards = new string[]
		{
			"Growth/KingdomPurposePortfolio.EffectAttemptRecovery.cs",
			"Growth/KingdomPurposePortfolio.EffectDebit.cs",
			"Growth/KingdomPurposePortfolio.EffectDebitEvidence.cs",
			"Growth/KingdomPurposePortfolio.EffectDebitRetirement.cs",
			"Growth/KingdomPurposePortfolio.EffectDriveHelpers.cs",
			"Growth/KingdomPurposePortfolio.EffectGround.cs",
			"Growth/KingdomPurposePortfolio.EffectManualRuntime.cs",
			"Growth/KingdomPurposePortfolio.EffectPreflight.cs",
			"Growth/KingdomPurposePortfolio.EffectProductCensus.cs",
			"Growth/KingdomPurposePortfolio.EffectProductRuntime.cs",
			"Growth/KingdomPurposePortfolio.EffectProductShape.cs",
			"Growth/KingdomPurposePortfolio.EffectProse.cs",
			"Growth/KingdomPurposePortfolio.EffectRecord.cs",
			"Growth/KingdomPurposePortfolio.EffectRetirement.cs",
			"Growth/KingdomPurposePortfolio.EffectRoster.cs",
			"Growth/KingdomPurposePortfolio.EffectRuntime.cs",
			"Growth/KingdomPurposePortfolioRules.CodecLegacy.cs",
			"Growth/KingdomPurposePortfolioRules.EffectEvidence.cs",
			"Growth/KingdomPurposePortfolioRules.EffectStep.cs",
			"Growth/KingdomPurposePortfolioRules.EffectTransaction.cs",
			"Growth/KingdomPurposePortfolioRules.EffectTransactionModels.cs"
		};

		private static string Source(string relative)
		{
			return TestMain.ReadRepositoryText(
				relative.Replace('/', Path.DirectorySeparatorChar));
		}

		private static string Between(string source, string from, string to)
		{
			int start = source.IndexOf(from, StringComparison.Ordinal);
			Assert.Greater(start, -1, from);
			int end = source.IndexOf(to, start + from.Length, StringComparison.Ordinal);
			Assert.Greater(end, start, to);
			return source.Substring(start, end - start);
		}

		private static void Ordered(string source, params string[] terms)
		{
			int cursor = -1;
			for (int i = 0; i < terms.Length; i++)
			{
				int next = source.IndexOf(terms[i], cursor + 1,
					StringComparison.Ordinal);
				Assert.Greater(next, cursor, terms[i]);
				cursor = next;
			}
		}

		[Test]
		public void CurrentWireAddsOneGenericStepAndLegacyReadsWithoutWriting()
		{
			string codec = Source("Growth/KingdomPurposePortfolioRules.Codec.cs");
			StringAssert.Contains("private const int OperationFields = 48;", codec);
			Ordered(Between(codec, "public static string EncodeOperation(",
				"public static bool TryDecodeOperation("),
				"\"2\"", "N(Operation.EffectStep)", "\"purpose-operation\"");
			StringAssert.Contains("f[0] != \"2\" || f[47] != \"purpose-operation\"", codec);
			StringAssert.Contains("!Int(f[46], out int effectStep)", codec);

			string legacy = Source(
				"Growth/KingdomPurposePortfolioRules.CodecLegacy.cs");
			StringAssert.Contains("private const int LegacyOperationFields = 47;", legacy);
			StringAssert.Contains("EffectStep = PurposeEffectExempt", legacy);
			StringAssert.Contains("EncodeLegacyOperation(Operation) == Receipt", legacy);
			StringAssert.Contains("EncodeLegacyPair(Pair) == Receipt", legacy);

			string registry = Source("Growth/KingdomPurposePortfolio.RuntimeRegistry.cs");
			string read = Between(registry, "internal static bool TryReadPortfolioPair(",
				"private static bool TryPublishPortfolioPair(");
			StringAssert.Contains("TryDecodePairAny(encoded, out Pair", read);
			StringAssert.DoesNotContain("SetStringGameState", read);
			StringAssert.DoesNotContain("TryReconcilePortfolioTopology", read);
			string publish = Between(registry, "private static bool TryPublishPortfolioPair(",
				"private static bool TryReplaceDormantPair(");
			Ordered(publish, "Before.LegacyWire", "EncodeLegacyPair(Before)",
				"EncodePair(After)", "current != expected", "SetStringGameState");
			StringAssert.Contains("After.LegacyWire = false;", publish);
		}

		[Test]
		public void CombinedPreflightUsesOnlyAnchoredLocalStoresAndExactArithmetic()
		{
			string preflight = Source("Growth/KingdomPurposePortfolio.EffectPreflight.cs");
			Ordered(preflight, "KingdomMaterialTally combined = row.Materials.Copy();",
				"combined.Add(Context.RawMaterial,",
				"PurposeEffectRawUnits", "StockForExactContainer(Context.Zone, Context.Store)",
				"debit.Cancel()");
			Ordered(preflight, "TryPlanFood(survey, Operation.FoodRequested, row",
				"PlannedFoodTake(row, item.IDIfAssigned)",
				"projected < KingdomPurposePortfolioRules.PurposeEffectCropUnits",
				"room < KingdomPurposePortfolioRules.PurposeEffectStapleUnits");
			string control = Source("Growth/KingdomPurposePortfolio.OperationControl.cs");
			Ordered(control, "KingdomMaster.NewWorkAllowed(system)",
				"TryPreflightCarriedFood(system, Operation", "TryPreflightPurposeEffect(system",
				"TryPlanLocalDebit(Operation");

			string ground = Source("Growth/KingdomPurposePortfolio.EffectGround.cs");
			StringAssert.Contains("ReferenceEquals(exact, Store)", ground);
			StringAssert.Contains("Store.CurrentCell.Objects.Contains(Store)", ground);
			StringAssert.Contains("KingdomMaterials.IsStockpile(Store)", ground);
			StringAssert.Contains("Store.GetIntProperty(\"KingdomLarder\") == 1", ground);
			StringAssert.Contains("TryLoadedLandingCustody(Context.Zone", ground);
		}

		[Test]
		public void EveryPhysicalCallbackHasPriorWitnessAndMeasuredAftermath()
		{
			string debit = Source("Growth/KingdomPurposePortfolio.EffectDebit.cs");
			Ordered(debit, "TryPurposeEffectExpectedDebitAfter(before",
				"before.Digest, afterDigest", "StampPurposeEffectAttempt(Context.Work, witness)",
				"EnsurePurposeEffectDebitReservation(Context, attempt",
				"ObservePurposeEffectDebit(Context, attempt",
				"StampPurposeEffectOffer(Context.Work, encoded)",
				"item.Destroy(null, Silent: true)", "ObservePurposeEffectDebit(Context, attempt",
				"ClassifyEffectDebitAftermath(true, threw");
			string evidence = Source(
				"Growth/KingdomPurposePortfolio.EffectDebitEvidence.cs");
			Ordered(evidence, "roster.Digest != Attempt.BeforeRosterDigest",
				"StampPurposeEffectAttempt(item, Witness)",
				"KingdomPurposeEffectRosterMode.DebitReserved",
				"StampPurposeEffectReady(Context.Work, Witness)");
			Ordered(Between(evidence, "private static bool ObservePurposeEffectDebit(",
				"private static bool DebitCandidateAtFrozenBefore("),
				"ExactPurposeEffectReady(Context.Work, witness)",
				"TryCapturePurposeEffectRoster", "Attempt.BeforeRosterDigest",
				"Attempt.AfterRosterDigest", "DebitCandidateAtFrozenAfter");

			string product = Source(
				"Growth/KingdomPurposePortfolio.EffectProductRuntime.cs");
			Ordered(product, "HeldIn(Context.Store) >= KingdomSurvey.CapacityOf(Context.Store)",
				"OfferPurposeEffectProduct(");
			Ordered(product, "TryCapturePurposeEffectRoster(Context, null",
				"TryPurposeEffectExpectedProductAfter(beforeRoster", "beforeRoster.Digest, afterDigest",
				"StampPurposeEffectAttempt(Context.Work, witness)",
				"Context.Store.Inventory.AddObject(product", "TryObservePurposeEffectProduct(Context",
				"ClassifyEffectProductAftermath(true, threw",
				"RecordReleaseAndClearPurposeEffectProduct(Context");
			StringAssert.Contains("return FaultedEffect(Context.Work", product);

			string census = Source(
				"Growth/KingdomPurposePortfolio.EffectProductCensus.cs");
			StringAssert.Contains("Refined = recorded.Refined", census);
			StringAssert.Contains("census.EvidenceCarrier", census);
			StringAssert.Contains("!attemptPresent && census.EvidenceCarrier != null", census);
		}

		[Test]
		public void ProductsReleaseThroughPerProductCheckpointsBeforeAttemptRetirement()
		{
			string shape = Source("Growth/KingdomPurposePortfolio.EffectProductShape.cs");
			Ordered(shape, "product.RemovePart(\"Stacker\")",
				"product.SetIntProperty(\"NeverStack\", 1)",
				"product.SetStringProperty(PortfolioEffectMarkProperty",
				"product.SetIntProperty(PortfolioEffectIndexProperty");
			StringAssert.Contains("Product.Count != 1", shape);
			StringAssert.Contains("Product.HasPart(\"r_KingdomSeed\")", shape);
			StringAssert.Contains("KingdomOrdinaryFoodAuthority.IsEdible(Product)", shape);

			string identity = Source("Growth/KingdomPurposePortfolioRules.Identity.cs");
			StringAssert.Contains("Evidence.EffectAttempt", identity);
			StringAssert.Contains("Evidence.EffectReady", identity);
			StringAssert.Contains("Evidence.EffectOffer", identity);
			StringAssert.Contains("Evidence.EffectCount", identity);
			StringAssert.Contains("Evidence.EffectMark || Evidence.EffectIndex", identity);
			string output = Source("Growth/KingdomPurposePortfolio.OutputRuntime.cs");
			Ordered(output, "TryRetireCompletedPurposeEffect(Pair",
				"KingdomPurposeOperationPhase.OutputPending");
			StringAssert.Contains("Cargo.SetIntProperty(\"NeverStack\", 1)", output);

			string retirement = Source(
				"Growth/KingdomPurposePortfolio.EffectRetirement.cs");
			Ordered(retirement, "operation.Phase != KingdomPurposeOperationPhase.EffectApplied",
				"ExactPublishedPortfolioPair(Pair)", "CompletedPurposeEffectProductCount",
				"ClearPurposeEffectProducts");
			StringAssert.DoesNotContain("RemoveStringProperty(PortfolioEffectMarkProperty)",
				retirement);
			StringAssert.DoesNotContain("RemoveIntProperty(PortfolioEffectIndexProperty)",
				retirement);

			string recovery = Source(
				"Growth/KingdomPurposePortfolio.EffectAttemptRecovery.cs");
			string record = Between(recovery,
				"private static bool RecordReleaseAndClearPurposeEffectProduct(",
				"private static bool TryReleasePurposeEffectProduct(");
			Ordered(record, "TryObservePurposeEffectProduct(Context", "RecordPurposeEffectProducts(",
				"TryReleasePurposeEffectProduct(Context");
			string release = Between(recovery,
				"private static bool TryReleasePurposeEffectProduct(",
				"private static bool TryRetirePublishedEffectAttempt(");
			Ordered(release, "PurposeEffectProductReleaseStage", "TryCapturePurposeEffectRoster",
				"Attempt.AfterRosterDigest", "RemoveStringProperty(PortfolioEffectMarkProperty)",
				"StampPurposeEffectReady(Context.Work, witness)",
				"RemoveIntProperty(PortfolioEffectIndexProperty)",
				"RemoveIntProperty(\"NeverStack\")", "ClearPurposeEffectReady(Context.Work, witness)",
				"ClearPurposeEffectAttempt(Context.Work, witness)");
		}

		[Test]
		public void FrozenRosterCoversEveryDirectIdentityShapeAndOwnedField()
		{
			string roster = Source("Growth/KingdomPurposePortfolio.EffectRoster.cs");
			Ordered(roster, "new List<GameObject>(Context.Store.Inventory.Objects)",
				"HashSet<string> ids", "Context.Store.Inventory.InventoryContains(item)",
				"result.Rows.Add", "result.Rows.Sort", "PurposeEffectRosterDigest(result.Rows)");
			string row = Between(roster, "private static bool TryPurposeEffectRosterRow(",
				"private static string PurposeEffectPropertyToken(");
			Ordered(row, "Item.IDIfAssigned", "Item.Blueprint", "Count.ToString()",
				"owner=1;listed=1;cell=0", "TryMaterialOf", "IsEdible",
				"HasPart(\"r_KingdomSeed\")", "Physics.Takeable", "HasPart(\"Stacker\")",
				"IsImportant()", "Equipped", "Inventory.Objects.Count",
				"PurposeEffectRosterProperties", "NeverStack", "StockProperty",
				"HasProtectedCargoEvidence");
			StringAssert.Contains("PortfolioEffectReadyProperty",
				Between(roster, "PurposeEffectRosterProperties", "};"));
			StringAssert.Contains("PortfolioEffectOfferProperty",
				Between(roster, "PurposeEffectRosterProperties", "};"));
			StringAssert.Contains("PortfolioEffectAttemptProperty",
				Between(roster, "PurposeEffectRosterProperties", "};"));
		}

		[Test]
		public void ManualLaneHasNoClockPoolPassiveOrOffscreenEscapeHatch()
		{
			string[] forbidden = new string[]
			{
				"TimeTicks", "TimeTick", "ElapsedDays", "AdvanceCheckpoint", "DateTime",
				"Stopwatch", "Environment.TickCount", "KingdomMaterials.Stock(",
				"MilledFoodPerDay", "GrindHarvest", "ConsumeCrop", "RefinedThisPass",
				"WantTurnTick", "TurnTick("
			};
			for (int i = 0; i < EffectShards.Length; i++)
			{
				string source = Source(EffectShards[i]);
				for (int j = 0; j < forbidden.Length; j++)
					StringAssert.DoesNotContain(forbidden[j], source, EffectShards[i]);
				Assert.Less(source.Split('\n').Length, 301,
					EffectShards[i] + " exceeds 300 physical lines");
				for (int j = 0; j < source.Length; j++)
					Assert.LessOrEqual((int)source[j], 127,
						EffectShards[i] + " contains non-ASCII source");
			}
		}

		[Test]
		public void CasCutStatusReadsDurableHighWaterWithoutMutatingEvidence()
		{
			string prose = Source("Growth/KingdomPurposePortfolio.EffectProse.cs");
			string refine = Between(prose, "private static string RefinePurposeEffectState(",
				"private static string HarvestPurposeEffectState(");
			StringAssert.Contains("TryPurposeEffectHighWater(Operation", refine);
			StringAssert.Contains("record.Refined", refine);
			string harvest = Between(prose, "private static string HarvestPurposeEffectState(",
				"private static bool TryPurposeEffectHighWater(");
			StringAssert.Contains("record.Seed", harvest);
			StringAssert.Contains("record.Staple", harvest);
			string helper = prose.Substring(prose.IndexOf(
				"private static bool TryPurposeEffectHighWater(", StringComparison.Ordinal));
			StringAssert.Contains("PurposeEffectEvidenceOnlyOnWorkOrProducts", helper);
			StringAssert.DoesNotContain("SetStringProperty", helper);
			StringAssert.DoesNotContain("SetIntProperty", helper);
			StringAssert.DoesNotContain("RemoveStringProperty", helper);
			StringAssert.DoesNotContain("RemoveIntProperty", helper);
		}

		[Test]
		public void BodyAuthorityAndCargoLandingPathsRemainInTheirOriginalBranches()
		{
			string runtime = Source("Growth/KingdomPurposePortfolio.EffectRuntime.cs");
			Ordered(runtime, "operation.SourceKind == KingdomPurposeKind.Flesh",
				"DriveBodyAuthority(", "EffectIsOwed(operation.SourceKind)",
				"DriveManualPurposeEffect(context");
			string output = Source("Growth/KingdomPurposePortfolio.OutputRuntime.cs");
			StringAssert.Contains("TryLandCarriedFood(System, operation, cargo", output);
			StringAssert.Contains("PurposeLandingStillExact(operation, cargo", output);
		}
	}
}
#endif
