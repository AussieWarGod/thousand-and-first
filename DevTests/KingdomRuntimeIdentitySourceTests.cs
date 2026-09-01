#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ThousandAndFirst.Api;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomRuntimeIdentitySourceTests
	{
		private static readonly string RealmId = new string('a', 64);
		private static readonly string SettlementA = new string('b', 64);
		private static readonly string SettlementB = new string('c', 64);

		private sealed class HostileSettlement : KingdomSettlement
		{
			public List<string> HiddenEvidence = new List<string> { "must-not-drop" };
		}

		private static string Source(string relative)
		{
			return TestMain.ReadRepositoryText(relative);
		}

		private static string ConversionSource()
		{
			return string.Join("\n", new[]
			{
				Source("Core/KingdomConversion.cs"),
				Source("Core/KingdomConversion.PressureAndHelpers.cs"),
				Source("Core/KingdomConversion.OsmosisAndBrink.cs"),
				Source("Core/KingdomConversion.MealConversionAndCohabitation.cs"),
				Source("Core/KingdomConversion.Transitions.cs")
			});
		}

		private static string RealmArchiveSource()
		{
			string[] files =
			{
				"KingdomRealmArchivePhase.cs",
				"KingdomRealmCallbackPhase.cs",
				"KingdomRealmCallbackDisposition.cs",
				"KingdomRealmCallbackScope.cs",
				"KingdomRealmCallbackReceipt.cs",
				"KingdomRealmArchive.00Core.cs",
				"KingdomRealmArchive.01Capture.cs",
				"KingdomRealmArchive.02AuthorityHash.cs",
				"KingdomRealmArchive.03Validation.cs",
				"KingdomRealmArchive.04GraphMatch.cs",
				"KingdomRealmArchive.05BoundedValidation.cs",
				"KingdomRealmArchive.06JobValidation.cs",
				"KingdomRealmArchive.07DeliveryValidation.cs",
				"KingdomRealmArchive.08Clone.cs",
				"KingdomRealmArchive.09ExactGraph.cs",
				"KingdomRealmArchive.10WireEnvelope.cs",
				"KingdomRealmArchive.11WirePrimitives.cs",
				"KingdomRealmArchive.12WireRegistry.cs"
			};
			string[] source = new string[files.Length];
			for (int i = 0; i < files.Length; i++)
				source[i] = Source(Path.Combine("Core", files[i]));
			return string.Join("\n", source);
		}

		[Test]
		public void ArchivedSettlementCodec_KeepsNestedAndStaticMetadata()
		{
			Type codec = typeof(KingdomArchivedSettlementCodec);
			Assert.AreEqual("ThousandAndFirst.KingdomArchivedSettlementCodec", codec.FullName);
			Assert.IsTrue(codec.IsAbstract);
			Assert.IsTrue(codec.IsSealed);

			string[] nestedNames = { "Budget", "CappedWriteStream", "ReferenceComparer" };
			for (int i = 0; i < nestedNames.Length; i++)
			{
				Type nested = codec.GetNestedType(nestedNames[i],
					System.Reflection.BindingFlags.NonPublic);
				Assert.IsNotNull(nested, nestedNames[i]);
				Assert.AreEqual(codec.FullName + "+" + nestedNames[i], nested.FullName);
				Assert.IsTrue(nested.IsNestedPrivate, nestedNames[i]);
			}

			System.Reflection.BindingFlags fields = System.Reflection.BindingFlags.NonPublic
				| System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.DeclaredOnly;
			Assert.AreEqual(typeof(System.Text.UTF8Encoding),
				codec.GetField("StrictUtf8", fields).FieldType);
			Assert.AreEqual(typeof(Type[]), codec.GetField("ApprovedObjects", fields).FieldType);
		}

		private static string FoundingTransactionSource()
		{
			string[] files =
			{
				"KingdomFoundingTransaction.00Core.cs",
				"KingdomFoundingTransaction.01DebugReset.cs",
				"KingdomFoundingTransaction.02ResetEvidence.cs",
				"KingdomFoundingTransaction.03AuthorityProof.cs",
				"KingdomFoundingTransaction.04DirectSecond.cs",
				"KingdomFoundingTransaction.05DirectFirst.cs",
				"KingdomFoundingTransaction.06GlobalReservation.cs",
				"KingdomFoundingTransaction.07ReservationCleanup.cs",
				"KingdomFoundingTransaction.08SiteReservation.cs",
				"KingdomFoundingTransaction.09EntryPoints.cs",
				"KingdomFoundingTransaction.10Begin.cs",
				"KingdomFoundingTransaction.10Staging.cs",
				"KingdomFoundingTransaction.11Run.cs",
				"KingdomFoundingTransaction.12PublishFirst.cs",
				"KingdomFoundingTransaction.13PublishSecond.cs",
				"KingdomFoundingTransaction.14PublishSecondCore.cs",
				"KingdomFoundingTransaction.15IdentityAndVillage.cs",
				"KingdomFoundingTransaction.16SecondProjection.cs",
				"KingdomFoundingTransaction.17ReceiptValidation.cs",
				"KingdomFoundingTransaction.18ReceiptCompletion.cs",
				"KingdomFoundingTransaction.19FactionRegistry.cs",
				"KingdomFoundingTransaction.20Chronicle.cs",
				"KingdomFoundingTransaction.21EngineProjection.cs",
				"KingdomFoundingTransaction.22RecoveryHelpers.cs"
			};
			string[] source = new string[files.Length];
			for (int i = 0; i < files.Length; i++)
				source[i] = Source(Path.Combine("Core", files[i]));
			return string.Join("\n", source);
		}

		private static KingdomTradeBook ExactTradeBook()
		{
			KingdomTradeBook book = new KingdomTradeBook();
			string failure;
			Assert.IsTrue(KingdomTradeRules.BindExactIdentity(book, RealmId,
				new[] { SettlementA, SettlementB }, out failure), failure);
			return book;
		}

		[Test]
		public void V10BoundaryRefusesPreRedesignDevelopmentGeometry()
		{
			string system = KingdomSystemLogicalSource.Read();
			StringAssert.Contains("private const int CurrentSerializationVersion = 10;", system);
			StringAssert.Contains("private const int FirstNamedSerializationVersion = 10;", system);
			int reflected = system.IndexOf("SerializationVersion == LegacyReflectedSerializationVersion",
				StringComparison.Ordinal);
			int migrate = system.IndexOf("NormalizeState(AllowLegacyIdentityMigration: true)",
				reflected, StringComparison.Ordinal);
			int named = system.IndexOf("Reader.ReadNamedFields", migrate, StringComparison.Ordinal);
			int refuseMigration = system.IndexOf(
				"NormalizeState(AllowLegacyIdentityMigration: false)", named,
				StringComparison.Ordinal);
			Assert.Greater(migrate, reflected);
			Assert.Greater(named, migrate);
			Assert.Greater(refuseMigration, named);
			StringAssert.Contains("pre-v8 authority is not readable", system);
			StringAssert.Contains("final pre-Alpha physical-product break", system);
		}

		[Test]
		public void FirstFoundingFreezesIdsBeforeFactionCallbackOrStepMarker()
		{
			string founding = KingdomFoundingLogicalSource.Read();
			int method = founding.IndexOf("internal static Faction Found", StringComparison.Ordinal);
			int bind = founding.IndexOf("TryBindFirstFoundingIdentity", method,
				StringComparison.Ordinal);
			int firstMarker = founding.IndexOf("FoundingStepProperty, 0", bind,
				StringComparison.Ordinal);
			int addFaction = founding.IndexOf("Factions.AddNewFaction", bind,
				StringComparison.Ordinal);
			Assert.Greater(bind, method);
			Assert.Greater(firstMarker, bind);
			Assert.Greater(addFaction, bind);
			StringAssert.Contains("SimulationSeedMatches(The.Game.GetWorldSeed(), system.RealmId",
				founding);
		}

		[Test]
		public void FounderBasinSplitKeepsOneAttributedPartBase()
		{
			string basin = FounderBasinLogicalSource.Read();
			Assert.AreEqual(7, FounderBasinLogicalSource.FileCount);
			Assert.AreEqual(7, Regex.Matches(basin,
				@"public partial class r_FounderBasin").Count);
			Assert.AreEqual(1, Regex.Matches(basin,
				@"public partial class r_FounderBasin : IPart").Count);
			StringAssert.Contains(
				"[Serializable]\n\tpublic partial class r_FounderBasin : IPart", basin);
		}

		[Test]
		public void NewRealmFactionKeyIsNamespacedAndDisplayNameRemainsPresentation()
		{
			string transaction = FoundingTransactionSource();
			string founding = KingdomFoundingLogicalSource.Read();
			string basin = FounderBasinLogicalSource.Read();
			StringAssert.Contains("KingdomIdentityRules.TryMintRealm(transaction, out realmFaction",
				transaction);
			StringAssert.Contains("PendingRealmFaction = realmFaction", transaction);
			StringAssert.Contains("faction.Name = factionId", founding);
			StringAssert.Contains("faction.DisplayName = Name", founding);
			StringAssert.Contains("system.KingdomFactionName != Basin.PendingRealmFaction",
				transaction);
			Assert.IsFalse(transaction.Contains(
				"string realmFaction = Kind == KingdomFoundingKind.FirstCity\n\t\t\t\t? Name"));
			Assert.IsFalse(founding.Contains("faction.Name = Name"));
			Assert.IsFalse(basin.Contains("Factions.Exists(name)"));
		}

		[Test]
		public void EveryFoundingEntryNormalizesPlainNamesAndRichSinksEscapeThem()
		{
			string transaction = FoundingTransactionSource();
			int directSecond = transaction.IndexOf(
				"private static bool TryFoundSecondWithoutWaterCore", StringComparison.Ordinal);
			int directFirst = transaction.IndexOf(
				"internal static bool TryFoundFirstWithoutWater", directSecond,
				StringComparison.Ordinal);
			string directSecondBody = transaction.Substring(directSecond,
				directFirst - directSecond);
			int normalize = directSecondBody.IndexOf(
				"KingdomPresentationRules.TryNormalizeName(Name", StringComparison.Ordinal);
			int system = directSecondBody.IndexOf(
				"KingdomSystem system = The.Game.RequireSystem<KingdomSystem>()",
				StringComparison.Ordinal);
			Assert.Greater(normalize, -1);
			Assert.Greater(system, normalize);
			StringAssert.Contains("KingdomPresentation.Rich(Basin.PendingName)", transaction);
			StringAssert.Contains("KingdomPresentation.Rich(Basin.PendingVillageDisplayName ??",
				transaction);

			string founding = KingdomFoundingLogicalSource.Read();
			StringAssert.Contains("KingdomPresentation.Rich(faction.DisplayName)", founding);
			StringAssert.Contains("KingdomPresentation.Rich(system.KingdomDisplayName)", founding);

			string basin = FounderBasinLogicalSource.Read();
			StringAssert.Contains("MaxLength: KingdomPresentationRules.MaxRawCodeUnits", basin);
			StringAssert.Contains("KingdomPresentationRules.TryNormalizeName(name", basin);
			StringAssert.Contains("KingdomPresentation.Rich(villageName)", basin);
			StringAssert.Contains("KingdomPresentation.Rich(System.KingdomDisplayName)", basin);
		}

		[Test]
		public void LaterFoundingFreezesSiteAndPendingTupleBeforePairedTopologyCommit()
		{
			string founding = FoundingTransactionSource();
			int publish = founding.IndexOf("private static void PublishSecondCore",
				StringComparison.Ordinal);
			int callFreeze = founding.IndexOf("TryFreezeSecondIdentity", publish,
				StringComparison.Ordinal);
			int marker = founding.IndexOf("SecondPublicationAuthorityProperty", callFreeze,
				StringComparison.Ordinal);
			int nonSeat = founding.IndexOf("System.TryAddNonSeatSettlement(founded", marker,
				StringComparison.Ordinal);
			Assert.Greater(callFreeze, publish);
			Assert.Greater(marker, callFreeze);
			Assert.Greater(nonSeat, marker);

			int freeze = founding.IndexOf("private static bool TryFreezeSecondIdentity",
				StringComparison.Ordinal);
			int transaction = founding.IndexOf(
				"Site.SetZoneProperty(SecondIdentityTransactionProperty", freeze,
				StringComparison.Ordinal);
			int settlement = founding.IndexOf(
				"Site.SetZoneProperty(SecondIdentitySettlementProperty", transaction,
				StringComparison.Ordinal);
			int prepare = founding.IndexOf("TryPrepareSecondCityTopology", freeze,
				StringComparison.Ordinal);
			int pending = founding.IndexOf("TryStagePendingSettlementIdentity", settlement,
				StringComparison.Ordinal);
			int irrevocable = founding.IndexOf(
				"Basin.PendingPhase = KingdomFoundingPhase.PublicationCommitted", pending,
				StringComparison.Ordinal);
			int commit = founding.IndexOf("TryCommitSecondCityTopology", irrevocable,
				StringComparison.Ordinal);
			Assert.Greater(transaction, freeze);
			Assert.Greater(settlement, transaction);
			Assert.Greater(prepare, freeze);
			Assert.Less(prepare, transaction);
			Assert.Greater(pending, settlement);
			Assert.Greater(irrevocable, pending);
			Assert.Greater(commit, irrevocable);
		}

		[Test]
		public void SecondCityCutBarriersPrecedeWaterAndTopologyMutations()
		{
			string founding = FoundingTransactionSource();
			int begin = founding.IndexOf("private static KingdomFoundingResult Begin(",
				StringComparison.Ordinal);
			int stageHelper = founding.IndexOf("private static bool TryStageFoundingReceipt",
				begin, StringComparison.Ordinal);
			int reservationHelper = founding.IndexOf(
				"private static bool TryAcquireFoundingReservations", stageHelper,
				StringComparison.Ordinal);
			int run = founding.IndexOf("private static KingdomFoundingResult Run(",
				reservationHelper, StringComparison.Ordinal);
			Assert.Greater(stageHelper, begin);
			Assert.Greater(reservationHelper, stageHelper);
			Assert.Greater(run, reservationHelper);

			string beginBody = founding.Substring(begin, stageHelper - begin);
			int prepare = beginBody.IndexOf("TryPrepareSecondCityTopology",
				StringComparison.Ordinal);
			int receiptCall = beginBody.IndexOf("TryStageFoundingReceipt", prepare,
				StringComparison.Ordinal);
			int reservationCall = beginBody.IndexOf("TryAcquireFoundingReservations", receiptCall,
				StringComparison.Ordinal);
			int waterBarrier = beginBody.IndexOf(
				"Basin.PendingPhase = KingdomFoundingPhase.WaterCommitted", reservationCall,
				StringComparison.Ordinal);
			int drain = beginBody.IndexOf("KingdomLiquids.Drain", waterBarrier,
				StringComparison.Ordinal);
			Assert.Greater(prepare, 0);
			Assert.Greater(receiptCall, prepare);
			Assert.Greater(reservationCall, receiptCall);
			Assert.Greater(waterBarrier, reservationCall);
			Assert.Greater(drain, waterBarrier);

			string stageBody = founding.Substring(stageHelper,
				reservationHelper - stageHelper);
			int receipt = stageBody.IndexOf("Basin.PendingKind = Kind",
				StringComparison.Ordinal);
			int receiptReadback = stageBody.IndexOf(
				"ValidateReceiptPayload(Basin, null, vessel", receipt,
				StringComparison.Ordinal);
			int originalReadback = stageBody.IndexOf("OriginalSnapshotStillExact(Basin, vessel)",
				receiptReadback, StringComparison.Ordinal);
			Assert.Greater(receipt, 0);
			Assert.Greater(receiptReadback, receipt);
			Assert.Greater(originalReadback, receiptReadback);

			string reservationBody = founding.Substring(reservationHelper,
				run - reservationHelper);
			int stageSite = reservationBody.IndexOf("StageSiteReservation",
				StringComparison.Ordinal);
			int siteReadback = reservationBody.IndexOf(
				"ValidateReceiptPayload(Basin, Site, vessel", stageSite,
				StringComparison.Ordinal);
			int acquireGlobal = reservationBody.IndexOf("AcquireGlobalReservation", siteReadback,
				StringComparison.Ordinal);
			Assert.Greater(stageSite, 0);
			Assert.Greater(siteReadback, stageSite);
			Assert.Greater(acquireGlobal, siteReadback);
			Assert.Greater(acquireGlobal, stageSite);
			StringAssert.Contains("TryFinishWaterCommit", founding);
		}

		[Test]
		public void DirectSecondRouteStagesSiteBeforeGlobalAndReacquiresExactCleanupCut()
		{
			string founding = FoundingTransactionSource();
			int direct = founding.IndexOf(
				"private static bool TryFoundSecondWithoutWaterCore", StringComparison.Ordinal);
			int next = founding.IndexOf("internal static bool TryFoundFirstWithoutWater",
				direct, StringComparison.Ordinal);
			string body = founding.Substring(direct, next - direct);
			int readExisting = body.IndexOf("TryReadSiteReservation", StringComparison.Ordinal);
			int mint = body.IndexOf("authority = NewAuthority", readExisting,
				StringComparison.Ordinal);
			int stage = body.IndexOf("StageSiteReservation", mint, StringComparison.Ordinal);
			int acquire = body.IndexOf("AcquireGlobalReservation", stage,
				StringComparison.Ordinal);
			Assert.Greater(readExisting, 0);
			Assert.Greater(mint, readExisting);
			Assert.Greater(stage, mint);
			Assert.Greater(acquire, stage);
			Assert.IsFalse(body.Substring(0, stage).Contains("AcquireGlobalReservation"));
			Assert.IsFalse(body.Contains(
				"hasSite && realm.GetStringProperty(RealmReservationProperty"));
			StringAssert.Contains("this exact site receipt can retry", body);

			int cleanup = founding.IndexOf("private static bool ClearExactReservationSet",
				StringComparison.Ordinal);
			int cleanupEnd = founding.IndexOf("internal static bool HasSiteReservation",
				cleanup, StringComparison.Ordinal);
			string cleanupBody = founding.Substring(cleanup, cleanupEnd - cleanup);
			int releaseGlobal = cleanupBody.IndexOf("ReleaseGlobalReservation",
				StringComparison.Ordinal);
			int releaseSite = cleanupBody.IndexOf("ReleaseSiteReservation",
				StringComparison.Ordinal);
			Assert.Greater(releaseGlobal, 0);
			Assert.Greater(releaseSite, releaseGlobal);
		}

		[Test]
		public void StaleDirectContenderClearsSiteAndGlobalAfterAnotherCityWins()
		{
			Assert.IsFalse(KingdomFoundingTransactionRules.SecondRecoveryCanProject(
				2, 2, HasOpenNonSeatSlot: false, TargetIsExactSeat: false,
				TargetIsExactNonSeat: false, AlreadyPublished: false));
			string founding = FoundingTransactionSource();
			int direct = founding.IndexOf(
				"private static bool TryFoundSecondWithoutWaterCore", StringComparison.Ordinal);
			int next = founding.IndexOf("internal static bool TryFoundFirstWithoutWater",
				direct, StringComparison.Ordinal);
			string body = founding.Substring(direct, next - direct);
			int lostSeat = body.IndexOf("SecondRecoveryCanProject", StringComparison.Ordinal);
			int redo = body.IndexOf("DirectSecondHasForwardRedo", lostSeat,
				StringComparison.Ordinal);
			int noRedo = body.IndexOf("if (!forwardRedo)", redo, StringComparison.Ordinal);
			int clear = body.IndexOf("ClearExactReservationSet", noRedo,
				StringComparison.Ordinal);
			Assert.Greater(redo, lostSeat);
			Assert.Greater(noRedo, redo);
			Assert.Greater(clear, noRedo);
			Assert.IsFalse(body.Substring(lostSeat, clear - lostSeat).Contains("if (!hasSite)"));

			int helper = founding.IndexOf("private static bool DirectSecondHasForwardRedo",
				StringComparison.Ordinal);
			int helperEnd = founding.IndexOf("private static bool PublishedSecondAuthorityMatches",
				helper, StringComparison.Ordinal);
			string helperBody = founding.Substring(helper, helperEnd - helper);
			StringAssert.Contains("PendingSettlementAuthority == EncodedAuthority", helperBody);
			StringAssert.Contains("PublishedSecondAuthorityMatches", helperBody);
			StringAssert.Contains("System.TradeBook.SettlementIds.Contains(settlementId)",
				helperBody);
			StringAssert.Contains("System.CarryBook.SettlementIds.Contains(settlementId)",
				helperBody);
		}

		[Test]
		public void PublishedSecondRetryAlwaysSettlesPairedTopologyAndCompletionProvesAbsence()
		{
			string founding = FoundingTransactionSource();
			int publish = founding.IndexOf("private static void PublishSecond(",
				StringComparison.Ordinal);
			int core = founding.IndexOf("private static void PublishSecondCore", publish,
				StringComparison.Ordinal);
			string body = founding.Substring(publish, core - publish);
			int conditionalCore = body.IndexOf("if (!published)", StringComparison.Ordinal);
			int seat = body.LastIndexOf("SeatSecond", StringComparison.Ordinal);
			int settle = body.IndexOf("TrySettlePendingSettlementIdentity", seat,
				StringComparison.Ordinal);
			Assert.Greater(conditionalCore, 0);
			Assert.Greater(seat, conditionalCore);
			Assert.Greater(settle, seat);
			StringAssert.Contains("TryProveSettledSecondCityTopology", founding);

			string system = KingdomSystemLogicalSource.Read();
			Assert.IsFalse(system.Contains("ClearPendingSettlementIdentity("));
			StringAssert.Contains("TryAbortPendingSettlementIdentity", system);
			StringAssert.Contains("TrySettlePendingSettlementIdentity", system);
		}

		[Test]
		public void PendingOldCarryCutIsAcceptedBeforeAnyRebindOrQuarantine()
		{
			string system = KingdomSystemLogicalSource.Read();
			int method = system.IndexOf("private bool TryBindDormantLifecycleIdentity",
				StringComparison.Ordinal);
			int carry = system.IndexOf("if (CarryBook == null)", method,
				StringComparison.Ordinal);
			int acceptExpanded = system.IndexOf("CarryIdentityMatches(carrySettlementIds)",
				carry, StringComparison.Ordinal);
			int acceptOldCut = system.IndexOf("KingdomLifecycleRules.CanOwnAuthority(CarryBook)",
				acceptExpanded, StringComparison.Ordinal);
			int transitional = system.IndexOf("CarryIdentityMatches()", acceptOldCut,
				StringComparison.Ordinal);
			int bind = system.IndexOf("KingdomLifecycleRules.BindCarryIdentity", transitional,
				StringComparison.Ordinal);
			Assert.Greater(acceptExpanded, carry);
			Assert.Greater(acceptOldCut, acceptExpanded);
			Assert.Greater(transitional, acceptOldCut);
			Assert.Greater(bind, transitional);
		}

		[Test]
		public void SecondRuinRestorationStagesPerObjectTransactionBeforeBuiltStamp()
		{
			string founding = KingdomFoundingLogicalSource.Read();
			int method = founding.IndexOf("internal static bool TryRestoreRuinStructures",
				StringComparison.Ordinal);
			int marker = founding.IndexOf(
				"item.SetStringProperty(RuinRestorationTransactionProperty, TransactionId)",
				method, StringComparison.Ordinal);
			int built = founding.IndexOf("item.SetIntProperty(\"KingdomBuilt\", 1)", marker,
				StringComparison.Ordinal);
			int recount = founding.IndexOf("for (int i = 0; i < objects.Count; i++)",
				built, StringComparison.Ordinal);
			Assert.Greater(marker, method);
			Assert.Greater(built, marker);
			Assert.Greater(recount, built);
			StringAssert.Contains("TryRestoreRuinStructures(foundingZone, TransactionID",
				founding);
			StringAssert.Contains(
				"if (eligible && item.GetIntProperty(\"KingdomBuilt\") == 1) continue;",
				founding);

			string transaction = FoundingTransactionSource();
			StringAssert.Contains("KingdomFounding.TryRestoreRuinStructures(Site,",
				transaction);
			StringAssert.Contains("realm != currentSystem.RealmId", transaction);
		}

		[Test]
		public void TradeNormalizationNeverPromotesOrClearsMutableNameRows()
		{
			string system = KingdomSystemLogicalSource.Read();
			int normalize = system.IndexOf("private void NormalizeTradeBook()",
				StringComparison.Ordinal);
			string body = system.Substring(normalize);
			StringAssert.Contains("KingdomTradeRules.BindExactIdentity", body);
			StringAssert.Contains("PendingSettlementIdentityAbsent", body);
			StringAssert.Contains("TryAuthenticateExactExileClosedTick", body);
			StringAssert.Contains("TradeBook.Archives.Count > 0", body);
			StringAssert.Contains("QuarantineBook(TradeBook", body);
			StringAssert.Contains("legacy name-based trade rows were preserved", body);
			int legacyCheck = body.IndexOf("if (hasLegacyTrade)", StringComparison.Ordinal);
			int mutatingNormalize = body.IndexOf("KingdomTradeRules.Normalize(TradeBook)",
				StringComparison.Ordinal);
			Assert.GreaterOrEqual(legacyCheck, 0);
			Assert.Greater(mutatingNormalize, legacyCheck);
			Assert.IsFalse(body.Contains("LegacyCharterId("));
			Assert.IsFalse(body.Contains("LegacyManifestId("));
			Assert.IsFalse(body.Contains("LegacySettlementId("));
			Assert.IsFalse(body.Contains("ExpandExactIdentity"));
			Assert.IsFalse(body.Contains("ActiveDealKeys.Clear()"));
			Assert.IsFalse(body.Contains("Manifest = null"));
			Assert.IsFalse(system.Contains("TryGetExactExileClosedTick"));
		}

		[Test]
		public void ArchiveTransactionBlocksLegacyMirrorNormalization()
		{
			string system = KingdomSystemLogicalSource.Read();
			int active = system.IndexOf("bool archiveTransactionActive = ExiledRealmArchive != null",
				StringComparison.Ordinal);
			int guard = system.IndexOf("if (!archiveTransactionActive)", active,
				StringComparison.Ordinal);
			int standings = system.IndexOf("ExiledStandings = new Dictionary<string, int>()", guard,
				StringComparison.Ordinal);
			int promotion = system.IndexOf("ExiledSeat = legacyExiled ?? new KingdomSettlement()",
				guard, StringComparison.Ordinal);
			int normalize = system.IndexOf("ExiledSeat?.Normalize()", guard,
				StringComparison.Ordinal);
			int topology = system.IndexOf("ExiledSettlementTopology.NormalizeMembers()", normalize,
				StringComparison.Ordinal);
			Assert.Greater(guard, active);
			Assert.Greater(standings, guard);
			Assert.Greater(promotion, guard);
			Assert.Greater(normalize, guard);
			Assert.Greater(topology, normalize);
		}

		[Test]
		public void ArchiveCodecBoundsRawLengthsBeforeAllocation()
		{
			string archive = RealmArchiveSource();
			int readString = archive.IndexOf("private static string ReadString",
				StringComparison.Ordinal);
			int stringCap = archive.IndexOf("length < 0 || length > maxBytes", readString,
				StringComparison.Ordinal);
			int bytes = archive.IndexOf("ReadBytesDirect(length)", stringCap,
				StringComparison.Ordinal);
			Assert.Greater(stringCap, readString);
			Assert.Greater(bytes, stringCap);

			int readList = archive.IndexOf("private static List<string> ReadStrings",
				StringComparison.Ordinal);
			int listCap = archive.IndexOf("count < 0 || count > MaxCount", readList,
				StringComparison.Ordinal);
			int allocation = archive.IndexOf("new List<string>(count)", listCap,
				StringComparison.Ordinal);
			Assert.Greater(listCap, readList);
			Assert.Greater(allocation, listCap);

			int readBindings = archive.IndexOf("ReadBindings(SerializationReader",
				StringComparison.Ordinal);
			int bindingCap = archive.IndexOf("count < 0 || count > MaxBindings", readBindings,
				StringComparison.Ordinal);
			int bindingRows = archive.IndexOf("for (int i = 0; i < count; i++)", bindingCap,
				StringComparison.Ordinal);
			Assert.Greater(bindingRows, bindingCap);

			int readJobs = archive.IndexOf("ReadJobs(SerializationReader",
				StringComparison.Ordinal);
			int jobCap = archive.IndexOf("jobs < 0 || jobs > MaxJobs", readJobs,
				StringComparison.Ordinal);
			int jobRows = archive.IndexOf("for (int i = 0; i < jobs; i++)", jobCap,
				StringComparison.Ordinal);
			Assert.Greater(jobRows, jobCap);

			StringAssert.Contains("WriteStrings(Writer, SettlementIds, KingdomIdentityRules.MaxSettlements",
				archive);
			StringAssert.Contains("SettlementIds = ReadStrings(Reader, KingdomIdentityRules.MaxSettlements",
				archive);
			StringAssert.Contains("byte quarantineFlag = Reader.ReadByte()", archive);
			StringAssert.Contains("if (quarantineFlag > 1)", archive);
			StringAssert.Contains("catch (Exception ex)", archive);
			StringAssert.Contains("ResetToPoisonEnvelope(ex.Message)", archive);
			StringAssert.Contains("CarryBook == null || CarryBook.WireRejected", archive);
			StringAssert.Contains("ChronicleRegistry = KingdomChronicleReceiptRules.Header", archive);
			int write = archive.IndexOf("public void Write(SerializationWriter Writer)",
				StringComparison.Ordinal);
			int read = archive.IndexOf("public void Read(SerializationReader Reader)", write,
				StringComparison.Ordinal);
			string writeBody = archive.Substring(write, read - write);
			StringAssert.Contains("ValidateEnvelope(out failure)", writeBody);
			Assert.IsFalse(writeBody.Contains("!Validate(out failure)"));
		}

		[Test]
		public void ExileClosesTradeBeforeAnyPersistentCoreOrChronicleMutation()
		{
			string system = KingdomSystemLogicalSource.Read();
			int exile = system.IndexOf("public bool Exile(", StringComparison.Ordinal);
			int topology = system.IndexOf("TryExactSettlementIds", exile,
				StringComparison.Ordinal);
			int trade = system.IndexOf("KingdomTrade.TryOnExile", topology,
				StringComparison.Ordinal);
			int preGraph = system.LastIndexOf("archive.CurrentGraphMatches(this", trade,
				StringComparison.Ordinal);
			int preSet = system.LastIndexOf("preTradeSettlements", trade,
				StringComparison.Ordinal);
			int provedTick = system.IndexOf("provedTick != settledTick", trade,
				StringComparison.Ordinal);
			int postSet = system.IndexOf("postTradeSettlements", provedTick,
				StringComparison.Ordinal);
			int closedTick = system.IndexOf("archive.ClosedTick = settledTick", provedTick,
				StringComparison.Ordinal);
			int archivePhase = system.IndexOf(
				"archive.Phase = KingdomRealmArchivePhase.TradeClosed", closedTick,
				StringComparison.Ordinal);
			int archive = system.IndexOf("ExiledRealmArchive = archive", archivePhase,
				StringComparison.Ordinal);
			int continueExile = system.IndexOf("ContinueExileTransition", archive,
				StringComparison.Ordinal);
			int dispatcher = system.IndexOf("private bool ContinueExileTransition", continueExile,
				StringComparison.Ordinal);
			int mirror = system.IndexOf("TryEnsureExileMirrors", dispatcher,
				StringComparison.Ordinal);
			int telling = system.IndexOf("DispatchExileChronicle", dispatcher,
				StringComparison.Ordinal);
			int frozen = system.IndexOf(
				"archive.Phase = KingdomRealmArchivePhase.ChronicleFrozen", telling,
				StringComparison.Ordinal);
			int chronicle = system.IndexOf("TryClearRealmRegistry", frozen,
				StringComparison.Ordinal);
			int reset = system.IndexOf("ResetCurrentRealmAfterExile", chronicle,
				StringComparison.Ordinal);
			Assert.Greater(topology, exile);
			Assert.Greater(trade, topology);
			Assert.Greater(preGraph, topology);
			Assert.Greater(preSet, preGraph);
			Assert.Less(preSet, trade);
			Assert.Greater(provedTick, trade);
			Assert.Greater(postSet, provedTick);
			Assert.Greater(closedTick, provedTick);
			Assert.Greater(archivePhase, trade);
			Assert.Greater(archive, archivePhase);
			Assert.Greater(dispatcher, continueExile);
			Assert.Greater(mirror, dispatcher);
			Assert.Greater(telling, dispatcher);
			Assert.Greater(frozen, telling);
			Assert.Greater(chronicle, frozen);
			Assert.Greater(reset, chronicle);
			string preTrade = system.Substring(exile, trade - exile);
			Assert.IsFalse(preTrade.Contains("ExiledRealmArchive ="));
			Assert.IsFalse(preTrade.Contains("ExiledFactionName ="));
			Assert.IsFalse(preTrade.Contains("KingdomChronicle.RecordOnce"));
			Assert.IsFalse(preTrade.Contains("TryClearRealmRegistry"));
			StringAssert.Contains("no realm state was changed", system.Substring(trade,
				archivePhase - trade));
			StringAssert.Contains("SimulationSeedHigh = 0UL", system.Substring(reset));
			StringAssert.Contains("CarryBook = new KingdomCarryBook()", system.Substring(reset));
		}

		[Test]
		public void TradeExileSeamUsesDetachedReplacementAndOnePublish()
		{
			string trade = KingdomTradeLogicalSource.Read();
			int method = trade.IndexOf("public static bool TryOnExile(",
				StringComparison.Ordinal);
			int freeze = trade.IndexOf("KingdomTradeCodec.EncodePayload(original)", method,
				StringComparison.Ordinal);
			int prepare = trade.IndexOf("KingdomTradeRules.TryPrepareExile", freeze,
				StringComparison.Ordinal);
			int reproof = trade.IndexOf("ExactBytes(before, after)", prepare,
				StringComparison.Ordinal);
			int publish = trade.IndexOf("System.TradeBook = replacement", reproof,
				StringComparison.Ordinal);
			Assert.Greater(freeze, method);
			Assert.Greater(prepare, freeze);
			Assert.Greater(reproof, prepare);
			Assert.Greater(publish, reproof);
			string beforePublish = trade.Substring(method, publish - method);
			Assert.IsFalse(beforePublish.Contains("System.ActiveDealKeys.Clear"));
			Assert.IsFalse(beforePublish.Contains("System.Manifest = null"));
			int enter = trade.IndexOf("if (!TryEnter(System, out lease))", method,
				StringComparison.Ordinal);
			int lease = trade.IndexOf("using (lease)", enter, StringComparison.Ordinal);
			Assert.Greater(enter, method);
			Assert.Greater(lease, enter);
			string refused = trade.Substring(enter, lease - enter);
			StringAssert.Contains("return false;", refused);
			Assert.IsFalse(refused.Contains("System.TradeBook ="));
		}

		[Test]
		public void TradeExileArchivesProjectionAndUnresolvedManifestWithoutMutatingSource()
		{
			KingdomTradeBook source = ExactTradeBook();
			string operationId = KingdomTradeRules.OperationId(RealmId, 1L);
			source.Projections.Add(new KingdomTradeProjectionRow
			{
				OperationSequence = 1L,
				OperationId = operationId,
				SettlementId = SettlementA,
				ZoneId = "zone-a",
				ProjectionId = KingdomTradeRules.ProjectionId(operationId),
				ObjectId = "projection-object-a"
			});
			string manifestOperationId = KingdomTradeRules.OperationId(RealmId, 2L);
			source.Manifest = new KingdomTradeManifestState
			{
				OperationSequence = 2L,
				OperationId = manifestOperationId,
				Id = KingdomTradeRules.ManifestId(manifestOperationId),
				OriginId = SettlementA,
				OriginName = "City A",
				DestinationId = SettlementB,
				DestinationName = "City B",
				OriginalDrams = 11,
				EscrowDrams = 7,
				LoadedTick = 3L,
				DeadlineTick = 30L,
				Status = KingdomTradeManifestStatus.InFlight
			};
			byte[] before = KingdomTradeCodec.EncodePayload(source);

			KingdomTradeBook replacement;
			string failure;
			Assert.IsTrue(KingdomTradeRules.TryPrepareExile(source, 40L, RealmId,
				new[] { SettlementB, SettlementA }, out replacement, out failure), failure);
			CollectionAssert.AreEqual(before, KingdomTradeCodec.EncodePayload(source));
			Assert.AreNotSame(source, replacement);
			Assert.IsFalse(replacement.IdentityBound);
			Assert.IsNull(replacement.Manifest);
			Assert.IsEmpty(replacement.Projections);
			Assert.AreEqual(1, replacement.Archives.Count);
			KingdomTradeArchive receipt = replacement.Archives[0];
			Assert.AreEqual(1, receipt.ProjectionCount);
			Assert.AreEqual(7, receipt.ManifestEscrowDrams);
			Assert.AreEqual(source.Manifest.Id, receipt.ManifestId);
			Assert.AreEqual(KingdomTradeManifestStatus.InFlight, receipt.ManifestStatus);
			CollectionAssert.AreEqual(new[] { SettlementA, SettlementB }, receipt.SettlementIds);
		}

		[Test]
		public void TradeExileArchiveCapacityFailureLeavesSourceBytesAndGraphUntouched()
		{
			KingdomTradeBook source = ExactTradeBook();
			for (int i = 0; i < KingdomTradeRules.MaxArchives; i++)
			{
				source.Archives.Add(new KingdomTradeArchive
				{
					RealmId = "archived-realm-" + i,
					SettlementIds = new System.Collections.Generic.List<string>
						{ "archived-settlement-" + i },
					AuthorityEvidenceHash = "archive-evidence-" + i,
					ClosedTick = i
				});
			}
			byte[] before = KingdomTradeCodec.EncodePayload(source);
			KingdomTradeBook replacement;
			string failure;

			Assert.IsFalse(KingdomTradeRules.TryPrepareExile(source, 99L, RealmId,
				new[] { SettlementA, SettlementB }, out replacement, out failure));
			Assert.IsNull(replacement);
			StringAssert.Contains("capacity is full", failure);
			CollectionAssert.AreEqual(before, KingdomTradeCodec.EncodePayload(source));
			Assert.AreEqual(KingdomTradeRules.MaxArchives, source.Archives.Count);
		}

		[Test]
		public void ReturnRestoresExactArchiveAndReprovesAfterEngineCallbacks()
		{
			string system = KingdomSystemLogicalSource.Read();
			int restore = system.IndexOf("private bool RestoreArchivedRealmCore",
				StringComparison.Ordinal);
			StringAssert.Contains("TryClone(Archive.Seat", system.Substring(restore));
			StringAssert.Contains("CloneBindings(Archive.Bindings)", system.Substring(restore));
			StringAssert.Contains("CloneJobs(Archive.Jobs)", system.Substring(restore));
			StringAssert.Contains("CloneStrings(Archive.ChronicleEntries)",
				system.Substring(restore));
			StringAssert.Contains("TryCloneCarry(Archive.CarryBook", system.Substring(restore));
			StringAssert.Contains("CarryBook = carry", system.Substring(restore));
			int finish = system.IndexOf("private bool FinishReturnedRealm",
				StringComparison.Ordinal);
			string callbacks = system.Substring(finish, restore - finish);
			StringAssert.Contains("DispatchReturnReputation", callbacks);
			StringAssert.Contains("DispatchReturnFeelings", callbacks);
			StringAssert.Contains("DispatchReturnSeat", callbacks);
			StringAssert.Contains("DispatchReturnAbility", callbacks);
			StringAssert.Contains("KingdomRealmCallbackPhase.Attempting", callbacks);
			StringAssert.Contains("CurrentGraphMatches", callbacks);
			int match = system.IndexOf("private bool CurrentRealmMatchesArchive", restore,
				StringComparison.Ordinal);
			StringAssert.Contains("Archive.SettlementIds", system.Substring(match));
		}

		[Test]
		public void CallbackReceiptsFreezeBothGraphsAndNeverReplayUncertainAttempts()
		{
			string system = KingdomSystemLogicalSource.Read();
			string archive = RealmArchiveSource();
			int prepare = system.IndexOf("private bool PrepareReturnCallback",
				StringComparison.Ordinal);
			int settle = system.IndexOf("private bool SettleReturnCallback", prepare,
				StringComparison.Ordinal);
			string body = system.Substring(prepare, settle - prepare);
			StringAssert.Contains("InvokeAuthorized = false", body);
			StringAssert.Contains("Receipt.BeforeArchiveGraph = archiveGraph", body);
			int intent = body.IndexOf("Receipt.Phase == KingdomRealmCallbackPhase.Intent",
				StringComparison.Ordinal);
			int attempting = body.IndexOf(
				"Receipt.Phase = KingdomRealmCallbackPhase.Attempting", intent,
				StringComparison.Ordinal);
			int authorize = body.IndexOf("InvokeAuthorized = true", attempting,
				StringComparison.Ordinal);
			Assert.Greater(attempting, intent);
			Assert.Greater(authorize, attempting);

			StringAssert.Contains("if (!invokeAuthorized)", system);
			foreach (string callback in new[]
			{
				"The.Game.PlayerReputation.Set", "TrySeat(Site);",
				".RequirePart<KingdomCharterPart>().EnsureAbility();",
				"ReassertFeelings();", ".GetPart<KingdomCharterPart>()?.RemoveAbility();"
			})
			{
				int call = system.IndexOf(callback, StringComparison.Ordinal);
				int guard = system.LastIndexOf("if (!invokeAuthorized)", call,
					StringComparison.Ordinal);
				Assert.Greater(guard, 0, callback);
				Assert.Less(guard, call, callback);
			}
			StringAssert.Contains("BeforeArchiveGraph", archive);
			StringAssert.Contains("AfterArchiveGraph", archive);
			StringAssert.Contains("ObservedEffect", archive);
			StringAssert.Contains("BeforeStamp = int.MinValue", archive);
			StringAssert.Contains("AfterStamp = int.MinValue", archive);
			StringAssert.Contains("Writer.Write((byte)Value.Scope)", archive);
			StringAssert.Contains("Writer.Write(Value.BeforeStamp)", archive);
			StringAssert.Contains("Scope = (KingdomRealmCallbackScope)Reader.ReadByte()", archive);
			int feeling = system.IndexOf("private bool DispatchReturnFeelings",
				StringComparison.Ordinal);
			int feelingCallback = system.IndexOf("ReassertFeelings();", feeling,
				StringComparison.Ordinal);
			int feelingStamp = system.IndexOf("TrySettleFeelingStamp(Archive", feelingCallback,
				StringComparison.Ordinal);
			Assert.Greater(feelingStamp, feelingCallback);
		}

		[Test]
		public void CallbackSpecificProofsCoverNonTargetRowsMapsAndOwnerReferences()
		{
			string system = KingdomSystemLogicalSource.Read();
			StringAssert.Contains("otherRows.Add(rows[i].Copy())", system);
			StringAssert.Contains("TryWriteRegistry(otherRows", system);
			StringAssert.Contains("TryDeclareDisputedOnce(this", system);
			StringAssert.Contains("RecordDeclaredOnce(this, Declaration)", system);
			StringAssert.Contains("KingdomRealmCallbackProofRules.ChronicleListsMatch", system);
			StringAssert.Contains("ChronicleFaultMatches", system);
			StringAssert.Contains("frozenRegistryFault, out before", system);
			StringAssert.Contains("TryDisputedFingerprint(Value.EventId", system);
			StringAssert.Contains("ReferenceEquals(officialReference, ChronicleEntries)", system);
			StringAssert.Contains("reputation.ReputationValues", system);
			StringAssert.Contains("reputation.FactionRanks", system);
			StringAssert.Contains("WriteWorshipProof", system);
			StringAssert.Contains("ReferenceEquals(The.Game.PlayerReputation, reputationReference)",
				system);
			StringAssert.Contains("Factions.GetList()", system);
			StringAssert.Contains("FeelingReferencesStillMatch", system);
			StringAssert.Contains("WriteActivatedAbilityProof", system);
			StringAssert.Contains("TryHashCharterInvariant", system);
			StringAssert.Contains("OtherEntryCooldowns", system);
			StringAssert.Contains("OtherTileCoolingDown", system);
			StringAssert.Contains("ReferenceEquals(row.Value?.Abilities", system);
			StringAssert.Contains("CharterReferencesStillMatch", system);
			StringAssert.Contains("partCount > 1", system);
		}

		[Test]
		public void ExileAndReturnFreezeAuthoredCounterHistoryWithLegacyResumeOnly()
		{
			string system = KingdomSystemLogicalSource.Read();
			StringAssert.Contains("KingdomExileRules.ExileRumour", system);
			StringAssert.Contains("KingdomExileRules.ReturnRumour", system);
			StringAssert.Contains("TryDeclareDisputedOnce(this", system);
			StringAssert.Contains("OutsiderTelling, false, null, out declaration", system);
			StringAssert.Contains("OutsiderText = Declaration.AuthoredOutsiderText", system);
			StringAssert.Contains("CurrentPrefix = \"chronicle-v3\"", system);
			StringAssert.Contains("LegacyPrefix = \"chronicle-v2\"", system);
			StringAssert.Contains("TryDecodeLegacy(Intent, ExpectedEventId", system);
			StringAssert.Contains("Legacy = true", system);
			StringAssert.Contains("Receipt.Phase == KingdomRealmCallbackPhase.Settled", system);
			StringAssert.Contains("RecordDeclaredOnce(this, Declaration)", system);
			StringAssert.Contains("TryInspectChronicle(EventId, Fingerprint", system);
			Assert.IsFalse(system.Contains("KingdomChronicle.RecordDisputed(this"),
				"realm transition must use receipt-backed publication, not direct append");
		}

		[Test]
		public void ExileAndReturnPublishRecoveryPhasesBeforePiecemealMutation()
		{
			string system = KingdomSystemLogicalSource.Read();
			string archive = RealmArchiveSource();
			StringAssert.Contains("Resetting = 9", archive);
			StringAssert.Contains("ReturnCleaning = 10", archive);
			StringAssert.Contains("MirrorsPublished = 11", archive);
			int mirrorIntent = system.IndexOf(
				"archive.Phase = KingdomRealmArchivePhase.MirrorsPublished", StringComparison.Ordinal);
			int chronicle = system.IndexOf("DispatchExileChronicle(archive", mirrorIntent,
				StringComparison.Ordinal);
			Assert.Greater(chronicle, mirrorIntent);
			int resetIntent = system.IndexOf(
				"archive.Phase = KingdomRealmArchivePhase.Resetting", StringComparison.Ordinal);
			int reset = system.IndexOf("ResetCurrentRealmAfterExile()", resetIntent,
				StringComparison.Ordinal);
			Assert.Greater(reset, resetIntent);
			int cleanupIntent = system.IndexOf(
				"Archive.Phase = KingdomRealmArchivePhase.ReturnCleaning", StringComparison.Ordinal);
			int firstCleanup = system.IndexOf("TryClearExileMirrors(Archive", cleanupIntent,
				StringComparison.Ordinal);
			int archiveLast = system.IndexOf("ExiledRealmArchive = null", firstCleanup,
				StringComparison.Ordinal);
			Assert.Greater(firstCleanup, cleanupIntent);
			Assert.Greater(archiveLast, firstCleanup);
			StringAssert.Contains("AllowCanonicalMissing: archive.Phase == KingdomRealmArchivePhase.TradeClosed",
				system);
			StringAssert.Contains("TryClearExileMirrors(Archive", system);
			StringAssert.Contains("ClearSettlementMirror(ref ExiledSeat, Archive.Seat", system);
			StringAssert.Contains("return cleanup mirror reached a third value", system);
			StringAssert.Contains("!ExactExileMirrors(Archive)", system);
			StringAssert.Contains("List<object> currentRoots = new List<object> { currentSeat }", system);
			StringAssert.Contains("currentRoots.Add(NonSeatSettlementAt(i))", system);
			StringAssert.Contains("List<object> mirrorRoots = new List<object> { ExiledSeat }", system);
			StringAssert.Contains("mirrorRoots.Add(ExiledSettlementTopology.Get(i))", system);
			StringAssert.Contains("KingdomArchivedSettlementCodec.EmptyRegistries(Bindings, Jobs)",
				system);
			StringAssert.Contains("KingdomArchivedSettlementCodec.EmptyCarry(CarryBook)", system);
			StringAssert.Contains("CurrentRealmIsCanonicalBlank(archive)", system);
			StringAssert.Contains("!candidate.CurrentGraphMatches(System, out Failure)", archive);
		}

		[Test]
		public void ArchivedGraphProofRejectsCrossRootAliasAndFutureWire()
		{
			var archived = new System.Collections.Generic.List<string> { "a" };
			var live = new System.Collections.Generic.List<string> { "a" };
			Assert.IsTrue(KingdomArchivedSettlementCodec.DisjointMutableGraphs(
				new object[] { archived }, new object[] { live }, out string failure), failure);
			Assert.IsFalse(KingdomArchivedSettlementCodec.DisjointMutableGraphs(
				new object[] { archived }, new object[] { archived }, out failure));
			Assert.IsFalse(KingdomArchivedSettlementCodec.DisjointMutableGraphs(
				new object[] { archived, archived }, new object[0], out failure));
			var otherArchivedRoot = new System.Collections.Generic.List<object> { archived };
			Assert.IsFalse(KingdomArchivedSettlementCodec.DisjointMutableGraphs(
				new object[] { archived, otherArchivedRoot }, new object[0], out failure));
			var otherLiveRoot = new System.Collections.Generic.List<object> { live };
			Assert.IsFalse(KingdomArchivedSettlementCodec.DisjointMutableGraphs(
				new object[0], new object[] { live, otherLiveRoot }, out failure));
			string archiveSource = RealmArchiveSource();
			StringAssert.Contains("ChronicleEntries, OutsiderEntries, Haul, CarryBook",
				archiveSource);

			KingdomSettlement settlement = new KingdomSettlement();
			settlement.SettlementName = "bounded";
			Assert.IsTrue(KingdomArchivedSettlementCodec.TryEncode(settlement,
				out byte[] payload, out failure), failure);
			KingdomSettlement exactClone;
			Assert.IsTrue(KingdomArchivedSettlementCodec.TryClone(settlement,
				out exactClone, out failure), failure);
			Assert.IsTrue(KingdomArchivedSettlementCodec.ExactGraph(settlement,
				exactClone, out failure), failure);
			var sameRootAlias = new System.Collections.Generic.List<string> { "one" };
			KingdomLegacyRosterProjectionTestAccess.SetNames(settlement, sameRootAlias);
			KingdomLegacyRosterProjectionTestAccess.SetOrigins(settlement, sameRootAlias);
			Assert.IsFalse(KingdomArchivedSettlementCodec.TryEncode(settlement,
				out byte[] aliasedPayload, out failure));
			Assert.IsNull(aliasedPayload);
			KingdomLegacyRosterProjectionTestAccess.SetOrigins(settlement,
				new System.Collections.Generic.List<string>());
			int futureVersion = KingdomArchivedSettlementCodec.CurrentVersion + 1;
			payload[4] = (byte)futureVersion;
			payload[5] = 0; payload[6] = 0; payload[7] = 0;
			Assert.IsFalse(KingdomArchivedSettlementCodec.TryDecode(payload,
				out KingdomSettlement decoded, out int future, out failure));
			Assert.IsNull(decoded);
			Assert.AreEqual(futureVersion, future);

			var bindings = new Simulation.City.KingdomBindingRegistry();
			var jobs = new Simulation.City.KingdomJobRegistry();
			Assert.IsTrue(KingdomArchivedSettlementCodec.EmptyRegistries(bindings, jobs));
			bindings.Keys.Add(7);
			Assert.IsFalse(KingdomArchivedSettlementCodec.EmptyRegistries(bindings, jobs));
			var carry = new KingdomCarryBook();
			Assert.IsTrue(KingdomArchivedSettlementCodec.EmptyCarry(carry));
			carry.NextSequence++;
			Assert.IsFalse(KingdomArchivedSettlementCodec.EmptyCarry(carry));
		}

		[Test]
		public void ArchivedSettlementV1_StagesDormantGrowthAndRewritesAsV2()
		{
			KingdomSettlement legacy = new KingdomSettlement { SettlementName = "old ground" };
			Assert.IsTrue(KingdomLifecycleRules.BindSettlementIdentity(legacy.LifecycleBook,
				SettlementA, false, null, new List<string>()));
			legacy.LifecycleBook.LocusOption = KingdomLifecycleOptionState.Enabled;
			legacy.LifecycleBook.LocusOptionTick = 41L;
			legacy.LifecycleBook.FormatVersion =
				KingdomLifecycleRules.LegacyLifecycleFormatVersion;
			legacy.LifecycleBook.Growth = null;

			Assert.IsTrue(KingdomArchivedSettlementCodec.TryEncodeLegacyV1ForTests(legacy,
				out byte[] v1, out string failure), failure);
			Assert.AreEqual(KingdomArchivedSettlementCodec.LegacyVersion,
				BitConverter.ToInt32(v1, 4));
			byte[] wrappedV1Enum = (byte[])v1.Clone();
			int wrappedV1Offset = UniqueLongPair(wrappedV1Enum,
				(long)KingdomLifecycleOptionState.Enabled, 41L);
			wrappedV1Enum[wrappedV1Offset + 1] = 1;
			Assert.IsFalse(KingdomArchivedSettlementCodec.TryDecode(wrappedV1Enum,
				out KingdomSettlement wrappedV1, out int wrappedFuture, out failure));
			Assert.IsNull(wrappedV1);
			Assert.AreEqual(0, wrappedFuture);
			Assert.IsTrue(KingdomArchivedSettlementCodec.TryDecode(v1,
				out KingdomSettlement staged, out int future, out failure), failure);
			Assert.AreEqual(0, future);
			Assert.AreEqual(KingdomLifecycleRules.CurrentFormatVersion,
				staged.LifecycleBook.FormatVersion);
			Assert.AreEqual(SettlementA, staged.LifecycleBook.SettlementId);
			Assert.AreEqual(KingdomLifecycleOptionState.Enabled,
				staged.LifecycleBook.LocusOption);
			Assert.AreEqual(41L, staged.LifecycleBook.LocusOptionTick);
			Assert.IsNotNull(staged.LifecycleBook.Growth);
			Assert.IsTrue(staged.LifecycleBook.Growth.MigrationPending);
			Assert.IsFalse(KingdomLifecycleRules.CanOwnGrowthAuthority(
				staged.LifecycleBook));

			Assert.IsTrue(KingdomArchivedSettlementCodec.TryEncode(staged,
				out byte[] v2, out failure), failure);
			Assert.AreEqual(KingdomArchivedSettlementCodec.CurrentVersion,
				BitConverter.ToInt32(v2, 4));
			byte[] wrappedV2Enum = (byte[])v2.Clone();
			int wrappedV2Offset = UniqueLongPair(wrappedV2Enum,
				(long)KingdomLifecycleOptionState.Enabled, 41L);
			wrappedV2Enum[wrappedV2Offset + 1] = 1;
			Assert.IsFalse(KingdomArchivedSettlementCodec.TryDecode(wrappedV2Enum,
				out KingdomSettlement wrappedV2, out wrappedFuture, out failure));
			Assert.IsNull(wrappedV2);
			Assert.AreEqual(0, wrappedFuture);
			Assert.IsTrue(KingdomArchivedSettlementCodec.TryDecode(v2,
				out KingdomSettlement roundTrip, out future, out failure), failure);
			Assert.AreEqual(0, future);
			Assert.IsTrue(roundTrip.LifecycleBook.Growth.MigrationPending);
			Assert.AreEqual(SettlementA, roundTrip.LifecycleBook.SettlementId);
		}

		[Test]
		public void ArchivedSettlementV1_NullSlotDecodesAndReencodesExactly()
		{
			Assert.IsTrue(KingdomArchivedSettlementCodec.TryEncodeLegacyV1ForTests(
				null, out byte[] v1, out string failure), failure);
			Assert.IsTrue(KingdomArchivedSettlementCodec.TryDecode(v1,
				out KingdomSettlement decoded, out int futureVersion, out failure), failure);
			Assert.IsNull(decoded);
			Assert.AreEqual(0, futureVersion);
			Assert.IsTrue(KingdomArchivedSettlementCodec.TryEncodeLegacyV1ForTests(
				decoded, out byte[] exactV1, out failure), failure);
			CollectionAssert.AreEqual(v1, exactV1);
			Assert.IsTrue(KingdomArchivedSettlementCodec.TryEncode(decoded,
				out byte[] v2, out failure), failure);
			Assert.AreEqual(KingdomArchivedSettlementCodec.CurrentVersion,
				BitConverter.ToInt32(v2, 4));
			Assert.IsTrue(KingdomArchivedSettlementCodec.TryDecode(v2,
				out decoded, out futureVersion, out failure), failure);
			Assert.IsNull(decoded);
			Assert.AreEqual(0, futureVersion);
		}

		[Test]
		public void ArchivedSettlementV2_ShapePinsPersistedEnumContract()
		{
			Assert.IsTrue(KingdomArchivedSettlementCodec.TryEncode(
				new KingdomSettlement(), out byte[] v2, out string failure), failure);
			int shapeLength = BitConverter.ToInt32(v2, 8);
			string shape = System.Text.Encoding.UTF8.GetString(v2, 12, shapeLength);
			StringAssert.Contains(
				"enum:ThousandAndFirst.KingdomLifecycleOptionState<System.Byte>" +
				"{Disabled=1;Enabled=2;Unknown=0;};", shape);
		}

		[Test]
		public void ArchivedSettlementCodec_RejectsRuntimeSubclassesBeforeFieldLoss()
		{
			KingdomSettlement source = new HostileSettlement
			{
				SettlementName = "declared base, hostile runtime subtype"
			};
			Assert.IsFalse(KingdomArchivedSettlementCodec.TryEncode(source,
				out byte[] payload, out string failure));
			Assert.IsNull(payload);
			StringAssert.Contains("runtime type is not exact", failure);
		}

		[Test]
		public void ArchivedSettlementCodec_StopsAggregateWriteAtTwoMiB()
		{
			KingdomSettlement source = new KingdomSettlement
			{
				SettlementName = "aggregate cap"
			};
			string individuallyLegal = new string('x',
				KingdomArchivedSettlementCodec.MaxStringBytes);
			KingdomLegacyRosterProjectionTestAccess.SetNames(source, new List<string>(
				KingdomArchivedSettlementCodec.MaxCollectionCount));
			for (int i = 0; i < KingdomArchivedSettlementCodec.MaxCollectionCount; i++)
				KingdomLegacyRosterProjectionTestAccess.Names(source).Add(individuallyLegal);
			Assert.IsFalse(KingdomArchivedSettlementCodec.TryEncode(source,
				out byte[] payload, out string failure));
			Assert.IsNull(payload);
			StringAssert.Contains("aggregate cap reached before write", failure);
		}

		[Test]
		public void ArchivedSettlementCodec_RejectsNoncanonicalDictionaryComparer()
		{
			KingdomSettlement source = new KingdomSettlement
			{
				SettlementName = "comparer evidence",
				OriginCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
				{
					{ "MiXeD", 7 }
				}
			};
			Assert.IsTrue(source.OriginCounts.ContainsKey("mixed"));
			Assert.IsFalse(KingdomArchivedSettlementCodec.TryClone(source,
				out KingdomSettlement clone, out string failure));
			Assert.IsNull(clone);
			StringAssert.Contains("dictionary comparer is noncanonical", failure);
		}

		[Test]
		public void ArchivedSettlementCodec_AcceptsIndependentOrdinalDictionaries()
		{
			Dictionary<string, int> archived =
				new Dictionary<string, int>(StringComparer.Ordinal) { { "a", 1 } };
			Dictionary<string, int> live =
				new Dictionary<string, int>(StringComparer.Ordinal) { { "a", 1 } };
			Assert.IsTrue(KingdomArchivedSettlementCodec.DisjointMutableGraphs(
				new object[] { archived }, new object[] { live }, out string failure), failure);
		}

		[Test]
		public void ArchivedSettlementV1Writer_MatchesIndependent0501Golden()
		{
			const string settlementId = "settlement-golden-v1";
			const string zoneId = "zone-golden-v1";
			const string ownerId = "vessel-golden-v1";
			KingdomSettlement settlement = new KingdomSettlement
			{
				SettlementName = "archive-v1-golden",
				FoundedTick = 23L,
				PendingCrop = 2,
				PendingCropBlueprint = "Watervine"
			};
			Assert.IsTrue(KingdomLifecycleRules.BindSettlementIdentity(
				settlement.LifecycleBook, settlementId, false, null, new List<string>()));
			string operationId = KingdomLifecycleRules.OperationId(settlementId,
				KingdomLifecycleLane.PlainGuest, 1L);
			string resourceKey = KingdomLifecycleRules.ResourceKey(
				KingdomLifecycleResourceKind.WaterVessel, zoneId, ownerId);
			KingdomLifecycleOperation operation = new KingdomLifecycleOperation
			{
				Sequence = 1L, Id = operationId, Lane = KingdomLifecycleLane.PlainGuest,
				Action = KingdomLifecycleAction.OfferWater,
				Phase = KingdomLifecyclePhase.WaterIntent,
				CreatedTick = 41L, UpdatedTick = 42L, SettlementId = settlementId,
				ZoneId = zoneId, WaterRequested = 3, WaterOutstanding = 3,
				WaterState = KingdomLifecyclePhysicalState.Prepared,
				RemovalState = KingdomLifecyclePhysicalState.Skipped,
				EffectState = KingdomLifecyclePhysicalState.Skipped
			};
			operation.WaterLegs.Add(new KingdomLifecycleWaterLeg
			{
				OperationId = operationId, LeaseKey = resourceKey, OwnerId = ownerId,
				Blueprint = "Waterskin", ZoneId = zoneId, Capacity = 20,
				Before = 10, Delta = 3, After = 7, Composition = "water",
				ReceiptId = KingdomLifecycleRules.ChildId(operationId, "water-receipt", 0),
				ReceiptState = KingdomLifecyclePhysicalState.Prepared,
				State = KingdomLifecyclePhysicalState.Prepared
			});
			operation.ResourceLeases.Add(new KingdomLifecycleResourceLease
			{
				OperationId = operationId,
				Kind = KingdomLifecycleResourceKind.WaterVessel,
				ScopeId = zoneId, SubjectId = ownerId, Key = resourceKey,
				Before = 10L, Delta = -3L, After = 7L,
				BeforeRevision = 7L, AfterRevision = 8L,
				State = KingdomLifecycleLeaseState.Prepared
			});
			settlement.LifecycleBook.PlainGuest = operation;
			settlement.LifecycleBook.PlainGuestNextSequence = 2L;
			settlement.LifecycleBook.Resources.Add(new KingdomLifecycleResourceRevision
			{
				Kind = KingdomLifecycleResourceKind.WaterVessel, ScopeId = zoneId,
				SubjectId = ownerId, Key = resourceKey, Revision = 7L,
				ActiveOperationId = operationId
			});
			settlement.LifecycleBook.FormatVersion =
				KingdomLifecycleRules.LegacyLifecycleFormatVersion;
			settlement.LifecycleBook.Growth = null;

			Assert.IsTrue(KingdomArchivedSettlementCodec.TryEncodeLegacyV1ForTests(
				settlement, out byte[] payload, out string failure), failure);
			Assert.AreEqual(14623, payload.Length);
			using (SHA256 sha = SHA256.Create())
			{
				string digest = BitConverter.ToString(sha.ComputeHash(payload))
					.Replace("-", "").ToLowerInvariant();
				Assert.AreEqual(
					"9d5c49ccc95ec7033a38cfc88b4b9ab1c3f48af5f628af6edcb4fe47b8895690",
					digest);
			}
		}

		[Test]
		public void ArchivedSettlementV5WriterIsFrozenAndV6DefaultsNewOpportunityState()
		{
			KingdomSettlement settlement = new KingdomSettlement
			{
				SettlementName = "archive-v5-golden"
			};
			settlement.CultureCounts.Add("culture:test", 2);
			settlement.SpeciesCounts.Add("species:test", 3);
			settlement.IdentityCounts.Add("extension:test", 4);
			// These are deliberately populated. A genuine v5 writer must omit them rather than
			// letting today's reflected nested shape rewrite yesterday's bytes.
			settlement.City.PilgrimLoudness = 2;
			settlement.City.PilgrimState = (int)KingdomLocusRules.PilgrimState.Standing;
			settlement.City.PilgrimSequence = 9;
			settlement.City.PilgrimCauseTick = 12000L;
			settlement.City.PilgrimCause = "the Ides feast kept at old ground";
			settlement.City.PilgrimObjectId = "old-pilgrim-body";
			settlement.City.PilgrimName = "Aeru";
			settlement.City.PilgrimPlaceName = "old ground";
			settlement.City.PilgrimGreeted = 1;
			settlement.Ledger.ExpeditionLines.Add("an expedition returned");

			Assert.IsTrue(KingdomArchivedSettlementCodec.TryEncodeExtensionIdentityV5ForTests(
				settlement, out byte[] payload, out string failure), failure);
			Assert.AreEqual(KingdomArchivedSettlementCodec.ExtensionIdentityVersion,
				BitConverter.ToInt32(payload, 4));
			string digest;
			using (SHA256 sha = SHA256.Create())
			{
				digest = BitConverter.ToString(sha.ComputeHash(payload)).Replace("-", "")
					.ToLowerInvariant();
			}
			Assert.AreEqual(41560, payload.Length);
			Assert.AreEqual(
				"d6de95b160ff76bc47613aab53a6084b260a2fc0bd9cce889fce21fbda461358",
				digest);

			Assert.IsTrue(KingdomArchivedSettlementCodec.TryDecode(payload,
				out KingdomSettlement loaded, out int future, out failure), failure);
			Assert.AreEqual(0, future);
			Assert.AreEqual(4, loaded.IdentityCounts["extension:test"]);
			Assert.AreEqual((int)KingdomLocusRules.PilgrimState.None,
				loaded.City.PilgrimState);
			Assert.AreEqual(0, loaded.City.PilgrimSequence);
			Assert.AreEqual(0, loaded.Ledger.ExpeditionLines.Count);

			Assert.IsTrue(KingdomArchivedSettlementCodec.TryEncode(loaded,
				out byte[] current, out failure), failure);
			Assert.AreEqual(KingdomArchivedSettlementCodec.CurrentVersion,
				BitConverter.ToInt32(current, 4));
			Assert.IsTrue(KingdomArchivedSettlementCodec.TryDecode(current,
				out KingdomSettlement roundTrip, out future, out failure), failure);
			Assert.AreEqual((int)KingdomLocusRules.PilgrimState.None,
				roundTrip.City.PilgrimState);
			Assert.AreEqual(0, roundTrip.Ledger.ExpeditionLines.Count);
		}

		[Test]
		public void ArchivedSettlementV6RoundTripKeepsExactCausalPilgrimReceiptState()
		{
			KingdomSettlement settlement = new KingdomSettlement
			{
				SettlementName = "Tamsketh"
			};
			settlement.City.SettlementId = SettlementA;
			settlement.City.PilgrimLoudness = 1;
			settlement.City.PilgrimState = (int)KingdomLocusRules.PilgrimState.Standing;
			settlement.City.PilgrimSequence = 17;
			settlement.City.PilgrimCauseTick = 81000L;
			settlement.City.PilgrimCause = "the Ides feast kept at Tamsketh over starapple jam";
			settlement.City.PilgrimObjectId = "pilgrim-body-17";
			settlement.City.PilgrimName = "Aeru";
			settlement.City.PilgrimPlaceName = "Tamsketh";
			settlement.City.PilgrimGreeted = 1;

			Assert.IsTrue(KingdomArchivedSettlementCodec.TryClone(settlement,
				out KingdomSettlement clone, out string failure), failure);
			Assert.AreEqual(1, clone.City.PilgrimLoudness);
			Assert.AreEqual((int)KingdomLocusRules.PilgrimState.Standing,
				clone.City.PilgrimState);
			Assert.AreEqual(17, clone.City.PilgrimSequence);
			Assert.AreEqual(81000L, clone.City.PilgrimCauseTick);
			Assert.AreEqual(settlement.City.PilgrimCause, clone.City.PilgrimCause);
			Assert.AreEqual("pilgrim-body-17", clone.City.PilgrimObjectId);
			Assert.AreEqual("Aeru", clone.City.PilgrimName);
			Assert.AreEqual("Tamsketh", clone.City.PilgrimPlaceName);
			Assert.AreEqual(1, clone.City.PilgrimGreeted);
		}

		[Test]
		public void ArchivedSettlementV6WriterIsFrozenAndV8CarriesBehaviourSidecar()
		{
			KingdomSettlement settlement = new KingdomSettlement
			{
				SettlementName = "archive-v6-golden"
			};
			settlement.City.SettlementId = SettlementA;
			settlement.City.PilgrimLoudness = 1;
			settlement.City.PilgrimState = (int)KingdomLocusRules.PilgrimState.Standing;
			settlement.City.PilgrimSequence = 23;
			settlement.City.PilgrimCauseTick = 84000L;
			settlement.City.PilgrimCause = "a salvage song reached the old ground";
			settlement.City.PilgrimObjectId = "v6-pilgrim-body";
			settlement.City.PilgrimName = "Eshum";
			settlement.City.PilgrimPlaceName = "old ground";
			settlement.City.PilgrimGreeted = 1;
			settlement.Ledger.ExpeditionLines.Add("Eshum returned from rusted arches");

			KingdomBehaviourState behaviour;
			int kept;
			Assert.IsTrue(KingdomBehaviourRules.TryApplyResources(KingdomBehaviourState.Empty,
				"archive fixture", new[]
				{
					new KingdomResourceDefinition("ore", "ore", "FixtureOreStore", "", "", 7, 20)
				}, out behaviour, out kept));
			Assert.AreEqual(1, kept);
			Assert.IsTrue(KingdomBehaviourRules.TryEncode(behaviour,
				out settlement.City.ExtensionModel));
			Assert.IsTrue(KingdomHappeningCursorRules.TrySourceKey("archive-fixture",
				"Archive.Fixture", "Fixture.HappeningSource", out string sourceKey));
			Assert.IsTrue(KingdomHappeningCursorRules.TryAdvance("", sourceKey, 81234L,
				out long firstSince, out settlement.City.ExtensionHappeningCursors));
			Assert.AreEqual(0L, firstSince);

			Assert.IsTrue(KingdomArchivedSettlementCodec.TryEncodeSalvageV6ForTests(
				settlement, out byte[] payload, out string failure), failure);
			Assert.AreEqual(KingdomArchivedSettlementCodec.SalvageVersion,
				BitConverter.ToInt32(payload, 4));
			string digest;
			using (SHA256 sha = SHA256.Create())
			{
				digest = BitConverter.ToString(sha.ComputeHash(payload)).Replace("-", "")
					.ToLowerInvariant();
			}
			Assert.AreEqual(42003, payload.Length, "PIN_V6_LENGTH: " + payload.Length);
			Assert.AreEqual(
				"dcdf333a91c13964b2307702e84d27478cf46b7f82531a4261c740c07f3f46bd",
				digest, "PIN_V6_SHA: " + digest);

			Assert.IsTrue(KingdomArchivedSettlementCodec.TryDecode(payload,
				out KingdomSettlement loadedV6, out int future, out failure), failure);
			Assert.AreEqual(0, future);
			Assert.AreEqual(23, loadedV6.City.PilgrimSequence);
			Assert.AreEqual(1, loadedV6.Ledger.ExpeditionLines.Count);
			Assert.AreEqual("", loadedV6.City.ExtensionModel,
				"v6 must default the v7 sidecar rather than reinterpret old bytes");
			Assert.AreEqual("", loadedV6.City.ExtensionHappeningCursors);

			Assert.IsTrue(KingdomArchivedSettlementCodec.TryEncode(settlement,
				out byte[] current, out failure), failure);
			Assert.AreEqual(KingdomArchivedSettlementCodec.CurrentVersion,
				BitConverter.ToInt32(current, 4));
			Assert.IsTrue(KingdomArchivedSettlementCodec.TryDecode(current,
				out KingdomSettlement loadedV8, out future, out failure), failure);
			Assert.AreEqual(settlement.City.ExtensionModel, loadedV8.City.ExtensionModel);
			Assert.AreEqual("", loadedV8.City.HappeningModel);
			Assert.AreEqual(settlement.City.ExtensionHappeningCursors,
				loadedV8.City.ExtensionHappeningCursors);
		}

		[Test]
		public void ArchivedSettlementV7WriterIsFrozenAndV8CarriesPhysicalHappeningSidecar()
		{
			KingdomSettlement settlement = new KingdomSettlement
			{
				SettlementName = "archive-v7-golden"
			};
			settlement.City.SettlementId = SettlementA;
			KingdomBehaviourState behaviour;
			Assert.IsTrue(KingdomBehaviourRules.TryApplyResources(KingdomBehaviourState.Empty,
				"v7 fixture", new[]
				{
					new KingdomResourceDefinition("salt", "salt", "FixtureSaltStore", "", "",
						4, 12)
				}, out behaviour, out int kept));
			Assert.AreEqual(1, kept);
			Assert.IsTrue(KingdomBehaviourRules.TryEncode(behaviour,
				out settlement.City.ExtensionModel));

			KingdomHappeningParticipant person = new KingdomHappeningParticipant(7,
				"body-7", "Eshum", "home-2", "zone-a.10.10", 10, 10, 12, 12, 3,
				(int)KingdomWorkKind.Growing, false, false, true);
			KingdomHappeningProposal proposal = new KingdomHappeningProposal(
				"taf:happening:" + SettlementA + ":1:84000:7:8:0",
				KingdomPhysicalHappeningKind.Wedding, 84000L, 7, 8, 0, SettlementA,
				"zone-a", "fixture-bench", "r_KingdomBench", 11, 11, true, false,
				"Eshum and Nara were married", "word reached us of Eshum and Nara",
				"", "", "the water was shared", "", "", "gathering bench", "",
				new[] { person });
			Assert.IsTrue(KingdomHappeningLifecycleRules.TryOpen(
				KingdomHappeningLifecycleBook.Empty, proposal, 84001L,
				out KingdomHappeningLifecycleBook lifecycle,
				out KingdomHappeningLifecycleFault lifecycleFault), lifecycleFault.ToString());
			Assert.IsTrue(KingdomHappeningLifecycleRules.TryEncode(lifecycle,
				out settlement.City.HappeningModel));

			Assert.IsTrue(KingdomArchivedSettlementCodec.TryEncodeBehaviourV7ForTests(
				settlement, out byte[] v7, out string failure), failure);
			Assert.AreEqual(KingdomArchivedSettlementCodec.BehaviourVersion,
				BitConverter.ToInt32(v7, 4));
			string digest;
			using (SHA256 sha = SHA256.Create())
				digest = BitConverter.ToString(sha.ComputeHash(v7)).Replace("-", "")
					.ToLowerInvariant();
			Assert.AreEqual(42040, v7.Length, "PIN_V7_LENGTH: " + v7.Length);
			Assert.AreEqual(
				"e10ba08efb5da6c8aeb45e87dbb08dd132963e5b7d00fcfc18647e5b55d2eb87",
				digest, "PIN_V7_SHA: " + digest);

			Assert.IsTrue(KingdomArchivedSettlementCodec.TryDecode(v7,
				out KingdomSettlement loadedV7, out int future, out failure), failure);
			Assert.AreEqual(0, future);
			Assert.AreEqual(settlement.City.ExtensionModel, loadedV7.City.ExtensionModel);
			Assert.AreEqual("", loadedV7.City.HappeningModel,
				"v7 must default v8 lifecycle authority rather than reinterpret old bytes");

			Assert.IsTrue(KingdomArchivedSettlementCodec.TryEncode(settlement,
				out byte[] v8, out failure), failure);
			Assert.AreEqual(KingdomArchivedSettlementCodec.CurrentVersion,
				BitConverter.ToInt32(v8, 4));
			Assert.IsTrue(KingdomArchivedSettlementCodec.TryDecode(v8,
				out KingdomSettlement loadedV8, out future, out failure), failure);
			Assert.AreEqual(settlement.City.ExtensionModel, loadedV8.City.ExtensionModel);
			Assert.AreEqual(settlement.City.HappeningModel, loadedV8.City.HappeningModel);
		}

		[Test]
		public void ArchivedSettlementV8ToV13WritersMigrateToV17()
		{
			KingdomSettlement settlement = new KingdomSettlement
			{
				SettlementName = "archive-v8-v10-golden",
				OfficeHolderResidentId = 77
			};
			Assert.IsTrue(KingdomLifecycleRules.BindSettlementIdentity(
				settlement.LifecycleBook, SettlementA, false, null, new List<string>()));
			settlement.City.SettlementId = SettlementA;
			settlement.City.HappeningModel = "physical-happening-v8-fixture";
			settlement.City.ResidentOrigins.Add("provenance:v11");
			settlement.City.ResidentArrived.Add("the Ides of Uulu Ut, 218 AR");
			Assert.IsTrue(KingdomHappeningCursorRules.TrySourceKey("archive-v12-fixture",
				"Archive.V12", "Fixture.Cursor", out string cursorSource));
			Assert.IsTrue(KingdomHappeningCursorRules.TryAdvance("", cursorSource, 91234L,
				out long cursorSince, out settlement.City.ExtensionHappeningCursors));
			Assert.AreEqual(0L, cursorSince);
			settlement.LifecycleBook.Growth.ArrivalCandidate =
				new KingdomGrowthArrivalCandidate
				{
					Sequence = 1L,
					Id = "candidate-v11-fields",
					SettlementId = SettlementA,
					Phase = KingdomGrowthArrivalCandidatePhase.Prepared,
					EvidencePhase = KingdomGrowthArrivalCandidatePhase.Prepared,
					LegacySemanticPlan = false,
					SemanticPlanVersion = 1,
					SemanticStreamId = "semantic-stream-v11",
					SemanticEventKind = 17U,
					PlannedOrigin = "pilgrim",
					PlannedCreed = "Water",
					PlannedName = "Eshum",
					PlannedArrived = "the Ides of Uulu Ut, 218 AR",
					ArrivalX = 12,
					ArrivalY = 9
				};

			KingdomLifecycleOperation warning = new KingdomLifecycleOperation
			{
				Lane = KingdomLifecycleLane.Raid,
				Action = KingdomLifecycleAction.RaidWarning,
				SettlementId = SettlementA,
				ZoneId = "zone-v10",
				Origin = "archive-v10-source",
				ObjectName = "authored act",
				Faction = "Snapjaws",
				DisplayFaction = "salt-road scouts",
				Creed = "explicit-slight",
				Detail = "specific authored evidence",
				ArrivalText = "zone-source",
				Target = 1,
				Count = 2,
				CreatedTick = 100L,
				DepartTick = 200L,
				PlunderRequested = 6,
				Kind = 24,
				Blueprint = "snapjaw-foragers"
			};
			warning.ObjectId = KingdomRaidIncidentRules.GrievanceId(warning.Origin);
			warning.ObjectMarker = KingdomRaidIncidentRules.IncidentId(warning.ObjectId);
			Assert.IsTrue(KingdomRaidIncidentRules.TryApply(
				settlement.LifecycleBook.RaidLedger, warning,
				out KingdomRaidLedger raid));
			KingdomRaidIncident incident = KingdomRaidIncidentRules.Active(raid);
			KingdomLifecycleOperation delivery = new KingdomLifecycleOperation
			{
				Id = KingdomLifecycleRules.ChildId(incident.Id, "test-delivery", 0),
				Lane = KingdomLifecycleLane.Raid,
				Action = KingdomLifecycleAction.RaidDeliverDemand,
				SettlementId = SettlementA,
				ZoneId = incident.TargetZoneId,
				ObjectId = incident.Id,
				Faction = incident.AttackerFactionId,
				CreatedTick = 101L,
				Origin = incident.DemandChannelId,
				Target = 1,
				ObjectMarker = KingdomRaidIncidentRules.DemandObjectId(
					incident.DemandChannelId, 1),
				Count = 1,
				Blueprint = "r_KingdomSnapjawRaidDemand"
			};
			Assert.IsTrue(KingdomRaidIncidentRules.TryApply(raid, delivery, out raid));
			incident = KingdomRaidIncidentRules.Active(raid);
			KingdomLifecycleOperation acknowledgement = new KingdomLifecycleOperation
			{
				Id = KingdomLifecycleRules.ChildId(incident.Id, "test-ack", 0),
				Lane = KingdomLifecycleLane.Raid,
				Action = KingdomLifecycleAction.RaidAcknowledgeDemand,
				SettlementId = SettlementA,
				ZoneId = incident.TargetZoneId,
				ObjectId = incident.Id,
				Faction = incident.AttackerFactionId,
				CreatedTick = 102L,
				Origin = incident.DemandObjectId,
				DepartTick = 202L
			};
			Assert.IsTrue(KingdomRaidIncidentRules.TryApply(raid, acknowledgement, out raid));
			incident = KingdomRaidIncidentRules.Active(raid);
			KingdomLifecycleOperation muster = new KingdomLifecycleOperation
			{
				Id = KingdomLifecycleRules.ChildId(incident.Id, "test-muster", 0),
				Lane = KingdomLifecycleLane.Raid,
				Action = KingdomLifecycleAction.RaidFortify,
				SettlementId = SettlementA,
				ZoneId = incident.TargetZoneId,
				ObjectId = incident.Id,
				Faction = incident.AttackerFactionId,
				CreatedTick = 103L,
				Detail = "R1;101=2[]",
				Defence = 2
			};
			Assert.IsTrue(KingdomRaidIncidentRules.TryApply(raid, muster, out raid));
			settlement.LifecycleBook.RaidLedger = raid;
			Assert.IsTrue(KingdomRaidIncidentRules.ValidLedger(raid));

			Assert.IsTrue(KingdomArchivedSettlementCodec.TryEncodePhysicalHappeningV8ForTests(
				settlement, out byte[] v8, out string failure), failure);
			Assert.IsTrue(KingdomArchivedSettlementCodec.TryEncodeExactLogisticsV9ForTests(
				settlement, out byte[] v9, out failure), failure);
			Assert.IsTrue(KingdomArchivedSettlementCodec.TryEncodeDefensiveReservationV10ForTests(
				settlement, out byte[] v10, out failure), failure);
			Assert.IsTrue(KingdomArchivedSettlementCodec.TryEncodeSemanticSelectionV11ForTests(
				settlement, out byte[] v11, out failure), failure);
			Assert.IsTrue(KingdomArchivedSettlementCodec.TryEncodeHappeningCursorV12ForTests(
				settlement, out byte[] v12, out failure), failure);

			Assert.AreEqual(44774, v8.Length, "PIN_V8_LENGTH: " + v8.Length);
			Assert.AreEqual(
				"194a3b9943626a3a2c693301c27070fbee9d0c0424196e0ec4cf66b8da9a4443",
				Sha256Hex(v8), "archive-v8 bytes changed");
			Assert.AreEqual(44774, v9.Length, "PIN_V9_LENGTH: " + v9.Length);
			Assert.AreEqual(
				"c23391d27e39f13218f880f14499cdbb107213ff5e64a828c87c484c85782315",
				Sha256Hex(v9), "archive-v9 bytes changed");
			Assert.AreEqual(44958, v10.Length, "PIN_V10_LENGTH: " + v10.Length);
			Assert.AreEqual(
				"5cb0d3c7677e67e329523cec52d875c8e566b5f1357e2b29b58681bc09625aca",
				Sha256Hex(v10), "archive-v10 bytes changed");

			AssertHistoricalV8ToV10Migration(v8,
				KingdomArchivedSettlementCodec.PhysicalHappeningVersion, false);
			AssertHistoricalV8ToV10Migration(v9,
				KingdomArchivedSettlementCodec.ExactLogisticsVersion, false);
			AssertHistoricalV8ToV10Migration(v10,
				KingdomArchivedSettlementCodec.DefensiveReservationVersion, true);
			Assert.IsTrue(KingdomArchivedSettlementCodec.TryDecode(v11,
				out KingdomSettlement migratedV11, out int futureV11, out failure), failure);
			Assert.AreEqual(0, futureV11);
			Assert.AreEqual("", migratedV11.City.ExtensionHappeningCursors,
				"v11 predates per-source cursors and must default rather than reinterpret bytes");
			AssertNoArchivedFirstGuestAuthority(migratedV11);

			Assert.AreEqual(KingdomArchivedSettlementCodec.HappeningCursorVersion,
				BitConverter.ToInt32(v12, 4));
			Assert.IsTrue(KingdomArchivedSettlementCodec.TryDecode(v12,
				out KingdomSettlement migratedV12, out int futureV12, out failure), failure);
			Assert.AreEqual(0, futureV12);
			Assert.AreEqual(settlement.City.ExtensionHappeningCursors,
				migratedV12.City.ExtensionHappeningCursors);
			AssertNoArchivedFirstGuestAuthority(migratedV12);
			Assert.IsTrue(KingdomArchivedSettlementCodec.TryEncodeHappeningCursorV12ForTests(
				migratedV12, out byte[] repeatedV12, out failure), failure);
			CollectionAssert.AreEqual(v12, repeatedV12,
				"the frozen v12 producer must not adopt v13 interpretation");
			Assert.IsTrue(KingdomArchivedSettlementCodec.TryEncode(migratedV12,
				out byte[] v17, out failure), failure);
			Assert.AreEqual(KingdomArchivedSettlementCodec.ArrivalCadenceVersion,
				BitConverter.ToInt32(v17, 4));
			Assert.IsTrue(KingdomArchivedSettlementCodec.TryDecode(v17,
				out KingdomSettlement roundTripV17, out int futureV17, out failure), failure);
			Assert.AreEqual(0, futureV17);
			Assert.AreEqual(migratedV12.City.ExtensionHappeningCursors,
				roundTripV17.City.ExtensionHappeningCursors);
		}

		[Test]
		public void ArchivedSettlementV13DefaultsAndV14RoundTripsCivicAuthorities()
		{
			const string realm = "taf:realm:archive-civic";
			const string settlementId = "taf:settlement:archive-civic";
			KingdomSettlement settlement = new KingdomSettlement
			{
				SettlementName = "Archive Civic"
			};
			settlement.City.SettlementId = settlementId;
			Assert.IsTrue(KingdomNamedCookRules.TryPrepare(realm, settlementId,
				settlement.SettlementName, 7, "Ari", "body-7", 1, 100L,
				out KingdomNamedCookReceipt cook, out string failure), failure);
			Assert.IsTrue(KingdomAssentingMootRules.TryPrepare(realm, settlementId,
				settlement.SettlementName, "zone-civic", "building-civic", "lot-civic",
				900, 1, 100L, out KingdomAssentingMootReceipt moot, out failure), failure);
			Assert.IsTrue(KingdomAssentingMootRules.TryChangeMember(moot,
				KingdomAssentingMootRole.Assent, true, 7, "Ari", "body-7", 101L,
				out moot, out failure), failure);
			settlement.City.NamedCook = cook;
			settlement.City.AssentingMoot = moot;

			Assert.IsTrue(KingdomArchivedSettlementCodec.TryEncodeDeliveryDomainV13ForTests(
				settlement, out byte[] v13, out failure), failure);
			Assert.AreEqual(KingdomArchivedSettlementCodec.DeliveryDomainVersion,
				BitConverter.ToInt32(v13, 4));
			Assert.IsTrue(KingdomArchivedSettlementCodec.TryDecode(v13,
				out KingdomSettlement migrated, out int future, out failure), failure);
			Assert.AreEqual(0, future);
			Assert.AreEqual(KingdomNamedCookPhase.None, migrated.City.NamedCook.Phase);
			Assert.AreEqual(KingdomAssentingMootPhase.None, migrated.City.AssentingMoot.Phase);
			AssertNoArchivedFirstGuestAuthority(migrated);
			Assert.IsTrue(KingdomArchivedSettlementCodec.TryEncodeDeliveryDomainV13ForTests(
				migrated, out byte[] repeatedV13, out failure), failure);
			CollectionAssert.AreEqual(v13, repeatedV13,
				"frozen v13 bytes cannot acquire post-v13 civic authority");

			Assert.IsTrue(KingdomArchivedSettlementCodec.TryEncodeCivicAuthorityV14ForTests(settlement,
				out byte[] v14, out failure), failure);
			Assert.AreEqual(KingdomArchivedSettlementCodec.CivicAuthorityVersion,
				BitConverter.ToInt32(v14, 4));
			Assert.IsTrue(KingdomArchivedSettlementCodec.TryDecode(v14,
				out KingdomSettlement restored, out future, out failure), failure);
			Assert.AreEqual(0, future);
			Assert.AreEqual(cook.RecipeId, restored.City.NamedCook.RecipeId);
			Assert.AreEqual(moot.AuthorityId, restored.City.AssentingMoot.AuthorityId);
			CollectionAssert.AreEqual(moot.AssentResidentIds,
				restored.City.AssentingMoot.AssentResidentIds);
			AssertNoArchivedFirstGuestAuthority(restored);
			Assert.IsFalse(ReferenceEquals(settlement.City.NamedCook,
				restored.City.NamedCook));
			Assert.IsFalse(ReferenceEquals(settlement.City.AssentingMoot.AssentResidentIds,
				restored.City.AssentingMoot.AssentResidentIds));

			settlement.City.NamedCook.RecipeId = "tampered";
			Assert.IsFalse(KingdomArchivedSettlementCodec.TryEncode(settlement,
				out byte[] _, out failure));
			StringAssert.Contains("civic authority is invalid", failure);
		}

		[Test]
		public void ArchiveCloneRetiresLegacyNotableAndPassiveFoodEconomy()
		{
			KingdomSettlement legacy = new KingdomSettlement
			{
				SettlementName = "Archive Shade",
				NotableShade = 17,
				MealShade = 1,
				HungerStreak = 4,
				Famished = true,
				ScrapsAnnounced = true
			};
			Assert.IsTrue(KingdomArchivedSettlementCodec.TryClone(legacy,
				out KingdomSettlement clone, out string failure), failure);
			Assert.AreEqual(17, legacy.NotableShade,
				"read-only archive preparation must not mutate its source object");
			Assert.AreEqual(0, clone.NotableShade);
			Assert.AreEqual(0, clone.MealShade);
			Assert.AreEqual(0, clone.HungerStreak);
			Assert.IsFalse(clone.Famished);
			Assert.IsFalse(clone.ScrapsAnnounced);
			Assert.IsTrue(KingdomArchivedSettlementCodec.TryEncode(legacy,
				out byte[] payload, out failure), failure);
			Assert.IsTrue(KingdomArchivedSettlementCodec.TryDecode(payload,
				out KingdomSettlement restored, out int future, out failure), failure);
			Assert.AreEqual(0, future);
			Assert.AreEqual(0, restored.NotableShade);
			Assert.AreEqual(0, restored.MealShade);
			Assert.AreEqual(0, restored.HungerStreak);
			Assert.IsFalse(restored.Famished);
		}

		private static void AssertHistoricalV8ToV10Migration(byte[] payload,
			int historicalVersion, bool retainsExactReservation)
		{
			Assert.AreEqual(historicalVersion, BitConverter.ToInt32(payload, 4));
			Assert.IsTrue(KingdomArchivedSettlementCodec.TryDecode(payload,
				out KingdomSettlement migrated, out int futureVersion, out string failure), failure);
			Assert.AreEqual(0, futureVersion);
			Assert.AreEqual(0, migrated.OfficeHolderResidentId);
			Assert.AreEqual("physical-happening-v8-fixture", migrated.City.HappeningModel);
			Assert.AreEqual(KingdomCityRules.SchemaVersion, migrated.City.SchemaVersion,
				"historical archive decode must complete city-v2 migration at its own boundary");
			Assert.AreEqual(0, migrated.City.ResidentOrigins.Count);
			Assert.AreEqual(0, migrated.City.ResidentArrived.Count);
			Assert.AreEqual(KingdomLifecycleRules.CurrentFormatVersion,
				migrated.LifecycleBook.FormatVersion);
			Assert.AreEqual(KingdomLifecycleRules.CurrentGrowthFormatVersion,
				migrated.LifecycleBook.Growth.FormatVersion);
			KingdomGrowthArrivalCandidate candidate =
				migrated.LifecycleBook.Growth.ArrivalCandidate;
			Assert.IsNotNull(candidate);
			Assert.IsTrue(candidate.LegacySemanticPlan);
			Assert.AreEqual(0, candidate.SemanticPlanVersion);
			Assert.IsNull(candidate.SemanticStreamId);
			Assert.AreEqual(0U, candidate.SemanticEventKind);
			Assert.IsNull(candidate.PlannedOrigin);
			Assert.IsNull(candidate.PlannedCreed);
			Assert.IsNull(candidate.PlannedName);
			Assert.IsNull(candidate.PlannedArrived);
			Assert.AreEqual(-1, candidate.ArrivalX);
			Assert.AreEqual(-1, candidate.ArrivalY);
			Assert.IsNull(candidate.FirstGuest,
				"pre-v11 sparse Prepared archives carry no first-guest choice evidence");
			Assert.IsFalse(candidate.LegacyAutomaticRecovery,
				"archive-column absence must not authorize legacy first-guest interposition");

			Assert.AreEqual(KingdomRaidLedger.CurrentVersion,
				migrated.LifecycleBook.RaidLedger.Version);
			Assert.IsTrue(KingdomRaidIncidentRules.ValidLedger(
				migrated.LifecycleBook.RaidLedger));
			KingdomRaidIncident incident = KingdomRaidIncidentRules.Active(
				migrated.LifecycleBook.RaidLedger);
			Assert.IsNotNull(incident);
			if (retainsExactReservation)
			{
				Assert.AreEqual(KingdomRaidIncidentState.Fortified, incident.State);
				Assert.AreEqual(KingdomRaidResponse.Fortify, incident.Response);
				Assert.AreEqual(KingdomRaidIncidentRules.CurrentDefenceReservationVersion,
					incident.DefenceReservationVersion);
				Assert.AreEqual(1, incident.DefenceReservations.Count);
				Assert.AreEqual(101, incident.DefenceReservations[0].WorkId);
				Assert.AreEqual(2, incident.DefenceReservations[0].FrozenScore);
				Assert.AreEqual(0, incident.DefenceReservations[0].CrewSemanticIds.Count);
			}
			else
			{
				Assert.AreEqual(KingdomRaidIncidentState.ConfrontationReady, incident.State);
				Assert.AreEqual(KingdomRaidResponse.None, incident.Response);
				Assert.AreEqual(0, incident.DefenceReservationVersion);
				Assert.AreEqual(0, incident.DefenceReservations.Count);
				Assert.AreEqual(0, incident.DefenceEstimate);
				Assert.IsNull(incident.DefenceCommitment);
				StringAssert.Contains("Every answer is open again", incident.LastNotice);
			}

			Assert.IsTrue(KingdomArchivedSettlementCodec.TryEncode(migrated,
				out byte[] current, out failure), failure);
			Assert.AreEqual(KingdomArchivedSettlementCodec.CurrentVersion,
				BitConverter.ToInt32(current, 4));
			Assert.IsTrue(KingdomArchivedSettlementCodec.TryDecode(current,
				out KingdomSettlement currentRoundTrip, out futureVersion, out failure), failure);
			Assert.AreEqual(0, futureVersion);
			Assert.AreEqual(KingdomCityRules.SchemaVersion,
				currentRoundTrip.City.SchemaVersion);
			Assert.IsTrue(currentRoundTrip.LifecycleBook.Growth.ArrivalCandidate
				.LegacySemanticPlan);
			Assert.IsTrue(KingdomRaidIncidentRules.ValidLedger(
				currentRoundTrip.LifecycleBook.RaidLedger));
		}

		private static void AssertNoArchivedFirstGuestAuthority(KingdomSettlement settlement)
		{
			KingdomGrowthArrivalCandidate candidate =
				settlement?.LifecycleBook?.Growth?.ArrivalCandidate;
			if (candidate == null) return;
			Assert.IsNull(candidate.FirstGuest,
				"pre-v15 archive absence cannot become current first-guest choice evidence");
			Assert.IsFalse(candidate.LegacyAutomaticRecovery,
				"sparse archive absence cannot authorize legacy first-guest interposition");
		}

		private static string Sha256Hex(byte[] payload)
		{
			using (SHA256 sha = SHA256.Create())
				return BitConverter.ToString(sha.ComputeHash(payload)).Replace("-", "")
					.ToLowerInvariant();
		}

		[Test]
		public void ArchivedSettlementV2_DeepCopiesOpaqueGrowthEvidence()
		{
			KingdomSettlement source = new KingdomSettlement { SettlementName = "new ground" };
			Assert.IsTrue(KingdomLifecycleRules.BindSettlementIdentity(source.LifecycleBook,
				SettlementB, false, null, new List<string>()));
			source.LifecycleBook.Growth.Quarantined = true;
			source.LifecycleBook.Growth.Fault = "future nested growth";
			source.LifecycleBook.Growth.OpaqueWireVersion =
				KingdomLifecycleRules.CurrentGrowthFormatVersion + 1;
			source.LifecycleBook.Growth.OpaquePayload = new byte[] { 9, 7, 5, 3, 1 };

			Assert.IsTrue(KingdomArchivedSettlementCodec.TryClone(source,
				out KingdomSettlement clone, out string failure), failure);
			Assert.IsTrue(KingdomArchivedSettlementCodec.ExactGraph(source, clone,
				out failure), failure);
			Assert.AreNotSame(source.LifecycleBook.Growth.OpaquePayload,
				clone.LifecycleBook.Growth.OpaquePayload);
			CollectionAssert.AreEqual(source.LifecycleBook.Growth.OpaquePayload,
				clone.LifecycleBook.Growth.OpaquePayload);
			Assert.IsTrue(KingdomArchivedSettlementCodec.DisjointMutableGraphs(
				new object[] { source }, new object[] { clone }, out failure), failure);

			clone.LifecycleBook.Growth.OpaquePayload[0] = 0;
			Assert.AreEqual(9, source.LifecycleBook.Growth.OpaquePayload[0]);
			clone.LifecycleBook.Growth.OpaquePayload =
				source.LifecycleBook.Growth.OpaquePayload;
			Assert.IsFalse(KingdomArchivedSettlementCodec.DisjointMutableGraphs(
				new object[] { source }, new object[] { clone }, out failure));

			source.LifecycleBook.Growth.OpaquePayload =
				new byte[KingdomArchivedSettlementCodec.MaxByteArrayBytes + 1];
			Assert.IsFalse(KingdomArchivedSettlementCodec.TryEncode(source,
				out byte[] oversized, out failure));
			Assert.IsNull(oversized);
		}

		private static int UniqueLongPair(byte[] Payload, long First, long Second)
		{
			byte[] first = BitConverter.GetBytes(First);
			byte[] second = BitConverter.GetBytes(Second);
			int found = -1;
			for (int i = 0; i <= Payload.Length - 16; i++)
			{
				bool same = true;
				for (int j = 0; j < 8 && same; j++)
					same = Payload[i + j] == first[j] && Payload[i + 8 + j] == second[j];
				if (!same) continue;
				Assert.AreEqual(-1, found, "long-pair marker must be unique");
				found = i;
			}
			Assert.GreaterOrEqual(found, 0, "long-pair marker was absent");
			return found;
		}

		[Test]
		public void DormantLifecycleBooksArePersistedAtCityAndRealmScope()
		{
			string system = KingdomSystemLogicalSource.Read();
			string settlement = KingdomSettlementLogicalSource.Read();
			StringAssert.Contains("public KingdomLifecycleBook LifecycleBook", system);
			StringAssert.Contains("public KingdomLifecycleBook LifecycleBook", settlement);
			StringAssert.Contains("public KingdomCarryBook CarryBook", system);
			StringAssert.Contains("BindSettlementIdentity(LifecycleBook", system);
			StringAssert.DoesNotContain("ExistingIds: null", system);
			StringAssert.Contains("BindCarryIdentity(CarryBook, RealmId", system);
			StringAssert.DoesNotContain("CarryBook.RealmId = RealmId", system);
			StringAssert.Contains("TryCloneCarry(Archive.CarryBook", system);
			StringAssert.Contains("CarryBook = carry", system);
		}

		[Test]
		public void FirstFoundingCarryIdentityIsBoundAtomicallyBeforeAuthorityCheck()
		{
			string system = KingdomSystemLogicalSource.Read();
			int method = system.IndexOf("internal bool TryBindFirstFoundingIdentity",
				StringComparison.Ordinal);
			int next = system.IndexOf("internal bool FirstIdentityMatches", method,
				StringComparison.Ordinal);
			Assert.Greater(method, -1);
			Assert.Greater(next, method);
			string body = system.Substring(method, next - method);
			int prepare = body.IndexOf("TryPrepareFirstIdentityBooks(LifecycleBook",
				StringComparison.Ordinal);
			int publishRealm = body.IndexOf("RealmId = realm", StringComparison.Ordinal);
			int publishLifecycle = body.IndexOf("LifecycleBook = preparedLifecycle",
				StringComparison.Ordinal);
			Assert.Greater(prepare, -1);
			Assert.Greater(publishRealm, prepare);
			Assert.Greater(publishLifecycle, publishRealm);
			StringAssert.DoesNotContain("CarryBook.RealmId = RealmId", body);
			StringAssert.Contains("CarryBook = preparedCarry", body);
		}

		[Test]
		public void LiveConsumersContainNoMutableNameIdentityFallback()
		{
			string[] files = new[]
			{
				Path.Combine("Chronicle", "KingdomChronicle.cs"),
				Path.Combine("Core", "KingdomConversion.cs"),
				Path.Combine("Experience", "KingdomGuestbook.cs"),
				Path.Combine("Experience", "KingdomFaith.cs"),
				Path.Combine("Experience", "KingdomVoices.cs"),
				Path.Combine("Experience", "KingdomCeremony.cs"),
				Path.Combine("Experience", "KingdomCitizenRite.cs"),
				Path.Combine("Experience", "KingdomCitizenRiteRules.cs"),
				Path.Combine("Core", "KingdomSealRules.cs"),
				Path.Combine("Core", "KingdomSealRules.Text.cs"),
				Path.Combine("Core", "KingdomSealRules.Capture.cs"),
				Path.Combine("Core", "KingdomSealRules.Selection.cs"),
				Path.Combine("Core", "KingdomSealRules.Ground.cs"),
				Path.Combine("Growth", "KingdomPlot.cs"),
				Path.Combine("Growth", "KingdomSubsidence.cs"),
				Path.Combine("Growth", "KingdomWear.cs"),
				Path.Combine("Simulation", "City", "KingdomHappenings.cs"),
				Path.Combine("Quests", "KingdomBounty.cs"),
				Path.Combine("Quests", "KingdomPetitions.cs")
			};
			foreach (string file in files)
			{
				string source = file == Path.Combine("Chronicle", "KingdomChronicle.cs")
					? KingdomChronicleLogicalSource.Read()
					: file == Path.Combine("Core", "KingdomConversion.cs")
						? ConversionSource()
					: file == Path.Combine("Experience", "KingdomGuestbook.cs")
						? KingdomGuestbookLogicalSource.Read()
					: file == Path.Combine("Experience", "KingdomFaith.cs")
						? KingdomFaithLogicalSource.Read()
					: file == Path.Combine("Experience", "KingdomCeremony.cs")
						? KingdomCeremonyLogicalSource.Read()
					: file == Path.Combine("Experience", "KingdomCitizenRite.cs")
					? KingdomCitizenRiteLogicalSource.Read()
					: file == Path.Combine("Quests", "KingdomBounty.cs")
						? KingdomBountyLogicalSource.Read()
						: file == Path.Combine("Growth", "KingdomWear.cs")
							? KingdomWearLogicalSource.Read()
						: file == Path.Combine("Growth", "KingdomSubsidence.cs")
							? KingdomSubsidenceLogicalSource.Read()
							: file == Path.Combine("Simulation", "City", "KingdomHappenings.cs")
								? KingdomHappeningsLogicalSource.Read()
								: Source(file);
				Assert.IsFalse(source.Contains(
					"KingdomChronicle.SettlementId(System.KingdomFactionName)"), file);
				Assert.IsFalse(source.Contains("LegacyOriginIdentity("), file);
			}
			string porters = KingdomPortersLogicalSource.Read();
			Assert.IsFalse(porters.Contains(
				"KingdomChronicle.SettlementId(System.KingdomFactionName)"), "KingdomPorters");
			Assert.IsFalse(porters.Contains("LegacyOriginIdentity("), "KingdomPorters");
			string lab = KingdomLabLogicalSource.Read();
			Assert.IsFalse(lab.Contains(
				"KingdomChronicle.SettlementId(System.KingdomFactionName)"));
			Assert.IsFalse(lab.Contains("LegacyOriginIdentity("));
			string chronicle = KingdomChronicleLogicalSource.Read();
			Assert.IsFalse(chronicle.Contains("SettlementIdPrefix"));
			Assert.IsFalse(chronicle.Contains("SettlementId(string"));
			StringAssert.Contains("return System?.CurrentSettlementId", chronicle);
			StringAssert.Contains("return System?.CurrentRealmId", lab);
			string guest = KingdomGuestbookLogicalSource.Read();
			string carry = KingdomCarryRuntimeLogicalSource.Read();
			// Exact carry publication moved out of the guest/notable surface into its own
			// lifecycle adapter. Keep the identity assertion on the live owner instead of
			// pinning the retired material-haul implementation's file boundary.
			StringAssert.Contains("DestinationSettlementId = destinationId", carry);
			StringAssert.Contains("system.CurrentSettlementId", carry);
			StringAssert.Contains("System.CurrentSettlementId", guest);
			string rite = KingdomCitizenRiteLogicalSource.Read();
			StringAssert.Contains("System.CurrentRealmId", rite);
			Assert.IsFalse(rite.Contains("TryTradableSecret(\n\t\t\t\t\tSystem.KingdomFactionName"));
			string riteRules = Source(Path.Combine("Experience", "KingdomCitizenRiteRules.cs"));
			StringAssert.Contains("KingdomIdentityRules.IsRealmId(ExactRealmId)", riteRules);
			string seal = Source(Path.Combine("Core", "KingdomSealRules.cs"));
			StringAssert.Contains("public static bool ExactIdentity", seal);
			StringAssert.Contains("KingdomIdentityRules.ReproveRealm", seal);
			StringAssert.Contains("KingdomIdentityRules.ReproveSettlement", seal);
			Assert.IsFalse(seal.Contains("? Seat.SettlementName : book.SettlementId"));
		}

		[Test]
		public void RenderedFounderNameSinksUseExactEngineEscapeBoundary()
		{
			string boundary = Source(Path.Combine("Core", "KingdomPresentation.cs"));
			StringAssert.Contains("ColorUtility.EscapeFormatting(Plain ?? \"\")", boundary);

			Regex name = new Regex(
				@"\b(?:System|system)\.(?:SeatName|KingdomDisplayName|ExiledDisplayName)\b|"
				+ @"\b(?:System|system)\.(?:Away|Seceded)\.SettlementName\b",
				RegexOptions.CultureInvariant);
			Regex rich = new Regex(@"KingdomPresentation\.Rich\([^;]*?\)",
				RegexOptions.CultureInvariant | RegexOptions.Singleline);
			Regex semanticTitle = new Regex(
				@"KingdomOfficeRules\.ChooseTitle\((?:System|system)\.SeatName\)",
				RegexOptions.CultureInvariant);
			Regex sink = new Regex(
				@"Popup\.|MessageQueue\.|KingdomChronicle\.|\.Ledger\.(?:Note|NoteBrink|NoteBrinkLifted)\s*\(|\.RecordDeed\(|"
				+ @"\b(?:Title|Intro)\s*:|\.Append\(|Description\.Short|\.DisplayName\s*=|"
				+ @"QuestGiver(?:Location)?Name\s*=",
				RegexOptions.CultureInvariant);
			string[] roots = new[]
			{
				"Chronicle", "Core", "Debug", "Experience", "Founding", "Growth",
				"Quests", "Raids", "Simulation", "Trade"
			};
			foreach (string root in roots)
			{
				string directory = Path.Combine(TestMain.RepositoryRoot, root);
				foreach (string file in Directory.GetFiles(directory, "*.cs",
					SearchOption.AllDirectories))
				{
					string relative = file.Substring(TestMain.RepositoryRoot.Length + 1);
					string[] statements = File.ReadAllText(file).Split(';');
					for (int i = 0; i < statements.Length; i++)
					{
						string statement = statements[i];
						if (!sink.IsMatch(statement) || statement.Contains("System.Ledger.Digest("))
							continue;
						string unprotected = rich.Replace(statement, "SAFE");
						unprotected = semanticTitle.Replace(unprotected, "SAFE");
						Match unsafeName = name.Match(unprotected);
						Assert.IsFalse(unsafeName.Success,
							relative + " rendered sink bypasses KingdomPresentation.Rich near statement "
							+ (i + 1) + " (sink " + sink.Match(statement).Value + "): "
							+ (unsafeName.Success ? unsafeName.Value : ""));
					}
				}
			}
		}

		[Test]
		public void FirstFoundingEmptyStateUsesAuthoritativeTopologyOnly()
		{
			string pending = Source("Core/KingdomSystem.z07.Identity.Pending.cs");
			StringAssert.Contains("SettlementTopology != null && SettlementTopology.Count == 0",
				pending);
			StringAssert.Contains("!SettlementTopology.HasOpaqueEvidence", pending);
			Assert.IsFalse(pending.Contains("Away == null"));
		}

		[Test]
		public void SuccessionSnapshotsStayPlainAndRitePresentationEscapesThem()
		{
			string succession = KingdomSuccessionLogicalSource.Read();
			StringAssert.Contains(
				"string shownHeir = KingdomPresentation.Rich(FormerRow.Name);", succession);
			StringAssert.Contains(
				"KingdomPresentation.Rich(PendingRiteFixtureName)", succession);
			StringAssert.Contains(
				"KingdomPresentation.Rich(PendingFounderCause)", succession);
			StringAssert.Contains(
				"ConsoleLib.Console.ColorUtility.StripFormatting(cause)", succession);
			StringAssert.DoesNotContain("+ \" to \" + FormerRow.Name", succession);

			string shrine = Source(Path.Combine("Experience", "KingdomFounderShrine.cs"));
			StringAssert.Contains(
				"ThousandAndFirst.KingdomPresentation.Rich(FounderName)", shrine);
			StringAssert.Contains(
				"ThousandAndFirst.KingdomPresentation.Rich(CityName)", shrine);
		}

		[Test]
		public void CityWordStaysPlainForCausalStateAndEscapesAtOutputOnly()
		{
			string word = Source(Path.Combine("Core", "KingdomWord.cs"));
			StringAssert.Contains("return Named;", word);
			StringAssert.Contains("? System.SeatName : null", word);
			StringAssert.Contains(
				"KingdomPresentation.Rich(CityName(System, From))", word);
			Assert.IsFalse(word.Contains("return KingdomPresentation.Rich(Named)"));

			string happenings = KingdomHappeningsLogicalSource.Read();
			StringAssert.Contains("string place = KingdomWord.CityName(System, label);",
				happenings);
			StringAssert.Contains("string shownPlace = KingdomPresentation.Rich(place);",
				happenings);
			StringAssert.Contains(
				"KingdomLocusRules.PilgrimCause(KingdomHappeningRules.AnchorName(anchor), place,",
				happenings);
			StringAssert.Contains("dish) + \"\\n\" + place", happenings);
		}

		[Test]
		public void ResidentLifecycleNamesStayPlainAndSharedBrinkBoundaryEscapesThem()
		{
			string brink = Source(Path.Combine("Core", "KingdomBrink.City.cs"));
			StringAssert.Contains(
				"string shownSubject = KingdomPresentation.Rich(Subject);", brink);
			StringAssert.Contains("? KingdomPresentation.Rich(Cause)", brink);
			StringAssert.Contains(
				"KingdomBrinkRules.LiftedNote(Kind, KingdomPresentation.Rich(Subject))",
				brink);

			string conversion = ConversionSource();
			StringAssert.Contains(
				"string.IsNullOrEmpty(roll) ? Settler.BaseDisplayNameStripped : roll", conversion);
			StringAssert.Contains("string shownName = KingdomPresentation.Rich(named);",
				conversion);

			string water = KingdomWaterRiteLogicalSource.Read();
			StringAssert.Contains(
				"string.IsNullOrEmpty(name) ? Resident.BaseDisplayNameStripped : name", water);
			StringAssert.Contains("string shownName = KingdomPresentation.Rich(name);", water);

			string lodging = KingdomLodgingLogicalSource.Read();
			StringAssert.Contains(
				"Resident.BaseDisplayNameStripped", lodging);
			StringAssert.Contains(
				"KingdomPresentation.Rich(ResidentName)", lodging);

			string growth = KingdomGrowthLogicalSource.Read();
			StringAssert.Contains(
				"leaver.BaseDisplayNameStripped : former.Name", growth);
			StringAssert.Contains(
				"KingdomPresentation.Rich(XRL.Language.Grammar.A(name))", growth);
		}

		[Test]
		public void GuestPetitionBookAndOfficeSnapshotsProjectOnlyAtRenderBoundaries()
		{
			string lifecycle = KingdomGuestLifecycleLogicalSource.Read();
			Assert.GreaterOrEqual(Regex.Matches(lifecycle,
				@"op\.ObjectName = PlainObjectName\(guest\);").Count, 2);
			StringAssert.Contains("guest.BaseDisplayNameStripped", lifecycle);

			string guestbook = KingdomGuestbookLogicalSource.Read();
			StringAssert.Contains("string shownName = KingdomPresentation.Rich(name);",
				guestbook);
			StringAssert.Contains("guest.DisplayName = KingdomPresentation.Rich(op.ObjectName);",
				guestbook);
			StringAssert.Contains(
				"guest.SetStringProperty(\"KingdomName\", op.ObjectName);", guestbook);

			string petitions = KingdomPetitionLifecycleLogicalSource.Read();
			StringAssert.Contains("candidate.BaseDisplayNameStripped", petitions);
			StringAssert.Contains("ColorUtility.StripFormatting(", petitions);
			StringAssert.Contains(
				"string petitioner = KingdomPresentation.Rich(op.ObjectName);", petitions);

			string report = Source(Path.Combine("Simulation", "City",
				"KingdomBookReport.cs")) + "\n" + Source(Path.Combine("Simulation", "City",
				"KingdomBookReport.WritersAndGround.cs"));
			Assert.AreEqual(2, Regex.Matches(report,
				@"public static partial class KingdomBookReport").Count);
			StringAssert.DoesNotContain("public static class KingdomBookReport", report);
			StringAssert.Contains(
				"KingdomNotables.HolderName(System), KingdomPresentation.Rich", report);
			StringAssert.Contains("private static string Writers()", report);
			StringAssert.Contains("private static string GroundName(string zoneId)", report);

			string notable = Source(Path.Combine("Experience", "KingdomNotables.cs"));
			StringAssert.Contains(
				"ColorUtility.StripFormatting(epithet)", notable);
			StringAssert.Contains("System.City.OfficeEpithet = Epithet ?? \"\";", notable);
		}
	}
}
#endif
