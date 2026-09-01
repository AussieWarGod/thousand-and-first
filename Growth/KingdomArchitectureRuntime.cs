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
	public static partial class KingdomArchitectureRuntime
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
						out snapshot, out Failure)
					|| !TryVerifyPhysicalIngressRoutes(Z, Rect, snapshot, out Failure)) return false;
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
		/// Resolves an improvement inside the predecessor's frozen plan, type, variant, and cardinal
		/// pose. Fixed-envelope work remains in the exact binding. Cross-size work is bounded to one
		/// adjacent authored expansion binding or one adjacent founding-heart rung; each returned rect
		/// must contain the standing lot and separately prove its new ground before debit. This is a
		/// pre-debit catalogue read;
		/// the returned canonical intent becomes the sole authority after funding.
		/// </summary>
	}
}
