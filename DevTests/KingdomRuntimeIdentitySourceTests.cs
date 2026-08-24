#if TAF_TESTS
using System;
using System.IO;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomRuntimeIdentitySourceTests
	{
		private static readonly string RealmId = new string('a', 64);
		private static readonly string SettlementA = new string('b', 64);
		private static readonly string SettlementB = new string('c', 64);

		private static string Source(string relative)
		{
			DirectoryInfo cursor = new DirectoryInfo(AppContext.BaseDirectory);
			while (cursor != null)
			{
				string path = Path.Combine(cursor.FullName, relative);
				if (File.Exists(path)) return File.ReadAllText(path);
				cursor = cursor.Parent;
			}
			throw new InvalidOperationException("Cannot locate " + relative);
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
			payload[4] = 2; payload[5] = 0; payload[6] = 0; payload[7] = 0;
			Assert.IsFalse(KingdomArchivedSettlementCodec.TryDecode(payload,
				out KingdomSettlement decoded, out int future, out failure));
			Assert.IsNull(decoded);
			Assert.AreEqual(2, future);

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
