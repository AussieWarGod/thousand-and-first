#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using NUnit.Framework;

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

		private static KingdomTradeBook ExactTradeBook()
		{
			KingdomTradeBook book = new KingdomTradeBook();
			string failure;
			Assert.IsTrue(KingdomTradeRules.BindExactIdentity(book, RealmId,
				new[] { SettlementA, SettlementB }, out failure), failure);
			return book;
		}

		[Test]
		public void V8BoundaryRefusesUnreadableNamedIdentityAndAllowsOnlyReflectedMigration()
		{
			string system = Source(Path.Combine("Core", "KingdomSystem.cs"));
			StringAssert.Contains("private const int CurrentSerializationVersion = 8;", system);
			StringAssert.Contains("private const int FirstNamedSerializationVersion = 8;", system);
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
		}

		[Test]
		public void FirstFoundingFreezesIdsBeforeFactionCallbackOrStepMarker()
		{
			string founding = Source(Path.Combine("Core", "KingdomFounding.cs"));
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
		public void LaterFoundingFreezesSiteAndPendingTupleBeforeMonotoneTradeExpansion()
		{
			string founding = Source(Path.Combine("Core", "KingdomFoundingTransaction.cs"));
			int publish = founding.IndexOf("private static void PublishSecondCore",
				StringComparison.Ordinal);
			int callFreeze = founding.IndexOf("TryFreezeSecondIdentity", publish,
				StringComparison.Ordinal);
			int marker = founding.IndexOf("SecondPublicationAuthorityProperty", callFreeze,
				StringComparison.Ordinal);
			int away = founding.IndexOf("System.Away = founded", marker,
				StringComparison.Ordinal);
			Assert.Greater(callFreeze, publish);
			Assert.Greater(marker, callFreeze);
			Assert.Greater(away, marker);

			int freeze = founding.IndexOf("private static bool TryFreezeSecondIdentity",
				StringComparison.Ordinal);
			int transaction = founding.IndexOf(
				"Site.SetZoneProperty(SecondIdentityTransactionProperty", freeze,
				StringComparison.Ordinal);
			int settlement = founding.IndexOf(
				"Site.SetZoneProperty(SecondIdentitySettlementProperty", transaction,
				StringComparison.Ordinal);
			int pending = founding.IndexOf("TryStagePendingSettlementIdentity", settlement,
				StringComparison.Ordinal);
			int expand = founding.IndexOf("TryExpandTradeIdentity", pending,
				StringComparison.Ordinal);
			int irrevocable = founding.IndexOf(
				"Basin.PendingPhase = KingdomFoundingPhase.PublicationCommitted", expand,
				StringComparison.Ordinal);
			Assert.Greater(transaction, freeze);
			Assert.Greater(settlement, transaction);
			Assert.Greater(pending, settlement);
			Assert.Greater(expand, pending);
			Assert.Greater(irrevocable, expand);
		}

		[Test]
		public void TradeNormalizationNeverPromotesOrClearsMutableNameRows()
		{
			string system = Source(Path.Combine("Core", "KingdomSystem.cs"));
			int normalize = system.IndexOf("private void NormalizeTradeBook()",
				StringComparison.Ordinal);
			string body = system.Substring(normalize);
			StringAssert.Contains("KingdomTradeRules.BindExactIdentity", body);
			StringAssert.Contains("KingdomTradeRules.ExpandExactIdentity", body);
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
			Assert.IsFalse(body.Contains("ActiveDealKeys.Clear()"));
			Assert.IsFalse(body.Contains("Manifest = null"));
			Assert.IsFalse(system.Contains("TryGetExactExileClosedTick"));
		}

		[Test]
		public void ArchiveTransactionBlocksLegacyMirrorNormalization()
		{
			string system = Source(Path.Combine("Core", "KingdomSystem.cs"));
			int active = system.IndexOf("bool archiveTransactionActive = ExiledRealmArchive != null",
				StringComparison.Ordinal);
			int guard = system.IndexOf("if (!archiveTransactionActive)", active,
				StringComparison.Ordinal);
			int standings = system.IndexOf("ExiledStandings = new Dictionary<string, int>()", guard,
				StringComparison.Ordinal);
			int promotion = system.IndexOf("ExiledSeat = ExiledAway ?? new KingdomSettlement()",
				guard, StringComparison.Ordinal);
			int normalize = system.IndexOf("ExiledSeat?.Normalize()", guard,
				StringComparison.Ordinal);
			Assert.Greater(guard, active);
			Assert.Greater(standings, guard);
			Assert.Greater(promotion, guard);
			Assert.Greater(normalize, guard);
		}

		[Test]
		public void ArchiveCodecBoundsRawLengthsBeforeAllocation()
		{
			string archive = Source(Path.Combine("Core", "KingdomRealmArchive.cs"));
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
			string system = Source(Path.Combine("Core", "KingdomSystem.cs"));
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
			string trade = Source(Path.Combine("Trade", "KingdomTrade.cs"));
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
			string system = Source(Path.Combine("Core", "KingdomSystem.cs"));
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
			string system = Source(Path.Combine("Core", "KingdomSystem.cs"));
			string archive = Source(Path.Combine("Core", "KingdomRealmArchive.cs"));
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
			string system = Source(Path.Combine("Core", "KingdomSystem.cs"));
			StringAssert.Contains("otherRows.Add(rows[i].Copy())", system);
			StringAssert.Contains("TryWriteRegistry(otherRows", system);
			StringAssert.Contains("TryDeclareOnce(this", system);
			StringAssert.Contains("RecordDeclaredOnce(this, declaration)", system);
			StringAssert.Contains("KingdomRealmCallbackProofRules.ChronicleListsMatch", system);
			StringAssert.Contains("ChronicleFaultMatches", system);
			StringAssert.Contains("registryFault, out before", system);
			StringAssert.Contains("TryFingerprint(eventId, Text, Accomplishment", system);
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
		public void ExileAndReturnPublishRecoveryPhasesBeforePiecemealMutation()
		{
			string system = Source(Path.Combine("Core", "KingdomSystem.cs"));
			string archive = Source(Path.Combine("Core", "KingdomRealmArchive.cs"));
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
			StringAssert.Contains("object[] currentRoots = { currentSeat, Away, Seceded", system);
			StringAssert.Contains("object[] mirrorRoots = { ExiledSeat, ExiledAway, ExiledStandings }",
				system);
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
			string archiveSource = Source(Path.Combine("Core", "KingdomRealmArchive.cs"));
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
			settlement.RosterNames = sameRootAlias;
			settlement.RosterOrigins = sameRootAlias;
			Assert.IsFalse(KingdomArchivedSettlementCodec.TryEncode(settlement,
				out byte[] aliasedPayload, out failure));
			Assert.IsNull(aliasedPayload);
			settlement.RosterOrigins = new System.Collections.Generic.List<string>();
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
			source.RosterNames = new List<string>(
				KingdomArchivedSettlementCodec.MaxCollectionCount);
			for (int i = 0; i < KingdomArchivedSettlementCodec.MaxCollectionCount; i++)
				source.RosterNames.Add(individuallyLegal);
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
			string system = Source(Path.Combine("Core", "KingdomSystem.cs"));
			string settlement = Source(Path.Combine("Core", "KingdomSettlement.cs"));
			StringAssert.Contains("public KingdomLifecycleBook LifecycleBook", system);
			StringAssert.Contains("public KingdomLifecycleBook LifecycleBook", settlement);
			StringAssert.Contains("public KingdomCarryBook CarryBook", system);
			StringAssert.Contains("BindSettlementIdentity(LifecycleBook", system);
			StringAssert.Contains("CarryBook.RealmId = RealmId", system);
			StringAssert.Contains("TryCloneCarry(Archive.CarryBook", system);
			StringAssert.Contains("CarryBook = carry", system);
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
				Path.Combine("Growth", "KingdomPlot.cs"),
				Path.Combine("Growth", "KingdomSubsidence.cs"),
				Path.Combine("Growth", "KingdomWear.cs"),
				Path.Combine("Growth", "KingdomLab.cs"),
				Path.Combine("Simulation", "City", "KingdomHappenings.cs"),
				Path.Combine("Simulation", "City", "KingdomPorters.cs"),
				Path.Combine("Quests", "KingdomBounty.cs"),
				Path.Combine("Quests", "KingdomPetitions.cs")
			};
			foreach (string file in files)
			{
				string source = Source(file);
				Assert.IsFalse(source.Contains(
					"KingdomChronicle.SettlementId(System.KingdomFactionName)"), file);
				Assert.IsFalse(source.Contains("LegacyOriginIdentity("), file);
			}
			string chronicle = Source(files[0]);
			Assert.IsFalse(chronicle.Contains("SettlementIdPrefix"));
			Assert.IsFalse(chronicle.Contains("SettlementId(string"));
			StringAssert.Contains("return System?.CurrentSettlementId", chronicle);
			string lab = Source(Path.Combine("Growth", "KingdomLab.cs"));
			StringAssert.Contains("return System?.CurrentRealmId", lab);
			string guest = Source(Path.Combine("Experience", "KingdomGuestbook.cs"));
			StringAssert.Contains("DestinationSettlementId = destinationId", guest);
			StringAssert.Contains("System.CurrentSettlementId", guest);
			string rite = Source(Path.Combine("Experience", "KingdomCitizenRite.cs"));
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
	}
}
#endif
