#if TAF_TESTS
using System;
using System.IO;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomArchitectureRuntimeSourceTests
	{
		private static string Runtime()
		{
			return TestMain.ReadRepositoryText(
				Path.Combine("Growth", "KingdomArchitectureRuntime.cs"));
		}

		private static string Rules()
		{
			return string.Join("\n", new string[]
			{
				TestMain.ReadRepositoryText(Path.Combine("Growth", "KingdomArchitectureRules.cs")),
				TestMain.ReadRepositoryText(Path.Combine("Growth", "KingdomArchitectureCodecRules.cs")),
				TestMain.ReadRepositoryText(Path.Combine("Growth", "KingdomArchitectureDecodeRules.cs")),
				TestMain.ReadRepositoryText(Path.Combine("Growth", "KingdomArchitectureDeltaRules.cs"))
			});
		}

		private static string Plot()
		{
			return TestMain.ReadRepositoryText(Path.Combine("Growth", "KingdomPlot2.cs"));
		}

		private static string Zoning()
		{
			return TestMain.ReadRepositoryText(Path.Combine("Growth", "KingdomZoning.cs"));
		}

		private static string Socket()
		{
			return TestMain.ReadRepositoryText(Path.Combine("Growth", "KingdomSocket.cs"));
		}

		private static string PlanMarker()
		{
			return TestMain.ReadRepositoryText(Path.Combine("Growth", "KingdomPlanMarker.cs"));
		}

		[Test]
		public void ReceiptUsesOnlyNamedBoundedPropertiesAndCarriesFullFrozenIdentity()
		{
			string source = Runtime();
			string[] properties =
			{
				"r_TAF_ArchitectureSchema", "r_TAF_ArchitectureBuildKey",
				"r_TAF_ArchitecturePlanKey", "r_TAF_ArchitectureBindingKey",
				"r_TAF_ArchitectureTierKey", "r_TAF_ArchitectureVariantKey",
				"r_TAF_ArchitecturePaletteKey", "r_TAF_ArchitectureLotType",
				"r_TAF_ArchitectureLotSize", "r_TAF_ArchitectureFacing",
				"r_TAF_ArchitectureSnapshot", "r_TAF_ArchitectureHash",
				"r_TAF_ArchitectureRectX1", "r_TAF_ArchitectureRectY1",
				"r_TAF_ArchitectureRectX2", "r_TAF_ArchitectureRectY2",
				"r_TAF_ArchitectureMainX", "r_TAF_ArchitectureMainY"
			};
			for (int i = 0; i < properties.Length; i++) StringAssert.Contains(properties[i], source);
			StringAssert.Contains("public const int ReceiptSchema = 1;", source);
			StringAssert.Contains("KingdomArchitectureRules.MaxSnapshotChars", source);
			StringAssert.Contains("KingdomArchitectureRules.MaxKeyChars", source);
			StringAssert.Contains("public sealed class KingdomArchitectureIntent", source);
			Assert.IsFalse(source.Contains(": IPart"));
		}

		[Test]
		public void ExactTypedPrepareDerivesActualStakeAndNeverFallsBackToAnotherBinding()
		{
			string source = Runtime();
			string exact = Between(source,
				"public static bool TryPrepare(KingdomSystem System, Zone Z,\n\t\t\tKingdomPlotRules.PlotRect Rect, string BuildKey, string LotType,",
				"private static bool TryPrepareMapped(");
			AssertOrdered(exact, "TryRectLotSize(Rect, out actualSize)",
				"KingdomArchitecture.TryGetMapping(BuildKey, LotType, actualSize, out mapping)",
				"TryPrepareMapped(System, Z, Rect, BuildKey, mapping");
			StringAssert.Contains("Missing larger authored bindings never fall back", source);

			string prepare = Between(source, "private static bool TryPrepareMapped(",
				"public static bool TryPrepareSuccessor(");
			AssertOrdered(prepare, "System == null || !System.Founded",
				"TrySelectionContext(System, Z", "mapping.Frontage == ArchitectureFrontage.Road");
			StringAssert.Contains("KingdomArchitecture.TryResolve(BuildKey, mapping.TypeKey,", prepare);
			StringAssert.Contains("mapping.LotSize, context, facing", prepare);
			StringAssert.Contains("MatchesMapping(snapshot, mapping)", prepare);
			Assert.IsFalse(exact.Contains("SetIntProperty"));
			Assert.IsFalse(exact.Contains("SetStringProperty"));
			Assert.IsFalse(prepare.Contains("SetIntProperty"));
			Assert.IsFalse(prepare.Contains("SetStringProperty"));

			string context = Between(source,
				"private static bool TrySelectionContext(",
				"// --- Durable named receipt");
			StringAssert.Contains("Style = System.Style", context);
			StringAssert.Contains("KingdomCreed.SeatCreed(System)", context);
			StringAssert.Contains("KingdomResidentIdentityRules.FactNames(System.CultureCounts",
				context);
			StringAssert.Contains("KingdomResidentIdentityRules.FactNames(System.SpeciesCounts",
				context);
			StringAssert.Contains("KingdomResidentIdentityRules.IdentityNames(System.IdentityCounts",
				context);
			StringAssert.Contains("KingdomResidentIdentityRules.KindGenotype", context);
			StringAssert.Contains("KingdomResidentIdentityRules.KindBody", context);
			StringAssert.Contains("Z.GetTerrainObject()", context);
			StringAssert.Contains("System.FoundingTerrainBlueprint", context);
			StringAssert.Contains("Z.Z > KingdomRules.SurfaceZLevel ? \"underground\" : \"surface\"",
				context);
			StringAssert.Contains("TechLevel tech = KingdomZoning.Tech(System)", context);
			StringAssert.Contains("Stage = (int)System.Stage", context);
			StringAssert.Contains("Tech = (int)tech", context);
		}

		[Test]
		public void PickerOffersOnlyExactMappedLotsAndNeverOffersInternalHeartRungs()
		{
			string plot = Plot();
			string picker = Between(plot,
				"public static List<KingdomPlotRules.PlotSize> StakeableSizes(",
				"public static string ForesightFor(");
			AssertOrdered(picker, "KingdomPlotRules.StakeableSizes(",
				"KingdomArchitecture.TryGetMapping(", "sizes.RemoveAt(i)", "return sizes;");
			StringAssert.Contains("Entry.Key, Entry.Category, architectureSize", picker);
			StringAssert.Contains("Missing exact-size", picker);

			string zoning = Zoning();
			string offered = Between(zoning, "public static bool Offered(",
				"public static bool Visible(");
			StringAssert.Contains("KingdomPlotRules.HeartRungOf(Entry.Key) > 0", offered);
			StringAssert.Contains("rite-owned internal growth rungs", offered);
		}

		[Test]
		public void FacingUsesExactPoseAndDeterministicAuthoredRoadIngress()
		{
			string source = Runtime();
			string heart = Between(source, "private static bool TryHeartFacing(",
				"private static bool TryRoadFacing(");
			StringAssert.Contains("KingdomArchitectureRules.TryCanonicalDimensions", heart);
			StringAssert.Contains("KingdomPlots.HeartFor(Z, Rect", heart);
			StringAssert.Contains("KingdomArchitectureRules.TryDimensions(Mapping.LotSize, Facing",
				heart);
			StringAssert.Contains("posedWidth != Rect.Width || posedHeight != Rect.Height", heart);

			string road = Between(source, "private static bool TryRoadFacing(",
				"private static bool TryRectLotSize(");
			AssertOrdered(road, "ArchitectureFacing.North, ArchitectureFacing.East",
				"ArchitectureFacing.South, ArchitectureFacing.West");
			StringAssert.Contains("width != Rect.Width || height != Rect.Height", road);
			StringAssert.Contains("KingdomArchitecture.TryResolve(BuildKey, Mapping.TypeKey", road);
			StringAssert.Contains("TryRoadIngressScore(Z, Rect, resolved", road);
			StringAssert.Contains("if (score > bestScore)", road);
			Assert.IsFalse(road.Contains("score >= bestScore"),
				"equal road scores must retain fixed N/E/S/W candidate order");
			StringAssert.Contains("no authored public entrance connected to existing road evidence", road);

			string ingress = Between(source, "private static bool TryRoadIngressScore(",
				"private static bool TrySelectionContext(");
			StringAssert.Contains("anchor.Key == \"entrance:public\"", ingress);
			StringAssert.Contains("anchor.Key.StartsWith(\"entrance:public@\"", ingress);
			StringAssert.Contains("TryWorldAnchor(Snapshot, Rect, anchor", ingress);
			StringAssert.Contains("Rect.Contains(roadX, roadY)", ingress);
			StringAssert.Contains("KingdomRoads.FindOurFloor(cell, out floor)", ingress);
			StringAssert.Contains("KingdomRoads.ReadTally(Z)", ingress);
			StringAssert.Contains("KingdomRoadRules.WearAt(worn.Traffic)", ingress);
			StringAssert.Contains("KingdomRoadRules.WearState.Untouched", ingress);
			Assert.IsFalse(ingress.Contains("Math.Abs"));
			Assert.IsFalse(ingress.ToLowerInvariant().Contains("nearest"));
			Assert.IsFalse(source.Contains("GenericRectangle"),
				"runtime must not invent a fallback map");
			Assert.IsFalse(source.Contains("GenericShell"),
				"runtime must not invent a fallback shell");
		}

		[Test]
		public void SuccessorAllowsOnlyAdjacentRiteAnchoredHeartAccretion()
		{
			string source = Runtime();
			string successor = Between(source, "public static bool TryPrepareSuccessor(",
				"private static bool TryHeartBasinInvariant(");
			AssertOrdered(successor,
				"KingdomArchitecture.TryResolveSuccessor(before.BuildKey, SuccessorBuildKey",
				"KingdomArchitectureRules.TryBuildDelta(before, after",
				"KingdomPlots.HeartRung(Z) != beforeRung",
				"KingdomPlots.TryHeartRectFor(Z, beforeRung",
				"KingdomPlots.TryHeartRectFor(Z, afterRung",
				"TryHeartBasinInvariant(before, Before.Rect, Z",
				"TryHeartBasinInvariant(after, successorRect, Z",
				"mainX != Before.MainWorldX || mainY != Before.MainWorldY");
			StringAssert.Contains("afterRung == beforeRung + 1", successor);
			StringAssert.Contains("civic-heart", successor);
			string basin = Between(source, "private static bool TryHeartBasinInvariant(",
				"private static bool SameRect(");
			StringAssert.Contains("placement.Blueprint != \"r_KingdomFirstBasin\"", basin);
			StringAssert.Contains("placement.StatefulAnchor != \"fixture:first-basin\"", basin);
			StringAssert.Contains("basinX != riteX || basinY != riteY", basin);
			Assert.IsFalse(successor.Contains("GrowInPlace"));
			Assert.IsFalse(successor.Contains("Generic"));
		}

		[Test]
		public void FreezeValidatesBeforeMutationAndWritesSchemaAsLastCommitMarker()
		{
			string source = Runtime();
			string freeze = Between(source,
				"public static bool TryFreeze(",
				"public static bool TryRead(");
			int validate = freeze.IndexOf("TryValidateIntent(Intent", StringComparison.Ordinal);
			int firstMutation = freeze.IndexOf("Target.RemoveIntProperty(SchemaProperty)",
				StringComparison.Ordinal);
			int schemaWrite = freeze.IndexOf(
				"Target.SetIntProperty(SchemaProperty, ReceiptSchema);",
				StringComparison.Ordinal);
			Assert.GreaterOrEqual(validate, 0);
			Assert.Greater(firstMutation, validate);
			Assert.Greater(schemaWrite, firstMutation);
			Assert.AreEqual(schemaWrite,
				freeze.LastIndexOf("Target.Set", StringComparison.Ordinal),
				"schema must be final property write");
			StringAssert.Contains("TryRead(Target, out read", freeze);
			StringAssert.Contains("Target.RemoveIntProperty(SchemaProperty)", freeze);
		}

		[Test]
		public void ReadFailsClosedAndProvesEveryScalarAgainstCanonicalSnapshot()
		{
			string source = Runtime();
			string read = Between(source,
				"public static bool TryRead(",
				"public static bool TryCopyFrozen(");
			StringAssert.Contains("receipt is absent or only partially written", read);
			StringAssert.Contains("receipt schema \" + schema + \" is unknown", read);
			StringAssert.Contains("ReadString(Source, SnapshotProperty", read);
			StringAssert.Contains("ReadString(Source, HashProperty", read);
			StringAssert.Contains("ReadInt(Source, RectX1Property", read);
			StringAssert.Contains("ReadInt(Source, MainXProperty", read);
			StringAssert.Contains("TryValidateIntent(read", read);

			string validation = Between(source,
				"private static bool TryValidateIntent(",
				"// --- Exact canonical-to-world helpers");
			StringAssert.Contains("KingdomArchitectureRules.TryDecodeSnapshot", validation);
			StringAssert.Contains("KingdomArchitectureRules.TryEncodedSnapshotHash", validation);
			StringAssert.Contains("snapshot.BuildKey != Intent.BuildKey", validation);
			StringAssert.Contains("snapshot.PlanKey != Intent.PlanKey", validation);
			StringAssert.Contains("snapshot.BindingKey != Intent.BindingKey", validation);
			StringAssert.Contains("snapshot.TierKey != Intent.TierKey", validation);
			StringAssert.Contains("snapshot.VariantKey != Intent.VariantKey", validation);
			StringAssert.Contains("snapshot.PaletteKey != Intent.PaletteKey", validation);
			StringAssert.Contains("snapshot.LotType != Intent.LotType", validation);
			StringAssert.Contains("snapshot.LotSize != Intent.LotSize", validation);
			StringAssert.Contains("snapshot.Facing != Intent.Facing", validation);
			StringAssert.Contains("mainX != Intent.MainWorldX || mainY != Intent.MainWorldY",
				validation);
		}

		[Test]
		public void SnapshotCodecWritesA2AndReadsA1OnlyAtItsOriginalVersion()
		{
			string source = Rules();
			StringAssert.Contains("public const int LegacySnapshotSchema = 1;", source);
			StringAssert.Contains("public const int SnapshotSchema = 2;", source);
			StringAssert.Contains(
				"return TryEncodeSnapshotVersion(Snapshot, SnapshotSchema, out Encoded, out Failure);",
				source);
			StringAssert.Contains("private static bool TryEncodeSnapshotVersion(", source);

			string decode = Between(source, "public static bool TryDecodeSnapshot(",
				"public static bool TrySnapshotHash(");
			StringAssert.Contains("terms[0] == \"a1\" ? LegacySnapshotSchema", decode);
			StringAssert.Contains("terms[0] == \"a2\" ? SnapshotSchema", decode);
			StringAssert.Contains("reader.ReadByte() != schema", decode);
			StringAssert.Contains("schema == LegacySnapshotSchema", decode);
			StringAssert.Contains("TryEncodeSnapshotVersion(parsed, schema", decode);
			StringAssert.Contains("canonical != Encoded", decode);

			string version = Between(source, "public static bool IsCurrentSnapshotEncoding(",
				"// --- Exact tier delta");
			StringAssert.Contains("Encoded.StartsWith(\"a2|\"", version);
			StringAssert.Contains("LegacyPlacementTruthOnly", source);
		}

		[Test]
		public void FrozenReadCopyAndWorldTransformsNeverRereadCurrentCatalogues()
		{
			string source = Runtime();
			string durable = source.Substring(source.IndexOf(
				"// --- Durable named receipt", StringComparison.Ordinal));
			Assert.IsFalse(durable.Contains("KingdomArchitecture.TryGetMapping"));
			Assert.IsFalse(durable.Contains("KingdomArchitecture.TryResolve"));
			Assert.IsFalse(durable.Contains("KingdomData"));
			Assert.IsFalse(durable.Contains("GameObjectFactory"));
			Assert.IsFalse(durable.Contains("KingdomPlots"));

			string copy = Between(source,
				"public static bool TryCopyFrozen(",
				"public static bool TryValidate(");
			int read = copy.IndexOf("TryRead(Source, out intent", StringComparison.Ordinal);
			int freeze = copy.IndexOf("TryFreeze(Target, intent", StringComparison.Ordinal);
			Assert.GreaterOrEqual(read, 0);
			Assert.Greater(freeze, read);

			string transforms = Between(source,
				"public static bool TryWorldCell(",
				"// --- Small receipt helpers");
			StringAssert.Contains("ContainsCell(Snapshot, Cell)", transforms);
			StringAssert.Contains("ContainsPlacement(Snapshot, Placement)", transforms);
			StringAssert.Contains("ContainsAnchor(Snapshot, Anchor)", transforms);
			StringAssert.Contains("KingdomArchitectureRules.TryWorldDimensions", transforms);
			StringAssert.Contains("KingdomArchitectureRules.TryToWorld", transforms);
			StringAssert.Contains("Rect.Contains(WorldX, WorldY)", transforms);
		}

		[Test]
		public void PlotPayloadV2IsCanonicalHashedBoundedAndV1IsReadOnly()
		{
			string source = Plot();
			string codec = Between(source,
				"internal static bool TryPreparePlotPayload(",
				"internal static void RetryConstruction(");
			StringAssert.Contains("KingdomArchitectureRuntime.TryPrepare", codec);
			StringAssert.Contains("KingdomArchitectureRuntime.TryValidate", codec);
			StringAssert.Contains("string preimage = \"v2|\"", codec);
			StringAssert.Contains("System.Security.Cryptography.SHA256.Create()", codec);
			StringAssert.Contains("KingdomConstructionRules.MaxPayloadChars", codec);
			StringAssert.Contains("KingdomArchitectureRules.TryDecodeSnapshot", codec);
			StringAssert.Contains("canonical != Payload", codec);
			StringAssert.Contains("Payload.StartsWith(\"v1|\"", codec);
			StringAssert.Contains("private static bool TryDecodeLegacyPlotPayload", codec);
			Assert.IsFalse(source.Contains("internal static string EncodePlotPayload("),
				"no current path may write the legacy payload");
			Assert.IsFalse(source.Contains("return \"v1|\""),
				"v1 is compatibility input only");
		}

		[Test]
		public void DirectAndPlanFundingPrepareExactAuthoredMainBeforeAnyDebit()
		{
			string plot = Plot();
			string commission = Between(plot,
				"public static bool Commission(KingdomSystem System, Zone Z, KingdomRules.BuildEntry Entry, string SkinKey, KingdomPlotRules.PlotSize Stake, out string Failure)",
				"private static KingdomPlotRules.PlotRect PlannedFootprint(");
			AssertOrdered(commission, "KingdomZoning.Permits(System, Z.ZoneID, Entry",
				"TryPreparePlotPayload(System, Z, rect", "ReserveExactWater(Entry.CostDrams)",
				"ReservePayment(Z, Entry.Key)", "KingdomConstruction.NewJob(System, Z");
			StringAssert.Contains("KingdomConstructionRoute.PlotCommission, mainCell", commission);
			StringAssert.Contains("null, Entry.Key, payload", commission);
			Assert.IsFalse(commission.Contains("Z.GetCell(rect.CenterX, rect.CenterY)"));

			string preparePlan = Between(plot, "internal static bool TryPreparePlan(",
				"public static bool StakeFromPlan(KingdomSystem System, GameObject Marker, KingdomRules.BuildEntry Entry)");
			AssertOrdered(preparePlan, "KingdomZoning.Permits(System, zone.ZoneID, Entry",
				"TryFindRect(zone, System, Entry", "TryPreparePlotPayload(System, zone, Rect",
				"MainX = architecture.MainWorldX", "HasActiveAt(System, zone, main)");

			string planPass = Between(PlanMarker(), "public static void OnSettlementPass(",
				"private static int CountBuilt(");
			AssertOrdered(planPass, "KingdomPlots.TryPlanPrice(item, entry",
				"KingdomZoning.Permits(System, Z.ZoneID, entry",
				"KingdomPlots.TryPreparePlan(System, markerObject, entry",
				"KingdomConstruction.NewJob(System, Z, route, cell",
				"KingdomConstruction.FreezeBuildTruth(job",
				"Survey.ReserveExactWater(waterPrice)",
				"KingdomMaterials.ReserveComposite(Z, claim)");
			StringAssert.Contains("cell = Z.GetCell(mainX, mainY);", planPass);
		}

		[Test]
		public void StakePublishesOnlyAfterFrozenReceiptAndProvesItsMainCell()
		{
			string source = Plot();
			string stake = Between(source,
				"private static GameObject Stake(KingdomSystem System, Zone Z,",
				"private static bool RemoveCreatedWorks(");
			AssertOrdered(stake, "KingdomArchitectureRuntime.TryValidate(",
				"GameObject.Create(WorksBlueprint)",
				"KingdomArchitectureRuntime.TryFreeze(",
				"KingdomConstruction.UpdateOutput(ref Job, works.ID)",
				"cell.AddObject(works)");
			StringAssert.Contains("Z.GetCell(Architecture.MainWorldX, Architecture.MainWorldY)",
				stake);
			StringAssert.Contains("ExpectedWorks(works, cell, Entry.Key, Architecture, LegacyArchitecture, Job)",
				stake);
			StringAssert.Contains("LegacyArchitecture && (Architecture != null || Job == null)",
				stake);

			string expected = Between(source,
				"internal static bool ExpectedArchitectureReceipt(",
				"internal static bool TryPreparePlotPayload(");
			StringAssert.Contains("HasArchitectureReceiptEvidence(Object)", expected);
			StringAssert.Contains("KingdomArchitectureRuntime.TryRead(Object", expected);
			StringAssert.Contains("SameIntent(frozen, Intent)", expected);
		}

		[Test]
		public void RetryAndPlanProjectionUseOnlyTheirFrozenPayloadIntent()
		{
			string source = Plot();
			string retry = Between(source, "internal static void RetryConstruction(",
				"internal static void InspectConstruction(");
			StringAssert.Contains("TryDecodePlotPayload(Job.Payload", retry);
			StringAssert.Contains("Job.Route == KingdomConstructionRoute.PlotPlan", retry);
			StringAssert.Contains("string.IsNullOrEmpty(Job.OutputId)",
				Between(source, "private static KingdomPhysicalLookupState FindConstructionResult(",
					"public static int CountBuilt("));
			StringAssert.Contains("Valid works are",
				Between(source, "private static KingdomPhysicalLookupState FindConstructionResult(",
					"public static int CountBuilt("));
			Assert.IsFalse(retry.Contains("KingdomArchitectureRuntime.TryPrepare"));
			Assert.IsFalse(retry.Contains("KingdomArchitecture.TryResolve"));

			string projection = Between(source, "private static bool ProjectPlot(",
				"internal static bool ProjectOnRect(");
			StringAssert.Contains("TryDecodePlotPayload(Job.Payload", projection);
			StringAssert.Contains("!SamePlotSkin(paidSkin, SkinKey)", projection);
			StringAssert.Contains("architecture.MainWorldX", projection);
			Assert.IsFalse(projection.Contains("KingdomArchitectureRuntime.TryPrepare"));
			Assert.IsFalse(projection.Contains("KingdomArchitecture.TryResolve"));

			string planned = Between(source,
				"public static bool StakeFromPlan(KingdomSystem System, GameObject Marker,\n\t\t\tKingdomRules.BuildEntry Entry, KingdomConstructionJob Job,",
				"private static bool HeartGrowRefused(");
			StringAssert.Contains("TryDecodePlotPayload(current.Payload", planned);
			Assert.IsFalse(planned.Contains("TryFindRect("));
			Assert.IsFalse(planned.Contains("KingdomArchitectureRuntime.TryPrepare"));
			Assert.IsFalse(planned.Contains("KingdomArchitecture.TryResolve"));
		}

		[Test]
		public void FinalRootCopiesAndProvesFrozenReceiptBeforeInsertion()
		{
			string source = Plot();
			string finish = Between(source, "private static bool Finish(r_KingdomPlotWorks Works,",
				"private static bool FinishPlotEffects(");
			AssertOrdered(finish, "HasArchitectureReceiptEvidence(parent)",
				"KingdomArchitectureRuntime.TryRead(parent", "GameObject.Create(entry.Blueprint)",
				"KingdomArchitectureRuntime.TryCopyFrozen(parent, building",
				"cell.AddObject(building)", "ExactFinalBuilding(building");
			Assert.IsFalse(finish.Contains("KingdomArchitectureRuntime.TryPrepare"));
			Assert.IsFalse(finish.Contains("KingdomArchitecture.TryResolve"));

			string exact = Between(source, "private static bool ExactFinalBuilding(",
				"private static bool ClearGround(");
			StringAssert.Contains("ExpectedArchitectureReceipt(Building, Cell, Entry.Key", exact);
		}

		[Test]
		public void SocketBuildAndConversionPrepareV2MainBeforeDebitAndRetryFrozen()
		{
			string source = Socket();
			string validate = Between(source, "private static bool Validate(",
				"public static bool AssessConvert(");
			StringAssert.Contains("KingdomZoning.Permits(System, Z.ZoneID, newEntry", validate);
			StringAssert.Contains("KingdomPlots.TryFindRect(Z, System, newEntry, newSpec", validate);
			StringAssert.Contains("new KingdomPlots.GroundGrid(Z)", validate);

			string prepareConvert = Between(source, "private static bool TryPrepareConvert(",
				"public static bool ExecuteConvert(");
			AssertOrdered(prepareConvert, "Validate(System, Z, Building, NewKey",
				"KingdomUpgrade.TryPreparePlanChange(System, Z, Building",
				"KingdomArchitectureRuntime.TryPrepare(System, Z, context.TargetRect",
				"KingdomArchitectureStamper.TryPreflightRestake(System, Z, Building",
				"KingdomPlots.TryEncodePlotPayload(context.TargetRect");
			Assert.IsFalse(prepareConvert.Contains("ReserveExactWater"));

			string convert = Between(source, "private static bool ExecutePreparedConvert(",
				"private static bool ProjectConvertOrder(");
			AssertOrdered(convert, "Validate(System, Z, Building, Prepared.Context.NewEntry.Key",
				"KingdomUpgrade.BeginPreparedPlanChange(System, Z, Building",
				"KingdomArchitectureStamper.TryPreflightRestake(System, Z, Building",
				"survey.ReserveExactWater(context.NewEntry.CostDrams)",
				"KingdomMaterials.ReservePayment(Z, context.NewEntry.Key)",
				"KingdomConstruction.NewJob(System, Z");
			StringAssert.Contains("KingdomConstructionRoute.SocketConvert, mainCell", convert);
			StringAssert.Contains("context.NewEntry.Key, payload", convert);
			Assert.IsFalse(convert.Contains("KingdomArchitectureRuntime.TryPrepare"));
			Assert.IsFalse(convert.Contains("KingdomArchitecture.TryResolve"));

			string prepareBuild = Between(source, "private static bool TryPrepareSocketBuild(",
				"private static bool ExecuteSocketBuild(");
			AssertOrdered(prepareBuild, "KingdomZoning.Permits(System, Z.ZoneID, entry",
				"KingdomPlots.TryPreparePlotPayload(System, Z, rect",
				"Prepared = new PreparedSocketBuild");
			Assert.IsFalse(prepareBuild.Contains("ReserveExactWater"));
			string build = Between(source, "private static bool ExecuteSocketBuild(",
				"public static bool Redress(");
			AssertOrdered(build, "KingdomArchitectureStamper.TryPreflight(System, Z, architecture",
				"survey.ReserveExactWater(entry.CostDrams)",
				"KingdomMaterials.ReservePayment(Z, entry.Key)",
				"KingdomConstruction.NewJob(System, Z");
			StringAssert.Contains("KingdomConstructionRoute.SocketBuild, mainCell", build);
			StringAssert.Contains("entry.Key, payload", build);

			string retry = Between(source, "internal static void RetryConstruction(",
				"internal static void InspectConstruction(");
			StringAssert.Contains("KingdomPlots.TryDecodePlotPayload(Job.Payload", retry);
			StringAssert.Contains("KingdomPlots.ProjectOnRect(System, Z, rect", retry);
			Assert.IsFalse(retry.Contains("KingdomArchitectureRuntime.TryPrepare"));
			StringAssert.Contains("Valid works are",
				Between(source, "private static KingdomPhysicalLookupState FindSocketResult(",
					"private static void ContinueSocketBuild("));
			Assert.IsFalse(source.Contains("internal static string EncodePlotPayload("));
		}

		private static string Between(string Source, string Start, string End)
		{
			int start = Source.IndexOf(Start, StringComparison.Ordinal);
			Assert.GreaterOrEqual(start, 0, "missing source boundary: " + Start);
			int end = Source.IndexOf(End, start + Start.Length, StringComparison.Ordinal);
			Assert.Greater(end, start, "missing source boundary: " + End);
			return Source.Substring(start, end - start);
		}

		private static void AssertOrdered(string Source, params string[] Terms)
		{
			int previous = -1;
			for (int i = 0; i < Terms.Length; i++)
			{
				int found = Source.IndexOf(Terms[i], previous + 1, StringComparison.Ordinal);
				Assert.Greater(found, previous, "missing or out-of-order source term: " + Terms[i]);
				previous = found;
			}
		}
	}
}
#endif
