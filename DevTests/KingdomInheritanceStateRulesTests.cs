#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class KingdomInheritanceStateRulesTests
	{
		private static string EnumShape(Type type)
		{
			Array values = Enum.GetValues(type);
			string[] shape = new string[values.Length];
			for (int i = 0; i < values.Length; i++)
			{
				object value = values.GetValue(i);
				shape[i] = Convert.ToInt32(value) + ":" + value;
			}
			return string.Join(",", shape);
		}

		[Test]
		public void InheritanceDeclarationsKeepExactInternalAbiAndDefaults()
		{
			ClassicAssert.AreEqual(typeof(int), Enum.GetUnderlyingType(typeof(KingdomInheritancePhase)));
			ClassicAssert.AreEqual("0:Empty,1:Reserved,2:SiteSelected,3:WorldValidated,4:Installed,"
				+ "5:AppliedPendingDurability,6:Committed,7:Refused,8:RepairRequired",
				EnumShape(typeof(KingdomInheritancePhase)));
			ClassicAssert.AreEqual(typeof(int), Enum.GetUnderlyingType(typeof(KingdomInheritanceStartFault)));
			ClassicAssert.AreEqual("0:None,1:MissingStart,2:AlternateWorld,3:TargetIsStart",
				EnumShape(typeof(KingdomInheritanceStartFault)));
			ClassicAssert.AreEqual(typeof(int), Enum.GetUnderlyingType(typeof(KingdomCommittedRewindAction)));
			ClassicAssert.AreEqual("0:DeferUntilPrimary,1:AdoptDurable,2:AwaitLazyBuilder,"
				+ "3:ReapplyCleanBuiltTarget,4:RepairRequired",
				EnumShape(typeof(KingdomCommittedRewindAction)));
			ClassicAssert.AreEqual(typeof(int), Enum.GetUnderlyingType(typeof(KingdomInheritanceLoadKind)));
			ClassicAssert.AreEqual("0:Unknown,1:Primary,2:SameGameRollback",
				EnumShape(typeof(KingdomInheritanceLoadKind)));

			Type rules = typeof(KingdomInheritanceStateRules);
			ClassicAssert.AreEqual("ThousandAndFirst.KingdomInheritanceStateRules", rules.FullName);
			ClassicAssert.IsTrue(rules.IsNotPublic && rules.IsAbstract && rules.IsSealed);
			Type flow = typeof(KingdomInheritanceLoadSourceFlow);
			Type loadSource = flow.GetNestedType("LoadSource",
				System.Reflection.BindingFlags.NonPublic);
			ClassicAssert.IsNotNull(loadSource);
			ClassicAssert.AreEqual("ThousandAndFirst.KingdomInheritanceLoadSourceFlow+LoadSource",
				loadSource.FullName);
			ClassicAssert.IsTrue(loadSource.IsNestedPrivate);

			Type saved = typeof(KingdomInheritanceSavedShape);
			ClassicAssert.AreEqual("ThousandAndFirst.KingdomInheritanceSavedShape", saved.FullName);
			ClassicAssert.IsTrue(saved.IsNotPublic && saved.IsClass && saved.IsSealed);
			System.Reflection.FieldInfo[] fields = saved.GetFields(
				System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic |
				System.Reflection.BindingFlags.DeclaredOnly);
			string[] expected = new[] { "PhaseValue", "LegacyText", "ReceiptText",
				"CommittedReceiptText", "TargetZoneId", "TargetTerrainBlueprint",
				"TargetTerrainRank", "SecretId", "SiteName", "ApplyStatus", "ApplyFault",
				"ApplicationMarker", "ReleasePending", "OwnsSkipTerrainBuilders", "OwnsNoBiomes",
				"OwnsZoneName", "RecoveryDisabled", "RetryAuthorized" };
			ClassicAssert.AreEqual(expected.Length, fields.Length);
			for (int i = 0; i < expected.Length; i++)
			{
				ClassicAssert.AreEqual(expected[i], fields[i].Name, "saved field order " + i);
			}

			KingdomInheritanceSavedShape empty = new KingdomInheritanceSavedShape();
			ClassicAssert.AreEqual(0, empty.PhaseValue);
			ClassicAssert.AreEqual("", empty.LegacyText);
			ClassicAssert.AreEqual("", empty.ReceiptText);
			ClassicAssert.AreEqual("", empty.CommittedReceiptText);
			ClassicAssert.AreEqual("", empty.TargetZoneId);
			ClassicAssert.AreEqual("", empty.TargetTerrainBlueprint);
			ClassicAssert.AreEqual(-1, empty.TargetTerrainRank);
			ClassicAssert.AreEqual("", empty.SecretId);
			ClassicAssert.AreEqual("", empty.SiteName);
			ClassicAssert.AreEqual(-1, empty.ApplyStatus);
			ClassicAssert.AreEqual(-1, empty.ApplyFault);
			ClassicAssert.AreEqual("", empty.ApplicationMarker);
			ClassicAssert.IsFalse(empty.ReleasePending || empty.OwnsSkipTerrainBuilders
				|| empty.OwnsNoBiomes || empty.OwnsZoneName || empty.RecoveryDisabled
				|| empty.RetryAuthorized);
		}

		private static string WorkspaceRoot()
		{
			return TestMain.RepositoryRoot;
		}

		private static string Source(string relative)
		{
			return TestMain.ReadRepositoryText(relative);
		}

		private static int MatchingBrace(string source, int open)
		{
			ClassicAssert.GreaterOrEqual(open, 0);
			int depth = 0;
			for (int i = open; i < source.Length; i++)
			{
				if (source[i] == '{') depth++;
				else if (source[i] == '}' && --depth == 0) return i;
			}
			Assert.Fail("Unclosed source block");
			return -1;
		}

		private static string MethodBody(string source, string signature)
		{
			int method = source.IndexOf(signature, StringComparison.Ordinal);
			ClassicAssert.GreaterOrEqual(method, 0);
			int open = source.IndexOf('{', method);
			int close = MatchingBrace(source, open);
			return source.Substring(open + 1, close - open - 1);
		}

		private static int Occurrences(string source, string token)
		{
			int count = 0;
			for (int at = 0; (at = source.IndexOf(token, at,
				StringComparison.Ordinal)) >= 0; at += token.Length) count++;
			return count;
		}

		[Test]
		public void LogicalSourceKeepsOneOrderedPartialAuthority()
		{
			string source = KingdomInheritanceStateLogicalSource.Read();
			// z10 split into z10a (reservation leases) and z10b (discoverability): one more shard.
			ClassicAssert.AreEqual(14, Occurrences(source,
				"public sealed partial class KingdomInheritanceState"));
			ClassicAssert.AreEqual(1, Occurrences(source, "[GameStateSingleton(StateId)]"));
			ClassicAssert.AreEqual(0, Occurrences(source,
				"public sealed class KingdomInheritanceState"));

			string[] ordered = new[]
			{
				"private int SerializationVersion",
				"public void HandleEvent(EmbarkEvent E)",
				"internal bool StageSite(",
				"internal bool TryGroundPaint(",
				"internal bool PrepareVanillaFallback(",
				"internal void ResumeAfterLoad(",
				"private void CommitDurableProof(",
				"private void RepairLoadedTarget(",
				"private bool TryProveDirectRepairPrecondition(",
				"private void ReleaseReservation(",
				"private bool EnsureReservationLease(",
				"private bool TryQuarantineExact(",
				"private bool TryGetReservation("
			};
			int previous = -1;
			for (int i = 0; i < ordered.Length; i++)
			{
				int current = source.IndexOf(ordered[i], StringComparison.Ordinal);
				ClassicAssert.Greater(current, previous, "logical member order " + ordered[i]);
				previous = current;
			}
		}

		private static KingdomSealRecord Legacy()
		{
			return new KingdomSealRecord
			{
				Status = KingdomSealStatus.Promoted,
				LineageId = "lineage-a",
				LegacyId = "legacy-a",
				InterregnumRoll = 17,
				InheritedState = (int)KingdomRules.InheritedState.Held
			};
		}

		private static KingdomSealReceipt Receipt(KingdomSealRecord Legacy)
		{
			return new KingdomSealReceipt
			{
				LineageId = Legacy.LineageId,
				LegacyId = Legacy.LegacyId,
				TargetGameId = "target-game",
				State = KingdomSealReceiptState.Reserved,
				WrittenTick = 321L
			};
		}

		private static KingdomSealRecord CanonicalLegacy()
		{
			KingdomSealRecord record = new KingdomSealRecord
			{
				WriterVersion = "test",
				EngineVersion = "test",
				Status = KingdomSealStatus.Living,
				LineageId = "lineage-a",
				LegacyId = "legacy-a",
				OriginGameId = "origin.game",
				Generation = 1,
				Revision = 7,
				WrittenTick = 100L,
				FounderName = "Abram",
				RealmName = "Old Realm",
				SettlementName = "Old Seat",
				SettlementId = "old-seat",
				Vocation = "holding",
				Style = "common",
				FoundedTick = 10L,
				GroundZoneId = "JoppaWorld.1.1.1.1.10",
				RegionName = "Salt",
				TerrainBlueprint = "TerrainSaltMarsh",
				Depth = 10,
				Stage = (int)GrowthStage.Camp,
				Population = 2,
				Defence = 1,
				StoredWater = 5
			};
			record.Vigour = KingdomRules.SealedVigour((GrowthStage)record.Stage,
				record.Population, record.Defence, record.StoredWater, record.Withered);
			return KingdomSealRules.PromoteRetirement(KingdomSealRules.WithRetirement(
				KingdomSealTestIdentity.Bind(record)));
		}

		private static KingdomInheritanceSavedShape PendingShape()
		{
			KingdomSealRecord legacy = CanonicalLegacy();
			KingdomSealReceipt receipt = Receipt(legacy);
			string marker;
			ClassicAssert.IsTrue(KingdomInheritanceStateRules.TryComposeApplicationMarker(legacy,
				receipt, "JoppaWorld.4.5.1.2.10", 1, out marker));
			return new KingdomInheritanceSavedShape
			{
				PhaseValue = (int)KingdomInheritancePhase.AppliedPendingDurability,
				LegacyText = legacy.Compose(),
				ReceiptText = receipt.Compose(),
				TargetZoneId = "JoppaWorld.4.5.1.2.10",
				TargetTerrainBlueprint = "TerrainSaltMarsh",
				TargetTerrainRank = 0,
				SecretId = "taf.inherit." + legacy.LegacyId,
				SiteName = KingdomInheritanceStateRules.ComposeSiteName(legacy),
				ApplyStatus = (int)KingdomInheritApplyStatus.Applied,
				ApplyFault = (int)KingdomInheritApplyFault.None,
				ApplicationMarker = marker,
				OwnsSkipTerrainBuilders = true,
				OwnsNoBiomes = true,
				OwnsZoneName = true
			};
		}

		private static string PriorRecordText(KingdomSealRecord Record, int Schema)
		{
			KingdomSealBody source = Record.WriteBody();
			KingdomSealBody body = new KingdomSealBody();
			HashSet<string> omitted = new HashSet<string>(StringComparer.Ordinal)
			{
				"profile_schema", "technology_band", "canonical_body",
				"source_profile_digest", "profile_provenance_digest"
			};
			if (Schema == 4) omitted.UnionWith(new[] { "spatial_version", "spatial_width",
				"spatial_height", "spatial_entry_side", "spatial_entry_x", "spatial_entry_y",
				"work_snapshot", "work_snapshot_hash", "street_x", "street_y" });
			for (int i = 0; i < source.Keys.Count; i++)
			{
				string key = source.Keys[i];
				if (omitted.Contains(key)) continue;
				switch (source.KindOf(key))
				{
				case KingdomSealKind.Text: body.Put(key, source.Text(key)); break;
				case KingdomSealKind.Number: body.Put(key, source.Number(key)); break;
				case KingdomSealKind.TextList: body.PutList(key, source.TextList(key)); break;
				case KingdomSealKind.NumberList: body.PutList(key, source.NumberList(key)); break;
				case KingdomSealKind.EmptyList:
					body.PutList(key, new List<string>()); break;
				}
			}
			return KingdomSealFormat.Compose(Schema, body);
		}

		private static string PriorReceiptText(KingdomSealReceipt Receipt, int Schema)
		{
			ClassicAssert.IsTrue(KingdomSealFormat.TryParse(Receipt.Compose(),
				KingdomSealRecord.FirstSchema, KingdomSealRecord.CurrentSchema, out int _,
				out KingdomSealBody body, out KingdomSealFault fault, out string detail),
				fault + ": " + detail);
			return KingdomSealFormat.Compose(Schema, body);
		}

		private static KingdomInheritanceSavedShape PriorSchemaShape(int LegacySchema,
			int ReceiptSchema, int CommittedSchema, KingdomInheritancePhase Phase)
		{
			KingdomSealRecord legacy = CanonicalLegacy();
			KingdomSealReceipt reserved = Receipt(legacy);
			KingdomInheritanceSavedShape shape = new KingdomInheritanceSavedShape
			{
				PhaseValue = (int)Phase,
				LegacyText = PriorRecordText(legacy, LegacySchema),
				ReceiptText = PriorReceiptText(reserved, ReceiptSchema)
			};
			if (Phase == KingdomInheritancePhase.Reserved ||
				Phase == KingdomInheritancePhase.Refused ||
				Phase == KingdomInheritancePhase.RepairRequired) return shape;
			shape.TargetZoneId = "JoppaWorld.4.5.1.2.10";
			shape.TargetTerrainBlueprint = "TerrainSaltMarsh";
			shape.TargetTerrainRank = 0;
			if (Phase == KingdomInheritancePhase.SiteSelected ||
				Phase == KingdomInheritancePhase.WorldValidated) return shape;
			shape.SecretId = "taf.inherit." + legacy.LegacyId;
			shape.SiteName = KingdomInheritanceStateRules.ComposeSiteName(legacy);
			shape.OwnsSkipTerrainBuilders = true;
			shape.OwnsNoBiomes = true;
			shape.OwnsZoneName = true;
			if (Phase == KingdomInheritancePhase.Installed) return shape;
			shape.ApplyStatus = (int)KingdomInheritApplyStatus.Applied;
			shape.ApplyFault = (int)KingdomInheritApplyFault.None;
			ClassicAssert.IsTrue(KingdomInheritanceStateRules.TryComposeApplicationMarker(legacy,
				reserved, shape.TargetZoneId, 1, out shape.ApplicationMarker));
			if (Phase == KingdomInheritancePhase.Committed)
			{
				KingdomSealReceipt committed = new KingdomSealReceipt
				{
					LineageId = legacy.LineageId, LegacyId = legacy.LegacyId,
					TargetGameId = reserved.TargetGameId,
					State = KingdomSealReceiptState.Committed, WrittenTick = 400L
				};
				shape.CommittedReceiptText = PriorReceiptText(committed, CommittedSchema);
			}
			return shape;
		}

		private static void AssertPriorSchemaShape(int LegacySchema, int ReceiptSchema,
			int CommittedSchema, KingdomInheritancePhase Phase)
		{
			string label = LegacySchema + "/" + ReceiptSchema + "/" +
				CommittedSchema + "/" + Phase;
			KingdomInheritanceSavedShape shape = PriorSchemaShape(LegacySchema,
				ReceiptSchema, CommittedSchema, Phase);
			ClassicAssert.IsTrue(KingdomSealRecord.TryParse(shape.LegacyText,
				out KingdomSealRecord legacy, out KingdomSealFault fault,
				out string detail), label + ": " + fault + ": " + detail);
			ClassicAssert.AreEqual(shape.LegacyText, legacy.Compose(), label + " legacy replay");
			ClassicAssert.IsTrue(KingdomSealReceipt.TryParse(shape.ReceiptText,
				out KingdomSealReceipt reserved));
			ClassicAssert.AreEqual(shape.ReceiptText, reserved.Compose(), label + " reservation replay");
			if (!string.IsNullOrEmpty(shape.CommittedReceiptText))
			{
				ClassicAssert.IsTrue(KingdomSealReceipt.TryParse(shape.CommittedReceiptText,
					out KingdomSealReceipt committed));
				ClassicAssert.AreEqual(shape.CommittedReceiptText, committed.Compose(),
					label + " committed replay");
			}
			ClassicAssert.Greater(KingdomInheritEngine.ReconstructionVersionForText(
				shape.LegacyText), 0, label + " reconstruction");
			ClassicAssert.IsTrue(KingdomInheritanceStateRules.TryValidateSavedShape(shape,
				"target-game", 1, out string failure), label + ": " + failure);
		}

		[Test]
		public void MarkerFormatHasOneCanonicalImplementation()
		{
			KingdomSealRecord legacy = Legacy();
			KingdomSealReceipt receipt = Receipt(legacy);
			string marker;
			ClassicAssert.IsTrue(KingdomInheritanceStateRules.TryComposeApplicationMarker(legacy,
				receipt, "JoppaWorld.4.5.1.2.10", 1, out marker));
			ClassicAssert.AreEqual("taf-inherit-v1|lineage-a|legacy-a|target-game|reserved|321|"
				+ "JoppaWorld.4.5.1.2.10", marker);
		}

		[Test]
		public void CrossRunImportRequiresExplicitPreWorldOptInWithoutSpendingDecline()
		{
			string options = Source("Options.xml");
			string state = KingdomInheritanceStateLogicalSource.Read();
			string seal = KingdomSealLogicalSource.Read();
			const string optionId = "r_TAF_OptionLegacyImport";
			int option = options.IndexOf("<option ID=\"" + optionId + "\"",
				StringComparison.Ordinal);
			ClassicAssert.GreaterOrEqual(option, 0);
			int optionEnd = options.IndexOf("/>", option, StringComparison.Ordinal);
			ClassicAssert.Greater(optionEnd, option);
			string declaration = options.Substring(option, optionEnd - option);
			StringAssert.Contains("Default=\"No\"", declaration);
			StringAssert.Contains("enable before creating a new world", declaration);

			string initialize = MethodBody(state, "public void Initialize()");
			int optionGate = initialize.IndexOf("!LegacyImportEnabled()",
				StringComparison.Ordinal);
			ClassicAssert.GreaterOrEqual(optionGate, 0);
			int gateOpen = initialize.IndexOf('{', optionGate);
			int gateClose = MatchingBrace(initialize, gateOpen);
			int reserve = initialize.IndexOf(".TryReserveImport(", optionGate,
				StringComparison.Ordinal);
			ClassicAssert.AreEqual("return;",
				initialize.Substring(gateOpen + 1, gateClose - gateOpen - 1).Trim(),
				"the disabled path must exit before acquiring the seal coordinator");
			ClassicAssert.Greater(reserve, optionGate,
				"option Off must return before any profile reservation attempt");
			ClassicAssert.AreEqual(1, Occurrences(initialize, ".TryReserveImport("));
			ClassicAssert.AreEqual(1, Occurrences(state, ".TryReserveImport("),
				"no helper or alternate call path may reserve outside the consent gate");
			int productionCalls = 0;
			string productionCaller = null;
			string root = WorkspaceRoot();
			foreach (string path in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
			{
				string relative = Path.GetRelativePath(root, path);
				if (relative.StartsWith("DevTests" + Path.DirectorySeparatorChar,
					StringComparison.Ordinal)) continue;
				int calls = Occurrences(File.ReadAllText(path), ".TryReserveImport(");
				if (calls > 0) productionCaller = path;
				productionCalls += calls;
			}
			ClassicAssert.AreEqual(1, productionCalls,
				"the consent-gated new-world singleton must be the sole production caller");
			ClassicAssert.AreEqual(Path.GetFullPath(Path.Combine(root, "World")),
				Path.GetDirectoryName(Path.GetFullPath(productionCaller)));
			StringAssert.StartsWith("KingdomInheritanceState",
				Path.GetFileName(productionCaller));
			StringAssert.EndsWith(".cs", productionCaller);
			StringAssert.Contains(
				"Options.GetOption(\"r_TAF_OptionLegacyImport\", \"No\") == \"Yes\"",
				state);
			StringAssert.Contains(
				"Options.GetOption(\"r_TAF_OptionLegacyImport\", \"No\") == \"Yes\"",
				seal);
			ClassicAssert.IsFalse(state.Contains("TryDeclineImport"),
				"global option Off is silence, not an explicit per-run decline");
		}

		[Test]
		public void UnsupportedSerializationHeaderMustTakeThrowAndSkipBlockPath()
		{
			const int magic = 1413568073;
			ClassicAssert.IsTrue(KingdomInheritanceStateRules.IsSupportedSerializationHeader(
				magic, 1, magic, 4));
			ClassicAssert.IsTrue(KingdomInheritanceStateRules.IsSupportedSerializationHeader(
				magic, 4, magic, 4));
			ClassicAssert.IsFalse(KingdomInheritanceStateRules.IsSupportedSerializationHeader(
				magic, 0, magic, 4));
			ClassicAssert.IsFalse(KingdomInheritanceStateRules.IsSupportedSerializationHeader(
				magic, 5, magic, 4));
			ClassicAssert.IsFalse(KingdomInheritanceStateRules.IsSupportedSerializationHeader(
				magic + 1, 4, magic, 4));
		}

		[Test]
		public void MarkerRejectsCommittedOrMismatchedReceipt()
		{
			KingdomSealRecord legacy = Legacy();
			KingdomSealReceipt receipt = Receipt(legacy);
			string marker;
			receipt.State = KingdomSealReceiptState.Committed;
			ClassicAssert.IsFalse(KingdomInheritanceStateRules.TryComposeApplicationMarker(legacy,
				receipt, "JoppaWorld.4.5.1.2.10", 1, out marker));
			receipt.State = KingdomSealReceiptState.Reserved;
			receipt.LegacyId = "another-legacy";
			ClassicAssert.IsFalse(KingdomInheritanceStateRules.TryComposeApplicationMarker(legacy,
				receipt, "JoppaWorld.4.5.1.2.10", 1, out marker));
		}

		[Test]
		public void LaterLoadDurabilityUsesPhaseAndMarkersNotMutableObjects()
		{
			const string marker = "taf-inherit-v1|lineage-a|legacy-a|target-game|reserved|321|zone";
			ClassicAssert.IsTrue(KingdomInheritanceStateRules.IsDurableMarkerProof(
				KingdomInheritancePhase.AppliedPendingDurability,
				(int)KingdomInheritApplyStatus.Applied, true, marker, marker, marker, false));
			ClassicAssert.IsTrue(KingdomInheritanceStateRules.IsDurableMarkerProof(
				KingdomInheritancePhase.RepairRequired,
				(int)KingdomInheritApplyStatus.AlreadyApplied, true, marker, marker, marker, false));
			// There is deliberately no live-object count or object-state argument: initial Apply owns
			// that proof, and later player movement, filling, or destruction cannot revoke durability.
		}

		[Test]
		public void TornOrUnownedMarkerProofFailsClosed()
		{
			const string marker = "expected";
			ClassicAssert.IsFalse(KingdomInheritanceStateRules.IsDurableMarkerProof(
				KingdomInheritancePhase.AppliedPendingDurability,
				(int)KingdomInheritApplyStatus.Applied, false, marker, marker, marker, false));
			ClassicAssert.IsFalse(KingdomInheritanceStateRules.IsDurableMarkerProof(
				KingdomInheritancePhase.AppliedPendingDurability,
				(int)KingdomInheritApplyStatus.Applied, true, marker, marker, "different", false));
			ClassicAssert.IsFalse(KingdomInheritanceStateRules.IsDurableMarkerProof(
				KingdomInheritancePhase.Installed,
				(int)KingdomInheritApplyStatus.Applied, true, "", marker, marker, false));
			ClassicAssert.IsTrue(KingdomInheritanceStateRules.IsDurableMarkerProof(
				KingdomInheritancePhase.Installed,
				(int)KingdomInheritApplyStatus.Applied, true, "", marker, marker, true));
			ClassicAssert.IsFalse(KingdomInheritanceStateRules.IsDurableMarkerProof(
				KingdomInheritancePhase.RepairRequired,
				(int)KingdomInheritApplyStatus.Failed, true, marker, marker, marker, false));
			ClassicAssert.IsTrue(KingdomInheritanceStateRules.RetainsDurableApplicationCandidate(
				(int)KingdomInheritApplyStatus.Applied, (int)KingdomInheritApplyFault.None,
				marker));
			ClassicAssert.IsFalse(KingdomInheritanceStateRules.RetainsDurableApplicationCandidate(
				(int)KingdomInheritApplyStatus.Failed,
				(int)KingdomInheritApplyFault.PartialApplication, marker));
		}

		[Test]
		public void ControlledRetryRequiresFirstTryFailureAndExactCleanup()
		{
			ClassicAssert.IsTrue(KingdomInheritanceStateRules.ShouldRetryBuild(
				KingdomInheritApplyStatus.Failed, 1, true));
			ClassicAssert.IsFalse(KingdomInheritanceStateRules.ShouldRetryBuild(
				KingdomInheritApplyStatus.Failed, 1, false));
			ClassicAssert.IsFalse(KingdomInheritanceStateRules.ShouldRetryBuild(
				KingdomInheritApplyStatus.Failed, 2, true));
			ClassicAssert.IsFalse(KingdomInheritanceStateRules.ShouldRetryBuild(
				KingdomInheritApplyStatus.Refused, 1, true));
			ClassicAssert.IsTrue(KingdomInheritanceStateRules.CanTransition(
				KingdomInheritancePhase.RepairRequired,
				KingdomInheritancePhase.AppliedPendingDurability));
		}

		[Test]
		public void CleanupDescriptorPreservesForeignSameClassPayloads()
		{
			ClassicAssert.IsTrue(KingdomInheritanceStateRules.IsExactSiteBuilder(
				"KingdomInheritedSiteBuilder", "legacy", "target", "zone", 1,
				"legacy", "target", "zone", 1));
			ClassicAssert.IsFalse(KingdomInheritanceStateRules.IsExactSiteBuilder(
				"KingdomInheritedSiteBuilder", "foreign", "target", "zone", 1,
				"legacy", "target", "zone", 1));
			ClassicAssert.IsFalse(KingdomInheritanceStateRules.IsExactSiteBuilder(
				"KingdomInheritedSiteBuilder", "legacy", "target", "zone", 2,
				"legacy", "target", "zone", 1));
			ClassicAssert.IsTrue(KingdomInheritanceStateRules.IsExactLocationFinder(
				"AddLocationFinder", "secret", 1, "secret"));
			ClassicAssert.IsFalse(KingdomInheritanceStateRules.IsExactLocationFinder(
				"AddLocationFinder", "secret", 0, "secret"));
			ClassicAssert.IsTrue(KingdomInheritanceStateRules.IsExactLocationFinderBuilder(
				"KingdomInheritanceLocationFinderBuilder", "legacy", "target", "zone", 1,
				"legacy", "target", "zone", 1));
			ClassicAssert.IsFalse(KingdomInheritanceStateRules.IsExactLocationFinderBuilder(
				"KingdomInheritanceLocationFinderBuilder", "foreign", "target", "zone", 1,
				"legacy", "target", "zone", 1));
		}

		[Test]
		public void SavedShapeAcceptsExactPendingAndCommittedStates()
		{
			KingdomInheritanceSavedShape pending = PendingShape();
			string failure;
			ClassicAssert.IsTrue(KingdomInheritanceStateRules.TryValidateSavedShape(pending,
				"target-game", 1, out failure), failure);

			KingdomSealReceipt committed = new KingdomSealReceipt
			{
				LineageId = "lineage-a",
				LegacyId = "legacy-a",
				TargetGameId = "target-game",
				State = KingdomSealReceiptState.Committed,
				WrittenTick = 400L
			};
			pending.PhaseValue = (int)KingdomInheritancePhase.Committed;
			pending.CommittedReceiptText = committed.Compose();
			ClassicAssert.IsTrue(KingdomInheritanceStateRules.TryValidateSavedShape(pending,
				"target-game", 1, out failure), failure);
		}

		[Test]
		public void SchemaFourAndFiveAuthorityReplaysThroughEveryPersistedPhase()
		{
			KingdomInheritancePhase[] phases =
			{
				KingdomInheritancePhase.Reserved, KingdomInheritancePhase.SiteSelected,
				KingdomInheritancePhase.WorldValidated, KingdomInheritancePhase.Installed,
				KingdomInheritancePhase.AppliedPendingDurability,
				KingdomInheritancePhase.Committed, KingdomInheritancePhase.Refused,
				KingdomInheritancePhase.RepairRequired
			};
			for (int legacySchema = 4; legacySchema <= 5; legacySchema++)
				for (int receiptSchema = 4; receiptSchema <= 6; receiptSchema++)
					for (int i = 0; i < phases.Length; i++)
					{
						if (phases[i] == KingdomInheritancePhase.Committed)
						{
							for (int committedSchema = 4; committedSchema <= 6; committedSchema++)
								AssertPriorSchemaShape(legacySchema, receiptSchema,
									committedSchema, phases[i]);
						}
						else AssertPriorSchemaShape(legacySchema, receiptSchema, 6, phases[i]);
					}
		}

		[Test]
		public void SavedShapeRejectsCorruptOwnershipStatusAndFault()
		{
			string failure;
			KingdomInheritanceSavedShape shape = PendingShape();
			shape.OwnsNoBiomes = false;
			ClassicAssert.IsFalse(KingdomInheritanceStateRules.TryValidateSavedShape(shape,
				"target-game", 1, out failure));

			shape = PendingShape();
			shape.PhaseValue = (int)KingdomInheritancePhase.RepairRequired;
			shape.ApplyStatus = (int)KingdomInheritApplyStatus.Failed;
			shape.ApplyFault = (int)KingdomInheritApplyFault.PartialApplication;
			shape.ApplicationMarker = "";
			shape.ReleasePending = true;
			shape.SiteName = "Foreign Exact Name";
			ClassicAssert.IsFalse(KingdomInheritanceStateRules.TryValidateSavedShape(shape,
				"target-game", 1, out failure),
				"saved cleanup authority cannot redefine the deterministic owned site name");

			shape = PendingShape();
			shape.OwnsZoneName = false;
			ClassicAssert.IsFalse(KingdomInheritanceStateRules.TryValidateSavedShape(shape,
				"target-game", 1, out failure));

			shape = PendingShape();
			shape.ApplyStatus = 999;
			ClassicAssert.IsFalse(KingdomInheritanceStateRules.TryValidateSavedShape(shape,
				"target-game", 1, out failure));

			shape = PendingShape();
			shape.ApplyFault = 999;
			ClassicAssert.IsFalse(KingdomInheritanceStateRules.TryValidateSavedShape(shape,
				"target-game", 1, out failure));
		}

		[Test]
		public void UnbuiltInstallRepairPersistsCleanupThenReleaseIntent()
		{
			KingdomInheritanceSavedShape shape = PendingShape();
			shape.PhaseValue = (int)KingdomInheritancePhase.RepairRequired;
			shape.ApplyStatus = (int)KingdomInheritApplyStatus.Failed;
			shape.ApplyFault = (int)KingdomInheritApplyFault.PartialApplication;
			shape.ApplicationMarker = "";
			shape.ReleasePending = true;
			string failure;
			ClassicAssert.IsTrue(KingdomInheritanceStateRules.TryValidateSavedShape(shape,
				"target-game", 1, out failure), failure);

			shape.ApplyStatus = (int)KingdomInheritApplyStatus.Applied;
			shape.ApplyFault = (int)KingdomInheritApplyFault.None;
			ClassicAssert.IsFalse(KingdomInheritanceStateRules.TryValidateSavedShape(shape,
				"target-game", 1, out failure),
				"release intent can never coexist with successful application evidence");
		}

		[Test]
		public void FallbackNeverDropsRepairBuildersBeforeZoneQuarantine()
		{
			ClassicAssert.IsFalse(KingdomInheritanceStateRules.ShouldAttemptFallbackArtifactCleanup(
				false, false), "unclean application retains its exact repair machinery");
			ClassicAssert.IsFalse(KingdomInheritanceStateRules.ShouldAttemptFallbackArtifactCleanup(
				true, true), "externally committed application can never enter release cleanup");
			ClassicAssert.IsTrue(KingdomInheritanceStateRules.ShouldAttemptFallbackArtifactCleanup(
				true, false));
			ClassicAssert.IsTrue(KingdomInheritanceStateRules.MustPersistFallbackReleaseIntent(
				true, false, false));
			ClassicAssert.IsFalse(KingdomInheritanceStateRules.MustPersistFallbackReleaseIntent(
				false, false, false));
			ClassicAssert.IsFalse(KingdomInheritanceStateRules.MustPersistFallbackReleaseIntent(
				true, true, false));
		}

		[Test]
		public void InvalidOrTruncatedShapeCanOnlyNormalizeToAuthorityFreeQuarantine()
		{
			string failure;
			KingdomInheritanceSavedShape disabled = new KingdomInheritanceSavedShape
			{
				PhaseValue = (int)KingdomInheritancePhase.RepairRequired,
				RecoveryDisabled = true
			};
			ClassicAssert.IsTrue(KingdomInheritanceStateRules.TryValidateSavedShape(disabled,
				"target-game", 1, out failure), failure);
			disabled.TargetZoneId = "JoppaWorld.4.5.1.2.10";
			disabled.OwnsSkipTerrainBuilders = true;
			disabled.OwnsNoBiomes = true;
			ClassicAssert.IsFalse(KingdomInheritanceStateRules.TryValidateSavedShape(disabled,
				"target-game", 1, out failure));

			KingdomInheritanceSavedShape empty = new KingdomInheritanceSavedShape
			{
				PhaseValue = (int)KingdomInheritancePhase.Empty,
				OwnsSkipTerrainBuilders = true,
				OwnsNoBiomes = true
			};
			ClassicAssert.IsFalse(KingdomInheritanceStateRules.TryValidateSavedShape(empty,
				"target-game", 1, out failure));

			KingdomInheritanceSavedShape committed = PendingShape();
			committed.PhaseValue = (int)KingdomInheritancePhase.Committed;
			ClassicAssert.IsFalse(KingdomInheritanceStateRules.TryValidateSavedShape(committed,
				"target-game", 1, out failure));

			KingdomInheritanceSavedShape refused = PendingShape();
			refused.PhaseValue = (int)KingdomInheritancePhase.Refused;
			ClassicAssert.IsFalse(KingdomInheritanceStateRules.TryValidateSavedShape(refused,
				"target-game", 1, out failure));
		}

		[TestCase(0, false, true)]
		[TestCase(0, true, false)]
		[TestCase(1, false, false)]
		[TestCase(2, false, false)]
		[TestCase(3, false, false)]
		[TestCase(4, false, false)]
		[TestCase(5, false, false)]
		[TestCase(6, false, false)]
		[TestCase(7, false, false)]
		[TestCase(8, false, true)]
		[TestCase(8, true, true)]
		public void EmptyLegacyNeedsNoReconstructionOnlyWhenEntireStateHasNoAuthority(
			int phase, bool disabled, bool expected)
		{
			KingdomInheritanceSavedShape shape = new KingdomInheritanceSavedShape
			{ PhaseValue = phase, RecoveryDisabled = disabled };
			int reconstruction = KingdomInheritEngine.ReconstructionVersionForText(shape.LegacyText);
			ClassicAssert.AreEqual(0, reconstruction, "actual empty-save route, not a fabricated fallback version");
			ClassicAssert.AreEqual(expected, KingdomInheritanceStateRules.TryValidateSavedShape(shape,
				"target-game", reconstruction, out _));
			ClassicAssert.IsFalse(KingdomInheritanceStateRules.TryValidateSavedShape(shape, "target-game", -1, out _));
		}

		[TestCase("LegacyText")]
		[TestCase("ReceiptText")]
		[TestCase("CommittedReceiptText")]
		[TestCase("TargetZoneId")]
		[TestCase("TargetTerrainBlueprint")]
		[TestCase("TargetTerrainRank")]
		[TestCase("SecretId")]
		[TestCase("SiteName")]
		[TestCase("ApplyStatus")]
		[TestCase("ApplyFault")]
		[TestCase("ApplicationMarker")]
		[TestCase("ReleasePending")]
		[TestCase("OwnsSkipTerrainBuilders")]
		[TestCase("OwnsNoBiomes")]
		[TestCase("OwnsZoneName")]
		[TestCase("RetryAuthorized")]
		public void ZeroReconstructionCannotHideAnyRetainedAuthorityField(string fieldName)
		{
			foreach (int phase in new[] { 0, 8 })
			{
				KingdomInheritanceSavedShape shape = new KingdomInheritanceSavedShape { PhaseValue = phase };
				System.Reflection.FieldInfo field = typeof(KingdomInheritanceSavedShape).GetField(fieldName,
					System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
				ClassicAssert.IsNotNull(field);
				object value = field.FieldType == typeof(string) ? (object)"retained"
					: field.FieldType == typeof(bool) ? (object)true : 0;
				field.SetValue(shape, value);
				ClassicAssert.IsFalse(KingdomInheritanceStateRules.TryValidateSavedShape(shape, "target-game", 0, out _));
			}
		}

		[Test]
		public void PayloadStillRequiresReconstructionAndReaderDelegatesWholeEmptyStateValidation()
		{
			ClassicAssert.IsFalse(KingdomInheritanceStateRules.TryValidateSavedShape(PendingShape(), "target-game", 0, out _));
			string source = TestMain.ReadRepositoryText("World/KingdomInheritanceState.z01.SerializationAndSelection.cs");
			StringAssert.Contains("invalid = !KingdomInheritanceStateRules.TryValidateSavedShape(shape,", source);
			StringAssert.DoesNotContain("invalid = reconstruction <= 0", source);
		}

		[Test]
		public void LoadClassifierAcceptsBothRootsAndTypedRollbackSources()
		{
			string temp = Path.Combine(Path.GetTempPath(), "taf-inherit-source");
			string syncedRoot = Path.Combine(temp, "synced", "Saves");
			string localRoot = Path.Combine(temp, "local", "Saves");
			string synced = Path.Combine(syncedRoot, "target-game", "Primary");
			string local = Path.Combine(localRoot, "target-game", "Primary");
			string failure;
			ClassicAssert.AreEqual(KingdomInheritanceLoadKind.Primary,
				KingdomInheritanceStateRules.ClassifyExactLoadSource(synced,
				syncedRoot, "target-game", FileAttributes.Directory, FileAttributes.Directory,
				true, FileAttributes.Normal, 1L, false, (FileAttributes)0, 0L,
				out failure), failure);
			ClassicAssert.AreEqual(KingdomInheritanceLoadKind.Primary,
				KingdomInheritanceStateRules.ClassifyExactLoadSource(local,
				localRoot, "target-game", FileAttributes.Directory, FileAttributes.Directory,
				true, FileAttributes.Normal, 1L, false, (FileAttributes)0, 0L,
				out failure), failure);
			ClassicAssert.AreEqual(KingdomInheritanceLoadKind.Primary,
				KingdomInheritanceStateRules.ClassifyExactLoadSource(local,
				localRoot, "target-game", FileAttributes.Directory, FileAttributes.Directory,
				false, (FileAttributes)0, 0L, true, FileAttributes.Normal, 1L,
				out failure), failure);
			ClassicAssert.AreEqual(KingdomInheritanceLoadKind.SameGameRollback,
				KingdomInheritanceStateRules.ClassifyExactLoadSource(
					Path.Combine(syncedRoot, "target-game", "Quick"), syncedRoot, "target-game",
				FileAttributes.Directory, FileAttributes.Directory, true, FileAttributes.Normal,
				1L, false, (FileAttributes)0, 0L, out failure));
			ClassicAssert.AreEqual(KingdomInheritanceLoadKind.SameGameRollback,
				KingdomInheritanceStateRules.ClassifyExactLoadSource(
					Path.Combine(localRoot, "target-game", "Checkpoint"), localRoot,
					"target-game", FileAttributes.Directory, FileAttributes.Directory,
					false, (FileAttributes)0, 0L, true, FileAttributes.Normal, 1L, out failure));
			ClassicAssert.AreEqual(KingdomInheritanceLoadKind.SameGameRollback,
				KingdomInheritanceStateRules.ClassifyExactLoadSource(
					Path.Combine(localRoot, "target-game", "Precognition"), localRoot,
					"target-game", FileAttributes.Directory, FileAttributes.Directory,
					true, FileAttributes.Normal, 1L, false, (FileAttributes)0, 0L, out failure));
			ClassicAssert.AreEqual(KingdomInheritanceLoadKind.Unknown,
				KingdomInheritanceStateRules.ClassifyExactLoadSource(
				Path.Combine(syncedRoot, "target-game", "primary"), syncedRoot, "target-game",
				FileAttributes.Directory, FileAttributes.Directory, true, FileAttributes.Normal,
				1L, false, (FileAttributes)0, 0L, out failure));
			ClassicAssert.AreEqual(KingdomInheritanceLoadKind.Unknown,
				KingdomInheritanceStateRules.ClassifyExactLoadSource(
				Path.Combine(syncedRoot, "TARGET-GAME", "Primary"), syncedRoot, "target-game",
				FileAttributes.Directory, FileAttributes.Directory, true, FileAttributes.Normal,
				1L, false, (FileAttributes)0, 0L, out failure));
			ClassicAssert.AreEqual(KingdomInheritanceLoadKind.Unknown,
				KingdomInheritanceStateRules.ClassifyExactLoadSource(synced + ".sav",
				syncedRoot, "target-game", FileAttributes.Directory, FileAttributes.Directory,
				true, FileAttributes.Normal, 1L, false, (FileAttributes)0, 0L, out failure));
			ClassicAssert.AreEqual(KingdomInheritanceLoadKind.Unknown,
				KingdomInheritanceStateRules.ClassifyExactLoadSource(synced,
				syncedRoot, "target-game", FileAttributes.Directory, FileAttributes.Directory,
				true, FileAttributes.Normal, 0L, true, FileAttributes.Normal, 1L, out failure));
			ClassicAssert.AreEqual(KingdomInheritanceLoadKind.Unknown,
				KingdomInheritanceStateRules.ClassifyExactLoadSource(synced,
				syncedRoot, "target-game", FileAttributes.Directory | FileAttributes.ReparsePoint,
				FileAttributes.Directory, true, FileAttributes.Normal, 1L, false,
				(FileAttributes)0, 0L, out failure));
			ClassicAssert.AreEqual(KingdomInheritanceLoadKind.Unknown,
				KingdomInheritanceStateRules.ClassifyExactLoadSource(
					Path.Combine(syncedRoot, "target-game", "Coda"), syncedRoot, "target-game",
					FileAttributes.Directory, FileAttributes.Directory, true,
					FileAttributes.Normal, 1L, false, (FileAttributes)0, 0L, out failure));
		}

		[Test]
		public void LoadSourceTrackerIsAsyncLocalAndConsumeOnce()
		{
			KingdomInheritanceLoadSourceFlow.Clear();
			KingdomInheritanceLoadSourceFlow.Record("one");
			string path;
			ClassicAssert.IsTrue(KingdomInheritanceLoadSourceFlow.TryConsume(out path));
			ClassicAssert.AreEqual("one", path);
			ClassicAssert.IsFalse(KingdomInheritanceLoadSourceFlow.TryConsume(out path));

			Task<string> first = Task.Run(async delegate
			{
				KingdomInheritanceLoadSourceFlow.Record("first");
				await Task.Yield();
				string value;
				return KingdomInheritanceLoadSourceFlow.TryConsume(out value) ? value : "missing";
			});
			Task<string> second = Task.Run(async delegate
			{
				KingdomInheritanceLoadSourceFlow.Record("second");
				await Task.Yield();
				string value;
				return KingdomInheritanceLoadSourceFlow.TryConsume(out value) ? value : "missing";
			});
			Task.WaitAll(first, second);
			ClassicAssert.AreEqual("first", first.Result);
			ClassicAssert.AreEqual("second", second.Result);
		}

		[Test]
		public void DeferredLoadResumeWaitsThenConsumesExactlyOnce()
		{
			KingdomInheritanceLoadKind kind;
			string failure;
			KingdomMasterDecision disabled = KingdomMasterRules.Observe(
				KingdomMasterLatchValue.Enabled, 1L, 0L, 0L, false, 10L);
			KingdomMasterDecision staged = KingdomMasterRules.Observe(disabled.State,
				disabled.ChangedAtTick, disabled.ResumeToken, disabled.AppliedResumeToken,
				true, 20L);
			ClassicAssert.IsFalse(KingdomInheritanceResumeRules.TryConsume(true,
				(int)KingdomInheritanceLoadKind.Primary, "", staged.AutomaticWorkAllowed,
				out kind, out failure),
				"master-off and the transition wake must retain the serialized slot");
			KingdomMasterDecision applied = KingdomMasterRules.ApplyResume(staged);
			bool transitionBoundaryAllowed = applied.AutomaticWorkAllowed
				&& applied.ChangedAtTick != 20L;
			ClassicAssert.IsFalse(transitionBoundaryAllowed,
				"publishing the resume token still consumes its equal-tick wake");
			KingdomMasterDecision next = KingdomMasterRules.Observe(applied.State,
				applied.ChangedAtTick, applied.ResumeToken, applied.AppliedResumeToken,
				true, 21L);
			ClassicAssert.IsTrue(KingdomInheritanceResumeRules.TryConsume(true,
				(int)KingdomInheritanceLoadKind.Primary, "",
				next.AutomaticWorkAllowed && next.ChangedAtTick != 21L,
				out kind, out failure));
			ClassicAssert.AreEqual(KingdomInheritanceLoadKind.Primary, kind);
			ClassicAssert.AreEqual("", failure);
			ClassicAssert.IsFalse(KingdomInheritanceResumeRules.TryConsume(false,
				(int)KingdomInheritanceLoadKind.Primary, "", true, out kind, out failure),
				"a retired slot must not form a backlog or duplicate recovery");
		}

		[Test]
		public void DeferredLoadResumeFailsClosedForMalformedSavedKind()
		{
			KingdomInheritanceLoadKind kind;
			string failure;
			ClassicAssert.IsTrue(KingdomInheritanceResumeRules.TryConsume(true, 99,
				"stale", true, out kind, out failure));
			ClassicAssert.AreEqual(KingdomInheritanceLoadKind.Unknown, kind);
			ClassicAssert.AreEqual("the saved deferred inheritance load kind was invalid", failure);
		}

		[Test]
		public void ZoneNameAndReachabilityProofsAreExact()
		{
			ClassicAssert.IsTrue(KingdomInheritanceStateRules.IsExactZoneNameFootprint("Old Seat",
				true, "", true, "", true, "", true, true, "Old Seat"));
			ClassicAssert.IsFalse(KingdomInheritanceStateRules.IsExactZoneNameFootprint("Old Seat",
				true, "changed", true, "", true, "", true, true, "Old Seat"));
			ClassicAssert.IsFalse(KingdomInheritanceStateRules.IsExactZoneNameFootprint("Old Seat",
				true, "", true, "", true, "", false, true, "Old Seat"));
			ClassicAssert.IsFalse(KingdomInheritanceStateRules.MeetsReachability(399));
			ClassicAssert.IsTrue(KingdomInheritanceStateRules.MeetsReachability(400));
			ClassicAssert.IsFalse(KingdomInheritanceStateRules.CanTerminalizeHiddenFallback(399,
				1200), "an isolated large pocket cannot replace entry-rooted reachability");
			ClassicAssert.IsTrue(KingdomInheritanceStateRules.CanTerminalizeHiddenFallback(400, 0));
		}

		[Test]
		public void ZoneNameOwnershipAcceptsEveryExactTornSubsetAndRejectsMismatch()
		{
			for (int mask = 0; mask < 32; mask++)
			{
				bool hasName = (mask & 1) != 0;
				bool hasContext = (mask & 2) != 0;
				bool hasProper = (mask & 4) != 0;
				bool hasIndefinite = (mask & 8) != 0;
				bool hasDefinite = (mask & 16) != 0;
				ClassicAssert.IsTrue(KingdomInheritanceStateRules.IsCompatibleOwnedZoneNameSubset(
					hasName, hasName ? "Old Seat" : null,
					hasContext, hasContext ? "" : null,
					hasIndefinite, hasIndefinite ? "" : null,
					hasDefinite, hasDefinite ? "" : null,
					hasProper, hasProper, "Old Seat"), "exact torn subset mask " + mask);
			}
			ClassicAssert.IsFalse(KingdomInheritanceStateRules.IsCompatibleOwnedZoneNameSubset(
				true, "Foreign", false, null, false, null, false, null, false, false,
				"Old Seat"));
			ClassicAssert.IsTrue(KingdomInheritanceStateRules.CanClearZoneNameOwnership(false),
				"post-write callback failure cannot outweigh exact five-key absence");
			ClassicAssert.IsFalse(KingdomInheritanceStateRules.CanClearZoneNameOwnership(true));
			ClassicAssert.IsFalse(KingdomInheritanceStateRules.IsCompatibleOwnedZoneNameSubset(
				false, null, true, "changed", false, null, false, null, false, false,
				"Old Seat"));
			ClassicAssert.IsFalse(KingdomInheritanceStateRules.IsCompatibleOwnedZoneNameSubset(
				false, null, false, null, false, null, false, null, true, false,
				"Old Seat"));
		}

		[Test]
		public void FinderRequiresCanonicalNonNullMapNoteCategoryAndText()
		{
			ClassicAssert.IsTrue(KingdomInheritanceStateRules.IsUsableOwnedMapNote(true, true, true,
				"Settlements", "the old seat", "Settlements", "the old seat"));
			ClassicAssert.IsFalse(KingdomInheritanceStateRules.IsUsableOwnedMapNote(true, true, true,
				null, "the old seat", "Settlements", "the old seat"));
			ClassicAssert.IsFalse(KingdomInheritanceStateRules.IsUsableOwnedMapNote(true, true, true,
				"Lairs", "the old seat", "Settlements", "the old seat"));
			ClassicAssert.IsFalse(KingdomInheritanceStateRules.IsUsableOwnedMapNote(true, true, true,
				"Settlements", null, "Settlements", "the old seat"));
			ClassicAssert.IsFalse(KingdomInheritanceStateRules.IsUsableOwnedMapNote(true, false, true,
				"Settlements", "the old seat", "Settlements", "the old seat"));
			ClassicAssert.IsFalse(KingdomInheritanceStateRules.IsUsableOwnedMapNote(true, true, false,
				"Settlements", "the old seat", "Settlements", "the old seat"));
		}

		[Test]
		public void EmergencyCleanupRequiresExactOwnershipAndPropertiesBeforeBuildersLeave()
		{
			ClassicAssert.IsTrue(KingdomInheritanceStateRules.CanClaimEmergencyOwnership(2,
				1, 1, true, true, true));
			ClassicAssert.IsTrue(KingdomInheritanceStateRules.CanClaimEmergencyOwnership(3,
				1, 1, true, true, true),
				"unrelated foreign builders do not erase exact ownership");
			ClassicAssert.IsFalse(KingdomInheritanceStateRules.CanClaimEmergencyOwnership(3,
				2, 1, true, true, true));
			ClassicAssert.IsFalse(KingdomInheritanceStateRules.CanClaimEmergencyOwnership(2,
				1, 0, true, true, true));
			ClassicAssert.IsFalse(KingdomInheritanceStateRules.CanClaimEmergencyOwnership(2,
				1, 1, false, true, true));
			ClassicAssert.IsFalse(KingdomInheritanceStateRules.CanClaimEmergencyOwnership(2,
				1, 1, true, false, true));
			ClassicAssert.IsFalse(KingdomInheritanceStateRules.CanRegenerateAfterEmergencyCleanup(
				false, true, true), "builders must remain when cleanup tears");
			ClassicAssert.IsFalse(KingdomInheritanceStateRules.CanRegenerateAfterEmergencyCleanup(
				true, false, true), "properties must be absent before builder removal completes");
			ClassicAssert.IsTrue(KingdomInheritanceStateRules.CanRegenerateAfterEmergencyCleanup(
				true, true, true));
		}

		[Test]
		public void RepairAuthorityRequiresExactPreproof()
		{
			ClassicAssert.IsTrue(KingdomInheritanceStateRules.CanAuthorizeDirectRepair(true, 0, true));
			ClassicAssert.IsFalse(KingdomInheritanceStateRules.CanAuthorizeDirectRepair(false, 0, true));
			ClassicAssert.IsFalse(KingdomInheritanceStateRules.CanAuthorizeDirectRepair(true, 1, true));
			ClassicAssert.IsFalse(KingdomInheritanceStateRules.CanAuthorizeDirectRepair(true, 0, false));
		}

		[Test]
		public void CommittedReceiptSurvivesOldCheckpointCopyAndReconcilesOnPrimary()
		{
			ClassicAssert.IsTrue(KingdomInheritanceStateRules.ProfileReceiptBlocksRelease(
				KingdomSealReceiptState.Committed),
				"Unknown source may defer target mutation but can never release a final receipt");
			ClassicAssert.IsFalse(KingdomInheritanceStateRules.ProfileReceiptBlocksRelease(
				KingdomSealReceiptState.Reserved));
			ClassicAssert.AreEqual(KingdomCommittedRewindAction.DeferUntilPrimary,
				KingdomInheritanceStateRules.DecideCommittedRewind(
					KingdomInheritanceLoadKind.SameGameRollback, false, false, false, true,
					true, false), "an uncommitted receipt still requires Primary");
			ClassicAssert.AreEqual(KingdomCommittedRewindAction.DeferUntilPrimary,
				KingdomInheritanceStateRules.DecideCommittedRewind(
					KingdomInheritanceLoadKind.Unknown, true, false, true, true, true, true));
			ClassicAssert.AreEqual(KingdomCommittedRewindAction.AwaitLazyBuilder,
				KingdomInheritanceStateRules.DecideCommittedRewind(
					KingdomInheritanceLoadKind.SameGameRollback, true, false, false, true,
					true, false));
			ClassicAssert.AreEqual(KingdomCommittedRewindAction.ReapplyCleanBuiltTarget,
				KingdomInheritanceStateRules.DecideCommittedRewind(
					KingdomInheritanceLoadKind.SameGameRollback, true, false, true, true,
					true, true), "the sole rollback event must reconstruct before archive copy");
			ClassicAssert.AreEqual(KingdomCommittedRewindAction.AdoptDurable,
				KingdomInheritanceStateRules.DecideCommittedRewind(
					KingdomInheritanceLoadKind.SameGameRollback, true, true, true, false,
					true, false), "external commit makes repeat rollback adoption idempotent");
			ClassicAssert.AreEqual(KingdomCommittedRewindAction.RepairRequired,
				KingdomInheritanceStateRules.DecideCommittedRewind(
					KingdomInheritanceLoadKind.Primary, true, false, true, false, true, true));
		}

		[Test]
		public void DiscoveryRepairPreservesSuccessfulDurableMarkerProof()
		{
			const string marker = "exact-marker";
			ClassicAssert.IsTrue(
				KingdomInheritanceStateRules.PreservesApplicationProofDuringDiscoveryRepair(
					KingdomInheritancePhase.AppliedPendingDurability,
					(int)KingdomInheritApplyStatus.Applied,
					(int)KingdomInheritApplyFault.None, marker));
			ClassicAssert.IsTrue(
				KingdomInheritanceStateRules.PreservesApplicationProofDuringDiscoveryRepair(
					KingdomInheritancePhase.Committed,
					(int)KingdomInheritApplyStatus.AlreadyApplied,
					(int)KingdomInheritApplyFault.None, marker));
			ClassicAssert.IsFalse(
				KingdomInheritanceStateRules.PreservesApplicationProofDuringDiscoveryRepair(
					KingdomInheritancePhase.RepairRequired,
					(int)KingdomInheritApplyStatus.Applied,
					(int)KingdomInheritApplyFault.None, marker));
			ClassicAssert.IsTrue(KingdomInheritanceStateRules.IsDurableMarkerProof(
				KingdomInheritancePhase.RepairRequired,
				(int)KingdomInheritApplyStatus.Applied, true, marker, marker, marker, false));
			ClassicAssert.IsFalse(KingdomInheritanceStateRules.IsDurableMarkerProof(
				KingdomInheritancePhase.RepairRequired,
				(int)KingdomInheritApplyStatus.Failed, true, marker, marker, marker, false));
		}

		[Test]
		public void ReachabilityThrowAfterApplyRetainsOnlyExactQuarantinableRetryProof()
		{
			const string marker = "exact-marker";
			ClassicAssert.IsTrue(KingdomInheritanceStateRules.CanRetryUnvalidatedApplication(
				(int)KingdomInheritApplyStatus.Failed,
				(int)KingdomInheritApplyFault.PartialApplication, true, marker, marker, marker));
			ClassicAssert.IsFalse(KingdomInheritanceStateRules.CanRetryUnvalidatedApplication(
				(int)KingdomInheritApplyStatus.Applied,
				(int)KingdomInheritApplyFault.None, true, marker, marker, marker));
			ClassicAssert.IsFalse(KingdomInheritanceStateRules.CanRetryUnvalidatedApplication(
				(int)KingdomInheritApplyStatus.Failed,
				(int)KingdomInheritApplyFault.PartialApplication, false, marker, marker, marker));
			ClassicAssert.IsFalse(KingdomInheritanceStateRules.CanRetryUnvalidatedApplication(
				(int)KingdomInheritApplyStatus.Failed,
				(int)KingdomInheritApplyFault.PartialApplication, true, marker, "other", marker));
		}

		[Test]
		public void GameAndStartGatesFailClosedWithoutRejectingJoppaAlternateVillages()
		{
			ClassicAssert.IsFalse(KingdomInheritanceStateRules.ShouldOffer("Tutorial", false));
			ClassicAssert.IsFalse(KingdomInheritanceStateRules.ShouldOffer("Daily", false));
			ClassicAssert.IsFalse(KingdomInheritanceStateRules.ShouldOffer("Classic", true));
			ClassicAssert.IsTrue(KingdomInheritanceStateRules.ShouldOffer("Classic", false));
			ClassicAssert.AreEqual(KingdomInheritanceStartFault.None,
				KingdomInheritanceStateRules.ValidateStart("JoppaWorld.1.1.1.1.10",
					"JoppaWorld", "JoppaWorld.2.2.1.1.10"));
			ClassicAssert.AreEqual(KingdomInheritanceStartFault.AlternateWorld,
				KingdomInheritanceStateRules.ValidateStart("JoppaWorld.1.1.1.1.10",
					"AnotherWorld", "AnotherWorld.2.2.1.1.10"));
			ClassicAssert.AreEqual(KingdomInheritanceStartFault.TargetIsStart,
				KingdomInheritanceStateRules.ValidateStart("JoppaWorld.1.1.1.1.10",
					"JoppaWorld", "JoppaWorld.1.1.1.1.10"));
		}
	}
}
#endif
