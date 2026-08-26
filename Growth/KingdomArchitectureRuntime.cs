using System;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>
	/// Fully prepared, mutation-free authored-building intent. Only the canonical encoded snapshot
	/// is durable authority; scalar fields make inspection cheap and are checked against it on every
	/// read. The exact rect is included so the world main cell can also be proven after reload.
	/// </summary>
	public sealed class KingdomArchitectureIntent
	{
		public int SchemaVersion { get; private set; }
		public string BuildKey { get; private set; }
		public string PlanKey { get; private set; }
		public string BindingKey { get; private set; }
		public string TierKey { get; private set; }
		public string VariantKey { get; private set; }
		public string PaletteKey { get; private set; }
		public string LotType { get; private set; }
		public ArchitectureLotSize LotSize { get; private set; }
		public ArchitectureFacing Facing { get; private set; }
		public string EncodedSnapshot { get; private set; }
		public string SnapshotHash { get; private set; }
		public KingdomPlotRules.PlotRect Rect { get; private set; }
		public int MainWorldX { get; private set; }
		public int MainWorldY { get; private set; }

		internal KingdomArchitectureIntent() { }

		internal static KingdomArchitectureIntent Create(ArchitectureLayoutSnapshot Snapshot,
			string Encoded, string Hash, KingdomPlotRules.PlotRect Rect, int MainX, int MainY)
		{
			return new KingdomArchitectureIntent
			{
				SchemaVersion = KingdomArchitectureRuntime.ReceiptSchema,
				BuildKey = Snapshot.BuildKey,
				PlanKey = Snapshot.PlanKey,
				BindingKey = Snapshot.BindingKey,
				TierKey = Snapshot.TierKey,
				VariantKey = Snapshot.VariantKey,
				PaletteKey = Snapshot.PaletteKey,
				LotType = Snapshot.LotType,
				LotSize = Snapshot.LotSize,
				Facing = Snapshot.Facing,
				EncodedSnapshot = Encoded,
				SnapshotHash = Hash,
				Rect = Rect,
				MainWorldX = MainX,
				MainWorldY = MainY
			};
		}

		internal static KingdomArchitectureIntent CreateRaw(int SchemaVersion, string BuildKey,
			string PlanKey, string BindingKey, string TierKey, string VariantKey,
			string PaletteKey, string LotType, ArchitectureLotSize LotSize,
			ArchitectureFacing Facing, string EncodedSnapshot, string SnapshotHash,
			KingdomPlotRules.PlotRect Rect, int MainWorldX, int MainWorldY)
		{
			return new KingdomArchitectureIntent
			{
				SchemaVersion = SchemaVersion, BuildKey = BuildKey, PlanKey = PlanKey,
				BindingKey = BindingKey, TierKey = TierKey, VariantKey = VariantKey,
				PaletteKey = PaletteKey, LotType = LotType, LotSize = LotSize, Facing = Facing,
				EncodedSnapshot = EncodedSnapshot, SnapshotHash = SnapshotHash, Rect = Rect,
				MainWorldX = MainWorldX, MainWorldY = MainWorldY
			};
		}
	}

	/// <summary>
	/// Engine-facing preparation and durable-receipt boundary for authored plot layouts. No method
	/// mutates the world during preparation. Once prepared, freeze/read/copy and coordinate helpers
	/// depend only on the canonical receipt and pure architecture rules, never on a current catalogue.
	/// </summary>
	public static class KingdomArchitectureRuntime
	{
		public const int ReceiptSchema = 1;
		private const int MaxFailureChars = 512;

		public const string SchemaProperty = "r_TAF_ArchitectureSchema";
		public const string BuildKeyProperty = "r_TAF_ArchitectureBuildKey";
		public const string PlanKeyProperty = "r_TAF_ArchitecturePlanKey";
		public const string BindingKeyProperty = "r_TAF_ArchitectureBindingKey";
		public const string TierKeyProperty = "r_TAF_ArchitectureTierKey";
		public const string VariantKeyProperty = "r_TAF_ArchitectureVariantKey";
		public const string PaletteKeyProperty = "r_TAF_ArchitecturePaletteKey";
		public const string LotTypeProperty = "r_TAF_ArchitectureLotType";
		public const string LotSizeProperty = "r_TAF_ArchitectureLotSize";
		public const string FacingProperty = "r_TAF_ArchitectureFacing";
		public const string SnapshotProperty = "r_TAF_ArchitectureSnapshot";
		public const string HashProperty = "r_TAF_ArchitectureHash";
		public const string RectX1Property = "r_TAF_ArchitectureRectX1";
		public const string RectY1Property = "r_TAF_ArchitectureRectY1";
		public const string RectX2Property = "r_TAF_ArchitectureRectX2";
		public const string RectY2Property = "r_TAF_ArchitectureRectY2";
		public const string MainXProperty = "r_TAF_ArchitectureMainX";
		public const string MainYProperty = "r_TAF_ArchitectureMainY";

		/// <summary>
		/// Resolves one future building without changing the zone or any object. The mapping is read
		/// first. Its exact typed envelope then constrains the possible poses. Heart frontage chooses
		/// one deterministically; Road frontage resolves each fitting pose and accepts only a
		/// transformed authored public entrance directly connected to durable road evidence.
		/// </summary>
		public static bool TryPrepare(KingdomSystem System, Zone Z,
			KingdomPlotRules.PlotRect Rect, string BuildKey,
			out KingdomArchitectureIntent Intent, out string Failure)
		{
			Intent = null;
			Failure = null;
			KingdomArchitectureMapping mapping;
			if (!KingdomArchitecture.TryGetMapping(BuildKey, out mapping))
				return Fail("no valid frozen architecture maps building "
					+ (BuildKey ?? "<null>"), out Failure);
			return TryPrepareMapped(System, Z, Rect, BuildKey, mapping, out Intent, out Failure);
		}

		/// <summary>Exact typed-lot preparation. Missing larger authored bindings never fall back.</summary>
		public static bool TryPrepare(KingdomSystem System, Zone Z,
			KingdomPlotRules.PlotRect Rect, string BuildKey, string LotType,
			out KingdomArchitectureIntent Intent, out string Failure)
		{
			Intent = null;
			Failure = null;
			ArchitectureLotSize actualSize;
			if (!TryRectLotSize(Rect, out actualSize))
				return Fail("the staked rectangle is not an exact authored lot size in any pose",
					out Failure);
			KingdomArchitectureMapping mapping;
			if (!KingdomArchitecture.TryGetMapping(BuildKey, LotType, actualSize, out mapping))
				return Fail("no exact frozen architecture maps building " + (BuildKey ?? "<null>")
					+ " to typed lot " + (LotType ?? "<null>") + " " + actualSize, out Failure);
			return TryPrepareMapped(System, Z, Rect, BuildKey, mapping, out Intent, out Failure);
		}

		private static bool TryPrepareMapped(KingdomSystem System, Zone Z,
			KingdomPlotRules.PlotRect Rect, string BuildKey, KingdomArchitectureMapping mapping,
			out KingdomArchitectureIntent Intent, out string Failure)
		{
			Intent = null;
			Failure = null;
			if (System == null || !System.Founded)
				return Fail("authored architecture needs a founded settlement", out Failure);
			if (Z == null)
				return Fail("authored architecture needs an exact zone", out Failure);
			if (!ValidRectInZone(Rect, Z))
				return Fail("the authored lot rectangle is malformed or outside the zone", out Failure);

			ArchitectureSelectionContext context;
			if (!TrySelectionContext(System, Z, out context, out Failure)) return false;
			ArchitectureFacing facing;
			ArchitectureLayoutSnapshot snapshot;
			if (mapping.Frontage == ArchitectureFrontage.Road)
			{
				if (!TryRoadFacing(BuildKey, mapping, Z, Rect, context,
					out facing, out snapshot, out Failure)) return false;
			}
			else
			{
				if (!TryHeartFacing(mapping, Z, Rect, out facing, out Failure)
					|| !KingdomArchitecture.TryResolve(BuildKey, mapping.TypeKey,
						mapping.LotSize, context, facing,
						out snapshot, out Failure)) return false;
			}
			if (!MatchesMapping(snapshot, mapping))
				return Fail("resolved architecture disagrees with its frozen building mapping", out Failure);

			string encoded;
			string hash;
			if (!KingdomArchitectureRules.TryEncodeSnapshot(snapshot, out encoded, out Failure)
				|| !KingdomArchitectureRules.TrySnapshotHash(snapshot, out hash, out Failure)) return false;
			int mainX;
			int mainY;
			if (!TryWorldCoordinate(snapshot, Rect, snapshot.MainX, snapshot.MainY,
				out mainX, out mainY, out Failure)) return false;
			KingdomArchitectureIntent prepared = KingdomArchitectureIntent.Create(
				snapshot, encoded, hash, Rect, mainX, mainY);
			ArchitectureLayoutSnapshot checkedSnapshot;
			if (!TryValidateIntent(prepared, out checkedSnapshot, out Failure)) return false;
			Intent = prepared;
			return true;
		}

		/// <summary>
		/// Resolves an improvement only inside the predecessor's frozen plan, binding, exact typed
		/// lot, and cardinal pose. The sole exception is one adjacent founding-heart rung, whose
		/// authored rect accretes around the immutable rite basin. This is a pre-debit catalogue read;
		/// the returned canonical intent becomes the sole authority after funding.
		/// </summary>
		public static bool TryPrepareSuccessor(KingdomSystem System, Zone Z,
			KingdomArchitectureIntent Before, string SuccessorBuildKey,
			out KingdomArchitectureIntent Intent, out string Failure)
		{
			Intent = null;
			Failure = null;
			ArchitectureLayoutSnapshot before;
			if (!TryValidateIntent(Before, out before, out Failure)) return false;
			if (!KingdomArchitectureRules.IsCurrentSnapshotEncoding(Before.EncodedSnapshot))
				return Fail("legacy architecture has no authored in-place tier transition", out Failure);
			if (System == null || !System.Founded || Z == null || !ValidRectInZone(Before.Rect, Z))
				return Fail("authored successor needs its founded settlement and exact loaded lot",
					out Failure);
			ArchitectureSelectionContext context;
			if (!TrySelectionContext(System, Z, out context, out Failure)) return false;
			ArchitectureLayoutSnapshot after;
			if (!KingdomArchitecture.TryResolveSuccessor(before.BuildKey, SuccessorBuildKey,
				before.PlanKey,
				before.BindingKey, before.LotType, before.LotSize, context, before.Facing,
				out after, out Failure)) return false;
			ArchitectureLayoutDelta delta;
			if (!KingdomArchitectureRules.TryBuildDelta(before, after, out delta, out Failure))
				return false;
			KingdomPlotRules.PlotRect successorRect = Before.Rect;
			int beforeRung = KingdomPlotRules.HeartRungOf(before.BuildKey);
			int afterRung = KingdomPlotRules.HeartRungOf(after.BuildKey);
			bool heartAccretion = beforeRung > 0 && afterRung == beforeRung + 1
				&& before.PlanKey == "civic-heart" && after.PlanKey == "civic-heart";
			if (heartAccretion)
			{
				KingdomPlotRules.PlotRect standingRect;
				if (KingdomPlots.HeartRung(Z) != beforeRung
					|| !KingdomPlots.TryHeartRectFor(Z, beforeRung, out standingRect)
					|| !SameRect(Before.Rect, standingRect)
					|| !KingdomPlots.TryHeartRectFor(Z, afterRung, out successorRect)
					|| !ValidRectInZone(successorRect, Z))
					return Fail("founding-heart successor does not accrete from its exact standing rung",
						out Failure);
				if (!TryHeartBasinInvariant(before, Before.Rect, Z, out Failure)
					|| !TryHeartBasinInvariant(after, successorRect, Z, out Failure)) return false;
			}
			string encoded;
			string hash;
			if (!KingdomArchitectureRules.TryEncodeSnapshot(after, out encoded, out Failure)
				|| !KingdomArchitectureRules.TrySnapshotHash(after, out hash, out Failure)) return false;
			int mainX;
			int mainY;
			if (!TryWorldCoordinate(after, successorRect, after.MainX, after.MainY,
				out mainX, out mainY, out Failure)) return false;
			if (mainX != Before.MainWorldX || mainY != Before.MainWorldY)
				return Fail("authored successor moves the frozen main behavior root", out Failure);
			KingdomArchitectureIntent prepared = KingdomArchitectureIntent.Create(after, encoded,
				hash, successorRect, mainX, mainY);
			ArchitectureLayoutSnapshot checkedSnapshot;
			if (!TryValidateIntent(prepared, out checkedSnapshot, out Failure)) return false;
			Intent = prepared;
			return true;
		}

		/// <summary>
		/// Resolves one explicitly declared same-set plan change. Unlike a tier successor this may
		/// cross plan and binding keys, but it may not change typed lot, rectangle, pose, or main
		/// behavior-root cell. Declaration is checked before debit; its endpoint hashes are then
		/// frozen on the predecessor so retries never consult a mutable catalogue.
		/// </summary>
		public static bool TryPreparePlanTransition(KingdomSystem System, Zone Z,
			KingdomArchitectureIntent Before, string SuccessorBuildKey,
			KingdomSocketTransition Transition, out KingdomArchitectureIntent Intent,
			out string Failure)
		{
			Intent = null;
			Failure = null;
			ArchitectureLayoutSnapshot before;
			if (!TryValidateIntent(Before, out before, out Failure)
				|| !KingdomArchitectureRules.IsCurrentSnapshotEncoding(Before.EncodedSnapshot))
				return Failure != null ? false : Fail(
					"legacy architecture has no authored same-set transition", out Failure);
			if (System == null || !System.Founded || Z == null || Transition == null
				|| Transition.FromBuildKey != before.BuildKey
				|| Transition.ToBuildKey != SuccessorBuildKey
				|| Transition.LotType != before.LotType
				|| Transition.LotSize != before.LotSize || !ValidRectInZone(Before.Rect, Z))
				return Fail("same-set transition declaration does not match the standing typed lot",
					out Failure);
			ArchitectureSelectionContext context;
			ArchitectureLayoutSnapshot after;
			if (!TrySelectionContext(System, Z, out context, out Failure)
				|| !KingdomArchitecture.TryResolve(SuccessorBuildKey, before.LotType,
					before.LotSize, context, before.Facing, out after, out Failure)) return false;
			if (after.LotType != before.LotType || after.LotSize != before.LotSize
				|| after.Facing != before.Facing)
				return Fail("same-set transition changes the frozen lot binding or pose", out Failure);
			ArchitectureLayoutDelta delta;
			if (!KingdomArchitectureRules.TryBuildDelta(before, after, out delta, out Failure))
				return false;
			string encoded;
			string hash;
			int mainX;
			int mainY;
			if (!KingdomArchitectureRules.TryEncodeSnapshot(after, out encoded, out Failure)
				|| !KingdomArchitectureRules.TrySnapshotHash(after, out hash, out Failure)
				|| !TryWorldCoordinate(after, Before.Rect, after.MainX, after.MainY,
					out mainX, out mainY, out Failure)) return false;
			if (mainX != Before.MainWorldX || mainY != Before.MainWorldY)
				return Fail("same-set transition moves the frozen main behavior root", out Failure);
			KingdomArchitectureIntent prepared = KingdomArchitectureIntent.Create(after, encoded,
				hash, Before.Rect, mainX, mainY);
			ArchitectureLayoutSnapshot checkedSnapshot;
			if (!TryValidateIntent(prepared, out checkedSnapshot, out Failure)) return false;
			Intent = prepared;
			return true;
		}

		private static bool TryHeartBasinInvariant(ArchitectureLayoutSnapshot Snapshot,
			KingdomPlotRules.PlotRect Rect, Zone Z, out string Failure)
		{
			Failure = null;
			int riteX;
			int riteY;
			if (Snapshot == null || Z == null || !KingdomPlots.TryRiteGround(Z, out riteX, out riteY))
				return Fail("founding-heart architecture has no recorded rite ground", out Failure);
			ArchitecturePlacement basin = null;
			for (int i = 0; i < Snapshot.Placements.Count; i++)
			{
				ArchitecturePlacement placement = Snapshot.Placements[i];
				if (!placement.ExistingAuthority) continue;
				if (basin != null || placement.Blueprint != "r_KingdomFirstBasin"
					|| placement.StatefulAnchor != "fixture:first-basin")
					return Fail("founding-heart architecture must bind exactly one immutable first basin",
						out Failure);
				basin = placement;
			}
			int basinX;
			int basinY;
			if (basin == null || !TryWorldPlacement(Snapshot, Rect, basin,
				out basinX, out basinY, out Failure))
				return Failure != null ? false : Fail(
					"founding-heart architecture has no immutable first basin", out Failure);
			if (basinX != riteX || basinY != riteY)
				return Fail("founding-heart immutable basin moves away from the recorded rite",
					out Failure);
			return true;
		}

		private static bool SameRect(KingdomPlotRules.PlotRect A,
			KingdomPlotRules.PlotRect B)
		{
			return A.X1 == B.X1 && A.Y1 == B.Y1 && A.X2 == B.X2 && A.Y2 == B.Y2;
		}

		private static bool TryHeartFacing(KingdomArchitectureMapping Mapping, Zone Z,
			KingdomPlotRules.PlotRect Rect, out ArchitectureFacing Facing, out string Failure)
		{
			Facing = ArchitectureFacing.North;
			Failure = null;
			int canonicalWidth;
			int canonicalHeight;
			if (!KingdomArchitectureRules.TryCanonicalDimensions(Mapping.LotSize,
				out canonicalWidth, out canonicalHeight))
				return Fail("the frozen mapping has an unknown lot size", out Failure);
			bool northSouth = Rect.Width == canonicalWidth && Rect.Height == canonicalHeight;
			bool eastWest = Rect.Width == canonicalHeight && Rect.Height == canonicalWidth;
			if (!northSouth && !eastWest)
				return Fail("the staked rectangle does not exactly fit the frozen lot size in any pose",
					out Failure);
			if (Mapping.Frontage != ArchitectureFrontage.Heart)
				return Fail("building " + Mapping.BuildKey + " has an unknown frontage", out Failure);

			int heartX;
			int heartY;
			KingdomPlots.HeartFor(Z, Rect, out heartX, out heartY);
			if (northSouth && !eastWest)
				Facing = heartY <= Rect.CenterY ? ArchitectureFacing.North : ArchitectureFacing.South;
			else if (eastWest && !northSouth)
				Facing = heartX >= Rect.CenterX ? ArchitectureFacing.East : ArchitectureFacing.West;
			else
			{
				// No shipped lot is square, but the tie law is fixed for additive sizes.
				int dx = heartX - Rect.CenterX;
				int dy = heartY - Rect.CenterY;
				if (Math.Abs(dx) > Math.Abs(dy))
					Facing = dx >= 0 ? ArchitectureFacing.East : ArchitectureFacing.West;
				else
					Facing = dy <= 0 ? ArchitectureFacing.North : ArchitectureFacing.South;
			}
			int posedWidth;
			int posedHeight;
			if (!KingdomArchitectureRules.TryDimensions(Mapping.LotSize, Facing,
				out posedWidth, out posedHeight)
				|| posedWidth != Rect.Width || posedHeight != Rect.Height)
				return Fail("the selected cardinal pose does not exactly fit the staked rectangle",
					out Failure);
			return true;
		}

		private static bool TryRoadFacing(string BuildKey, KingdomArchitectureMapping Mapping,
			Zone Z, KingdomPlotRules.PlotRect Rect, ArchitectureSelectionContext Context,
			out ArchitectureFacing Facing, out ArchitectureLayoutSnapshot Snapshot,
			out string Failure)
		{
			Facing = ArchitectureFacing.North;
			Snapshot = null;
			Failure = null;
			if (Mapping.Frontage != ArchitectureFrontage.Road)
				return Fail("road-facing resolution needs a Road frontage mapping", out Failure);
			ArchitectureFacing[] candidates = new ArchitectureFacing[]
			{
				ArchitectureFacing.North, ArchitectureFacing.East,
				ArchitectureFacing.South, ArchitectureFacing.West
			};
			int bestScore = -1;
			ArchitectureLayoutSnapshot best = null;
			for (int i = 0; i < candidates.Length; i++)
			{
				ArchitectureFacing candidate = candidates[i];
				int width;
				int height;
				if (!KingdomArchitectureRules.TryDimensions(Mapping.LotSize, candidate,
					out width, out height) || width != Rect.Width || height != Rect.Height) continue;
				ArchitectureLayoutSnapshot resolved;
				if (!KingdomArchitecture.TryResolve(BuildKey, Mapping.TypeKey,
					Mapping.LotSize, Context, candidate,
					out resolved, out Failure)) return false;
				if (!MatchesMapping(resolved, Mapping))
					return Fail("road-facing candidate disagrees with its frozen mapping", out Failure);
				int score;
				if (!TryRoadIngressScore(Z, Rect, resolved, out score, out Failure)) return false;
				// Candidate order is the fixed N/E/S/W tie law; equal scores keep the earlier pose.
				if (score > bestScore)
				{
					bestScore = score;
					Facing = candidate;
					best = resolved;
				}
			}
			if (best == null || bestScore <= 0)
				return Fail("building " + Mapping.BuildKey
					+ " has no authored public entrance connected to existing road evidence", out Failure);
			Snapshot = best;
			return true;
		}

		private static bool TryRectLotSize(KingdomPlotRules.PlotRect Rect,
			out ArchitectureLotSize Size)
		{
			Size = ArchitectureLotSize.Small;
			for (int i = (int)ArchitectureLotSize.Small; i <= (int)ArchitectureLotSize.Huge; i++)
			{
				ArchitectureLotSize candidate = (ArchitectureLotSize)i;
				int width;
				int height;
				if (!KingdomArchitectureRules.TryCanonicalDimensions(candidate, out width, out height))
					continue;
				if ((Rect.Width == width && Rect.Height == height)
					|| (Rect.Width == height && Rect.Height == width))
				{
					Size = candidate;
					return true;
				}
			}
			return false;
		}

		private static bool TryRoadIngressScore(Zone Z, KingdomPlotRules.PlotRect Rect,
			ArchitectureLayoutSnapshot Snapshot, out int Score, out string Failure)
		{
			Score = 0;
			Failure = null;
			bool foundEntrance = false;
			for (int i = 0; i < Snapshot.Anchors.Count; i++)
			{
				ArchitectureAnchor anchor = Snapshot.Anchors[i];
				if (anchor == null || !(anchor.Key == "entrance:public"
					|| anchor.Key.StartsWith("entrance:public@", StringComparison.Ordinal))) continue;
				foundEntrance = true;
				int x;
				int y;
				if (!TryWorldAnchor(Snapshot, Rect, anchor, out x, out y, out Failure)) return false;
				int[] dx = new int[4] { 0, 1, 0, -1 };
				int[] dy = new int[4] { -1, 0, 1, 0 };
				for (int d = 0; d < 4; d++)
				{
					int roadX = x + dx[d];
					int roadY = y + dy[d];
					if (Rect.Contains(roadX, roadY) || roadX < 0 || roadX >= Z.Width
						|| roadY < 0 || roadY >= Z.Height) continue;
					int evidence;
					if (!TryRoadEvidenceAt(Z, roadX, roadY, out evidence, out Failure)) return false;
					if (evidence > Score) Score = evidence;
				}
			}
			if (!foundEntrance)
				return Fail("road-facing architecture has no entrance:public anchor", out Failure);
			return true;
		}

		private static bool TryRoadEvidenceAt(Zone Z, int X, int Y,
			out int Score, out string Failure)
		{
			Score = 0;
			Failure = null;
			Cell cell = Z.GetCell(X, Y);
			GameObject floor;
			KingdomPhysicalLookupState floorState = KingdomRoads.FindOurFloor(cell, out floor);
			if (floorState == KingdomPhysicalLookupState.Ambiguous)
				return Fail("road ingress evidence is physically ambiguous", out Failure);
			if (floorState == KingdomPhysicalLookupState.Exact)
				Score = 100000 + 1000 * floor.GetIntProperty(KingdomRoads.PathStateProperty);
			System.Collections.Generic.List<KingdomRoadRules.WornCell> tally = KingdomRoads.ReadTally(Z);
			for (int i = 0; i < tally.Count; i++)
			{
				KingdomRoadRules.WornCell worn = tally[i];
				if (worn.X != X || worn.Y != Y
					|| KingdomRoadRules.WearAt(worn.Traffic) <= KingdomRoadRules.WearState.Untouched) continue;
				int traffic = worn.Traffic > KingdomRoadRules.MaxTraffic
					? KingdomRoadRules.MaxTraffic : worn.Traffic;
				int evidence = 1000 + traffic;
				if (evidence > Score) Score = evidence;
			}
			return true;
		}

		private static bool TrySelectionContext(KingdomSystem System, Zone Z,
			out ArchitectureSelectionContext Context, out string Failure)
		{
			Context = null;
			Failure = null;
			if (string.IsNullOrWhiteSpace(System.Style)
				|| System.Style.Length > KingdomArchitectureRules.MaxSelectorChars
				|| HasControl(System.Style))
				return Fail("the settlement style is absent or over the architecture selector bound",
					out Failure);
			if (!KingdomRules.IsKnownStage(System.Stage))
				return Fail("the settlement has an unknown growth stage", out Failure);
			TechLevel tech = KingdomZoning.Tech(System);
			if (!KingdomZoningRules.IsKnownTechLevel(tech))
				return Fail("the settlement has an unknown craft rung", out Failure);

			string terrain = null;
			try
			{
				GameObject current = Z.GetTerrainObject();
				terrain = current == null ? null : current.Blueprint;
			}
			catch
			{
				// The persisted founding evidence remains the exact fallback authority.
			}
			if (string.IsNullOrEmpty(terrain)) terrain = System.FoundingTerrainBlueprint;
			if (terrain != null && (terrain.Length > KingdomArchitectureRules.MaxSelectorChars
				|| HasControl(terrain)))
				return Fail("terrain evidence is over the architecture selector bound", out Failure);
			string creed = KingdomCreed.SeatCreed(System);
			if (creed != null && (creed.Length > KingdomArchitectureRules.MaxSelectorChars
				|| HasControl(creed)))
				return Fail("the dominant seat creed is over the architecture selector bound", out Failure);

			Context = new ArchitectureSelectionContext
			{
				Style = System.Style,
				Creed = creed,
				Cultures = KingdomResidentIdentityRules.FactNames(System.CultureCounts,
					KingdomZoningRules.KindCulture),
				Species = KingdomResidentIdentityRules.FactNames(System.SpeciesCounts,
					KingdomZoningRules.KindSpecies),
				Genotypes = KingdomResidentIdentityRules.IdentityNames(System.IdentityCounts,
					KingdomResidentIdentityRules.KindGenotype),
				Bodies = KingdomResidentIdentityRules.IdentityNames(System.IdentityCounts,
					KingdomResidentIdentityRules.KindBody),
				Terrain = terrain,
				Stratum = Z.Z > KingdomRules.SurfaceZLevel ? "underground" : "surface",
				Stage = (int)System.Stage,
				Tech = (int)tech
			};
			return true;
		}

		// --- Durable named receipt ---------------------------------------------------------

		/// <summary>
		/// Freezes a fully validated intent. Schema is removed to invalidate any old receipt, every
		/// field is written, and schema is written last as the sole commit marker.
		/// </summary>
		public static bool TryFreeze(GameObject Target, KingdomArchitectureIntent Intent,
			out string Failure)
		{
			ArchitectureLayoutSnapshot snapshot;
			if (!TryValidateIntent(Intent, out snapshot, out Failure)) return false;
			if (Target == null) return Fail("architecture receipt target is absent", out Failure);
			try
			{
				Target.RemoveIntProperty(SchemaProperty);
				Target.SetStringProperty(BuildKeyProperty, Intent.BuildKey);
				Target.SetStringProperty(PlanKeyProperty, Intent.PlanKey);
				Target.SetStringProperty(BindingKeyProperty, Intent.BindingKey);
				Target.SetStringProperty(TierKeyProperty, Intent.TierKey);
				Target.SetStringProperty(VariantKeyProperty, Intent.VariantKey);
				Target.SetStringProperty(PaletteKeyProperty, Intent.PaletteKey);
				Target.SetStringProperty(LotTypeProperty, Intent.LotType);
				Target.SetIntProperty(LotSizeProperty, (int)Intent.LotSize);
				Target.SetIntProperty(FacingProperty, (int)Intent.Facing);
				Target.SetStringProperty(SnapshotProperty, Intent.EncodedSnapshot);
				Target.SetStringProperty(HashProperty, Intent.SnapshotHash);
				Target.SetIntProperty(RectX1Property, Intent.Rect.X1);
				Target.SetIntProperty(RectY1Property, Intent.Rect.Y1);
				Target.SetIntProperty(RectX2Property, Intent.Rect.X2);
				Target.SetIntProperty(RectY2Property, Intent.Rect.Y2);
				Target.SetIntProperty(MainXProperty, Intent.MainWorldX);
				Target.SetIntProperty(MainYProperty, Intent.MainWorldY);
				Target.SetIntProperty(SchemaProperty, ReceiptSchema);
			}
			catch (Exception exception)
			{
				try { Target.RemoveIntProperty(SchemaProperty); } catch { }
				return Fail("architecture receipt write failed: " + exception.Message, out Failure);
			}
			KingdomArchitectureIntent read;
			if (!TryRead(Target, out read, out Failure))
			{
				try { Target.RemoveIntProperty(SchemaProperty); } catch { }
				return false;
			}
			return true;
		}

		/// <summary>Reads and proves a complete canonical receipt without consulting live data.</summary>
		public static bool TryRead(GameObject Source, out KingdomArchitectureIntent Intent,
			out string Failure)
		{
			Intent = null;
			Failure = null;
			if (Source == null) return Fail("architecture receipt source is absent", out Failure);
			if (!Source.HasIntProperty(SchemaProperty)
				|| Source.HasStringProperty(SchemaProperty))
				return Fail("architecture receipt is absent or only partially written", out Failure);
			int schema = Source.GetIntProperty(SchemaProperty);
			if (schema != ReceiptSchema)
				return Fail("architecture receipt schema " + schema + " is unknown", out Failure);

			string buildKey;
			string planKey;
			string bindingKey;
			string tierKey;
			string variantKey;
			string paletteKey;
			string lotType;
			string encoded;
			string hash;
			int lotSize;
			int facing;
			int x1;
			int y1;
			int x2;
			int y2;
			int mainX;
			int mainY;
			if (!ReadString(Source, BuildKeyProperty, KingdomArchitectureRules.MaxKeyChars,
				out buildKey, out Failure)
				|| !ReadString(Source, PlanKeyProperty, KingdomArchitectureRules.MaxKeyChars,
					out planKey, out Failure)
				|| !ReadString(Source, BindingKeyProperty, KingdomArchitectureRules.MaxKeyChars,
					out bindingKey, out Failure)
				|| !ReadString(Source, TierKeyProperty, KingdomArchitectureRules.MaxKeyChars,
					out tierKey, out Failure)
				|| !ReadString(Source, VariantKeyProperty, KingdomArchitectureRules.MaxKeyChars,
					out variantKey, out Failure)
				|| !ReadString(Source, PaletteKeyProperty, KingdomArchitectureRules.MaxKeyChars,
					out paletteKey, out Failure)
				|| !ReadString(Source, LotTypeProperty, KingdomArchitectureRules.MaxKeyChars,
					out lotType, out Failure)
				|| !ReadInt(Source, LotSizeProperty, out lotSize, out Failure)
				|| !ReadInt(Source, FacingProperty, out facing, out Failure)
				|| !ReadString(Source, SnapshotProperty, KingdomArchitectureRules.MaxSnapshotChars,
					out encoded, out Failure)
				|| !ReadString(Source, HashProperty, 64, out hash, out Failure)
				|| !ReadInt(Source, RectX1Property, out x1, out Failure)
				|| !ReadInt(Source, RectY1Property, out y1, out Failure)
				|| !ReadInt(Source, RectX2Property, out x2, out Failure)
				|| !ReadInt(Source, RectY2Property, out y2, out Failure)
				|| !ReadInt(Source, MainXProperty, out mainX, out Failure)
				|| !ReadInt(Source, MainYProperty, out mainY, out Failure)) return false;

			KingdomArchitectureIntent read = KingdomArchitectureIntent.CreateRaw(schema,
				buildKey, planKey, bindingKey, tierKey, variantKey, paletteKey, lotType,
				(ArchitectureLotSize)lotSize, (ArchitectureFacing)facing, encoded, hash,
				new KingdomPlotRules.PlotRect(x1, y1, x2, y2), mainX, mainY);
			ArchitectureLayoutSnapshot snapshot;
			if (!TryValidateIntent(read, out snapshot, out Failure)) return false;
			Intent = read;
			return true;
		}

		/// <summary>
		/// Copies a works receipt to its final behavior root. Source is fully read before Target is
		/// touched; no architecture catalogue or current building entry is consulted.
		/// </summary>
		public static bool TryCopyFrozen(GameObject Source, GameObject Target, out string Failure)
		{
			KingdomArchitectureIntent intent;
			if (!TryRead(Source, out intent, out Failure)) return false;
			return TryFreeze(Target, intent, out Failure);
		}

		public static bool TryValidate(KingdomArchitectureIntent Intent, out string Failure)
		{
			ArchitectureLayoutSnapshot snapshot;
			return TryValidateIntent(Intent, out snapshot, out Failure);
		}

		public static bool TryDecode(KingdomArchitectureIntent Intent,
			out ArchitectureLayoutSnapshot Snapshot, out string Failure)
		{
			return TryValidateIntent(Intent, out Snapshot, out Failure);
		}

		private static bool TryValidateIntent(KingdomArchitectureIntent Intent,
			out ArchitectureLayoutSnapshot Snapshot, out string Failure)
		{
			Snapshot = null;
			Failure = null;
			if (Intent == null) return Fail("architecture intent is absent", out Failure);
			if (Intent.SchemaVersion != ReceiptSchema)
				return Fail("architecture intent schema is absent or unknown", out Failure);
			if (!ValidKey(Intent.BuildKey) || !ValidKey(Intent.PlanKey)
				|| !ValidKey(Intent.BindingKey) || !ValidKey(Intent.TierKey)
				|| !ValidKey(Intent.VariantKey) || !ValidKey(Intent.PaletteKey)
				|| !ValidKey(Intent.LotType))
				return Fail("architecture intent scalar identity is malformed", out Failure);
			if (string.IsNullOrEmpty(Intent.EncodedSnapshot)
				|| Intent.EncodedSnapshot.Length > KingdomArchitectureRules.MaxSnapshotChars)
				return Fail("architecture intent snapshot is absent or over the bound", out Failure);
			if (!CanonicalHash(Intent.SnapshotHash))
				return Fail("architecture intent hash is malformed", out Failure);
			ArchitectureLayoutSnapshot snapshot;
			if (!KingdomArchitectureRules.TryDecodeSnapshot(Intent.EncodedSnapshot,
				out snapshot, out Failure)) return false;
			string hash;
			if (!KingdomArchitectureRules.TryEncodedSnapshotHash(Intent.EncodedSnapshot,
				out hash, out Failure)
				|| hash != Intent.SnapshotHash)
				return Fail("architecture intent hash disagrees with its canonical snapshot", out Failure);
			if (snapshot.BuildKey != Intent.BuildKey || snapshot.PlanKey != Intent.PlanKey
				|| snapshot.BindingKey != Intent.BindingKey || snapshot.TierKey != Intent.TierKey
				|| snapshot.VariantKey != Intent.VariantKey || snapshot.PaletteKey != Intent.PaletteKey
				|| snapshot.LotType != Intent.LotType || snapshot.LotSize != Intent.LotSize
				|| snapshot.Facing != Intent.Facing)
				return Fail("architecture intent scalars disagree with the canonical snapshot", out Failure);
			if (!ValidRect(Intent.Rect))
				return Fail("architecture intent rectangle is malformed", out Failure);
			int worldWidth;
			int worldHeight;
			if (!KingdomArchitectureRules.TryWorldDimensions(snapshot.Width, snapshot.Height,
				snapshot.Facing, out worldWidth, out worldHeight)
				|| Intent.Rect.Width != worldWidth || Intent.Rect.Height != worldHeight)
				return Fail("architecture intent rectangle does not fit its canonical pose", out Failure);
			int mainX;
			int mainY;
			if (!KingdomArchitectureRules.TryToWorld(Intent.Rect.X1, Intent.Rect.Y1,
				snapshot.Width, snapshot.Height, snapshot.Facing, snapshot.MainX, snapshot.MainY,
				out mainX, out mainY)
				|| !Intent.Rect.Contains(mainX, mainY)
				|| mainX != Intent.MainWorldX || mainY != Intent.MainWorldY)
				return Fail("architecture intent world main cell disagrees with its snapshot and rect",
					out Failure);
			Snapshot = snapshot;
			return true;
		}

		// --- Exact canonical-to-world helpers ---------------------------------------------

		public static bool TryWorldCell(ArchitectureLayoutSnapshot Snapshot,
			KingdomPlotRules.PlotRect Rect, ArchitectureCellState Cell,
			out int WorldX, out int WorldY, out string Failure)
		{
			WorldX = 0;
			WorldY = 0;
			if (Cell == null || !ContainsCell(Snapshot, Cell))
				return Fail("cell is not an exact member of the snapshot", out Failure);
			return TryWorldCoordinate(Snapshot, Rect, Cell.X, Cell.Y,
				out WorldX, out WorldY, out Failure);
		}

		public static bool TryWorldPlacement(ArchitectureLayoutSnapshot Snapshot,
			KingdomPlotRules.PlotRect Rect, ArchitecturePlacement Placement,
			out int WorldX, out int WorldY, out string Failure)
		{
			WorldX = 0;
			WorldY = 0;
			if (Placement == null || !ContainsPlacement(Snapshot, Placement))
				return Fail("placement is not an exact member of the snapshot", out Failure);
			return TryWorldCoordinate(Snapshot, Rect, Placement.X, Placement.Y,
				out WorldX, out WorldY, out Failure);
		}

		public static bool TryWorldAnchor(ArchitectureLayoutSnapshot Snapshot,
			KingdomPlotRules.PlotRect Rect, ArchitectureAnchor Anchor,
			out int WorldX, out int WorldY, out string Failure)
		{
			WorldX = 0;
			WorldY = 0;
			if (Anchor == null || !ContainsAnchor(Snapshot, Anchor))
				return Fail("anchor is not an exact member of the snapshot", out Failure);
			return TryWorldCoordinate(Snapshot, Rect, Anchor.X, Anchor.Y,
				out WorldX, out WorldY, out Failure);
		}

		private static bool TryWorldCoordinate(ArchitectureLayoutSnapshot Snapshot,
			KingdomPlotRules.PlotRect Rect, int X, int Y,
			out int WorldX, out int WorldY, out string Failure)
		{
			WorldX = 0;
			WorldY = 0;
			if (Snapshot == null || !ValidRect(Rect))
				return Fail("snapshot or exact world rectangle is malformed", out Failure);
			int worldWidth;
			int worldHeight;
			if (!KingdomArchitectureRules.TryWorldDimensions(Snapshot.Width, Snapshot.Height,
				Snapshot.Facing, out worldWidth, out worldHeight)
				|| Rect.Width != worldWidth || Rect.Height != worldHeight)
				return Fail("world rectangle does not exactly fit the snapshot pose", out Failure);
			if (!KingdomArchitectureRules.TryToWorld(Rect.X1, Rect.Y1, Snapshot.Width,
				Snapshot.Height, Snapshot.Facing, X, Y, out WorldX, out WorldY)
				|| !Rect.Contains(WorldX, WorldY))
				return Fail("snapshot coordinate does not transform inside its exact rectangle", out Failure);
			Failure = null;
			return true;
		}

		private static bool ContainsCell(ArchitectureLayoutSnapshot Snapshot,
			ArchitectureCellState Cell)
		{
			if (Snapshot == null || Snapshot.Cells == null) return false;
			for (int i = 0; i < Snapshot.Cells.Count; i++)
			{
				ArchitectureCellState candidate = Snapshot.Cells[i];
				if (candidate != null && candidate.X == Cell.X && candidate.Y == Cell.Y
					&& candidate.Claim == Cell.Claim
					&& candidate.Passability == Cell.Passability && candidate.Cover == Cell.Cover)
					return true;
			}
			return false;
		}

		private static bool ContainsPlacement(ArchitectureLayoutSnapshot Snapshot,
			ArchitecturePlacement Placement)
		{
			if (Snapshot == null || Snapshot.Placements == null) return false;
			for (int i = 0; i < Snapshot.Placements.Count; i++)
			{
				ArchitecturePlacement candidate = Snapshot.Placements[i];
				if (candidate != null && candidate.Layer == Placement.Layer
					&& candidate.X == Placement.X && candidate.Y == Placement.Y
					&& candidate.Blueprint == Placement.Blueprint && candidate.Slot == Placement.Slot
					&& candidate.StatefulAnchor == Placement.StatefulAnchor
					&& candidate.Material == Placement.Material
					&& candidate.MinTech == Placement.MinTech
					&& candidate.Knowledge == Placement.Knowledge
					&& candidate.Power == Placement.Power
					&& candidate.Natural == Placement.Natural
					&& candidate.ExistingAuthority == Placement.ExistingAuthority) return true;
			}
			return false;
		}

		private static bool ContainsAnchor(ArchitectureLayoutSnapshot Snapshot,
			ArchitectureAnchor Anchor)
		{
			if (Snapshot == null || Snapshot.Anchors == null) return false;
			for (int i = 0; i < Snapshot.Anchors.Count; i++)
			{
				ArchitectureAnchor candidate = Snapshot.Anchors[i];
				if (candidate != null && candidate.Key == Anchor.Key && candidate.X == Anchor.X
					&& candidate.Y == Anchor.Y && candidate.Access == Anchor.Access) return true;
			}
			return false;
		}

		// --- Small receipt helpers ---------------------------------------------------------

		private static bool ReadString(GameObject Source, string Property, int Maximum,
			out string Value, out string Failure)
		{
			Value = null;
			if (!Source.HasStringProperty(Property) || Source.HasIntProperty(Property))
				return Fail("architecture receipt property " + Property + " is absent or has the wrong type",
					out Failure);
			Value = Source.GetStringProperty(Property, null);
			if (string.IsNullOrEmpty(Value) || Value.Length > Maximum || HasControl(Value))
				return Fail("architecture receipt property " + Property + " is malformed", out Failure);
			Failure = null;
			return true;
		}

		private static bool ReadInt(GameObject Source, string Property,
			out int Value, out string Failure)
		{
			Value = 0;
			if (!Source.HasIntProperty(Property) || Source.HasStringProperty(Property))
				return Fail("architecture receipt property " + Property + " is absent or has the wrong type",
					out Failure);
			Value = Source.GetIntProperty(Property);
			Failure = null;
			return true;
		}

		private static bool MatchesMapping(ArchitectureLayoutSnapshot Snapshot,
			KingdomArchitectureMapping Mapping)
		{
			return Snapshot != null && Mapping != null && Snapshot.BuildKey == Mapping.BuildKey
				&& Snapshot.PlanKey == Mapping.PlanKey && Snapshot.BindingKey == Mapping.BindingKey
				&& Snapshot.TierKey == Mapping.TierKey && Snapshot.LotType == Mapping.TypeKey
				&& Snapshot.LotSize == Mapping.LotSize;
		}

		private static bool ValidRectInZone(KingdomPlotRules.PlotRect Rect, Zone Z)
		{
			return ValidRect(Rect) && Z != null && Rect.X1 >= 0 && Rect.Y1 >= 0
				&& Rect.X2 < Z.Width && Rect.Y2 < Z.Height;
		}

		private static bool ValidRect(KingdomPlotRules.PlotRect Rect)
		{
			if (Rect.X2 < Rect.X1 || Rect.Y2 < Rect.Y1) return false;
			long width = (long)Rect.X2 - Rect.X1 + 1L;
			long height = (long)Rect.Y2 - Rect.Y1 + 1L;
			return width > 0 && height > 0
				&& width * height <= KingdomArchitectureRules.MaxMapArea;
		}

		private static bool ValidKey(string Value)
		{
			return !string.IsNullOrEmpty(Value) && Value.Length <= KingdomArchitectureRules.MaxKeyChars
				&& Value == Value.Trim() && !HasControl(Value);
		}

		private static bool CanonicalHash(string Value)
		{
			if (Value == null || Value.Length != 64) return false;
			for (int i = 0; i < Value.Length; i++)
				if (!((Value[i] >= '0' && Value[i] <= '9')
					|| (Value[i] >= 'a' && Value[i] <= 'f'))) return false;
			return true;
		}

		private static bool HasControl(string Value)
		{
			if (Value == null) return false;
			for (int i = 0; i < Value.Length; i++) if (char.IsControl(Value[i])) return true;
			return false;
		}

		private static bool Fail(string Message, out string Failure)
		{
			if (string.IsNullOrEmpty(Message)) Message = "architecture runtime failed";
			char[] cleaned = null;
			for (int i = 0; i < Message.Length; i++)
				if (char.IsControl(Message[i]))
				{
					if (cleaned == null) cleaned = Message.ToCharArray();
					cleaned[i] = ' ';
				}
			if (cleaned != null) Message = new string(cleaned);
			if (Message.Length > MaxFailureChars) Message = Message.Substring(0, MaxFailureChars);
			Failure = Message;
			return false;
		}
	}
}
