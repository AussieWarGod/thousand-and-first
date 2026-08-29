#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomPurposeFoodLandingSourceTests
	{
		private const string Landing = "Growth/KingdomPurposePortfolio.LandingFood.cs";
		private const string Proof = "Growth/KingdomPurposePortfolio.LandingProof.cs";
		private const string Rules = "Growth/KingdomPurposePortfolioRules.LandingFood.cs";
		private const string Output = "Growth/KingdomPurposePortfolio.OutputRuntime.cs";
		private const string Control = "Growth/KingdomPurposePortfolio.OperationControl.cs";
		private const string Drive = "Growth/KingdomPurposePortfolio.OperationDrive.cs";
		private const string CargoRoot = "Growth/KingdomPurposePortfolio.CargoRoot.cs";
		private const string Cargo = "Growth/KingdomPurposePortfolio.ConstructionCargo.cs";
		private const string CargoRules = "Growth/KingdomPurposePortfolioRules.Identity.cs";
		private const string LegacyCargo = "Growth/KingdomPurpose.03.CargoIdentityAndEscrow.cs";
		private const string StockClass = "Growth/KingdomMaterials.03.StockClassification.cs";
		private const string Stock = "Growth/KingdomMaterials.04.MaterialStock.cs";
		private const string StockGate = "Growth/KingdomMaterials.05.StockpileAndPaymentGates.cs";
		private const string Lease = "Growth/KingdomConstructionInputLeaseAuthority.cs";
		private const string DebitValidation = "Growth/KingdomMaterialDebit.Validation.cs";
		private const string LocalDebit = "Growth/KingdomPurposePortfolio.LocalDebitRuntime.cs";
		private const string InputScan = "Growth/KingdomConstruction.InputPlannerScan.cs";
		private const string InputObservation =
			"Growth/KingdomConstruction.InputObservationRegistry.cs";
		private const string InputReservation =
			"Growth/KingdomConstruction.InputPlannerReservation.cs";
		private const string Funding = "Growth/KingdomPurposePortfolio.Funding.cs";
		private const string InputSource = "Growth/KingdomConstruction.InputDrive.Source.cs";
		private const string InputArrival = "Growth/KingdomConstruction.InputDrive.Arrival.cs";
		private const string InputDebit = "Growth/KingdomConstruction.InputDrive.Debit.cs";
		private const string InputCancellation =
			"Growth/KingdomConstruction.InputDrive.Cancellation.cs";
		private const string InputCancellationSplit =
			"Growth/KingdomConstruction.InputDrive.Cancellation.Split.cs";
		private const string InputClose = "Growth/KingdomConstruction.InputDrive.Close.cs";
		private const string ClearanceGround = "Growth/KingdomMaterials.15.GroundAndWalls.cs";
		private const string ClearanceWork = "Growth/KingdomMaterials.14.ClearanceWork.cs";
		private const string StrikeOrder = "Growth/KingdomMaterials.08.StrikeOrdering.cs";
		private const string StrikeRemoval =
			"Growth/KingdomMaterials.13.StrikeRemovalAndSalvage.cs";
		private const string StrikeGatehouseRemoval =
			"Growth/KingdomMaterials.12b.GatehouseRemovalProof.cs";
		private const string StrikeProtection = "Growth/KingdomMaterials.StrikeProtection.cs";
		private const string BountyRead = "Quests/KingdomBounty.ReadingGround.cs";
		private const string BountyCarry = "Quests/KingdomBounty.WorkAndCarry.cs";
		private const string Input = "Growth/KingdomPurposePortfolio.InputRuntime.cs";
		private const string Record = "Growth/KingdomPurposePortfolio.LandingRecord.cs";
		private const string Attempt = "Growth/KingdomPurposePortfolioRules.LandingAttempt.cs";
		private const string Ground = "Growth/KingdomPurposePortfolio.LandingGround.cs";

		private static readonly string[] LandingShards = new string[]
		{
			Landing, Proof, Record, Ground, Rules, Attempt,
			"Growth/KingdomPurposeFoodLandingAction.cs"
		};

		private static readonly string[] ClockVocabulary = new string[]
		{
			"TimeTicks", "TimeTick", "ElapsedDays", "AdvanceCheckpoint", "MasterOptionTick",
			"LastDrawTick", "LastWorkedTick", "TicksPerDay", "DateTime", "Stopwatch",
			"Environment.TickCount"
		};

		private static string Source(string relative)
		{
			return TestMain.ReadRepositoryText(relative.Replace('/', Path.DirectorySeparatorChar));
		}

		/// <summary>Source with its commentary removed. These shards name the very engine methods
		/// and clock idioms they are forbidden to call, because naming them is how the reasoning is
		/// evidenced, so a sweep over raw text would convict the explanation instead of the code.</summary>
		private static string Code(string text)
		{
			string[] lines = text.Split('\n');
			System.Text.StringBuilder code = new System.Text.StringBuilder();
			for (int i = 0; i < lines.Length; i++)
			{
				int comment = lines[i].IndexOf("//", StringComparison.Ordinal);
				code.Append(comment < 0 ? lines[i] : lines[i].Substring(0, comment));
				code.Append('\n');
			}
			return code.ToString();
		}

		private static void Ordered(string source, params string[] terms)
		{
			int cursor = -1;
			for (int i = 0; i < terms.Length; i++)
			{
				int next = source.IndexOf(terms[i], cursor + 1, StringComparison.Ordinal);
				Assert.Greater(next, cursor, terms[i]);
				cursor = next;
			}
		}

		/// <summary>The source between two markers, so a claim about one method's body cannot be
		/// satisfied by text belonging to its neighbours.</summary>
		private static string Between(string source, string from, string to)
		{
			int start = source.IndexOf(from, StringComparison.Ordinal);
			Assert.Greater(start, -1, from);
			int end = source.IndexOf(to, start + 1, StringComparison.Ordinal);
			Assert.Greater(end, start, to);
			return source.Substring(start, end - start);
		}

		private static int Count(string source, string term)
		{
			int count = 0;
			for (int at = source.IndexOf(term, StringComparison.Ordinal); at >= 0;
				at = source.IndexOf(term, at + 1, StringComparison.Ordinal)) count++;
			return count;
		}

		[Test]
		public void TheLandingShardsReadNoClockAtAll()
		{
			for (int i = 0; i < LandingShards.Length; i++)
			{
				string code = Code(Source(LandingShards[i]));
				for (int j = 0; j < ClockVocabulary.Length; j++)
					StringAssert.DoesNotContain(ClockVocabulary[j], code,
						LandingShards[i] + " acquired a clock; it now owes the ruled freeze+rebase"
						+ " treatment or the MasterOptionTick clamp");
			}
		}

		[Test]
		public void TheLandingShardsOwnNoDurableCheckpointAndNoPauseGateOfTheirOwn()
		{
			for (int i = 0; i < 4; i++)
			{
				string code = Code(Source(LandingShards[i]));
				StringAssert.DoesNotContain("TryPublishOperation(", code);
				StringAssert.DoesNotContain("TryPublishPortfolioPair(", code);
				StringAssert.DoesNotContain("NewWorkAllowed", code);
				StringAssert.DoesNotContain("QuarantinePortfolio(", code);
			}
		}

		[Test]
		public void EveryProductionShardInTheLaneStaysUnderThreeHundredLines()
		{
			string[] files = { Landing, Proof, Record, Ground, Rules, Attempt, Output, Control,
				Drive, CargoRoot, Input, "Growth/KingdomPurposeFoodLandingAction.cs" };
			for (int i = 0; i < files.Length; i++)
			{
				int lines = Source(files[i]).Split('\n').Length;
				Assert.Less(lines, 301, files[i] + " is " + lines + " lines");
			}
		}

		[Test]
		public void LandedNessRidesAMarkedServingRatherThanTheOperationReceipt()
		{
			string proof = Source(Proof);
			string record = Source(Record);
			StringAssert.Contains(
				"private const string PortfolioLandedFoodProperty = \"r_TAF_PurposeLandedFood\";",
				record);
			StringAssert.Contains("private const string PortfolioLandedReceiptProperty"
				+ " = \"r_TAF_PurposeLandedReceipt\";", record);
			StringAssert.Contains("private const string PortfolioLandedCountProperty"
				+ " = \"r_TAF_PurposeLandedCount\";", record);
			StringAssert.Contains("private const string PortfolioLandedAttemptProperty"
				+ " = \"r_TAF_PurposeLandedAttempt\";", record);
			StringAssert.Contains(
				"food.SetStringProperty(PortfolioLandedReceiptProperty, Receipt);", proof);
			StringAssert.Contains("food.SetIntProperty(PortfolioLandedFoodProperty, Prefilter);",
				proof);
			string codec = Source("Growth/KingdomPurposePortfolioRules.Codec.cs");
			StringAssert.Contains("private const int CargoFields = 20;", codec);
			StringAssert.Contains("private const int OperationFields = 48;", codec);
			StringAssert.Contains("private const int PairFields = 31;", codec);
			StringAssert.Contains("private const int LegacyOperationFields = 47;",
				Source("Growth/KingdomPurposePortfolioRules.CodecLegacy.cs"));
			string receipt = Source("Growth/KingdomPurposeOperationReceipt.cs");
			StringAssert.DoesNotContain("Landed", receipt);
		}

		[Test]
		public void TheLandingCallSitsBetweenTheStockpileProofAndTheDeliveredPublish()
		{
			string output = Source(Output);
			Ordered(output, "private static bool DrivePurposeLanding(",
				"ReferenceEquals(cargo.InInventory, destination)",
				"Purpose destination replaced or rejected the exact cargo.",
				"TryLandCarriedFood(System, operation, cargo, destinationZone",
				"KingdomPurposeOperationPhase.Delivered",
				"TryPublishOperation(Pair, next, delivered");
			Assert.AreEqual(1, Count(output, "TryLandCarriedFood("));
		}

		[Test]
		public void TheMarkedCountIsObservedBeforeAnyServingIsCreatedAndTheDeltaBeforeTheReturn()
		{
			string landing = Source(Landing);
			Ordered(landing, "private static bool TryLandCarriedFood(",
				"if (!TryPurposeLarderRoster(survey, DestinationZone, out List<GameObject> larders))",
				"MarkedPurposeFood(larders, receipt, prefilter, blueprint,",
				"TryLandingOutstanding(carried, physical,",
				"TryRecoverCarriedFood(carried, unmarked",
				"AddPurposeFood(survey, larders, Cargo, receipt, prefilter,");
			string proof = Source(Proof);
			Ordered(proof, "private static int AddPurposeFood(",
				"int before = MarkedPurposeFood(Larders, Receipt, Prefilter, Blueprint,",
				"out int unmarked, out _);",
				"int expected = held + 1;",
				"if (!StampPurposeLandingAttempt(Cargo, Receipt, expected))",
				"Aftermath = PlacePurposeServing(Survey, larder, Blueprint, Receipt, Prefilter);",
				"int step = MarkedPurposeFood(Larders, Receipt, Prefilter, Blueprint, out _,",
				"out bool stepExact);",
				"if (Aftermath != KingdomPurposeServingAftermath.Settled || !stepExact",
				"|| step != expected || !PurposeLardersWithinCapacity(Larders)",
				"settled++;",
				"int after = MarkedPurposeFood(Larders, Receipt, Prefilter, Blueprint,",
				"out int unmarkedAfter, out bool exact);",
				"KingdomPurposePortfolioRules.LandingPartitionIsExact(before, unmarked, settled,",
				"after, unmarkedAfter, exact)",
				"Aftermath = KingdomPurposeServingAftermath.Stranded;",
				"int added = after - before;",
				"if (!Survey.SynchronizeReceiptObject(larders[i]))",
				"Aftermath = KingdomPurposeServingAftermath.Stranded;");
			Assert.AreEqual(0, Count(Code(proof), "\t\t\t\t\tSurvey.SynchronizeReceiptObject("),
				"a discarded synchronization result is divergence nobody reads");
			// The count of settled offers is what the increment is owed against; counting offers
			// instead would let a refused one buy a short delta a WAIT could retry over.
			Assert.AreEqual(0, Count(Code(proof), "attempted"),
				"the exact increment is owed against settled offers, never against attempts");
			Assert.AreEqual(0, Count(Code(proof), "LandingDeltaIsSound"),
				"the short-delta envelope was replaced by the exact-partition law");
			// No exception may cross the placement seam: a throw past an out-parameter leaves the
			// caller unable to tell a clean shortfall from a stamped serving loose in the world.
			Assert.AreEqual(0, Count(Code(proof), "throw;"),
				"the aftermath crosses this boundary as a value, never as an exception");
			Assert.AreEqual(0, Count(Code(Source(Landing)), "catch"),
				"the landing transaction no longer catches across the placement seam");
		}

		[Test]
		public void EveryAddIsPreflightedUnstackableAndProvedSettledInTheExactLarder()
		{
			string proof = Source(Proof);
			// Preflight: nothing is offered to the engine that the engine would refuse, so a
			// refusal can never strand a stamped object. Inventory.cs:258-277 returns the object
			// un-added for an untakeable, graveyard, or invalid object.
			Ordered(proof, "private static GameObject ExactPurposeServing(",
				"food.RemovePart(\"Stacker\");", "food.Count != 1", "food.IsInvalid()",
				"food.IsInGraveyard()", "food.Blueprint != Blueprint", "!food.Physics.Takeable",
				"food.SetStringProperty(PortfolioLandedReceiptProperty, Receipt);");
			// NoStack closes the Stacker.cs:137-144 -> :312-315 merge that obliterates the object
			// just added and carries its count away from this operation's marker.
			StringAssert.Contains("NoStack: true", proof);
			Assert.AreEqual(0, Count(Code(proof), "AddObject(food, Silent: true)"),
				"a stackable add lets the engine obliterate the stamped serving");
			// Postcondition: the return value alone proves nothing, so the physical aftermath is
			// what decides, and any divergence is ambiguous rather than a shortfall.
			Ordered(proof, "private static KingdomPurposeServingAftermath PlacePurposeServing(",
				"GameObject food = ExactPurposeServing(Blueprint, Receipt, Prefilter);",
				"if (food == null) return KingdomPurposePortfolioRules.ClassifyServingAftermath(false,",
				"catch { threw = true; }",
				"ClassifyServingAftermath(true, threw,",
				"ReferenceEquals(accepted, food)", "food.IsInvalid()", "food.IsInGraveyard()",
				"ReferenceEquals(food.InInventory, Larder) && food.CurrentCell == null",
				"food.Count == 1", "food.Blueprint == Blueprint",
				"food.Physics != null && food.Physics.Takeable",
				"food.GetIntProperty(Simulation.City.KingdomPorters.StockProperty) == 1,",
				"LandingMarkerIsOurs(Receipt, Prefilter,");
			// Only a serving that reached no owner may be withdrawn; destroying one the engine
			// placed elsewhere would erase the very ambiguity it proves.
			Ordered(proof, "if (aftermath != KingdomPurposeServingAftermath.Settled",
				"&& GameObject.Validate(food) && food.InInventory == null",
				"&& food.CurrentCell == null) food.Obliterate();");
			// Every marked unit already in the larders is held to the same exactness.
			Ordered(proof, "private static bool ExactLandedServing(",
				"Item.Count == 1 && Item.Blueprint == Blueprint",
				"Item.GetIntProperty(Simulation.City.KingdomPorters.StockProperty) == 1",
				"Item.Physics != null && Item.Physics.Takeable",
				"ReferenceEquals(Item.InInventory, Larder) && Item.CurrentCell == null");
			string landing = Source(Landing);
			Ordered(landing, "out KingdomPurposeServingAftermath aftermath);",
				"if (aftermath == KingdomPurposeServingAftermath.Stranded)",
				"A marked purpose serving did not settle inside the exact destination larder.",
				"if (aftermath == KingdomPurposeServingAftermath.Unavailable)",
				"The realm's staple stopped making exact servings after this operation was committed.");
		}

		[Test]
		public void OwnershipAndRetirementAreDecidedOnTheFullReceiptNeverOnTheIndex()
		{
			string proof = Source(Proof);
			Ordered(proof, "private static int MarkedPurposeFood(",
				"KingdomPurposePortfolioRules.LandingMarkerIsOurs(Receipt, Prefilter,",
				"OwnedIntField(item, PortfolioLandedFoodProperty),",
				"OwnedStringField(item, PortfolioLandedReceiptProperty),",
				"if (ExactLandedServing(item, larders[i], Blueprint)) marked += 1;",
				"else Exact = false;",
				"KingdomPurposePortfolioRules.LandingMarkerIsPresent(",
				"OwnedFieldPresent(item, PortfolioLandedFoodProperty),",
				"OwnedFieldPresent(item, PortfolioLandedReceiptProperty))) Exact = false;");
			Assert.AreEqual(1, Count(Code(proof), "marked += 1;"),
				"each proved unit counts as exactly one serving; a marked unit that grew is not"
				+ " a serving this operation landed, and must be judged inexact instead");
			StringAssert.DoesNotContain("!= Key)", Code(proof));
			string rules = Source(Rules);
			StringAssert.Contains("StringComparison.Ordinal", rules);
		}

		[Test]
		public void MarksAreRetiredOnTheWholeMarkThroughFreshCustodyBeforeTheCheckpoint()
		{
			string record = Source(Record);
			// The whole mark, not the receipt text: a malformed, wrong-index or missing-index mark
			// carrying the same receipt survives and is cut on.
			Ordered(record, "private static bool TryRetirePurposeLandingMarks(",
				"|| !TryLoadedLandingCustody(DestinationZone, out IList<GameObject> loaded))",
				"return false;",
				"KingdomPurposePortfolioRules.LandingMarkerIsRetiredReceipt(RetiredReceipt,",
				"Prefilter, OwnedIntField(item, PortfolioLandedFoodProperty),",
				"OwnedStringField(item, PortfolioLandedReceiptProperty),",
				"item.RemoveStringProperty(PortfolioLandedReceiptProperty);",
				"item.RemoveIntProperty(PortfolioLandedFoodProperty);");
			Assert.AreEqual(2, Count(record,
				"RemoveStringProperty(PortfolioLandedReceiptProperty)"),
				"a stamp is cleared in exactly two places: a serving's mark at retirement, and the"
				+ " credited cargo's own record as it leaves the operation for good");
			Assert.AreEqual(0, Count(Code(record), "DestinationLarders("),
				"cached larder children would miss the serving already carried out of a larder");
			// Retirement runs inside the landing, before its own checkpoint, while the servings are
			// still provably in the measured larders; and it is reproved before the caller returns.
			string landing = Source(Landing);
			Ordered(landing, "private static bool CompletePurposeLanding(",
				"RecordPurposeLanded(Cargo, Receipt, Carried, Landed);",
				"TryPurposeLandedRecord(Cargo, Receipt, Carried, out int written)",
				"written != Landed",
				"The durable landing record did not take the measured landing.",
				"if (Landed != Carried)",
				"The destination larders took only part of the exact carried provision.",
				"if (!PurposeLardersWithinCapacity(Larders))",
				"A measured destination larder holds more than it can hold.",
				"if (!PurposeLandingStillExact(Operation, Cargo, out string moved))",
				"if (!TryRetirePurposeLandingMarks(DestinationZone, Receipt, Prefilter))",
				"if (MarkedPurposeFood(Larders, Receipt, Prefilter, Blueprint, out _, out bool exact) != 0",
				"TryPurposeCustodyStrays(Larders, DestinationZone, Cargo, Receipt, Prefilter,",
				"|| !SamePurposeLarderRoster(Larders, Survey))",
				"Evidence under this operation's landing fields survived its own retirement.");
			string output = Source(Output);
			Ordered(output, "TryLandCarriedFood(System, operation, cargo, destinationZone",
				"KingdomPurposeOperationPhase.Delivered",
				"TryPublishOperation(Pair, next, delivered");
			string rules = Source(Rules);
			Ordered(rules, "public static bool LandingMarkerIsRetiredReceipt(",
				"return LandingMarkerIsOurs(RetiredReceipt, Prefilter, MarkPresent, MarkPrefilter,");
			Assert.AreEqual(0, Count(Code(rules), "StartsWith(Scope"),
				"a prefix rule would retire any crafted receipt in the pair's namespace");
		}

		[Test]
		public void EveryPointThatLeavesADeliveredOperationRetiresItsMarksAndItsRoot()
		{
			// Retirement is one act with two halves, and it must happen wherever a pair stops
			// naming a delivered operation. Both credit paths reach it; so does the return start,
			// which drops the delivered bootstrap operation that no credit path ever reaches.
			string drive = Source(Drive);
			Ordered(drive, "private static bool AcceptPortfolioCredit(",
				"if (!ExactPublishedPortfolioPair(Pair))",
				"The purpose-pair register changed before this credit could retire its cargo.",
				"if (!TryRetireCreditedPurposeCargo(Pair.Operation))",
				"This delivered landing could not be retired from the destination's custody; nothing was released.",
				"TryPublishPortfolioPair(Pair, activating, out Failure)");
			Ordered(drive, "if (!TryRetireCreditedPurposeCargo(Pair.Operation))",
				"next.Phase = KingdomPurposePairPhase.Active;",
				"TryPublishPortfolioPair(Pair, next, out Failure)");
			Assert.AreEqual(2, Count(drive, "if (!ExactPublishedPortfolioPair(Pair))"),
				"both credit paths reprove the register immediately before their cleanup");
			Ordered(drive, "private static bool ExactPublishedPortfolioPair(",
				"KingdomPurposePortfolioRules.EncodePair(Pair);",
				"The.Game.GetStringGameState(PortfolioStateKey, \"\") == expected;");
			Assert.AreEqual(2, Count(drive, "if (!TryRetireCreditedPurposeCargo(Pair.Operation))"),
				"both credit paths retire, and both before their publish so a crash simply retries");
			// A retirement that cannot prove itself blocks the release: clearing witnesses and the
			// root while legacy marks stood would leave marks whose operation no longer exists.
			Ordered(drive, "private static bool TryRetireCreditedPurposeCargo(",
				"Operation.Phase != KingdomPurposeOperationPhase.Delivered) return true;",
				"if (!TryRetireDeliveredPurposeLanding(Operation)) return false;",
				"KingdomPurposePortfolioRules.TryDecodeCargo(Operation.OutputCargoReceipt,",
				"RemovePurposeCargoRoots(cargo);");
			// Order is load-bearing. The credited cargo stands on the destination ground, so its
			// own record is classified and cleared before any global absence proof; taking that
			// proof first would refuse every lawful delivery instead of only the malformed ones.
			Ordered(drive, "private static bool TryRetireDeliveredPurposeLanding(",
				"TryPurposeLandingMark(Operation, out string receipt, out int prefilter)",
				"TryPurposeZone(Operation.DestinationZoneId, out Zone destination)",
				"bool rooted = TryRootedPurposeCargoExact(Operation, out GameObject cargo);",
				"GameObject allowed = rooted ? cargo : null;",
				"if (rooted && !PurposeCargoRecordIsRetirable(cargo, receipt, carried)) return false;",
				"if (!OnlyRetirableLandingEvidence(destination, allowed, receipt, prefilter)",
				"|| !TryRetirePurposeLandingMarks(destination, receipt, prefilter)",
				"|| !NoPurposeLandingEvidenceRemains(destination, allowed)) return false;",
				"if (rooted && !TryClearPurposeLandingWitnesses(cargo, receipt, carried)) return false;",
				"return NoPurposeLandingEvidenceRemains(destination, null);");
			// Nothing is mutated before every piece of owned evidence has been read and allowed,
			// so a refused retirement leaves every serving mark and the root exactly as they were.
			Ordered(Source(Ground), "private static bool OnlyRetirableLandingEvidence(",
				"if (!AnyPurposeLandingField(item) || ReferenceEquals(item, Allowed)) continue;",
				"if (OwnedFieldPresent(item, PortfolioLandedCountProperty)",
				"|| OwnedFieldPresent(item, PortfolioLandedAttemptProperty)",
				"|| OwnedFieldPresent(item, PortfolioLandedFaultProperty)",
				"|| !WearsPurposeLandingMark(item, Receipt, Prefilter)) return false;");
			// The absence reproof covers all five owned names in both tables: proving only
			// well-formed marks gone is no proof, because retirement preserves the malformed ones.
			Ordered(Source(Ground), "private static bool NoPurposeLandingEvidenceRemains(",
				"if (!TryLoadedLandingCustody(DestinationZone, out IList<GameObject> loaded))",
				"&& AnyPurposeLandingField(loaded[i])) return false;");
			Ordered(Source(Ground), "private static bool AnyPurposeLandingField(",
				"return OwnedFieldPresent(Item, PortfolioLandedFoodProperty)",
				"|| OwnedFieldPresent(Item, PortfolioLandedReceiptProperty)",
				"|| OwnedFieldPresent(Item, PortfolioLandedCountProperty)",
				"|| OwnedFieldPresent(Item, PortfolioLandedAttemptProperty)",
				"|| OwnedFieldPresent(Item, PortfolioLandedFaultProperty);");
			// The bootstrap cycle: the second shell consumes the bootstrap cargo by construction,
			// so its landing marks would otherwise stand in the destination larders forever and
			// quarantine the next landing there as evidence of an owner nobody can name.
			string control = Source(Control);
			Ordered(control, "private static bool TryStartPortfolioOperation(",
				"next.Revision++;", "if (!ExactPublishedPortfolioPair(Pair))",
				"if (!TryRetireCreditedPurposeCargo(Pair.Operation))",
				"The delivered bootstrap landing could not be retired from the destination's custody; nothing was released.",
				"TryPublishPortfolioPair(Pair, next, out Failure)");
			Assert.AreEqual(1,
				Count(control, "if (!TryRetireCreditedPurposeCargo(Pair.Operation))"));
			// Retiring before every publish is what makes a refused CAS harmless: the removal is
			// idempotent, the delivered cargo is still found by the zone scan every credit path
			// uses rather than through the root, and retired servings become ordinary larder food
			// that no other operation can ever claim as its own marked landing.
			Ordered(drive, "FindExactKnown(zone, Pair.Operation.OutputCargoId, out GameObject cargo)",
				"if (!TryRetireCreditedPurposeCargo(Pair.Operation))");
			Assert.AreEqual(0, Count(Between(Code(drive),
					"private static bool AcceptPortfolioCredit(",
					"private static bool ExactPublishedPortfolioPair("), "TryRootedPurposeCargo"),
				"the credit paths locate their cargo by zone scan, so retiring the root early"
				+ " cannot strand a retry after a refused pair CAS");
		}

		[Test]
		public void TheLandingIdentityIsCanonicalAndSharedWithTheRootedCargoKey()
		{
			string rules = Source(Rules);
			Ordered(rules, "public static bool TryLandingReceipt(", "TryCanonicalKey(",
				"public static bool TryCargoRootBody(", "TryCanonicalKey(",
				"private static bool TryCanonicalKey(",
				"if (!Id(PairId) || PairEpoch < 1L) return false;",
				"if (!WithOperation)",
				"EncodeFields(new string[] { Tag, PairId, N(PairEpoch) })",
				"return Id(OperationId) && (Key = EncodeFields(");
			Assert.AreEqual(0, Count(Code(rules), "\":\" + PairId"),
				"a delimiter join is not injective while Id() admits ':'");
			string root = Source(CargoRoot);
			Assert.AreEqual(2, Count(root, "KingdomPurposePortfolioRules.TryCargoRootBody("),
				"both root-key overloads must share the one canonical encoder");
			// The candidate under a colliding legacy key must fully reprove before it is seized,
			// or the migration installs another operation's object under this one's name.
			Ordered(root, "private static bool TryRootedPurposeCargo(",
				"string legacy = PortfolioLegacyCargoRootKey(Operation.PairId, Operation.PairEpoch,",
				"The.Game.ObjectGameState.ContainsKey(key)",
				"!ExactRootedPurposeCargo(Operation, value, out Cargo)) return false;",
				"The.Game.ObjectGameState.Remove(legacy);",
				"The.Game.ObjectGameState[key] = Cargo;");
			Ordered(root, "private static bool ExactRootedPurposeCargo(",
				"Cargo.IDIfAssigned == Operation.OutputCargoId",
				"ExactPortfolioCargoIdentity(Cargo, Operation.OutputCargoReceipt)) return true;");
			// Status reads through the non-migrating lookup, so rendering cannot mutate the roots.
			Ordered(root, "private static bool TryRootedPurposeCargoExact(",
				"The.Game.ObjectGameState.TryGetValue(PurposeCargoRootKey(Operation),");
			Assert.AreEqual(0, Count(Between(Code(root),
					"private static bool TryRootedPurposeCargoExact(",
					"private static bool ExactRootedPurposeCargo("), "ObjectGameState.Remove"),
				"the read-only lookup must not migrate a root key");
			StringAssert.Contains("internal static void RemovePurposeCargoRoots(", root);
			string landing = Source(Landing);
			Ordered(landing, "private static bool TryPurposeLandingMark(",
				"TryLandingReceipt(Operation.PairId,",
				"KingdomPurposePortfolioRules.LandingIndex(");
			Assert.AreEqual(0, Count(Code(landing), "prefilter) != 0"),
				"a lawful receipt whose cheap index hashes to zero must not be refused");
		}

		[Test]
		public void PurposeCargoIsProtectedFromOrdinaryMaterialButExactFundingCanAdmitIt()
		{
			string output = Source(Output);
			Ordered(output, "private static bool TryPreparePurposeCargo(",
				"Cargo.SetIntProperty(PortfolioCargoSchemaProperty, PortfolioCargoSchema);",
				"return ExactPortfolioCargoIdentity(Cargo, encoded)");
			string identity = Source(Cargo);
			Ordered(identity, "private static bool ExactPortfolioCargoIdentity(",
				"!Cargo.HasPart(\"Stacker\")",
				"Cargo.HasIntProperty(PortfolioCargoSchemaProperty)",
				"Cargo.HasStringProperty(PortfolioCargoSchemaProperty), true)",
				"Cargo.GetIntProperty(PortfolioCargoSchemaProperty)");
			StringAssert.Contains("Cargo.IDIfAssigned == receipt.ObjectId", identity);
			StringAssert.DoesNotContain("Cargo.ID == receipt.ObjectId", identity);
			StringAssert.Contains("store.IDIfAssigned == ExpectedStoreId", identity);
			StringAssert.Contains("ExactOwned(Cargo, store)", identity);
			StringAssert.DoesNotContain("GetIntProperty(\"NeverStack\")", identity);
			string portfolioShape = Between(identity,
				"private static bool ExactPortfolioCargoEvidenceShape(",
				"internal static bool HasProtectedCargoEvidence(");
			string[] foreignPortfolioFields =
			{
				"CargoSchemaProperty", "CargoKeyProperty", "CargoManifestProperty",
				"CargoConsignmentProperty", "CargoOriginProperty", "CargoDestinationProperty",
				"PortfolioLandedFoodProperty", "PortfolioLandedAttemptProperty",
				"PortfolioLandedFaultProperty"
			};
			for (int i = 0; i < foreignPortfolioFields.Length; i++)
				StringAssert.Contains("CargoFieldPresent(Cargo, " + foreignPortfolioFields[i] + ")",
					portfolioShape, foreignPortfolioFields[i]);
			Ordered(portfolioShape, "TryLandingReceipt(Receipt.PairId,",
				"TryPurposeLandedRecord(Cargo, landingReceipt, Receipt.CarriedFood");
			Ordered(identity, "internal static bool HasProtectedCargoEvidence(",
				"CargoFieldPresent(Cargo, CargoSchemaProperty)",
				"CargoFieldPresent(Cargo, CargoKeyProperty)",
				"CargoFieldPresent(Cargo, CargoManifestProperty)",
				"CargoFieldPresent(Cargo, CargoConsignmentProperty)",
				"CargoFieldPresent(Cargo, CargoOriginProperty)",
				"CargoFieldPresent(Cargo, CargoDestinationProperty)",
				"CargoFieldPresent(Cargo, PortfolioCargoSchemaProperty)",
				"CargoFieldPresent(Cargo, PortfolioCargoReceiptProperty)",
				"CargoFieldPresent(Cargo, PortfolioCargoKeyProperty)",
				"CargoFieldPresent(Cargo, PortfolioCargoFoodProperty)",
				"CargoFieldPresent(Cargo, PortfolioLandedFaultProperty)");
			StringAssert.Contains("cargo.SetIntProperty(CargoSchemaProperty, CargoSchema);",
				Source(LegacyCargo));
			string legacyIdentity = Source(LegacyCargo);
			Ordered(legacyIdentity, "private static bool ExactCargo(",
				"CargoFieldPresent(Cargo, PortfolioCargoSchemaProperty)",
				"CargoFieldPresent(Cargo, PortfolioLandedFaultProperty)",
				"Cargo.HasIntProperty(CargoSchemaProperty)",
				"Cargo.HasStringProperty(CargoSchemaProperty), true)",
				"Cargo.HasIntProperty(CargoKeyProperty)",
				"Cargo.HasStringProperty(CargoKeyProperty), false)");

			string rules = Source(CargoRules);
			Ordered(rules, "internal static bool PurposeCargoIsProtected(",
				"return Evidence.LegacySchema || Evidence.LegacyKey || Evidence.LegacyManifest",
				"|| Evidence.LandedAttempt || Evidence.LandedFault;");
			Ordered(Source(StockClass), "internal static bool TryOrdinaryMaterialOf(",
				"!KingdomPurpose.HasProtectedCargoEvidence(Object)",
				"KingdomOrdinaryCustody.TryProveEmpty(Object, out _)", "TryMaterialOf(Object");
			StringAssert.Contains("!KingdomPurpose.HasProtectedCargoEvidence(item)",
				Source(Lease));
			StringAssert.Contains("TryOrdinaryMaterialOf(item", Source(Stock));
			StringAssert.Contains("TryOrdinaryMaterialOf(held", Source(StockGate));
			StringAssert.Contains("KingdomMaterials.TryOrdinaryMaterialOf(Item",
				Source(DebitValidation));
			StringAssert.Contains("KingdomMaterials.TryOrdinaryMaterialOf(item",
				Source(LocalDebit));
			StringAssert.Contains("KingdomMaterials.TryOrdinaryMaterialOf(held",
				Source(BountyRead));
			StringAssert.Contains("KingdomMaterials.TryOrdinaryMaterialOf(candidate",
				Source(BountyCarry));

			// The durable observation shard classifies the physical item; the planner shard then
			// admits protected cargo only when this exact frozen commitment names it once.
			Ordered(Source(InputObservation),
				"bool valid = GameObject.Validate(item), protectedCargo = valid",
				"&& KingdomPurpose.HasProtectedCargoEvidence(item)",
				"|| !protectedCargo && item.GetIntProperty(\"NeverStack\") != 0",
				"|| !TryInputClassification(item");
			string scan = Source(InputScan);
			Ordered(scan, "private static bool ScanInputMaterials(",
				"|| line.ProtectedCargo && !RequiredPurposeCargo(job,",
				"requiredObjectIds, line.SourceObjectId)) continue;");
			Ordered(scan, "private static bool RequiredPurposeCargo(",
				"KingdomPurpose.RequiredFundingObjectsMatch(job, requiredObjectIds)",
				"if (requiredObjectIds[i] == objectId) count++;",
				"return count == 1;");
			string reservation = Source(InputReservation);
			Ordered(reservation, "TryScanInputCandidates(System, zones, leases, Job,",
				"RequiredObjectIds", "TryPlanWithRequiredObjects(Job.Id");
			Ordered(reservation, "private static bool ValidRoutedInputRequest(",
				"KingdomConstructionInputRules.TryRequiredObjectIds(",
				"KingdomPurpose.RequiredFundingObjectsMatch(job, required)");
			string funding = Source(Funding);
			Ordered(funding, "internal static bool ExactRequiredFundingItem(",
				"if (!RequiresExactFunding(Job)) return false;",
				"ExactPortfolioCargoIdentity(Item, commitment.ReciprocalCargoReceipt)");
			Ordered(funding, "internal static bool ExactProtectedFundingAuthorization(",
				"RequiredFundingObjectsMatch(Job, RequiredObjectIds)",
				"if (RequiredObjectIds[i] == id) matches++;",
				"matches == 1 && ExactRequiredFundingItem(Job, Item)");
			StringAssert.Contains("RoutedInputItemAuthorized(job, receipt, item)",
				Source(InputSource));
			Ordered(Source(InputArrival), "private static bool ExactInputCargo(",
				"RoutedInputItemAuthorized(job, receipt, exact)",
				"internal static bool RoutedInputItemAuthorized(",
				"KingdomPurpose.HasProtectedCargoEvidence(item)",
				"receipt.RequiresObject(item.IDIfAssigned)",
				"ExactProtectedFundingAuthorization(job");
			StringAssert.Contains("ExactInputCargo(target, carrier, job, receipt, cargo",
				Source(InputDebit));
			Ordered(Source(InputCancellationSplit), "private static bool RestoreCancelledSplit(",
				"KingdomPurpose.HasProtectedCargoEvidence(item)",
				"ExactRoutedSplitRemainder(zone, holder, job, receipt, source, remainder)",
				"remainder.Obliterate");
			StringAssert.Contains("KingdomPurpose.HasProtectedCargoEvidence(remainder)",
				Source("Growth/KingdomConstruction.InputDrive.SourcePhysical.cs"));
			string close = Source(InputClose);
			StringAssert.DoesNotContain("CargoSchemaProperty", close);
			StringAssert.DoesNotContain("PortfolioCargoSchemaProperty", close);
			StringAssert.DoesNotContain("PortfolioCargoReceiptProperty", close);
		}

		[Test]
		public void ClearanceAndStrikeNeverDestroyPurposeCargoOrOccupiedContainers()
		{
			Ordered(Source(ClearanceGround), "public static bool IsProtected(",
				"KingdomPurpose.HasProtectedCargoEvidence(Object)",
				"KingdomOrdinaryCustody.TryProveEmpty(Object, out _)", "TryClassify(Object");
			Ordered(Source(ClearanceWork), "List<GameObject> standing",
				"IsProtected(item, out string reason)", "TryClassify(item",
				"KingdomOrdinaryCustody.TryProveEmpty(item, out _)", "item.Obliterate");
			string protection = Source(StrikeProtection);
			Ordered(protection, "private static bool StrikeObjectUnencumbered(",
				"KingdomPurpose.HasProtectedCargoEvidence(target)",
				"KingdomOrdinaryCustody.TryProveEmpty(target, out _)");
			Ordered(Source(StrikeOrder), "StrikeTargetsUnencumbered(Building, Z, intent",
				"TryEncodeStrikeIntent(intent");
			// The gatehouse removal proof shard carries two of the four unencumbered proofs
			// since it was split out of the strike-removal shard; the law counts both.
			Assert.AreEqual(4, Count(Source(StrikeRemoval), "StrikeObjectUnencumbered(")
				+ Count(Source(StrikeGatehouseRemoval), "StrikeObjectUnencumbered("));
		}

		[Test]
		public void RawMaterialClassifierHasOnlyReviewedIdentityAndPlanningCallers()
		{
			Dictionary<string, int> expected = new Dictionary<string, int>
			{
				{ "Growth/KingdomMaterials.03.StockClassification.cs", 2 },
				{ "Growth/KingdomPurpose.03.CargoIdentityAndEscrow.cs", 1 },
				{ "Growth/KingdomPurposePortfolio.ConstructionCargo.cs", 1 },
				{ "Growth/KingdomPurposePortfolio.EffectDebitEvidence.cs", 2 },
				{ "Growth/KingdomPurposePortfolio.EffectRoster.cs", 1 },
				{ "Growth/KingdomConstruction.InputPlannerScan.cs", 1 }
			};
			Dictionary<string, int> actual = new Dictionary<string, int>();
			foreach (string path in Directory.GetFiles(TestMain.RepositoryRoot, "*.cs",
				SearchOption.AllDirectories))
			{
				string relative = path.Substring(TestMain.RepositoryRoot.Length)
					.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
					.Replace(Path.DirectorySeparatorChar, '/');
				if (relative.StartsWith("DevTests/", StringComparison.Ordinal)
					|| relative.Contains("/bin/") || relative.Contains("/obj/")) continue;
				int count = Count(Code(File.ReadAllText(path)), "TryMaterialOf(");
				if (count > 0) actual.Add(relative, count);
			}
			Assert.AreEqual(expected.Count, actual.Count,
				"unreviewed raw material classifier caller: " + string.Join(", ", actual.Keys));
			foreach (KeyValuePair<string, int> row in expected)
			{
				Assert.IsTrue(actual.ContainsKey(row.Key), row.Key);
				Assert.AreEqual(row.Value, actual[row.Key], row.Key);
			}
		}

		[Test]
		public void ConservationIsAgainstDurableProgressRatherThanASurvivingCount()
		{
			string landing = Source(Landing);
			Ordered(landing,
				"if (!TryPurposeLandedRecord(Cargo, receipt, carried, out int recorded))",
				"The landing cargo carries a durable record this operation cannot claim.",
				"bool reconciled = pending == KingdomPurposeLandingAttemptState.Settled;",
				"if (!TryPurposeCustodyStrays(larders, DestinationZone, Cargo, receipt, prefilter,",
				"carried, reconciled, out int strays))",
				"The destination's loaded custody could not be proved complete.",
				"KingdomPurposePortfolioRules.ClassifyLandingRecord(physical, recorded, strays)",
				"if (state != KingdomPurposeLandingRecordState.Intact",
				"&& state != KingdomPurposeLandingRecordState.Consumed)",
				"A serving wearing this operation's exact receipt is outside the destination larders.",
				"TryLandingOutstanding(carried, physical, recorded,",
				"More provision wears this operation's exact receipt than it ever carried.",
				"TryRecoverCarriedFood(carried, unmarked",
				"RecordPurposeLanded(Cargo, receipt, carried, progress);",
				"if (outstanding <= 0)",
				"landed = progress + AddPurposeFood(");
			Assert.AreEqual(0, Count(Code(landing), "landed = physical +"),
				"measuring the total against the surviving physical count re-mints eaten servings");
			// A foreign or half-bound record must never read as zero and be overwritten, and every
			// owned field name is checked in BOTH type tables: a wrong-typed or dual-typed value
			// standing on one of these names is interference, never absence.
			string record = Source(Record);
			Ordered(record, "private static bool TryPurposeLandedRecord(",
				"OwnedFieldPresent(Cargo, PortfolioLandedReceiptProperty),",
				"OwnedStringField(Cargo, PortfolioLandedReceiptProperty)",
				"OwnedFieldPresent(Cargo, PortfolioLandedCountProperty),",
				"OwnedIntField(Cargo, PortfolioLandedCountProperty)",
				"? Cargo.GetIntProperty(PortfolioLandedCountProperty)",
				": (OwnedFieldPresent(Cargo, PortfolioLandedCountProperty) ? -1 : 0),");
			Ordered(record, "private static bool OwnedFieldPresent(",
				"return Item.HasStringProperty(Name) || Item.HasIntProperty(Name);");
			Ordered(record, "private static bool OwnedStringField(",
				"return Item.HasStringProperty(Name) && !Item.HasIntProperty(Name);");
			Ordered(record, "private static bool OwnedIntField(",
				"return Item.HasIntProperty(Name) && !Item.HasStringProperty(Name);");
			// No landing shard may decide presence from a value or from one type table alone.
			string[] owned = { Landing, Proof, Record, Ground };
			for (int i = 0; i < owned.Length; i++)
			{
				string body = Between(Code(Source(owned[i])), "namespace ThousandAndFirst", "\n}");
				Assert.AreEqual(owned[i] == Record ? 6 : 0,
					Count(body, "HasStringProperty(") + Count(body, "HasIntProperty("),
					owned[i] + " must read presence through the owned-field helpers");
			}
			Ordered(record, "private static void RecordPurposeLanded(",
				"!TryPurposeLandedRecord(Cargo, Receipt, Carried, out int recorded)",
				"Progress <= recorded) return;",
				"Cargo.SetIntProperty(PortfolioLandedCountProperty, Progress);",
				"Cargo.SetStringProperty(PortfolioLandedReceiptProperty, Receipt);");
			Assert.AreEqual(1, Count(record, "SetIntProperty(PortfolioLandedCountProperty"),
				"the record is written in exactly one place and only ever upward");
		}

		[Test]
		public void ProgressRisesOnlyPastEveryCutAndNeverOnAnAmbiguousAftermath()
		{
			// Raising the record before the aftermath branches would let a rejected, moved,
			// mutated or obliterated callback lift the high-water ahead of the quarantine publish.
			// A refused quarantine CAS would then leave the next pass reading that lift as
			// consumption, owing nothing, and publishing Delivered over provision never kept.
			string landing = Source(Landing);
			Ordered(landing, "landed = progress + AddPurposeFood(",
				"if (aftermath == KingdomPurposeServingAftermath.Stranded)",
				"A marked purpose serving did not settle inside the exact destination larder.",
				"if (aftermath == KingdomPurposeServingAftermath.Unavailable)",
				"The realm's staple stopped making exact servings after this operation was committed.",
				"The durable landing record changed under the provision callbacks.",
				"TryRevalidateLandingGround(survey, larders, DestinationZone, Cargo, receipt,",
				"return CompletePurposeLanding(Operation, survey, larders, Cargo, DestinationZone,");
			Assert.AreEqual(1, Count(landing, "RecordPurposeLanded(Cargo, receipt, carried,"),
				"the pre-add record write happens once, past every cut of its pass");
			Assert.AreEqual(1, Count(landing, "RecordPurposeLanded(Cargo, Receipt, Carried,"),
				"the final record write happens once, inside the completion seam");
			Assert.AreEqual(0, Count(Between(Code(landing),
					"landed = progress + AddPurposeFood(",
					"if (aftermath == KingdomPurposeServingAftermath.Stranded)"),
				"RecordPurposeLanded"),
				"no write may sit between the callbacks and the aftermath that judges them");
			// The record is snapshotted after the pre-add write and reproved unchanged afterwards,
			// so a callback can neither remove nor raise it behind the transaction's back.
			Ordered(landing, "out int baseline)", "baseline != progress",
				"The durable landing record did not take this operation's proved progress.",
				"out int carriedOver)", "carriedOver != baseline",
				"The durable landing record changed under the provision callbacks.");
		}

		[Test]
		public void TheStrayScanWalksTheWholeLoadedCustodyAndRefusesAnIncompleteIndex()
		{
			// One inventory level is not custody: an inventory callback is arbitrary engine code
			// and can nest a serving inside a container inside an actor. The survey's own proved
			// recursive index is the scan, and an index that could not be completed is refused
			// rather than reported clean, because unscanned custody is unknown ownership.
			string ground = Source(Ground);
			Ordered(ground, "private static bool TryPurposeCustodyStrays(",
				"if (!TryLoadedLandingCustody(DestinationZone, out IList<GameObject> loaded))",
				"bool marked = OwnedFieldPresent(item, PortfolioLandedFoodProperty)",
				"|| OwnedFieldPresent(item, PortfolioLandedReceiptProperty);",
				"bool held = OwnedFieldPresent(item, PortfolioLandedCountProperty)",
				"|| OwnedFieldPresent(item, PortfolioLandedAttemptProperty)",
				"|| OwnedFieldPresent(item, PortfolioLandedFaultProperty);",
				"if (!marked && !held) continue;",
				"if (ReferenceEquals(item, Cargo))",
				"if (!OwnedFieldPresent(item, PortfolioLandedFoodProperty)",
				"&& TryPurposeLandedRecord(item, Receipt, Carried, out _)) continue;",
				"if (held || !IsMeasuredLarder(Larders, item.InInventory)",
				"|| !WearsPurposeLandingMark(item, Receipt, Prefilter)) Strays++;");
			// The walk is taken fresh from the current zone roots, never read from the survey's
			// cached index: that index is maintained by observations the callback never made, and
			// would bless exactly the nested clone this scan exists to find.
			// An index that cannot be read is incomplete, never empty: a null root list, an
			// inventory that reports itself present but hands back no list, and an entry that
			// cannot be inspected are all custody the scan did not see.
			Ordered(ground, "private static bool TryLoadedLandingCustody(",
				"List<GameObject> roots = DestinationZone.GetObjects();",
				"if (roots == null) return false;",
				"if (!GameObject.Validate(roots[i]) || roots[i].CurrentZone != DestinationZone)",
				"return false;",
				"if (!GameObject.Validate(item) || !seen.Add(item)",
				"|| walked.Count >= MaxLandingCustodyObjects) return false;",
				"if (item.Inventory == null) continue;",
				"if (item.Inventory.Objects == null) return false;",
				"for (int i = 0; i < item.Inventory.Objects.Count; i++)",
				"pending.Add(item.Inventory.Objects[i]);");
			Assert.AreEqual(1, Count(Between(Code(ground),
					"private static bool TryLoadedLandingCustody(",
					"Loaded = walked;"), "continue;"),
				"only an inventory-less leaf ends a branch; nothing else may be skipped");
			Assert.AreEqual(0, Count(Code(ground), "ActiveFor("),
				"a cached index cannot bless the absence of a mark it never observed");
			Assert.AreEqual(0, Count(Code(ground), "TryLoaded("),
				"the survey's own index is not a fresh walk");
			// A mark whose immediate owner is not one of the measured larders is a stray, so a
			// serving nested inside a container inside a larder is caught as surely as one carried
			// out of the settlement.
			Ordered(ground, "private static bool IsMeasuredLarder(",
				"if (ReferenceEquals(Larders[i], Candidate)) return true;");
			// Every measured larder is reproved and resynchronised after the callbacks, and the
			// frozen destination store is reproved against the operation's own identity.
			Ordered(ground, "private static bool TryRevalidateLandingGround(",
				"if (!SamePurposeLarderRoster(Larders, Survey))",
				"The destination's measured larder roster changed under the provision callbacks.",
				"if (!ExactMeasuredLarder(Larders[i], DestinationZone))",
				"A measured destination larder is no longer a dedicated larder on this ground.",
				"if (!Survey.SynchronizeReceiptObject(Larders[i]))",
				"A measured destination larder refused to resynchronise after the provision callbacks.",
				"TryPurposeCustodyStrays(Larders, DestinationZone, Cargo, Receipt, Prefilter,",
				"return strays == 0");
			// The roster is injective: duplicates are refused at the snapshot and the comparison
			// runs both ways, so a callback cannot rewrite [A,B] into [A,A] and pass on count.
			Ordered(ground, "private static bool TryPurposeLarderRoster(",
				"if (!ExactMeasuredLarder(Survey.Larders[i], DestinationZone)",
				"|| IsMeasuredLarder(Roster, Survey.Larders[i])) return false;",
				"Roster.Add(Survey.Larders[i]);");
			Ordered(ground, "private static bool SamePurposeLarderRoster(",
				"Survey.Larders.Count != Roster.Count",
				"if (!IsMeasuredLarder(Roster, Survey.Larders[i])) return false;",
				"if (ReferenceEquals(Survey.Larders[i], Survey.Larders[j])) return false;",
				"if (!IsMeasuredLarder(Survey.Larders, Roster[i])) return false;");
			// The cargo whitelist is the record and nothing else: an attempt or fault standing
			// beside it is an offer this pass never reconciled.
			Ordered(ground, "if (ReferenceEquals(item, Cargo))",
				"if (!OwnedFieldPresent(item, PortfolioLandedFoodProperty)",
				"&& (Attempted || !OwnedFieldPresent(item, PortfolioLandedAttemptProperty))",
				"&& !OwnedFieldPresent(item, PortfolioLandedFaultProperty)",
				"&& TryPurposeLandedRecord(item, Receipt, Carried, out _)) continue;");
			Ordered(ground, "private static bool PurposeLardersWithinCapacity(",
				"KingdomSurvey.HeldIn(Larders[i]) > KingdomSurvey.CapacityOf(Larders[i])");
			Ordered(Source(Proof), "while (remaining > 0",
				"&& KingdomSurvey.HeldIn(larder) < KingdomSurvey.CapacityOf(larder))");
			Assert.AreEqual(0, Count(Code(Source(Proof)), "int room ="),
				"a snapshotted room survives the callback that invalidated it");
			Ordered(ground, "private static bool ExactMeasuredLarder(",
				"Larder.CurrentZone == DestinationZone && Larder.InInventory == null",
				"Larder.GetIntProperty(\"KingdomLarder\") == 1 && Larder.Inventory != null",
				"KingdomSurvey.HeldIn(Larder) <= KingdomSurvey.CapacityOf(Larder);");
			Ordered(ground, "private static bool TryExactDestinationStore(",
				"TryPurposeZone(Operation.DestinationZoneId, out Zone zone)",
				"FindExactKnown(zone, Operation.DestinationInputStoreId, out Store)",
				"Store.CurrentZone != zone",
				"|| Store.InInventory != null || Store.CurrentCell == null",
				"|| !ReferenceEquals(Store.CurrentCell.ParentZone, zone)",
				"|| Store.CurrentCell.Objects == null",
				"|| !Store.CurrentCell.Objects.Contains(Store))",
				"The frozen destination store is no longer exactly on its own ground.",
				"KingdomMaterials.IsStockpile(Store) && Store.Inventory != null",
				"The frozen destination store lost its stockpile dedication.");
		}

		[Test]
		public void TheProvisionDiscriminatorReportsUnknownRatherThanInferringLegacyLandings()
		{
			string landing = Source(Landing);
			Ordered(landing, "internal static bool TryPurposeProvisionLanded(",
				"Applicable = false;", "carried <= 0) return false;", "Applicable = true;",
				"!TryPurposeLandingMark(Operation, out string receipt, out _)",
				"!TryRootedPurposeCargoExact(Operation, out GameObject cargo)",
				"!TryPurposeLandedRecord(cargo, receipt, carried, out Landed)) return false;",
				"KingdomPurposePortfolioRules.LandingIsProved(",
				"cargo.GetStringProperty(PortfolioLandedReceiptProperty) == receipt, Landed,");
			// Presence of a count alone is not the discriminator, and rendering must not migrate.
			Assert.AreEqual(0, Count(Code(landing), "HasProperty(PortfolioLandedCountProperty)"),
				"the stamped receipt, not the bare count property, proves whose landing this was");
			Assert.AreEqual(0, Count(Code(landing), "TryRootedPurposeCargo(Operation"),
				"status must read through the non-migrating lookup");
			string rules = Source(Rules);
			Ordered(rules, "public static bool LandingIsProved(",
				"return Recorded && Carried > 0 && Progress == Carried;");
		}

		[Test]
		public void APartialLandingWaitsRatherThanClaimingDelivery()
		{
			string landing = Source(Landing);
			Ordered(landing, "landed = progress + AddPurposeFood(", "if (Landed != Carried)",
				"The destination larders took only part of the exact carried provision.");
			Assert.AreEqual(1, Count(landing, "Landed != Carried"),
				"a larder that filled between the aggregate FoodSpace check and the per-larder"
				+ " room walk, or a staple that stops being food mid-loop, must WAIT: the Delivered"
				+ " publish is only reachable once every carried serving is measured in place");
			string output = Source(Output);
			Ordered(output, "TryLandCarriedFood(System, operation, cargo, destinationZone",
				"KingdomPurposeOperationPhase.Delivered");
		}

		[Test]
		public void ACapacityShortfallWaitsAndOnlyAnAmbiguousAftermathQuarantines()
		{
			string landing = Source(Landing);
			Ordered(landing, "KingdomPurposeFoodLandingAction.Interference)",
				"The destination larders changed under this operation's exact provision receipt.",
				"if (outstanding <= 0)", "survey.FoodSpace < outstanding",
				"Fail(\"Dedicated larders at the destination cannot cover the exact carried provision.\"");
			// Every ambiguous end goes through the one seam that stamps the durable fault first.
			Assert.AreEqual(1, Count(landing, "Ambiguous = true;"),
				"exactly one seam sets the flag, and it stamps the durable fault first");
			Assert.AreEqual(18, Count(landing, "return FaultedLanding("),
				"an unclaimable record, an unreconciled or replaced callback witness, an unproved"
				+ " custody scan, a stray mark, forged marks, changed larders, a stranded serving,"
				+ " a staple that stopped being makeable, a record that would not take or that"
				+ " changed under the callbacks, a ground that no longer reproves, and a mark that"
				+ " survived its own retirement all cut; only a shortfall of room waits");
			Ordered(landing, "private static bool FaultedLanding(", "Ambiguous = true;",
				"return StampPurposeLandingFault(Cargo, Receipt, Expected, Observed)",
				"? Fail(Reason, out Failure)",
				": Fail(Reason + \" The durable landing fault could not be stamped.\", out Failure);");
			string output = Source(Output);
			StringAssert.Contains("return !ambiguous ? Fail(landing, out Failure)", output);
			StringAssert.Contains(": QuarantinePortfolio(Pair, landing, out Published, out Failure);",
				output);
		}

		[Test]
		public void TheArrivalNoteIsWrittenOnlyAfterTheDurableDeliveredPublishSucceeds()
		{
			string output = Source(Output);
			Ordered(output, "TryLandCarriedFood(System, operation, cargo, destinationZone",
				"if (!TryPublishOperation(Pair, next, delivered, out Published, out Failure))"
				+ " return false;",
				"NotePurposeProvisionArrival(System, operation);");
			Assert.AreEqual(1, Count(output, "NotePurposeProvisionArrival("));
			Assert.AreEqual(0, Count(Code(output), "Ledger.Note("),
				"the landing runtime must not write arrival prose of its own");
		}

		[Test]
		public void TheArrivalNoteReportsTheWholeCarriedAmountNeverAPartialRemainder()
		{
			string landing = Source(Landing);
			Assert.AreEqual(1, Count(landing, "Ledger.Note("),
				"there is exactly one arrival phrase, and it lives past the checkpoint");
			Ordered(landing, "private static void NotePurposeProvisionArrival(",
				"KingdomPurposePortfolioRules.TryCarriedFood(Operation.SourceKind,",
				"Simulation.City.KingdomStockKind.Food, carried,");
			StringAssert.DoesNotContain("KingdomStockKind.Food, landed", landing);
			StringAssert.DoesNotContain("landed - marked", Code(landing));
		}

		[Test]
		public void EveryOfferIsWitnessedOnTheDurableCargoBeforeTheEngineEverSeesIt()
		{
			// The witness must be written before the offer and live on the cargo, not on the
			// serving: the serving is exactly what a callback may destroy, so a witness carried on
			// it would vanish with the evidence it exists to keep. Only an exactly reproved
			// one-step increment retires it, so a refused quarantine publication cannot be
			// forgotten by the next pass and answered with a fresh serving.
			string proof = Source(Proof);
			Ordered(proof, "int expected = held + 1;",
				"if (!StampPurposeLandingAttempt(Cargo, Receipt, expected))",
				"Aftermath = KingdomPurposeServingAftermath.Stranded;",
				"Aftermath = PlacePurposeServing(Survey, larder, Blueprint, Receipt, Prefilter);");
			Assert.AreEqual(0, Count(Between(Code(proof),
					"if (!StampPurposeLandingAttempt(Cargo, Receipt, expected))",
					"Aftermath = PlacePurposeServing("), "TryClearPurposeLandingAttempt"),
				"nothing may retire the witness between writing it and making the offer");
			// A serving that was never offered names no aftermath, so its witness is retired and
			// the shortfall is still reported as itself.
			Ordered(proof, "if (Aftermath == KingdomPurposeServingAftermath.Unavailable)",
				"if (!TryClearPurposeLandingAttempt(Cargo, Receipt, expected))");
			string record = Source(Record);
			// A witness a callback replaced is evidence; the clear reproves it and refuses.
			Ordered(record, "private static bool TryClearPurposeLandingAttempt(",
				"|| !OwnedFieldPresent(Cargo, PortfolioLandedAttemptProperty)) return false;",
				"if (!OwnedStringField(Cargo, PortfolioLandedAttemptProperty)",
				"out int pending) || pending != Expected) return false;",
				"Cargo.RemoveStringProperty(PortfolioLandedAttemptProperty);");
			Ordered(record, "private static bool StampPurposeLandingAttempt(",
				"KingdomPurposePortfolioRules.TryLandingAttempt(",
				"Receipt, Expected, out string witness)) return false;",
				"Cargo.SetStringProperty(PortfolioLandedAttemptProperty, witness);");
			// Presence is the property existing, not its value being non-empty: a witness torn down
			// to an empty string is still the record of an offer, and reading emptiness as "no
			// offer" would hand exactly that case a fresh serving.
			Ordered(record, "private static KingdomPurposeLandingAttemptState ReadPurposeLandingAttempt(",
				"bool present = GameObject.Validate(Cargo)",
				"&& OwnedFieldPresent(Cargo, PortfolioLandedAttemptProperty);",
				"bool ours = present && OwnedStringField(Cargo, PortfolioLandedAttemptProperty)",
				"Cargo.GetStringProperty(PortfolioLandedAttemptProperty), Receipt, out expected);",
				"KingdomPurposePortfolioRules.ClassifyLandingWitnesses(",
				"PurposeLandingIsFaulted(Cargo), present, ours, expected, Observed, Exact);");
			Assert.AreEqual(0, Count(Between(Code(record),
					"private static KingdomPurposeLandingAttemptState ReadPurposeLandingAttempt(",
					"ClassifyLandingWitnesses("), "IsNullOrEmpty"),
				"presence read from the value would let an emptied witness read as no offer");
			// The fault witness is distinct and unconditional: a callback that throws after
			// placing the exact unit leaves a ground the attempt alone would call settled.
			// The stamp is total and checked: a forged or excess figure folds onto an over-bound
			// sentinel rather than refusing, and the witness is read back before the caller
			// returns, so an ambiguity can never quarantine without a durable fault behind it.
			Ordered(record, "private static bool StampPurposeLandingFault(",
				"KingdomPurposePortfolioRules.LandingFaultFigure(Expected),",
				"KingdomPurposePortfolioRules.LandingFaultFigure(Observed), out string witness))",
				"Cargo.SetStringProperty(PortfolioLandedFaultProperty, witness);",
				"return PurposeLandingIsFaulted(Cargo)",
				"&& Cargo.GetStringProperty(PortfolioLandedFaultProperty) == witness;");
			Ordered(Source(Attempt), "public static int LandingFaultFigure(",
				"return Value < 0 || Value > MaxCarriedFood ? OverBoundLandingFigure : Value;");
			Ordered(record, "private static bool PurposeLandingIsFaulted(",
				"OwnedFieldPresent(Cargo, PortfolioLandedFaultProperty);");
			// The credited cargo's own record goes with the witnesses: left behind after its
			// operation and root are forgotten, it is evidence of an owner nobody can name, and
			// every later landing in that city would cut on it.
			// Retirement is a checked classification, not an erasure: only nothing at all, or one
			// whole record of this operation's whole carriage, may be cleared. A partial, torn,
			// wrong-typed or foreign record, a serving index, or an unexpected attempt or fault
			// blocks, because once the operation is forgotten it names nobody.
			Ordered(record, "private static bool PurposeCargoRecordIsRetirable(",
				"if (OwnedFieldPresent(Cargo, PortfolioLandedFoodProperty)",
				"|| OwnedFieldPresent(Cargo, PortfolioLandedAttemptProperty)",
				"|| OwnedFieldPresent(Cargo, PortfolioLandedFaultProperty)) return false;",
				"if (!OwnedFieldPresent(Cargo, PortfolioLandedReceiptProperty)",
				"&& !OwnedFieldPresent(Cargo, PortfolioLandedCountProperty)) return true;",
				"return Carried > 0 && OwnedStringField(Cargo, PortfolioLandedReceiptProperty)",
				"&& Cargo.GetStringProperty(PortfolioLandedReceiptProperty) == Receipt",
				"&& TryPurposeLandedRecord(Cargo, Receipt, Carried, out int recorded)",
				"&& recorded == Carried;");
			Ordered(record, "private static bool TryClearPurposeLandingWitnesses(",
				"if (!PurposeCargoRecordIsRetirable(Cargo, Receipt, Carried)) return false;",
				"Cargo.RemoveStringProperty(PortfolioLandedReceiptProperty);",
				"Cargo.RemoveIntProperty(PortfolioLandedCountProperty);",
				"return !OwnedFieldPresent(Cargo, PortfolioLandedReceiptProperty)");
			Assert.AreEqual(0, Count(Code(record), "RemoveStringProperty(PortfolioLandedFaultProperty)"),
				"a standing fault is never erased by a credit; it blocks it");
			string witnesses = Source(Attempt);
			Ordered(witnesses,
				"public static KingdomPurposeLandingAttemptState ClassifyLandingWitnesses(",
				"return Faulted ? KingdomPurposeLandingAttemptState.Ambiguous",
				": ClassifyLandingAttempt(Present, Ours, Expected, Observed, Exact);");
			// The outstanding witness outranks every other reading of the ground, and it is
			// consulted before a single serving of this pass is offered.
			string landing = Source(Landing);
			Ordered(landing, "ReadPurposeLandingAttempt(Cargo, receipt, physical, exact);",
				"if (pending == KingdomPurposeLandingAttemptState.Ambiguous)",
				"An earlier provision offer left a callback witness this pass cannot reconcile.",
				"bool reconciled = pending == KingdomPurposeLandingAttemptState.Settled;",
				"if (reconciled && !TryClearPurposeLandingAttempt(Cargo, receipt, physical))",
				"RecordPurposeLanded(Cargo, receipt, carried, progress);",
				"AddPurposeFood(survey, larders, Cargo, receipt, prefilter,");
			Assert.AreEqual(1, Count(Code(landing), "TryClearPurposeLandingAttempt"),
				"the transaction retires the witness in exactly one place, past every cut");
			string attempt = Source(Attempt);
			Ordered(attempt, "public static KingdomPurposeLandingAttemptState ClassifyLandingAttempt(",
				"if (!Present) return KingdomPurposeLandingAttemptState.Clear;",
				"if (!Ours || Expected < 1 || Observed < 0)",
				"return KingdomPurposeLandingAttemptState.Ambiguous;",
				"return Exact && Observed == Expected ? KingdomPurposeLandingAttemptState.Settled",
				": KingdomPurposeLandingAttemptState.Ambiguous;");
			Ordered(attempt, "public static bool TryReadLandingAttempt(",
				"TryDecodeFields(Witness, 3, out string[] fields)",
				"string.Equals(fields[0], LandingAttemptTag, StringComparison.Ordinal)",
				"string.Equals(fields[1], Receipt, StringComparison.Ordinal)",
				"Int(fields[2], out Expected) && Expected >= 1");
		}

		[Test]
		public void ARootIsRemovedOnlyWhenItsValueReprovesThisExactConsumedCargo()
		{
			// Root keys share one namespace. The legacy delimiter form can be named by a different
			// pair/epoch/operation tuple, so a blind delete drops another operation's live root.
			string root = Source(CargoRoot);
			Ordered(root, "internal static void RemovePurposeCargoRoots(",
				"string encoded = KingdomPurposePortfolioRules.EncodeCargo(Cargo);",
				"RemovePurposeCargoRoot(PurposeCargoRootKey(Cargo), Cargo, encoded);",
				"RemovePurposeCargoRoot(PortfolioLegacyCargoRootKey(Cargo.PairId, Cargo.PairEpoch,");
			Ordered(root, "private static void RemovePurposeCargoRoot(",
				"if (!The.Game.ObjectGameState.TryGetValue(Key, out object value)) return;",
				"GameObject rooted = value as GameObject;",
				"KingdomPurposePortfolioRules.RootEntryIsRetirable(rooted != null,",
				"rooted != null && rooted.IDIfAssigned == Cargo.ObjectId,",
				"GameObject.Validate(rooted),",
				"rooted != null && ExactPortfolioCargoIdentity(rooted, Encoded))) return;",
				"The.Game.ObjectGameState.Remove(Key);");
			Assert.AreEqual(1, Count(Code(root), "ObjectGameState.Remove(Key)"),
				"every removal goes through the one checked seam");
			Assert.AreEqual(2, Count(Code(root), "RemovePurposeCargoRoot(P"),
				"both the canonical and the legacy key are offered to the check, and only those");
			// The legacy form must read back the same on every machine, or the key a save wrote is
			// not the key this reads.
			Ordered(root, "private static string PortfolioLegacyCargoRootKey(",
				"PairEpoch.ToString(CultureInfo.InvariantCulture)");
			Assert.AreEqual(0, Count(Code(root), "+ Cargo.PairEpoch +"),
				"a culture-sensitive epoch is a different key on a different machine");
			Assert.AreEqual(0, Count(Code(root), "+ Operation.PairEpoch +"));
			// Input consumption removes both roots through the same check, not the canonical alone.
			string input = Source(Input);
			Ordered(input, "KingdomPurposePortfolioRules.TryDecodeCargo(operation.InputCargoReceipt,",
				"out KingdomPurposeCargoReceipt consumed)) RemovePurposeCargoRoots(consumed);");
			Assert.AreEqual(0, Count(Code(input), "ObjectGameState.Remove"),
				"the input seam must not delete a root key of its own");
		}

		[Test]
		public void TheDeliveredCheckpointReprovesIdentityRootAndCustodyAfterTheCallbacks()
		{
			// Placing servings runs engine code that can reach the cargo itself, so nothing is
			// carried forward across the callbacks. Each postcondition is its own refusal.
			string output = Source(Output);
			Ordered(output, "TryLandCarriedFood(System, operation, cargo, destinationZone",
				"PurposeLandingStillExact(operation, cargo, out string moved)",
				"QuarantinePortfolio(Pair, moved, out Published, out Failure);",
				"KingdomPurposeOperationPhase.Delivered",
				"TryPublishOperation(Pair, next, delivered");
			Ordered(output, "private static bool PurposeLandingStillExact(",
				"ExactPortfolioCargoIdentity(Cargo, Operation.OutputCargoReceipt)",
				"The landing cargo lost its exact identity under the provision callbacks.",
				"TryRootedPurposeCargoExact(Operation, out GameObject rooted)",
				"!ReferenceEquals(rooted, Cargo)",
				"The landing cargo lost its canonical root under the provision callbacks.",
				"TryExactDestinationStore(Operation, out GameObject store, out Fault)",
				"ReferenceEquals(Cargo.InInventory, store) && Cargo.CurrentCell == null",
				"&& store.Inventory.InventoryContains(Cargo)",
				"The landing cargo left the frozen destination store under the provision callbacks.");
			Assert.AreEqual(1, Count(output, "PurposeLandingStillExact(operation, cargo,"),
				"exactly one reproof, and it stands between the callbacks and the checkpoint");
		}

		[Test]
		public void ThePreflightRefusesNewWorkBeforeAnyGroundReadOrBitConsumption()
		{
			string control = Source(Control);
			Ordered(control, "private static bool TryPortfolioOperationPreflight(",
				"if (!KingdomMaster.NewWorkAllowed(system))",
				"New purpose work is paused by realm transition authority.",
				"TryOperationGround(Operation, out Zone sourceZone",
				"TryPreflightCarriedFood(system, Operation, destinationZone, out Failure)",
				"TryPlanLocalDebit(Operation, sourceZone, input, out _, out Failure)");
			Ordered(control, "if (!TryPortfolioOperationPreflight(operation, out Failure)) return false;",
				"next.BootstrapUsed = true;", "next.ReturnUsed = true;",
				"TryPublishPortfolioPair(Pair, next, out Failure)");
		}

		[Test]
		public void APausedRealmRefusesNewWorkButStillFinishesCommittedRecovery()
		{
			string drive = Source(Drive);
			// Doctrine: only work that is not already committed consults the master gate, so an
			// in-flight landing stays resumable while paused instead of being stranded by it.
			Ordered(drive, "private static bool DrivePortfolioOperation(",
				"if (!KingdomPurposePortfolioRules.OperationPhaseIsCommitted(operation.Phase)",
				"&& !KingdomMaster.NewWorkAllowed(System))",
				"New purpose work is paused by realm transition authority.");
			Assert.AreEqual(0, Count(Code(drive), "if (!KingdomMaster.NewWorkAllowed(System))"),
				"an unconditional block would stall every committed hop of a paused realm");
			// Crediting a delivered cargo is committed recovery and carries no gate of its own;
			// the activation branch opens a brand-new operation and is gated in the preflight.
			Ordered(drive, "private static bool AcceptPortfolioCredit(",
				"TryPortfolioOperationPreflight(operation, out Failure)");
			string family = drive + Source(Control) + Source(Output) + Source(Landing)
				+ Source(Proof) + Source(CargoRoot);
			Assert.AreEqual(2, Count(Code(family), "KingdomMaster.NewWorkAllowed("),
				"the portfolio's pause surface moved; re-check which work is committed recovery");
		}

		[Test]
		public void ThePreflightAndTheLandingShareOneCapacityBoundAndOneRefusal()
		{
			string landing = Source(Landing);
			StringAssert.Contains("KingdomSurvey.Take(DestinationZone, System).FoodSpace >= carried",
				landing);
			Assert.AreEqual(2, Count(landing,
				"Dedicated larders at the destination cannot cover the exact carried provision."),
				"preflight and landing must refuse on the same measured figure");
			StringAssert.Contains("KingdomConstructionInputLeaseAuthority"
				+ ".TryObjectAvailableForLocalDebit",
				Source("Growth/KingdomPurposePortfolio.LocalPlan.cs"));
			// The staple is proved makeable before the operation is published, which is what makes
			// a later refusal an ambiguity rather than a wait nobody can ever clear.
			Ordered(landing, "private static bool TryPreflightCarriedFood(",
				"if (carried <= 0) return true;",
				"!PurposeServingIsMakeable(KingdomData.CropForStyle(System.Style))",
				"The realm's own staple cannot become an exact landed serving.",
				"KingdomSurvey.Take(DestinationZone, System).FoodSpace >= carried");
			Ordered(Source(Proof), "private static bool PurposeServingIsMakeable(",
				"GameObject sample = ExactPurposeServing(", "sample.Obliterate();",
				"return true;");
		}
	}
}
#endif
