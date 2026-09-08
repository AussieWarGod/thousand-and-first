#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomArchitectureRulesTests
	{
		[Test]
		public void LotDimensions_AreCanonicalAndQuarterTurnsSwapAxes()
		{
			ArchitectureLotSize[] sizes = new ArchitectureLotSize[]
			{
				ArchitectureLotSize.Small, ArchitectureLotSize.Medium,
				ArchitectureLotSize.Large, ArchitectureLotSize.Huge
			};
			KingdomPlotRules.PlotSize[] plotSizes = new KingdomPlotRules.PlotSize[]
			{
				KingdomPlotRules.PlotSize.Small, KingdomPlotRules.PlotSize.Medium,
				KingdomPlotRules.PlotSize.Large, KingdomPlotRules.PlotSize.Huge
			};
			int[] widths = new int[] { 6, 8, 12, 20 };
			int[] heights = new int[] { 4, 6, 10, 18 };
			for (int i = 0; i < sizes.Length; i++)
			{
				ClassicAssert.IsTrue(KingdomArchitectureRules.TryCanonicalDimensions(
					sizes[i], out int width, out int height));
				ClassicAssert.AreEqual(widths[i], width);
				ClassicAssert.AreEqual(heights[i], height);
				ClassicAssert.IsTrue(KingdomPlotRules.TryDimensions(plotSizes[i],
					out int plotWidth, out int plotHeight));
				ClassicAssert.AreEqual(plotWidth, width, "architecture reads plot width authority");
				ClassicAssert.AreEqual(plotHeight, height, "architecture reads plot height authority");
				ClassicAssert.AreEqual(0, width % 2);
				ClassicAssert.AreEqual(0, height % 2);
				ClassicAssert.IsTrue(KingdomArchitectureRules.TryDimensions(sizes[i],
					ArchitectureFacing.East, out int eastWidth, out int eastHeight));
				ClassicAssert.AreEqual(height, eastWidth);
				ClassicAssert.AreEqual(width, eastHeight);
			}
			ClassicAssert.IsFalse(KingdomArchitectureRules.TryCanonicalDimensions(
				(ArchitectureLotSize)99, out _, out _));
		}

		[Test]
		public void ArchitectureCaps_AreDerivedFromTheExactXlEnvelope()
		{
			ClassicAssert.AreEqual(KingdomPlotRules.HugeWidth * KingdomPlotRules.HugeHeight,
				KingdomArchitectureRules.MaxMapArea);
			ClassicAssert.AreEqual(KingdomArchitectureRules.MaxMapArea * 2,
				KingdomArchitectureRules.MaxPlacements,
				"receipt admits ground plus one feature per cell on average");
			ClassicAssert.AreEqual(1024, KingdomArchitectureRules.MaxPoseRecords);
			ClassicAssert.AreEqual(12 * 1024, KingdomArchitectureRules.MaxSnapshotPayloadBytes);
			int exactTextEnvelope = "a4||".Length + 64
				+ 4 * ((KingdomArchitectureRules.MaxSnapshotPayloadBytes + 3) / 3);
			ClassicAssert.AreEqual(exactTextEnvelope, KingdomArchitectureRules.MaxSnapshotChars);
		}

		[Test]
		public void PoseTransforms_RoundTripEveryCellInEveryFacing()
		{
			foreach (ArchitectureFacing facing in Enum.GetValues(typeof(ArchitectureFacing)))
			{
				ClassicAssert.IsTrue(KingdomArchitectureRules.TryWorldDimensions(6, 4, facing,
					out int worldWidth, out int worldHeight));
				ClassicAssert.AreEqual(facing == ArchitectureFacing.East || facing == ArchitectureFacing.West
					? 4 : 6, worldWidth);
				ClassicAssert.AreEqual(facing == ArchitectureFacing.East || facing == ArchitectureFacing.West
					? 6 : 4, worldHeight);
				for (int v = 0; v < 4; v++)
					for (int u = 0; u < 6; u++)
					{
						ClassicAssert.IsTrue(KingdomArchitectureRules.TryToWorld(-20, 37, 6, 4,
							facing, u, v, out int x, out int y));
						ClassicAssert.IsTrue(KingdomArchitectureRules.TryToCanonical(-20, 37, 6, 4,
							facing, x, y, out int roundU, out int roundV));
						ClassicAssert.AreEqual(u, roundU);
						ClassicAssert.AreEqual(v, roundV);
					}
			}
			ClassicAssert.IsFalse(KingdomArchitectureRules.TryToWorld(int.MaxValue, 0, 6, 4,
				ArchitectureFacing.North, 5, 0, out _, out _));
			ClassicAssert.IsFalse(KingdomArchitectureRules.TryToCanonical(0, 0, 6, 4,
				ArchitectureFacing.North, 6, 0, out _, out _));
		}

		[Test]
		public void FixturePoseResolution_ComposesLocalAndLotCardinalsWithoutReceiptSchemaChange()
		{
			List<ArchitecturePoseDraft> poses = new List<ArchitecturePoseDraft>
			{
				new ArchitecturePoseDraft
				{
					Blueprint = "bench", Mode = ArchitecturePoseMode.Cardinal,
					North = "bench-n", East = "bench-e", South = "bench-s", West = "bench-w"
				}
			};
			string[] expected = new string[] { "bench-e", "bench-s", "bench-w", "bench-n" };
			foreach (ArchitectureFacing facing in Enum.GetValues(typeof(ArchitectureFacing)))
			{
				ClassicAssert.IsTrue(KingdomArchitectureRules.TryResolvePose(poses, "bench", true,
					ArchitectureFacing.East, facing, out string concrete, out string failure), failure);
				ClassicAssert.AreEqual(expected[(int)facing], concrete);
			}
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryResolvePose(new List<ArchitecturePoseDraft>
			{
				new ArchitecturePoseDraft { Blueprint = "wall", Mode = ArchitecturePoseMode.Connected }
			}, "wall", false, ArchitectureFacing.North, ArchitectureFacing.West,
				out string connected, out string connectedFailure), connectedFailure);
			ClassicAssert.AreEqual("wall", connected);
		}

		[Test]
		public void FixturePoseResolution_FailsClosedOnMissingDuplicateAndIncoherentDeclarations()
		{
			ArchitecturePoseDraft invariant = new ArchitecturePoseDraft
				{ Blueprint = "bed", Mode = ArchitecturePoseMode.Invariant };
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryResolvePose(
				new List<ArchitecturePoseDraft> { invariant }, "missing", false,
				ArchitectureFacing.North, ArchitectureFacing.North,
				out string undeclared, out string failure), failure);
			ClassicAssert.AreEqual("missing", undeclared, "undeclared vanilla scenery is invariant");
			ClassicAssert.IsFalse(KingdomArchitectureRules.TryResolvePose(
				new List<ArchitecturePoseDraft> { invariant }, "missing", true,
				ArchitectureFacing.North, ArchitectureFacing.North, out _, out failure));
			StringAssert.Contains("requires an exact cardinal", failure);
			ClassicAssert.IsFalse(KingdomArchitectureRules.TryResolvePose(
				new List<ArchitecturePoseDraft> { invariant, invariant }, "bed", false,
				ArchitectureFacing.North, ArchitectureFacing.North, out _, out failure));
			StringAssert.Contains("duplicate", failure);
			invariant.North = "bed-n";
			ClassicAssert.IsFalse(KingdomArchitectureRules.TryResolvePose(
				new List<ArchitecturePoseDraft> { invariant }, "bed", false,
				ArchitectureFacing.North, ArchitectureFacing.North, out _, out failure));
			StringAssert.Contains("incoherent directional siblings", failure);
		}

		[Test]
		public void CardinalPoseIdentityRequiresExplicitReviewForEveryTafSemanticFixture()
		{
			ClassicAssert.IsFalse(KingdomArchitectureRules.CardinalPoseIdentityAllowed(
				"r_KingdomUnlistedVisualFixture"));
			ClassicAssert.IsFalse(KingdomArchitectureRules.CardinalPoseIdentityAllowed("r_KingdomBench"));
			ClassicAssert.IsFalse(KingdomArchitectureRules.CardinalPoseIdentityAllowed("StairsDown"));
			ClassicAssert.IsFalse(KingdomArchitectureRules.CardinalPoseIdentityAllowed("StairsUp"));
			ClassicAssert.IsTrue(KingdomArchitectureRules.CardinalPoseIdentityAllowed("Bed"));
			ClassicAssert.IsTrue(KingdomArchitectureRules.CardinalPoseIdentityAllowed("OtherMod_Workbench"));
		}

		[Test]
		public void Compiler_FreezesConcreteFixtureSiblingAndEnforcesLayerLocalOrientation()
		{
			ArchitectureCompileRequest request = Request();
			ArchitecturePoseDraft bed = new ArchitecturePoseDraft
			{
				Blueprint = "Bed", Mode = ArchitecturePoseMode.Cardinal,
				North = "Bed N", East = "Bed E", South = "Bed S", West = "Bed W"
			};
			request.PoseRegistry = Registry(bed);
			ArchitectureGlyphDraft glyph = request.Map.Glyphs.Find(g => g.Character == 'b');
			glyph.HasObjectOrientation = true;
			glyph.ObjectOrientation = ArchitectureFacing.East;
			request.Facing = ArchitectureFacing.South;
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryCompile(request,
				out ArchitectureLayoutSnapshot snapshot, out string failure), failure);
			ClassicAssert.AreEqual("Bed W",
				FindPlacement(snapshot, ArchitectureLayer.Object, 3, 2).Blueprint);
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryEncodeSnapshot(snapshot,
				out string encoded, out failure), failure);
			StringAssert.StartsWith("a4|", encoded);
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryDecodeSnapshot(encoded,
				out ArchitectureLayoutSnapshot roundTrip, out failure), failure);
			ClassicAssert.AreEqual("Bed W",
				FindPlacement(roundTrip, ArchitectureLayer.Object, 3, 2).Blueprint,
				"a4 freezes the audited concrete sibling without a schema change");

			glyph.HasObjectOrientation = false;
			AssertCompileFails(request, "cardinal scenery requires");
			request = Request();
			request.Map.Glyphs.Find(g => g.Character == 'b').HasObjectOrientation = true;
			AssertCompileFails(request, "requires an exact cardinal fixture pose declaration");
		}

		[Test]
		public void Compiler_RejectsRawPoseBypassAndPoisonedSelectedPalette()
		{
			ArchitectureCompileRequest request = Request();
			request.Poses = new List<ArchitecturePoseDraft>
			{
				new ArchitecturePoseDraft { Blueprint = "Bed", Mode = ArchitecturePoseMode.Invariant }
			};
			AssertCompileFails(request, "refuses unaudited raw fixture pose declarations");

			request = Request();
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryCreatePoseRegistry(
				new List<ArchitecturePoseDraft>(), new string[] { "Bed" },
				out ArchitecturePoseRegistry poisoned, out string failure), failure);
			request.PoseRegistry = poisoned;
			AssertCompileFails(request, "selected palette references a malformed fixture pose declaration");
		}

		[Test]
		public void PoseRegistry_FreezesValidatedRowsAndRejectsPoisonOverlap()
		{
			ArchitecturePoseDraft authored = new ArchitecturePoseDraft
			{
				Blueprint = "Bed", Mode = ArchitecturePoseMode.Cardinal,
				North = "Bed N", East = "Bed E", South = "Bed S", West = "Bed W"
			};
			ArchitecturePoseRegistry registry = Registry(authored);
			authored.East = "mutated after freeze";
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryResolvePose(registry, "Bed", true,
				ArchitectureFacing.East, ArchitectureFacing.North,
				out string concrete, out string failure), failure);
			ClassicAssert.AreEqual("Bed E", concrete);
			ClassicAssert.IsFalse(KingdomArchitectureRules.TryCreatePoseRegistry(
				new List<ArchitecturePoseDraft> { authored }, new string[] { "Bed" },
				out _, out failure));
			StringAssert.Contains("overlapping", failure);
		}

		[Test]
		public void Compiler_ComposesEachPlacementLayerAndAllowsSymmetricPoseAliases()
		{
			ArchitectureCompileRequest request = Request();
			request.PoseRegistry = Registry(
				new ArchitecturePoseDraft
				{
					Blueprint = "Dirt Floor", Mode = ArchitecturePoseMode.Cardinal,
					North = "Dirt Axis NS", East = "Dirt Axis EW",
					South = "Dirt Axis NS", West = "Dirt Axis EW"
				},
				new ArchitecturePoseDraft
				{
					Blueprint = "Mud Wall", Mode = ArchitecturePoseMode.Cardinal,
					North = "Mud Wall N", East = "Mud Wall E",
					South = "Mud Wall S", West = "Mud Wall W"
				},
				new ArchitecturePoseDraft
				{
					Blueprint = "Bed", Mode = ArchitecturePoseMode.Cardinal,
					North = "Bed N", East = "Bed E", South = "Bed S", West = "Bed W"
				}
			);
			for (int i = 0; i < request.Map.Glyphs.Count; i++)
			{
				ArchitectureGlyphDraft authored = request.Map.Glyphs[i];
				if (!string.IsNullOrEmpty(authored.Ground))
				{
					authored.HasGroundOrientation = true;
					authored.GroundOrientation = ArchitectureFacing.North;
				}
			}
			ArchitectureGlyphDraft wall = request.Map.Glyphs.Find(g => g.Character == '#');
			wall.GroundOrientation = ArchitectureFacing.South;
			wall.HasStructureOrientation = true;
			wall.StructureOrientation = ArchitectureFacing.West;
			ArchitectureGlyphDraft bed = request.Map.Glyphs.Find(g => g.Character == 'b');
			bed.HasObjectOrientation = true;
			bed.ObjectOrientation = ArchitectureFacing.East;
			request.Facing = ArchitectureFacing.East;

			ClassicAssert.IsTrue(KingdomArchitectureRules.TryCompile(request,
				out ArchitectureLayoutSnapshot snapshot, out string failure), failure);
			ClassicAssert.AreEqual("Dirt Axis EW",
				FindPlacement(snapshot, ArchitectureLayer.Ground, 0, 0).Blueprint);
			ClassicAssert.AreEqual("Mud Wall N",
				FindPlacement(snapshot, ArchitectureLayer.Structure, 0, 0).Blueprint);
			ClassicAssert.AreEqual("Bed S",
				FindPlacement(snapshot, ArchitectureLayer.Object, 3, 2).Blueprint);

			request = Request();
			ArchitectureGlyphDraft floor = request.Map.Glyphs.Find(g => g.Character == '_');
			floor.HasStructureOrientation = true;
			AssertCompileFails(request, "requires scenery on the same layer");
		}

		[Test]
		public void TypedSet_UsesDurableFoldedTypeAndExactSize()
		{
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryClassifySetChange(" Housing ",
				ArchitectureLotSize.Small, "HOUSING", ArchitectureLotSize.Small,
				out ArchitectureSetChange same));
			ClassicAssert.AreEqual(ArchitectureSetChange.SameSet, same);
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryClassifySetChange("housing",
				ArchitectureLotSize.Small, "housing", ArchitectureLotSize.Medium,
				out ArchitectureSetChange restake));
			ClassicAssert.AreEqual(ArchitectureSetChange.Restake, restake);
			ClassicAssert.IsFalse(KingdomArchitectureRules.TryClassifySetChange("", ArchitectureLotSize.Small,
				"housing", ArchitectureLotSize.Small, out _));
		}

		[Test]
		public void VariantSelection_IsOrderIndependentAndUsesPrioritySpecificityThenKey()
		{
			List<ArchitectureVariantDraft> variants = new List<ArchitectureVariantDraft>
			{
				Variant("fallback", 0, null),
				Variant("z-style", 5, new ArchitectureSelector { Styles = "barathrumite" }),
				Variant("b-specific", 5, new ArchitectureSelector
					{ Styles = "barathrumite", Terrains = "salt" }),
				Variant("a-specific", 5, new ArchitectureSelector
					{ Styles = "barathrumite", Terrains = "salt" })
			};
			ArchitectureSelectionContext context = new ArchitectureSelectionContext
				{ Style = "Barathrumite", Terrain = "SALT", Stage = 2, Tech = 1 };
			ClassicAssert.IsTrue(KingdomArchitectureRules.TrySelectVariant(variants, context,
				out ArchitectureVariantDraft selected, out string failure), failure);
			ClassicAssert.AreEqual("a-specific", selected.Key);
			variants.Reverse();
			ClassicAssert.IsTrue(KingdomArchitectureRules.TrySelectVariant(variants, context,
				out selected, out failure), failure);
			ClassicAssert.AreEqual("a-specific", selected.Key);
		}

		[Test]
		public void VariantSelection_AcceptsCanonicalStyleAndFrozenCompatibilityAlias()
		{
			List<ArchitectureVariantDraft> variants = new List<ArchitectureVariantDraft>
			{
				Variant("fallback", 0, null),
				Variant("stair", 10, new ArchitectureSelector { Styles = "gyre" })
			};
			ArchitectureSelectionContext context = new ArchitectureSelectionContext
			{
				Style = "moonstair",
				StyleKeys = new List<string> { "moonstair", "gyre" },
				Stage = 2,
				Tech = 1
			};
			ClassicAssert.IsTrue(KingdomArchitectureRules.TrySelectVariant(variants, context,
				out ArchitectureVariantDraft selected, out string failure), failure);
			ClassicAssert.AreEqual("stair", selected.Key);
		}

		[Test]
		public void VariantSelection_UsesCanonicalSurfaceFallbackAndDeepMap()
		{
			ArchitectureVariantDraft fallback = Variant("fallback", 0, null);
			fallback.MapKey = "surface-map";
			ArchitectureVariantDraft deep = Variant("deep", 30,
				new ArchitectureSelector { Strata = KingdomZoningRules.StratumDeep });
			deep.MapKey = "deepend-delve-deep-m0";
			List<ArchitectureVariantDraft> variants = new List<ArchitectureVariantDraft>
				{ fallback, deep };

			ArchitectureSelectionContext surface = new ArchitectureSelectionContext
			{
				Stratum = KingdomZoningRules.StratumOfGround(false), Stage = 2, Tech = 1
			};
			ClassicAssert.IsTrue(KingdomArchitectureRules.TrySelectVariant(variants, surface,
				out ArchitectureVariantDraft selected, out string failure), failure);
			ClassicAssert.AreEqual("fallback", selected.Key);
			ClassicAssert.AreEqual("surface-map", selected.MapKey);

			ArchitectureSelectionContext underground = new ArchitectureSelectionContext
			{
				Stratum = KingdomZoningRules.StratumOfGround(true), Stage = 2, Tech = 1
			};
			ClassicAssert.IsTrue(KingdomArchitectureRules.TrySelectVariant(variants, underground,
				out selected, out failure), failure);
			ClassicAssert.AreEqual("deep", selected.Key);
			ClassicAssert.AreEqual("deepend-delve-deep-m0", selected.MapKey);
		}

		[Test]
		public void TierSuccessor_PreservesFrozenVariantDespiteChangedLiveSelectors()
		{
			List<ArchitectureVariantDraft> variants = new List<ArchitectureVariantDraft>
			{
				Variant("fallback", 0, null),
				Variant("new-creed", 40, new ArchitectureSelector { Creeds = "Mechanimists" })
			};
			ArchitectureSelectionContext changed = new ArchitectureSelectionContext
				{ Creed = "Mechanimists", Stage = 3, Tech = 2 };
			ClassicAssert.IsTrue(KingdomArchitectureRules.TrySelectVariant(variants, changed,
				out ArchitectureVariantDraft fresh, out string failure), failure);
			ClassicAssert.AreEqual("new-creed", fresh.Key, "new commissions use current facts");
			ClassicAssert.IsTrue(KingdomArchitectureRules.TrySelectFrozenSuccessorVariant(variants,
				"fallback", out ArchitectureVariantDraft successor, out failure), failure);
			ClassicAssert.AreEqual("fallback", successor.Key, "paid fabric keeps receipt identity");
			ClassicAssert.IsFalse(KingdomArchitectureRules.TrySelectFrozenSuccessorVariant(variants,
				"missing", out _, out failure));
			StringAssert.Contains("exact frozen variant", failure);
		}

		[Test]
		public void VariantSelection_HonoursExclusionsRangesAndMandatoryFallback()
		{
			List<ArchitectureVariantDraft> variants = new List<ArchitectureVariantDraft>
			{
				Variant("conditional", 10, new ArchitectureSelector
				{
					Styles = "all,!eater", MinimumStage = 2, MaximumStage = 4,
					MinimumTech = 1, MaximumTech = 3
				}),
				Variant("fallback", 0, null)
			};
			ClassicAssert.IsTrue(KingdomArchitectureRules.TrySelectVariant(variants,
				new ArchitectureSelectionContext { Style = "eater", Stage = 3, Tech = 2 },
				out ArchitectureVariantDraft selected, out string failure), failure);
			ClassicAssert.AreEqual("fallback", selected.Key);
			ClassicAssert.IsTrue(KingdomArchitectureRules.TrySelectVariant(variants,
				new ArchitectureSelectionContext { Style = "water", Stage = 3, Tech = 2 },
				out selected, out failure), failure);
			ClassicAssert.AreEqual("conditional", selected.Key);
			variants.RemoveAt(1);
			ClassicAssert.IsFalse(KingdomArchitectureRules.TryValidateVariants(variants, out failure));
			StringAssert.Contains("fallback", failure);
		}

		[Test]
		public void VariantSelection_ComposesLiveCultureSpeciesGenotypeAndBodySets()
		{
			List<ArchitectureVariantDraft> variants = new List<ArchitectureVariantDraft>
			{
				Variant("fallback", 0, null),
				Variant("a-body", 10, new ArchitectureSelector
					{ Bodies = "robot,!wet-bodied" }),
				Variant("identity", 10, new ArchitectureSelector
				{
					Cultures = "Hindren", Species = "hindren",
					Genotypes = "True Kin", Bodies = "robot,broad-bodied,!wet-bodied"
				})
			};
			ArchitectureSelectionContext context = new ArchitectureSelectionContext
			{
				Cultures = new List<string> { "hindren", "mechanimist" },
				Species = new List<string> { "HINDREN" },
				Genotypes = new List<string> { "true kin" },
				Bodies = new List<string> { "broad-bodied", "robot" },
				Stage = 2,
				Tech = 1
			};
			ClassicAssert.IsTrue(KingdomArchitectureRules.TrySelectVariant(variants, context,
				out ArchitectureVariantDraft selected, out string failure), failure);
			ClassicAssert.AreEqual("identity", selected.Key,
				"identity dimensions add specificity before ordinal key breaks a true tie");

			context.Bodies = new List<string> { "robot", "wet-bodied" };
			ClassicAssert.IsTrue(KingdomArchitectureRules.TrySelectVariant(variants, context,
				out selected, out failure), failure);
			ClassicAssert.AreEqual("fallback", selected.Key,
				"any explicitly excluded live fact refuses the bounded set-valued selector");

			context.Bodies = new List<string>();
			ClassicAssert.IsTrue(KingdomArchitectureRules.TrySelectVariant(variants, context,
				out selected, out failure), failure);
			ClassicAssert.AreEqual("fallback", selected.Key,
				"named body positives cannot match an empty live roster");
		}

		[Test]
		public void PlanValidation_RejectsDuplicateTypedSetsAndTierLevels()
		{
			ArchitectureCompileRequest request = Request();
			ArchitecturePlanDraft plan = new ArchitecturePlanDraft { Key = "housing-plan" };
			plan.Bindings.Add(request.Binding);
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryValidatePlan(plan, out string failure), failure);

			ArchitectureBindingDraft duplicate = Binding("second", "HOUSING", ArchitectureLotSize.Small);
			duplicate.Tiers.Add(Tier("other", 0));
			plan.Bindings.Add(duplicate);
			ClassicAssert.IsFalse(KingdomArchitectureRules.TryValidatePlan(plan, out failure));
			plan.Bindings.RemoveAt(1);

			request.Binding.Tiers.Add(Tier("other", request.Tier.Level));
			ClassicAssert.IsFalse(KingdomArchitectureRules.TryValidatePlan(plan, out failure));
			StringAssert.Contains("level", failure);
		}

		[Test]
		public void PlanValidation_AcceptsHeartOrRoadFrontageAndRejectsUnknownFrontage()
		{
			ArchitectureCompileRequest request = Request();
			ArchitecturePlanDraft plan = new ArchitecturePlanDraft { Key = "housing-plan" };
			plan.Bindings.Add(request.Binding);
			request.Binding.Frontage = ArchitectureFrontage.Road;
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryValidatePlan(plan, out string failure), failure);
			request.Binding.Frontage = (ArchitectureFrontage)99;
			ClassicAssert.IsFalse(KingdomArchitectureRules.TryValidatePlan(plan, out failure));
		}

		[Test]
		public void Compiler_MaterialisesAuthoredLayersAndSemanticAnchors()
		{
			ArchitectureCompileRequest request = Request();
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryCompile(request,
				out ArchitectureLayoutSnapshot snapshot, out string failure), failure);
			ClassicAssert.AreEqual(6, snapshot.Width);
			ClassicAssert.AreEqual(4, snapshot.Height);
			ClassicAssert.AreEqual(24, snapshot.Cells.Count);
			ClassicAssert.AreEqual(2, snapshot.MainX);
			ClassicAssert.AreEqual(1, snapshot.MainY);
			ClassicAssert.AreEqual(1, CountAnchorRole(snapshot, "main"));
			ClassicAssert.AreEqual(1, CountAnchorRole(snapshot, "entrance:public"));
			ClassicAssert.AreEqual(1, CountAnchorRole(snapshot, "function:dwelling"));
			ClassicAssert.AreEqual(1, CountAnchorRole(snapshot, "fixture:storage"));
			ClassicAssert.AreEqual(1, CountAnchorRole(snapshot, "sleep:bed"));
			ClassicAssert.IsNotNull(FindPlacement(snapshot, ArchitectureLayer.Object, 1, 2));
			ClassicAssert.IsNotNull(FindPlacement(snapshot, ArchitectureLayer.Object, 3, 2));
			ClassicAssert.IsNull(FindPlacement(snapshot, ArchitectureLayer.Object, 2, 1),
				"$building is main behavior metadata, not disposable scenery");
			StringAssert.StartsWith("fixture:storage@", FindPlacement(snapshot,
				ArchitectureLayer.Object, 1, 2).StatefulAnchor);
		}

		[Test]
		public void Compiler_ReusedSemanticGlyphsReceiveStableCoordinateIdentities()
		{
			ArchitectureCompileRequest request = Request();
			request.Map.Rows[2] = "#ssb_#";
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryCompile(request,
				out ArchitectureLayoutSnapshot snapshot, out string failure), failure);
			ClassicAssert.AreEqual(2, CountAnchorRole(snapshot, "fixture:storage"));
			ArchitecturePlacement first = FindPlacement(snapshot, ArchitectureLayer.Object, 1, 2);
			ArchitecturePlacement second = FindPlacement(snapshot, ArchitectureLayer.Object, 2, 2);
			ClassicAssert.AreNotEqual(first.StatefulAnchor, second.StatefulAnchor);
			StringAssert.EndsWith("@1,2", first.StatefulAnchor);
			StringAssert.EndsWith("@2,2", second.StatefulAnchor);
		}

		[Test]
		public void Compiler_BenefitCustodyAnchorCoexistsWithFunctionalTopology()
		{
			ArchitectureCompileRequest request = Request();
			ArchitectureGlyphDraft storage = request.Map.Glyphs.Find(g => g.Character == 's');
			storage.Anchors.Add("benefit:larder-main");
			storage.Anchors.Add("light:store");
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryCompile(request,
				out ArchitectureLayoutSnapshot snapshot, out string failure), failure);
			ArchitecturePlacement placement = FindPlacement(snapshot,
				ArchitectureLayer.Object, 1, 2);
			StringAssert.StartsWith("benefit:larder-main@", placement.StatefulAnchor);
			ClassicAssert.AreEqual(1, CountAnchorRole(snapshot, "fixture:storage"));
			ClassicAssert.AreEqual(1, CountAnchorRole(snapshot, "light:store"));

			request = Request();
			storage = request.Map.Glyphs.Find(g => g.Character == 's');
			storage.Anchors.Add("benefit:larder-main");
			storage.Anchors.Add("benefit:larder-spare");
			AssertCompileFails(request, "exactly one benefit custody anchor");

			request = Request();
			storage = request.Map.Glyphs.Find(g => g.Character == 's');
			storage.Anchors.Add("light:store");
			AssertCompileFails(request, "exactly one stable functional anchor");
		}

		[Test]
		public void Compiler_AllowsSeveralPublicEntrancesAndChecksEachOne()
		{
			ArchitectureCompileRequest request = Request();
			request.Map.Rows[3] = "##+###";
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryCompile(request,
				out ArchitectureLayoutSnapshot snapshot, out string failure), failure);
			ClassicAssert.AreEqual(2, CountAnchorRole(snapshot, "entrance:public"));
			ClassicAssert.AreNotEqual(FindAnchor(snapshot, "entrance:public", 0).Key,
				FindAnchor(snapshot, "entrance:public", 1).Key);
		}

		[Test]
		public void Compiler_FreezesExplicitFootprintWithoutConflatingCoveredYard()
		{
			ArchitectureCompileRequest request = Request();
			request.CatalogueFootprintWidth = 4;
			request.CatalogueFootprintHeight = 4;
			request.Map.HasFootprint = true;
			request.Map.FootprintX = 1;
			request.Map.FootprintY = 0;
			request.Map.FootprintWidth = 4;
			request.Map.FootprintHeight = 4;
			request.Map.Glyphs.Find(g => g.Character == '#').Claim = ArchitectureClaim.Yard;
			for (int i = 0; i < request.Map.Glyphs.Count; i++)
				if (request.Map.Glyphs[i].Character != '#')
				{
					request.Map.Glyphs[i].HasCover = true;
					request.Map.Glyphs[i].Cover = ArchitectureCover.Open;
				}
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryCompile(request,
				out ArchitectureLayoutSnapshot snapshot, out string failure), failure);
			ClassicAssert.AreEqual(1, snapshot.FootprintX);
			ClassicAssert.AreEqual(4, snapshot.FootprintWidth);
			ClassicAssert.AreEqual(ArchitectureClaim.Yard, FindCell(snapshot, 0, 0).Claim);
			ClassicAssert.AreEqual(ArchitectureCover.Walled, FindCell(snapshot, 0, 0).Cover,
				"covered yard remains legal outside building footprint");
			ClassicAssert.IsFalse(KingdomArchitectureRules.ContainsFootprintCell(snapshot, 0, 0));
			ClassicAssert.IsTrue(KingdomArchitectureRules.ContainsFootprintCell(snapshot,
				snapshot.MainX, snapshot.MainY));
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryEncodeSnapshot(snapshot,
				out string encoded, out failure), failure);
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryDecodeSnapshot(encoded,
				out ArchitectureLayoutSnapshot decoded, out failure), failure);
			ClassicAssert.AreEqual(ArchitectureClaim.Yard, FindCell(decoded, 0, 0).Claim);
			ClassicAssert.AreEqual(ArchitectureClaim.Building, FindCell(decoded, 2, 1).Claim);
		}

		[Test]
		public void Compiler_RequiresExactAuthoredOrImplicitFullFootprintAuthority()
		{
			ArchitectureCompileRequest request = Request();
			request.CatalogueFootprintWidth = 4;
			request.CatalogueFootprintHeight = 3;
			AssertCompileFails(request, "explicitly match");

			request = Request();
			request.Map.HasFootprint = true;
			request.Map.FootprintX = 1;
			request.Map.FootprintY = 0;
			request.Map.FootprintWidth = 5;
			request.Map.FootprintHeight = 4;
			AssertCompileFails(request, "fill-plot");

			request = Request();
			request.CatalogueFootprintWidth = 4;
			request.CatalogueFootprintHeight = 4;
			request.Map.HasFootprint = true;
			request.Map.FootprintX = 1;
			request.Map.FootprintY = 0;
			request.Map.FootprintWidth = 4;
			request.Map.FootprintHeight = 4;
			AssertCompileFails(request, "outside the frozen footprint");
		}

		[Test]
		public void SnapshotCodec_RequiresCompatibleAggregateAndLocalRoofTruth()
		{
			ArchitectureLayoutSnapshot snapshot = Compile();
			snapshot.BaseRoof = KingdomPlotRules.RoofState.Soft;
			ClassicAssert.IsFalse(KingdomArchitectureRules.TryEncodeSnapshot(snapshot,
				out _, out string failure));
			StringAssert.Contains("soft catalogue roof", failure);

			for (int i = 0; i < snapshot.Cells.Count; i++)
				snapshot.Cells[i].Cover = ArchitectureCover.Soft;
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryEncodeSnapshot(snapshot,
				out _, out failure), failure);

			snapshot.BaseRoof = KingdomPlotRules.RoofState.Carved;
			ClassicAssert.IsFalse(KingdomArchitectureRules.TryEncodeSnapshot(snapshot,
				out _, out failure));
			StringAssert.Contains("no local natural", failure);
			for (int i = 0; i < snapshot.Cells.Count; i++)
				snapshot.Cells[i].Cover = ArchitectureCover.Natural;
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryEncodeSnapshot(snapshot,
				out _, out failure), failure);

			snapshot.BaseRoof = KingdomPlotRules.RoofState.Open;
			ClassicAssert.IsFalse(KingdomArchitectureRules.TryEncodeSnapshot(snapshot,
				out _, out failure));
			StringAssert.Contains("open catalogue roof", failure);
			for (int i = 0; i < snapshot.Cells.Count; i++)
				snapshot.Cells[i].Cover = ArchitectureCover.Walled;
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryEncodeSnapshot(snapshot,
				out _, out failure), failure
					?? "an aggregate open plot may contain a local enclosed subwork");
		}

		[Test]
		public void Compiler_FailsClosedOnDimensionsRowsGlyphsPalettesAndRequirements()
		{
			ArchitectureCompileRequest request = Request();
			request.Map.Width = 4;
			AssertCompileFails(request, "dimensions");

			request = Request();
			request.Map.Rows[0] = "##?###";
			AssertCompileFails(request, "undefined");

			request = Request();
			request.Palette.Slots.Add(new ArchitecturePaletteSlot
				{ Key = "wall", Blueprint = "AnotherWall" });
			AssertCompileFails(request, "palette");

			request = Request();
			request.Map.Glyphs[0].Ground = "$missing";
			AssertCompileFails(request, "glyph");

			request = Request();
			request.Tier.Requirements.Add(new ArchitectureAnchorRequirement
				{ Role = "workbench", Minimum = 1 });
			AssertCompileFails(request, "workbench");
		}

		[Test]
		public void Compiler_DistinguishesFunctionalAnchorsFromProtectedState()
		{
			ArchitectureCompileRequest request = Request();
			request.Map.Glyphs.Find(g => g.Character == 's').Claim = ArchitectureClaim.Unclaimed;
			AssertCompileFails(request, "malformed");

			request = Request();
			request.Map.Glyphs.Find(g => g.Character == 's').StatefulObject = false;
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryCompile(request,
				out ArchitectureLayoutSnapshot replaceable, out string functionalFailure),
				functionalFailure);
			ClassicAssert.AreEqual(1, CountAnchorRole(replaceable, "fixture:storage"));
			ClassicAssert.IsNull(FindPlacement(replaceable, ArchitectureLayer.Object, 1, 2)
				.StatefulAnchor, "semantic function does not silently create upgrade custody");

			request = Request();
			request.Map.Glyphs.Find(g => g.Character == '@').StatefulObject = false;
			AssertCompileFails(request, "stateful");

			ArchitectureLayoutSnapshot snapshot = Compile();
			FindCell(snapshot, 0, 0).Claim = ArchitectureClaim.Unclaimed;
			ClassicAssert.IsFalse(KingdomArchitectureRules.TryValidateTopology(snapshot, null,
				out string failure));
			StringAssert.Contains("placement", failure);
		}

		[Test]
		public void Topology_RejectsInternalEntranceAndUnreachableFixture()
		{
			ArchitectureLayoutSnapshot snapshot = Compile();
			ArchitectureAnchor entrance = FindAnchor(snapshot, "entrance:public", 0);
			entrance.X = 2;
			entrance.Y = 2;
			ClassicAssert.IsFalse(KingdomArchitectureRules.TryValidateTopology(snapshot, null,
				out string failure));
			StringAssert.Contains("boundary", failure);

			snapshot = Compile();
			FindCell(snapshot, 1, 1).Passability = ArchitecturePassability.Blocked;
			FindCell(snapshot, 2, 2).Passability = ArchitecturePassability.Blocked;
			ClassicAssert.IsFalse(KingdomArchitectureRules.TryValidateTopology(snapshot, null,
				out failure));
			StringAssert.Contains("unreachable", failure);
		}

		[Test]
		public void SnapshotCodec_IsCanonicalRoundTripsAndHashesExactLayout()
		{
			ArchitectureLayoutSnapshot snapshot = Compile();
			snapshot.Placements[0].Knowledge = "masonry";
			snapshot.Placements[0].Power = "grid";
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryEncodeSnapshot(snapshot,
				out string first, out string failure), failure);
			ClassicAssert.IsTrue(KingdomArchitectureRules.TrySnapshotHash(snapshot,
				out string hash, out failure), failure);
			ClassicAssert.AreEqual(64, hash.Length);
			ClassicAssert.IsTrue(first.EndsWith("|" + hash, StringComparison.Ordinal));
			StringAssert.StartsWith("a4|", first);
			snapshot.Cells.Reverse();
			snapshot.Anchors.Reverse();
			snapshot.Placements.Reverse();
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryEncodeSnapshot(snapshot,
				out string shuffled, out failure), failure);
			ClassicAssert.AreEqual(first, shuffled);
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryDecodeSnapshot(first,
				out ArchitectureLayoutSnapshot decoded, out failure), failure);
			ClassicAssert.AreEqual(snapshot.PlanKey, decoded.PlanKey);
			ClassicAssert.AreEqual(snapshot.Cells.Count, decoded.Cells.Count);
			ClassicAssert.AreEqual(snapshot.Placements.Count, decoded.Placements.Count);
			ClassicAssert.AreEqual(snapshot.Anchors.Count, decoded.Anchors.Count);
			ClassicAssert.AreEqual("mud", decoded.Placements[0].Material);
			ClassicAssert.AreEqual("hands", decoded.Placements[0].MinTech);
			ClassicAssert.AreEqual("masonry", decoded.Placements[0].Knowledge);
			ClassicAssert.AreEqual("grid", decoded.Placements[0].Power);
			ClassicAssert.AreEqual(ArchitectureTransitionMode.None,
				decoded.IncomingTransitionMode);
			ClassicAssert.AreEqual(snapshot.FootprintX, decoded.FootprintX);
			ClassicAssert.AreEqual(snapshot.FootprintY, decoded.FootprintY);
			ClassicAssert.AreEqual(snapshot.FootprintWidth, decoded.FootprintWidth);
			ClassicAssert.AreEqual(snapshot.FootprintHeight, decoded.FootprintHeight);
			ClassicAssert.AreEqual(KingdomPlotRules.RoofState.Walled, decoded.BaseRoof);
			ClassicAssert.IsFalse(decoded.Placements[0].Natural);
			ClassicAssert.IsFalse(decoded.Placements[0].ExistingAuthority);
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryEncodeSnapshot(decoded,
				out string second, out failure), failure);
			ClassicAssert.AreEqual(first, second);
			decoded.BaseRoof = KingdomPlotRules.RoofState.Open;
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryEncodeSnapshot(decoded,
				out string changedRoof, out failure), failure);
			ClassicAssert.AreNotEqual(first, changedRoof, "catalogue roof is frozen hash authority");
		}

		[Test]
		public void SnapshotCodec_RejectsTamperFutureVersionsAndOversize()
		{
			int largestBoundedEncoding = "a4||".Length + 64
				+ 4 * ((KingdomArchitectureRules.MaxSnapshotPayloadBytes + 2) / 3);
			ClassicAssert.LessOrEqual(largestBoundedEncoding,
				KingdomArchitectureRules.MaxSnapshotChars,
				"the outer character cap must admit every payload the binary cap admits");
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryEncodeSnapshot(Compile(),
				out string encoded, out string failure), failure);
			string tamperedHash = encoded.Substring(0, encoded.Length - 1)
				+ (encoded[encoded.Length - 1] == '0' ? "1" : "0");
			ClassicAssert.IsFalse(KingdomArchitectureRules.TryDecodeSnapshot(tamperedHash,
				out _, out failure));
			StringAssert.Contains("hash", failure);
			string future = "a5" + encoded.Substring(2);
			ClassicAssert.IsFalse(KingdomArchitectureRules.TryDecodeSnapshot(future,
				out _, out failure));
			StringAssert.Contains("version", failure);
			ClassicAssert.IsFalse(KingdomArchitectureRules.TryDecodeSnapshot(
				new string('x', KingdomArchitectureRules.MaxSnapshotChars + 1), out _, out failure));
			string oversizedPayload = "a4|" + Convert.ToBase64String(
				new byte[KingdomArchitectureRules.MaxSnapshotPayloadBytes + 1])
				+ "|" + new string('0', 64);
			ClassicAssert.LessOrEqual(oversizedPayload.Length,
				KingdomArchitectureRules.MaxSnapshotChars,
				"this case must reach the binary bound rather than stop at the outer string bound");
			ClassicAssert.IsFalse(KingdomArchitectureRules.TryDecodeSnapshot(oversizedPayload,
				out _, out failure));
			StringAssert.Contains("byte bound", failure);
		}

		[Test]
		public void SnapshotCodec_CurrentWriterRejectsLegacyClaimTruth()
		{
			ArchitectureLayoutSnapshot snapshot = Compile();
			snapshot.Cells[0].Claim = ArchitectureClaim.LegacyClaimed;
			ClassicAssert.IsFalse(KingdomArchitectureRules.TryEncodeSnapshot(snapshot,
				out _, out string failure));
			StringAssert.Contains("legacy", failure);
		}

		[Test]
		public void SnapshotCodec_ReadsCanonicalV1ButNeverSilentlyUpgradesIt()
		{
			ArchitectureLayoutSnapshot legacy = Compile();
			for (int i = 0; i < legacy.Placements.Count; i++)
			{
				legacy.Placements[i].Material = null;
				legacy.Placements[i].MinTech = null;
				legacy.Placements[i].Natural = false;
				legacy.Placements[i].ExistingAuthority = false;
			}
			UseLegacyClaims(legacy);
			MethodInfo legacyWriter = typeof(KingdomArchitectureRules).GetMethod(
				"TryEncodeSnapshotVersion", BindingFlags.NonPublic | BindingFlags.Static);
			ClassicAssert.IsNotNull(legacyWriter);
			object[] args = new object[] { legacy, 1, null, null };
			ClassicAssert.IsTrue((bool)legacyWriter.Invoke(null, args), args[3] as string);
			string encoded = (string)args[2];
			StringAssert.StartsWith("a1|", encoded);
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryDecodeSnapshot(encoded,
				out ArchitectureLayoutSnapshot decoded, out string failure), failure);
			ClassicAssert.IsNull(decoded.Placements[0].Material);
			ClassicAssert.IsFalse(KingdomArchitectureRules.TryEncodeSnapshot(decoded,
				out _, out failure));
			ClassicAssert.IsNotEmpty(failure);
		}

		[Test]
		public void SnapshotCodec_FreezesIncomingModeAndReadsA2AsNonCurrent()
		{
			ArchitectureLayoutSnapshot target = Compile();
			target.IncomingTransitionMode = ArchitectureTransitionMode.Renovate;
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryEncodeSnapshot(target,
				out string renovate, out string failure), failure);
			target.IncomingTransitionMode = ArchitectureTransitionMode.Additive;
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryEncodeSnapshot(target,
				out string additive, out failure), failure);
			ClassicAssert.AreNotEqual(renovate, additive, "incoming edge is hash authority");
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryDecodeSnapshot(renovate,
				out ArchitectureLayoutSnapshot decoded, out failure), failure);
			ClassicAssert.AreEqual(ArchitectureTransitionMode.Renovate,
				decoded.IncomingTransitionMode);

			ArchitectureLayoutSnapshot old = Compile();
			UseLegacyClaims(old);
			MethodInfo writer = typeof(KingdomArchitectureRules).GetMethod(
				"TryEncodeSnapshotVersion", BindingFlags.NonPublic | BindingFlags.Static);
			object[] args = new object[] { old, 2, null, null };
			ClassicAssert.IsTrue((bool)writer.Invoke(null, args), args[3] as string);
			string a2 = (string)args[2];
			StringAssert.StartsWith("a2|", a2);
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryDecodeSnapshot(a2,
				out decoded, out failure), failure);
			ClassicAssert.AreEqual(ArchitectureTransitionMode.None,
				decoded.IncomingTransitionMode);
			ClassicAssert.AreEqual(ArchitectureClaim.LegacyClaimed,
				decoded.Cells.Find(cell => KingdomArchitectureRules.IsClaimed(cell.Claim)).Claim);
			ClassicAssert.IsFalse(KingdomArchitectureRules.IsCurrentSnapshotEncoding(a2));

			ArchitectureLayoutSnapshot transitional = Compile();
			transitional.IncomingTransitionMode = ArchitectureTransitionMode.Renovate;
			UseLegacyClaims(transitional);
			args = new object[] { transitional, 3, null, null };
			ClassicAssert.IsTrue((bool)writer.Invoke(null, args), args[3] as string);
			string a3 = (string)args[2];
			StringAssert.StartsWith("a3|", a3);
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryDecodeSnapshot(a3,
				out decoded, out failure), failure);
			ClassicAssert.AreEqual(ArchitectureTransitionMode.Renovate,
				decoded.IncomingTransitionMode);
			ClassicAssert.AreEqual(0, decoded.FootprintX);
			ClassicAssert.AreEqual(0, decoded.FootprintY);
			ClassicAssert.AreEqual(decoded.Width, decoded.FootprintWidth);
			ClassicAssert.AreEqual(decoded.Height, decoded.FootprintHeight);
			ClassicAssert.AreEqual((KingdomPlotRules.RoofState)byte.MaxValue, decoded.BaseRoof);
			ClassicAssert.IsTrue(KingdomArchitectureRules.IsManagedSnapshotEncoding(a3));
			ClassicAssert.IsFalse(KingdomArchitectureRules.IsLatestSnapshotEncoding(a3));
			ClassicAssert.IsTrue(KingdomArchitectureRules.IsLatestSnapshotEncoding(renovate));
			ClassicAssert.IsTrue(KingdomArchitectureRules.IsManagedSnapshotEncoding(renovate));
		}

		[Test]
		public void RootBehavior_IsImplicitAndNeverBecomesDisposableSceneryOrDeltaWork()
		{
			ArchitectureCompileRequest firstRequest = Request();
			ArchitectureCompileRequest secondRequest = Request();
			secondRequest.BuildingBlueprint = "Successor Dwelling";
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryCompile(firstRequest,
				out ArchitectureLayoutSnapshot first, out string failure), failure);
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryCompile(secondRequest,
				out ArchitectureLayoutSnapshot second, out failure), failure);
			ClassicAssert.IsNull(FindPlacement(first, ArchitectureLayer.Object, first.MainX, first.MainY));
			ClassicAssert.IsNull(FindPlacement(second, ArchitectureLayer.Object, second.MainX, second.MainY));
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryEncodeSnapshot(first,
				out string firstReceipt, out failure), failure);
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryEncodeSnapshot(second,
				out string secondReceipt, out failure), failure);
			ClassicAssert.AreEqual(firstReceipt, secondReceipt);
			second.IncomingTransitionMode = ArchitectureTransitionMode.Renovate;
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryBuildDelta(first, second,
				ArchitectureTransitionMode.Renovate,
				out ArchitectureLayoutDelta delta, out failure), failure);
			ClassicAssert.AreEqual(0, delta.Added.Count);
			ClassicAssert.AreEqual(0, delta.Removed.Count);
		}

		[Test]
		public void ExactDelta_ReportsStatelessAndSemanticChanges()
		{
			ArchitectureLayoutSnapshot before = Compile();
			ArchitectureLayoutSnapshot after = Clone(before);
			after.IncomingTransitionMode = ArchitectureTransitionMode.Renovate;
			after.TierKey = "housing-t1";
			after.BuildKey = "dwelling-t1";
			ArchitecturePlacement wall = FindPlacement(after, ArchitectureLayer.Structure, 0, 0);
			wall.Blueprint = "Brick Wall";
			FindCell(after, 0, 0).Cover = ArchitectureCover.Soft;
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryBuildDelta(before, after,
				out ArchitectureLayoutDelta delta, out string failure), failure);
			ClassicAssert.AreEqual(1, delta.Removed.Count);
			ClassicAssert.AreEqual("Mud Wall", delta.Removed[0].Blueprint);
			ClassicAssert.AreEqual(1, delta.Added.Count);
			ClassicAssert.AreEqual("Brick Wall", delta.Added[0].Blueprint);
			ClassicAssert.AreEqual(1, delta.Cells.Count);
			ClassicAssert.Greater(delta.Retained.Count, 0);
		}

		[Test]
		public void TransitionModes_SeparateAdditionRenovationAndReplacement()
		{
			ArchitectureLayoutSnapshot before = Compile();
			ArchitectureLayoutSnapshot changed = Clone(before);
			FindPlacement(changed, ArchitectureLayer.Structure, 0, 0).Blueprint = "Brick Wall";
			changed.IncomingTransitionMode = ArchitectureTransitionMode.Additive;
			ClassicAssert.IsFalse(KingdomArchitectureRules.TryBuildDelta(before, changed,
				out _, out string failure));
			StringAssert.Contains("additive", failure);

			changed.IncomingTransitionMode = ArchitectureTransitionMode.Renovate;
			ClassicAssert.IsFalse(KingdomArchitectureRules.TryBuildDelta(before, changed,
				ArchitectureTransitionMode.Additive, out _, out failure));
			StringAssert.Contains("frozen successor", failure);
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryBuildDelta(before, changed,
				out ArchitectureLayoutDelta renovated, out failure), failure);
			ClassicAssert.AreEqual(1, renovated.Removed.Count);
			ClassicAssert.AreEqual(1, renovated.Added.Count);

			changed.IncomingTransitionMode = ArchitectureTransitionMode.Replacement;
			ClassicAssert.IsFalse(KingdomArchitectureRules.TryBuildDelta(before, changed,
				out _, out failure));
			StringAssert.Contains("strike", failure);
			StringAssert.Contains("commission", failure);

			changed.IncomingTransitionMode = ArchitectureTransitionMode.None;
			ClassicAssert.IsFalse(KingdomArchitectureRules.TryBuildDelta(before, changed,
				out _, out failure));
			StringAssert.Contains("no authored", failure);
		}

		[Test]
		public void TransitionModes_SeparateAdditiveGrowthFromHybridGrowth()
		{
			ArchitectureLayoutSnapshot small = HeartSnapshot(1, ArchitectureLotSize.Small,
				6, 4, 2, 0);
			ArchitectureLayoutSnapshot medium = HeartSnapshot(2, ArchitectureLotSize.Medium,
				8, 6, 3, 1);

			medium.IncomingTransitionMode = ArchitectureTransitionMode.Additive;
			ClassicAssert.IsFalse(KingdomArchitectureRules.TryBuildDelta(small, medium,
				out _, out string failure));
			StringAssert.Contains("additive-expand", failure);

			medium.IncomingTransitionMode = ArchitectureTransitionMode.AdditiveExpand;
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryBuildDelta(small, medium,
				out ArchitectureLayoutDelta extension, out failure), failure);
			ClassicAssert.AreEqual(0, extension.Removed.Count);
			ClassicAssert.Greater(extension.Added.Count, 0);

			FindPlacement(medium, ArchitectureLayer.Ground, 1, 1).Blueprint = "Wood Floor";
			ClassicAssert.IsFalse(KingdomArchitectureRules.TryBuildDelta(small, medium,
				out _, out failure));
			StringAssert.Contains("additive", failure);

			medium.IncomingTransitionMode = ArchitectureTransitionMode.RenovateExpand;
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryBuildDelta(small, medium,
				out ArchitectureLayoutDelta hybrid, out failure), failure);
			ClassicAssert.Greater(hybrid.Removed.Count, 0);
		}

		[Test]
		public void InPlaceTransition_FootprintIsMainRelativeAndMonotonic()
		{
			ArchitectureLayoutSnapshot before = Compile();
			ArchitectureLayoutSnapshot expanded = Clone(before);
			for (int i = 0; i < before.Cells.Count; i++)
				if (before.Cells[i].X == 0 || before.Cells[i].X == 5)
				{
					before.Cells[i].Claim = ArchitectureClaim.Yard;
					expanded.Cells[i].Claim = ArchitectureClaim.Yard;
				}
			before.FootprintX = 1;
			before.FootprintWidth = 4;
			expanded.IncomingTransitionMode = ArchitectureTransitionMode.Additive;
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryBuildDelta(before, expanded,
				out _, out string failure), failure);

			ArchitectureLayoutSnapshot standing = Compile();
			ArchitectureLayoutSnapshot shrunk = Clone(standing);
			shrunk.IncomingTransitionMode = ArchitectureTransitionMode.Renovate;
			shrunk.FootprintX = 1;
			shrunk.FootprintWidth = 5;
			for (int i = 0; i < shrunk.Cells.Count; i++)
				if (shrunk.Cells[i].X == 0) shrunk.Cells[i].Claim = ArchitectureClaim.Yard;
			ClassicAssert.IsFalse(KingdomArchitectureRules.TryBuildDelta(standing, shrunk,
				out _, out failure));
			StringAssert.Contains("shrinks or shifts", failure);
		}

		[Test]
		public void AdditiveTransition_CannotWeakenFrozenAggregateRoof()
		{
			ArchitectureLayoutSnapshot before = Compile();
			ArchitectureLayoutSnapshot after = Clone(before);
			after.IncomingTransitionMode = ArchitectureTransitionMode.Additive;
			after.BaseRoof = KingdomPlotRules.RoofState.Open;
			ClassicAssert.IsFalse(KingdomArchitectureRules.TryBuildDelta(before, after,
				out _, out string failure));
			StringAssert.Contains("weakens", failure);

			after.IncomingTransitionMode = ArchitectureTransitionMode.Renovate;
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryBuildDelta(before, after,
				out _, out failure), failure);
		}

		[Test]
		public void AdditiveExpand_PreservesOldCellsButAllowsNewEnvelopeToStayOpen()
		{
			ArchitectureLayoutSnapshot small = HeartSnapshot(1, ArchitectureLotSize.Small,
				6, 4, 2, 0);
			ArchitectureLayoutSnapshot medium = HeartSnapshot(2, ArchitectureLotSize.Medium,
				8, 6, 3, 1);
			medium.IncomingTransitionMode = ArchitectureTransitionMode.AdditiveExpand;

			// One genuinely new target-envelope cell may remain empty, open, and unclaimed.
			ArchitectureCellState newOpen = FindCell(medium, 0, 1);
			newOpen.Claim = ArchitectureClaim.Unclaimed;
			medium.Placements.RemoveAll(p => p.X == 0 && p.Y == 1);
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryBuildDelta(small, medium,
				out _, out string failure), failure);

			// Cropping even placement-free predecessor fabric is still forbidden.
			ArchitectureLayoutSnapshot sparseSmall = HeartSnapshot(1,
				ArchitectureLotSize.Small, 6, 4, 2, 0);
			sparseSmall.Placements.RemoveAll(p => p.X == 0);
			sparseSmall.FootprintX = 1;
			sparseSmall.FootprintWidth = 5;
			for (int i = 0; i < sparseSmall.Cells.Count; i++)
				if (sparseSmall.Cells[i].X == 0)
					sparseSmall.Cells[i].Claim = ArchitectureClaim.Yard;
			ArchitectureLayoutSnapshot shiftedMedium = HeartSnapshot(2,
				ArchitectureLotSize.Medium, 8, 6, 1, 1);
			shiftedMedium.IncomingTransitionMode = ArchitectureTransitionMode.AdditiveExpand;
			ClassicAssert.IsFalse(KingdomArchitectureRules.TryBuildDelta(sparseSmall, shiftedMedium,
				out _, out failure));
			StringAssert.Contains("crops", failure);
		}

		[Test]
		public void AdditiveTransition_OnlyStrengthensCellSemanticsWithNewFabric()
		{
			ArchitectureLayoutSnapshot before = Compile();
			FindCell(before, 2, 1).Cover = ArchitectureCover.Open;
			ArchitectureLayoutSnapshot after = Clone(before);
			after.IncomingTransitionMode = ArchitectureTransitionMode.Additive;
			ArchitectureCellState cell = FindCell(after, 2, 1);
			cell.Cover = ArchitectureCover.Soft;
			ClassicAssert.IsFalse(KingdomArchitectureRules.TryBuildDelta(before, after,
				out _, out string failure));
			StringAssert.Contains("without new fabric", failure);

			after.Placements.Add(new ArchitecturePlacement
			{
				Layer = ArchitectureLayer.Structure, X = 2, Y = 1, Slot = "s:02:01",
				Blueprint = "Canvas Roof", Material = "mud", MinTech = "hands"
			});
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryBuildDelta(before, after,
				out _, out failure), failure);

			ArchitectureLayoutSnapshot strongBefore = Compile();
			ArchitectureLayoutSnapshot weakened = Clone(strongBefore);
			weakened.IncomingTransitionMode = ArchitectureTransitionMode.Additive;
			FindCell(weakened, 0, 0).Cover = ArchitectureCover.Soft;
			weakened.Placements.Add(new ArchitecturePlacement
			{
				Layer = ArchitectureLayer.Object, X = 0, Y = 0, Slot = "o:00:00",
				Blueprint = "Wall Pennant", Material = "mud", MinTech = "hands"
			});
			ClassicAssert.IsFalse(KingdomArchitectureRules.TryBuildDelta(strongBefore, weakened,
				out _, out failure));
			StringAssert.Contains("weakens", failure);
		}

		[Test]
		public void ExactDelta_RefusesMainMovementAndStatefulLossOrMutation()
		{
			ArchitectureLayoutSnapshot before = Compile();
			ArchitectureLayoutSnapshot movedMain = Clone(before);
			movedMain.IncomingTransitionMode = ArchitectureTransitionMode.Renovate;
			movedMain.MainX = 3;
			movedMain.MainY = 1;
			ArchitectureAnchor main = FindAnchor(movedMain, "main", 0);
			main.X = 3;
			main.Y = 1;
			ClassicAssert.IsFalse(KingdomArchitectureRules.TryBuildDelta(before, movedMain,
				out _, out string failure));
			StringAssert.Contains("main", failure);

			ArchitectureLayoutSnapshot removed = Clone(before);
			removed.IncomingTransitionMode = ArchitectureTransitionMode.Renovate;
			removed.Placements.Remove(FindPlacement(removed, ArchitectureLayer.Object, 1, 2));
			ClassicAssert.IsFalse(KingdomArchitectureRules.TryBuildDelta(before, removed,
				out _, out failure));
			StringAssert.Contains("registered handover", failure);

			ArchitectureLayoutSnapshot changed = Clone(before);
			changed.IncomingTransitionMode = ArchitectureTransitionMode.Renovate;
			FindPlacement(changed, ArchitectureLayer.Object, 1, 2).Blueprint = "Metal Chest";
			ClassicAssert.IsFalse(KingdomArchitectureRules.TryBuildDelta(before, changed,
				out _, out failure));
			StringAssert.Contains("registered handover", failure);
		}

		[Test]
		public void ExactDelta_AllowsNewStatefulFixtureButRejectsAnotherTypedSet()
		{
			ArchitectureLayoutSnapshot before = Compile();
			ArchitectureLayoutSnapshot after = Clone(before);
			after.IncomingTransitionMode = ArchitectureTransitionMode.Renovate;
			after.Anchors.Add(new ArchitectureAnchor
				{ Key = "fixture:table@2,2", X = 2, Y = 2, Access = ArchitectureAnchorAccess.OnCell });
			after.Placements.Add(new ArchitecturePlacement
			{
				Layer = ArchitectureLayer.Object, X = 2, Y = 2, Blueprint = "Low Table",
				Slot = "o:02:02", Material = "mud", MinTech = "hands",
				StatefulAnchor = "fixture:table@2,2"
			});
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryBuildDelta(before, after,
				out ArchitectureLayoutDelta delta, out string failure), failure);
			ClassicAssert.AreEqual(1, delta.Added.Count);

			after = Clone(before);
			after.IncomingTransitionMode = ArchitectureTransitionMode.Renovate;
			after.LotType = "water";
			ClassicAssert.IsFalse(KingdomArchitectureRules.TryBuildDelta(before, after,
				out _, out failure));
			StringAssert.Contains("typed lot", failure);
		}

		[Test]
		public void HeartDelta_AllowsAdjacentRenovationWhileRetainingRiteCustody()
		{
			ArchitectureLayoutSnapshot small = HeartSnapshot(1, ArchitectureLotSize.Small,
				6, 4, 2, 0);
			ArchitectureLayoutSnapshot medium = HeartSnapshot(2, ArchitectureLotSize.Medium,
				8, 6, 3, 1);
			FindPlacement(medium, ArchitectureLayer.Ground, 1, 1).Blueprint = "Wood Floor";
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryBuildDelta(small, medium,
				out ArchitectureLayoutDelta delta, out string failure), failure);
			ClassicAssert.Greater(delta.Removed.Count, 0,
				"a larger heart may lawfully rebuild prior stateless fabric");
			ClassicAssert.AreEqual(delta.Retained.Count, delta.RetainedAfter.Count);
			ClassicAssert.Greater(delta.Retained.Count, 0);
			ClassicAssert.Greater(delta.Added.Count, 0);
			ArchitecturePlacement oldBasin = small.Placements.Find(p => p.ExistingAuthority);
			int basinIndex = delta.Retained.IndexOf(oldBasin);
			ClassicAssert.GreaterOrEqual(basinIndex, 0);
			ClassicAssert.IsTrue(delta.RetainedAfter[basinIndex].ExistingAuthority);
			ClassicAssert.AreEqual("fixture:first-basin@2,1", oldBasin.StatefulAnchor);
			ClassicAssert.AreEqual("fixture:first-basin@3,2",
				delta.RetainedAfter[basinIndex].StatefulAnchor);

			ArchitectureLayoutSnapshot skipped = HeartSnapshot(3, ArchitectureLotSize.Large,
				12, 10, 5, 3);
			skipped.IncomingTransitionMode = ArchitectureTransitionMode.Renovate;
			ClassicAssert.IsFalse(KingdomArchitectureRules.TryBuildDelta(small, skipped,
				out _, out failure));
			StringAssert.Contains("renovate-expand", failure);

			ArchitectureLayoutSnapshot moved = Clone(medium);
			moved.MainX = 4;
			ArchitectureAnchor main = FindAnchor(moved, "main", 0);
			main.X = 4;
			ClassicAssert.IsFalse(KingdomArchitectureRules.TryBuildDelta(small, moved,
				out _, out failure));
			StringAssert.Contains("protected anchor", failure);

			ArchitectureLayoutSnapshot ordinary = Clone(medium);
			ordinary.LotType = "housing";
			ClassicAssert.IsFalse(KingdomArchitectureRules.TryBuildDelta(small, ordinary,
				out _, out failure));
			StringAssert.Contains("typed lot", failure);
		}

		[Test]
		public void HeartDelta_AllowsCourtToArcologyOnTheSameHugeLot()
		{
			ArchitectureLayoutSnapshot court = HeartSnapshot(4,
				ArchitectureLotSize.Huge, 20, 18, 9, 7);
			ArchitectureLayoutSnapshot arcology = HeartSnapshot(5,
				ArchitectureLotSize.Huge, 20, 18, 9, 7);
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryBuildDelta(court, arcology,
				out ArchitectureLayoutDelta delta, out string failure), failure);
			ClassicAssert.AreEqual(0, delta.Removed.Count);
			ClassicAssert.AreEqual(delta.Retained.Count, delta.RetainedAfter.Count);
		}

		[Test]
		public void HeartDelta_AllowsAuthoredCellRenovation()
		{
			ArchitectureLayoutSnapshot small = HeartSnapshot(1, ArchitectureLotSize.Small,
				6, 4, 2, 0);
			ArchitectureLayoutSnapshot medium = HeartSnapshot(2, ArchitectureLotSize.Medium,
				8, 6, 3, 1);
			ArchitectureCellState oldCell = FindCell(small, 0, 0);
			ArchitectureCellState retainedCell = FindCell(medium, 1, 1);

			retainedCell.Cover = ArchitectureCover.Soft;
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryBuildDelta(small, medium,
				out ArchitectureLayoutDelta enclosed, out string failure), failure);
			ClassicAssert.IsTrue(enclosed.Cells.Exists(delegate(ArchitectureCellDelta change)
			{
				return change.Before == oldCell && change.After == retainedCell
					&& change.Before.Cover == ArchitectureCover.Open
					&& change.After.Cover == ArchitectureCover.Soft;
			}));

			medium = HeartSnapshot(2, ArchitectureLotSize.Medium, 8, 6, 3, 1);
			FindCell(medium, 1, 1).Cover = ArchitectureCover.Walled;
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryBuildDelta(small, medium,
				out _, out failure), failure);

			small = HeartSnapshot(1, ArchitectureLotSize.Small, 6, 4, 2, 0);
			medium = HeartSnapshot(2, ArchitectureLotSize.Medium, 8, 6, 3, 1);
			FindCell(small, 0, 0).Cover = ArchitectureCover.Soft;
			FindCell(medium, 1, 1).Cover = ArchitectureCover.Walled;
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryBuildDelta(small, medium,
				out _, out failure), failure);
		}

		[Test]
		public void HeartDelta_AllowsReopeningAndNaturalReworkingWhenTopologyRemainsValid()
		{
			ArchitectureLayoutSnapshot small = HeartSnapshot(1, ArchitectureLotSize.Small,
				6, 4, 2, 0);
			ArchitectureLayoutSnapshot medium = HeartSnapshot(2, ArchitectureLotSize.Medium,
				8, 6, 3, 1);
			ArchitectureCellState before = FindCell(small, 0, 0);
			ArchitectureCellState after = FindCell(medium, 1, 1);
			before.Cover = ArchitectureCover.Walled;
			after.Cover = ArchitectureCover.Natural;
			after.Passability = ArchitecturePassability.Adjacent;
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryBuildDelta(small, medium,
				out ArchitectureLayoutDelta renovated, out string failure), failure);
			ClassicAssert.IsTrue(renovated.Cells.Exists(delegate(ArchitectureCellDelta change)
			{
				return change.Before == before && change.After == after;
			}));

			medium = HeartSnapshot(2, ArchitectureLotSize.Medium, 8, 6, 3, 1);
			after = FindCell(medium, 1, 1);
			after.Claim = ArchitectureClaim.Unclaimed;
			medium.Placements.RemoveAll(delegate(ArchitecturePlacement placement)
				{ return placement.X == 1 && placement.Y == 1; });
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryBuildDelta(
				HeartSnapshot(1, ArchitectureLotSize.Small, 6, 4, 2, 0), medium,
				out _, out failure), failure);
		}

		[Test]
		public void LabourProgress_MultipliesTimeLabourAndInfrastructureAndSpendsIdleTime()
		{
			ArchitectureLabourProgress partial = KingdomArchitectureRules.AdvanceLabour(
				100, 200, 100, 50, 50);
			ClassicAssert.AreEqual(25, partial.WorkedTicks);
			ClassicAssert.AreEqual(75, partial.RemainingTicks);
			ClassicAssert.AreEqual(200, partial.NextTick);
			ClassicAssert.IsFalse(partial.Complete);

			ArchitectureLabourProgress idle = KingdomArchitectureRules.AdvanceLabour(
				100, 200, 10, 0, 100);
			ClassicAssert.AreEqual(0, idle.WorkedTicks);
			ClassicAssert.AreEqual(200, idle.NextTick);
			ArchitectureLabourProgress resumed = KingdomArchitectureRules.AdvanceLabour(
				idle.NextTick, 210, idle.RemainingTicks, 100, 100);
			ClassicAssert.AreEqual(10, resumed.WorkedTicks);
			ClassicAssert.AreEqual(210, resumed.CompletionTick);
		}

		[Test]
		public void LabourProgress_ClampsInputsAndFindsExactCompletionTickWithoutOverflow()
		{
			ArchitectureLabourProgress quantised = KingdomArchitectureRules.AdvanceLabour(
				100, 102, 1, 50, 100);
			ClassicAssert.IsTrue(quantised.Complete);
			ClassicAssert.AreEqual(102, quantised.CompletionTick);
			ArchitectureLabourProgress clamped = KingdomArchitectureRules.AdvanceLabour(
				0, 100, 100, 900, 900);
			ClassicAssert.IsTrue(clamped.Complete);
			ClassicAssert.AreEqual(100, clamped.CompletionTick);
			ArchitectureLabourProgress huge = KingdomArchitectureRules.AdvanceLabour(
				0, long.MaxValue, long.MaxValue, 100, 100);
			ClassicAssert.IsTrue(huge.Complete);
			ClassicAssert.AreEqual(long.MaxValue, huge.WorkedTicks);
			ClassicAssert.AreEqual(long.MaxValue, huge.CompletionTick);
		}

		private static ArchitectureVariantDraft Variant(string key, int priority,
			ArchitectureSelector selector)
		{
			return new ArchitectureVariantDraft
				{ Key = key, Priority = priority, Selector = selector };
		}

		private static ArchitectureBindingDraft Binding(string key, string type,
			ArchitectureLotSize size)
		{
			return new ArchitectureBindingDraft
				{ Key = key, TypeKey = type, Size = size, Frontage = ArchitectureFrontage.Heart };
		}

		private static ArchitectureTierDraft Tier(string key, int level)
		{
			ArchitectureTierDraft tier = new ArchitectureTierDraft
			{
				Key = key, BuildKey = "build-" + key, Level = level,
				IncomingTransitionMode = level == 0 ? ArchitectureTransitionMode.None
					: ArchitectureTransitionMode.Renovate,
				MapKey = "house-map", PaletteKey = "house-palette"
			};
			tier.Variants.Add(Variant("fallback", 0, null));
			return tier;
		}

		private static ArchitectureCompileRequest Request()
		{
			ArchitecturePaletteDraft palette = new ArchitecturePaletteDraft
				{ Key = "house-palette" };
			palette.Slots.Add(Slot("floor", "Dirt Floor", "floor"));
			palette.Slots.Add(Slot("wall", "Mud Wall", "wall"));
			palette.Slots.Add(Slot("door", "Door", "door"));
			palette.Slots.Add(Slot("storage", "Woven Basket", "storage"));
			palette.Slots.Add(Slot("bed", "Bed", "sleep"));

			ArchitectureMapDraft map = new ArchitectureMapDraft
			{
				Key = "house-map", Width = 6, Height = 4,
				DefaultCover = ArchitectureCover.Walled
			};
			map.Glyphs.Add(Glyph('#', "$floor", "$wall", null, true,
				ArchitecturePassability.Blocked, false));
			map.Glyphs.Add(Glyph('+', "$floor", "$door", null, true,
				ArchitecturePassability.Walkable, false, "entrance:public"));
			map.Glyphs.Add(Glyph('_', "$floor", null, null, true,
				ArchitecturePassability.Walkable, false));
			map.Glyphs.Add(Glyph('s', "$floor", null, "$storage", true,
				ArchitecturePassability.Adjacent, true, "fixture:storage"));
			map.Glyphs.Add(Glyph('b', "$floor", null, "$bed", true,
				ArchitecturePassability.Adjacent, true, "sleep:bed"));
			map.Glyphs.Add(Glyph('@', "$floor", null, "$building", true,
				ArchitecturePassability.Walkable, true, "main", "function:dwelling"));
			map.Rows.Add("##+###");
			map.Rows.Add("#_@__#");
			map.Rows.Add("#s_b_#");
			map.Rows.Add("######");

			ArchitectureBindingDraft binding = Binding("housing-small", "housing",
				ArchitectureLotSize.Small);
			ArchitectureTierDraft tier = Tier("housing-t0", 0);
			tier.Requirements.Add(new ArchitectureAnchorRequirement
				{ Role = "function:dwelling", Minimum = 1, Maximum = 1 });
			tier.Requirements.Add(new ArchitectureAnchorRequirement
				{ Role = "fixture:storage", Minimum = 1 });
			tier.Requirements.Add(new ArchitectureAnchorRequirement
				{ Role = "sleep", Minimum = 1 });
			binding.Tiers.Add(tier);
			return new ArchitectureCompileRequest
			{
				PlanKey = "housing-plan", Binding = binding, Tier = tier,
				Variant = tier.Variants[0], Map = map, Palette = palette,
				BuildingBlueprint = "Dwelling",
				CatalogueRoof = KingdomPlotRules.RoofState.Walled,
				Facing = ArchitectureFacing.North
			};
		}

		private static ArchitecturePoseRegistry Registry(
			params ArchitecturePoseDraft[] Poses)
		{
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryCreatePoseRegistry(Poses, null,
				out ArchitecturePoseRegistry result, out string failure), failure);
			return result;
		}

		private static ArchitectureGlyphDraft Glyph(char character, string ground,
			string structure, string item, bool claim, ArchitecturePassability passability,
			bool stateful, params string[] anchors)
		{
			ArchitectureGlyphDraft glyph = new ArchitectureGlyphDraft
			{
				Character = character, Ground = ground, Structure = structure, Object = item,
				Claim = claim ? ArchitectureClaim.Building : ArchitectureClaim.Unclaimed,
				Passability = passability, HasCover = false,
				StatefulObject = stateful
			};
			glyph.Anchors.AddRange(anchors);
			return glyph;
		}

		private static ArchitecturePaletteSlot Slot(string key, string blueprint, string role)
		{
			return new ArchitecturePaletteSlot
				{ Key = key, Blueprint = blueprint, Role = role, Material = "mud", MinTech = "hands" };
		}

		private static ArchitectureLayoutSnapshot Compile()
		{
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryCompile(Request(),
				out ArchitectureLayoutSnapshot snapshot, out string failure), failure);
			return snapshot;
		}

		private static ArchitectureLayoutSnapshot HeartSnapshot(int rung,
			ArchitectureLotSize size, int width, int height, int mainX, int mainY)
		{
			ArchitecturePaletteDraft palette = new ArchitecturePaletteDraft
				{ Key = "heart-palette-" + rung };
			palette.Slots.Add(Slot("floor", "Dirt Floor", "floor"));
			palette.Slots.Add(Slot("basin", "r_KingdomFirstBasin", "first-basin"));
			ArchitectureMapDraft map = new ArchitectureMapDraft
			{
				Key = "heart-map-" + rung, Width = width, Height = height,
				DefaultCover = ArchitectureCover.Open
			};
			map.Glyphs.Add(Glyph('_', "$floor", null, null, true,
				ArchitecturePassability.Walkable, false));
			map.Glyphs.Add(Glyph('+', "$floor", null, null, true,
				ArchitecturePassability.Walkable, false, "entrance:public"));
			map.Glyphs.Add(Glyph('@', "$floor", null, "$building", true,
				ArchitecturePassability.Walkable, true, "main",
				"function:settlement-heart"));
			map.Glyphs.Add(Glyph('B', "$floor", null, "$basin", true,
				ArchitecturePassability.Walkable, true, "fixture:first-basin"));
			for (int y = 0; y < height; y++)
			{
				char[] row = new string('_', width).ToCharArray();
				if (y == 0) row[0] = '+';
				if (y == mainY) row[mainX] = '@';
				if (y == mainY + 1) row[mainX] = 'B';
				map.Rows.Add(new string(row));
			}
			ArchitectureBindingDraft binding = Binding("heart-binding-" + rung, "civic", size);
			ArchitectureTierDraft tier = new ArchitectureTierDraft
			{
				Key = "heart-tier-" + rung,
				BuildKey = KingdomPlotRules.HeartRungKeys[rung - 1], Level = rung - 1,
				IncomingTransitionMode = rung == 1 ? ArchitectureTransitionMode.None
					: (rung == 5 ? ArchitectureTransitionMode.Renovate
						: ArchitectureTransitionMode.RenovateExpand),
				MapKey = map.Key, PaletteKey = palette.Key
			};
			tier.Requirements.Add(new ArchitectureAnchorRequirement
				{ Role = "function:settlement-heart", Minimum = 1, Maximum = 1 });
			tier.Requirements.Add(new ArchitectureAnchorRequirement
				{ Role = "fixture:first-basin", Minimum = 1, Maximum = 1 });
			tier.Variants.Add(Variant("fallback", 0, null));
			binding.Tiers.Add(tier);
			ArchitectureCompileRequest request = new ArchitectureCompileRequest
			{
				PlanKey = "civic-heart", Binding = binding, Tier = tier,
				Variant = tier.Variants[0], Map = map, Palette = palette,
				BuildingBlueprint = "Heart Root " + rung,
				CatalogueRoof = KingdomPlotRules.RoofState.Open,
				Facing = ArchitectureFacing.North
			};
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryCompile(request,
				out ArchitectureLayoutSnapshot snapshot, out string failure), failure);
			return snapshot;
		}

		private static ArchitectureLayoutSnapshot Clone(ArchitectureLayoutSnapshot source)
		{
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryEncodeSnapshot(source,
				out string encoded, out string failure), failure);
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryDecodeSnapshot(encoded,
				out ArchitectureLayoutSnapshot clone, out failure), failure);
			return clone;
		}

		private static void UseLegacyClaims(ArchitectureLayoutSnapshot snapshot)
		{
			for (int i = 0; i < snapshot.Cells.Count; i++)
				if (KingdomArchitectureRules.IsClaimed(snapshot.Cells[i].Claim))
					snapshot.Cells[i].Claim = ArchitectureClaim.LegacyClaimed;
		}

		private static void AssertCompileFails(ArchitectureCompileRequest request, string fragment)
		{
			ClassicAssert.IsFalse(KingdomArchitectureRules.TryCompile(request, out _, out string failure));
			StringAssert.Contains(fragment, failure);
		}

		private static ArchitecturePlacement FindPlacement(ArchitectureLayoutSnapshot snapshot,
			ArchitectureLayer layer, int x, int y)
		{
			return snapshot.Placements.Find(delegate(ArchitecturePlacement placement)
			{
				return placement.Layer == layer && placement.X == x && placement.Y == y;
			});
		}

		private static ArchitectureCellState FindCell(ArchitectureLayoutSnapshot snapshot, int x, int y)
		{
			return snapshot.Cells.Find(delegate(ArchitectureCellState cell)
				{ return cell.X == x && cell.Y == y; });
		}

		private static ArchitectureAnchor FindAnchor(ArchitectureLayoutSnapshot snapshot,
			string role, int occurrence)
		{
			for (int i = 0; i < snapshot.Anchors.Count; i++)
			{
				string key = snapshot.Anchors[i].Key;
				if (key == role || key.StartsWith(role + "@", StringComparison.Ordinal))
				{
					if (occurrence-- == 0) return snapshot.Anchors[i];
				}
			}
			return null;
		}

		private static int CountAnchorRole(ArchitectureLayoutSnapshot snapshot, string role)
		{
			int count = 0;
			for (int i = 0; i < snapshot.Anchors.Count; i++)
			{
				string key = snapshot.Anchors[i].Key;
				if (key == role || key.StartsWith(role + "@", StringComparison.Ordinal)) count++;
			}
			return count;
		}
	}
}
#endif
