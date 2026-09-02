using System;
using XRL;
using XRL.Messages;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPlots
	{
		public const string FoundingHeartReceiptProperty = "r_TAF_FoundingHeartReceipt";
		public const string FoundingHeartOwnerProperty = "r_TAF_FoundingHeartOwner";
		public const string FoundingHeartSlotProperty = "r_TAF_FoundingHeartSlot";
		public const string FoundingHeartRootPrefix = "r_TAF_FoundingHeartRoot:";
		public const string FoundingHeartSealProperty = "r_TAF_FoundingHeartSeal";
			private enum FoundingHeartLegacyState : byte
		{
			Empty = 0,
			Complete = 1,
			PartialOrForeign = 2
		}

		private sealed class FoundingHeartContext
		{
			public KingdomFoundingHeartPlan Plan;
			public KingdomRules.BuildEntry Entry;
			public KingdomPlotRules.PlotSpec Spec;
			public KingdomPlotRules.PlotRect Survey;
			public KingdomPlotRules.PlotRect Rect;
			public KingdomArchitectureIntent Architecture;
			public KingdomFoundingHeartStakeTruth Stake;
			public string Receipt;
		}

		private sealed class FoundingHeartPlacement
		{
			public Zone Zone;
			public FoundingHeartContext Context;
			public int Slot;
		}

			/// <summary>Projects the founding heart from one frozen zero-cost receipt.</summary>
		public static bool EnsureFoundingHeartProjection(KingdomSystem System, Zone Z,
			int RiteX, int RiteY)
		{
			if (!TryFoundingHeartTransaction(System, Z, out string transaction)
				|| Z.GetCell(RiteX, RiteY) == null) return HeartRefused("transaction or rite cell");
			string raw = Z.GetZoneProperty(FoundingHeartReceiptProperty, null);
			FoundingHeartContext context;
			bool newlyPublished = false;
			if (string.IsNullOrEmpty(raw))
			{
				if (!TryDraftFoundingHeart(System, Z, transaction, RiteX, RiteY, out context))
					return HeartRefused("draft");
				FoundingHeartLegacyState legacy = ClassifyLegacyHeart(Z, context);
				if (legacy == FoundingHeartLegacyState.Complete) return true;
				if (legacy != FoundingHeartLegacyState.Empty
					|| !NewHeartIdentitiesAreEmpty(context.Plan)) return HeartRefused("legacy ground or identities already standing");
				newlyPublished = true;
			}
			else if (!KingdomFoundingHeartRules.TryDecode(raw,
				out KingdomFoundingHeartPlan plan)
				|| plan.TransactionId != transaction || plan.ZoneId != Z.ZoneID
					|| plan.RiteX != RiteX || plan.RiteY != RiteY
					|| !TryReadFoundingHeartContext(Z, plan, out context)) return HeartRefused("receipt decode or context");
			if (KingdomFoundingHeartRules.Complete(context.Plan)
				&& ExactFoundingHeartSeal(Z, context.Plan))
			{
				if (!EnsureFoundingHeartReservations(context.Plan)) return HeartRefused("sealed reservations");
				return RecoverSealedFoundingHeart(System, Z, context);
			}
			if (!FoundingHeartSealAbsent(Z)) return HeartRefused("a seal already stands");
			if (!PreflightFoundingHeartWorld(Z, context)
				|| newlyPublished && !PublishFoundingHeartPlan(Z, null, context.Plan)) return HeartRefused("preflight or publish");
			context.Receipt = KingdomFoundingHeartRules.Encode(context.Plan);
			if (!EnsureFoundingHeartZoneTruth(Z, context.Plan)
				|| !EnsureFoundingHeartReservations(context.Plan)) return HeartRefused("zone truth or reservations");
			bool before = KingdomFoundingHeartRules.Complete(context.Plan);
			for (int slot = 0; slot < KingdomFoundingHeartRules.WorksSlot; slot++)
				if (!DriveFoundingHeartMark(Z, context, slot)) return HeartRefused("mark slot " + slot);
			if (!DriveFoundingHeartWorks(System, Z, context)) return HeartRefused("works");
			if (!KingdomFoundingHeartRules.Complete(context.Plan)
				|| !ExactFoundingHeartWorld(Z, context)
				|| !SealFoundingHeart(Z, context)) return HeartRefused("completion, exact world, or seal");
			if (!before || newlyPublished)
			{
				KingdomLog.Log("heart surveyed: " + context.Survey.X1 + "," + context.Survey.Y1
					+ " to " + context.Survey.X2 + "," + context.Survey.Y2 + " around rite "
					+ RiteX + "," + RiteY);
				MessageQueue.AddPlayerMessage("{{W|" + KingdomPlotRules.SurveyLine(context.Survey) + "}}");
			}
			return true;
		}

		internal static bool RecoverFoundingHeart(KingdomSystem System, Zone Z)
		{
			string raw = Z?.GetZoneProperty(FoundingHeartReceiptProperty, null);
			if (string.IsNullOrEmpty(raw))
			{
				if (!TryRiteGround(Z, out int riteX, out int riteY))
					return !HasReceiptlessFoundingHeartEvidence(System, Z);
				return EnsureFoundingHeartProjection(System, Z, riteX, riteY);
			}
			if (!KingdomFoundingHeartRules.TryDecode(raw,
				out KingdomFoundingHeartPlan plan)) return false;
			return EnsureFoundingHeartProjection(System, Z, plan.RiteX, plan.RiteY);
		}

		private static bool TryFoundingHeartTransaction(KingdomSystem System, Zone Z,
			out string Transaction)
		{
			Transaction = null;
			if (System == null || Z == null) return false;
			if (System.SettlementIdentityFirstClaimedZone == Z.ZoneID
				&& System.SettlementIdentityOrigin == KingdomIdentityOrigin.FoundingTransaction)
				Transaction = System.SettlementIdentityTransactionId;
			else
			{
				KingdomSettlement other = System.FindNonSeatSettlementByZone(Z.ZoneID);
				if (other != null && other.SettlementIdentityFirstClaimedZone == Z.ZoneID
					&& other.SettlementIdentityOrigin == KingdomIdentityOrigin.FoundingTransaction)
					Transaction = other.SettlementIdentityTransactionId;
			}
			return KingdomIdentityRules.IsFoundingTransaction(Transaction);
		}

		private static bool TryDraftFoundingHeart(KingdomSystem System, Zone Z,
			string Transaction, int RiteX, int RiteY, out FoundingHeartContext Context)
		{
			Context = null;
			string key = KingdomPlotRules.HeartKeyForRung(1);
			if (!KingdomPlotRules.TrySurveyedHeart(RiteX, RiteY, Z.Width, Z.Height,
				out KingdomPlotRules.PlotRect survey)
				|| !KingdomPlotRules.TryHeartRect(survey, RiteX, RiteY,
					KingdomPlotRules.HeartSizeForRung(1), out KingdomPlotRules.PlotRect rect)
				|| !KingdomData.TryGetBuilding(key, out KingdomRules.BuildEntry entry)
				|| !TryGetSpec(key, out KingdomPlotRules.PlotSpec spec)
				|| !KingdomZoning.Permits(System, Z.ZoneID, entry, out _)
					|| !FoundingHeartGroundAllows(Z, rect)
				// Generic plot preflight requires the immutable basin to stand already. Founding
				// instead freezes the same authored intent and codec first; slot zero then places
				// that exact basin, which the stamper binds as existing authority at completion.
					|| (!KingdomArchitectureRuntime.TryPrepareFoundingHeart(System, Z, rect, key,
						entry.Category, RiteX, RiteY, out KingdomArchitectureIntent architecture,
						out string prepareFailure) && HeartNoted("prepare: " + prepareFailure))
				|| !TryEncodePlotPayload(rect, null, architecture, out string payload, out _))
				return HeartRefused("draft: survey, rect, catalogue, spec, zoning, liquid ground, or payload");
			GroundGrid grid = new GroundGrid(Z);
			bool carved = KingdomPlotRules.IsUnderground(Z.Z);
			if (!KingdomArchitectureRuntime.TryWorldFootprint(architecture,
				out KingdomPlotRules.PlotRect footprint, out string footprintFailure)) return HeartRefused("footprint: " + footprintFailure);
			if (!KingdomArchitectureRuntime.TryRoofOnGround(architecture, carved,
				out KingdomPlotRules.RoofState roof, out string roofFailure)) return HeartRefused("roof: " + roofFailure);
			long total = KingdomPlotRules.RaiseTicks(
				KingdomCommission.CraftBuildTicks(entry.BuildTicks, System.ZoneDistricts.Values),
				grid.CellsOf(rect), footprint, roof, carved);
			string wall = KingdomPlotRules.RaisesWalls(roof)
				? KingdomPlotRules.WallBlueprintFor(System.Style, System.FoundingRegionName) : null;
			bool door = KingdomPlotRules.TryDoor(footprint, RiteX, RiteY,
				out int doorX, out int doorY) && KingdomPlotRules.Encloses(roof);
			bool tinkering = The.Player != null && The.Player.HasSkill("Tinkering");
			bool advanced = The.Player != null && The.Player.HasSkill("Tinkering_Tinker1");
			int defence = KingdomRules.BuiltDefence(entry.Defence, true,
				System.FoundingTerrainBlueprint, System.FoundingRegionName, tinkering, advanced);
			if (!KingdomFoundingHeartStakeRules.TryCreate(key, entry.Name, entry.Blueprint,
				footprint.X1, footprint.Y1, footprint.X2, footprint.Y2, (int)roof, spec.Open,
				carved, wall, spec.Contents, entry.Staff,
				KingdomRules.IsThresholdManning(entry.Manning), defence, door, doorX, doorY,
				KingdomPurpose.FoundingHeartPurposeIsLegacy(key),
				out KingdomFoundingHeartStakeTruth stake)) return HeartRefused("stake truth");
			string stakeTruth = KingdomFoundingHeartStakeRules.Encode(stake);
			if (!KingdomFoundingHeartRules.TryCreate(Transaction, Z.ZoneID, RiteX, RiteY,
				survey.X1, survey.Y1, survey.X2, survey.Y2, rect.X1, rect.Y1, rect.X2,
				rect.Y2, The.Game.TimeTicks, total, payload, stakeTruth,
				out KingdomFoundingHeartPlan plan)) return HeartRefused("plan");
			Context = new FoundingHeartContext { Plan = plan, Entry = entry, Spec = spec,
				Survey = survey, Rect = rect, Architecture = architecture, Stake = stake };
			return true;
		}

		private static bool TryReadFoundingHeartContext(Zone Z,
			KingdomFoundingHeartPlan Plan, out FoundingHeartContext Context)
		{
			Context = null;
			if (!KingdomFoundingHeartRules.Valid(Plan)
				|| !TryDecodePlotPayload(Plan.Payload, out KingdomPlotRules.PlotRect rect,
					out string skin, out KingdomArchitectureIntent architecture,
					out bool legacy, out _)
				|| legacy || !string.IsNullOrEmpty(skin)
				|| rect.X1 != Plan.RectX1 || rect.Y1 != Plan.RectY1
				|| rect.X2 != Plan.RectX2 || rect.Y2 != Plan.RectY2
				|| architecture == null
				|| !KingdomFoundingHeartStakeRules.TryDecode(Plan.StakeTruth,
					out KingdomFoundingHeartStakeTruth stake)
				|| architecture.BuildKey != stake.BuildKey
				|| stake.Carved != KingdomPlotRules.IsUnderground(Z.Z)) return false;
			KingdomRules.BuildEntry entry = new KingdomRules.BuildEntry
			{
				Key = stake.BuildKey, DisplayName = stake.DisplayName, Blueprint = stake.Blueprint,
				BuildTicks = 1L, Staff = stake.Staff,
				Manning = stake.ThresholdManning ? "threshold" : "scaled"
			};
			KingdomPlotRules.PlotSpec spec = new KingdomPlotRules.PlotSpec
			{
				Key = stake.BuildKey, Open = stake.Open, Contents = stake.Contents,
				FootprintWidth = stake.FootprintX2 - stake.FootprintX1 + 1,
				FootprintHeight = stake.FootprintY2 - stake.FootprintY1 + 1,
				Roof = (KingdomPlotRules.RoofState)stake.Roof, RoofDeclared = true
			};
			KingdomPlotRules.PlotRect survey = new KingdomPlotRules.PlotRect(Plan.SurveyX1,
				Plan.SurveyY1, Plan.SurveyX2, Plan.SurveyY2);
			Context = new FoundingHeartContext { Plan = Plan, Entry = entry, Spec = spec,
				Survey = survey, Rect = rect, Architecture = architecture, Stake = stake,
				Receipt = KingdomFoundingHeartRules.Encode(Plan) };
			return Z.Width > Plan.SurveyX2 && Z.Height > Plan.SurveyY2;
		}

		private static bool FoundingHeartGroundAllows(Zone Z, KingdomPlotRules.PlotRect Rect)
		{
			GroundGrid grid = new GroundGrid(Z);
			for (int y = Rect.Y1; y <= Rect.Y2; y++)
				for (int x = Rect.X1; x <= Rect.X2; x++)
					if (grid.KindAt(x, y) == KingdomPlotRules.GroundKind.Liquid) return false;
			return true;
		}

		private static bool PublishFoundingHeartPlan(Zone Z, string Expected,
			KingdomFoundingHeartPlan Plan)
		{
			string encoded = KingdomFoundingHeartRules.Encode(Plan);
			if (Z == null || encoded == null
				|| Z.GetZoneProperty(FoundingHeartReceiptProperty, null) != Expected) return false;
			Z.SetZoneProperty(FoundingHeartReceiptProperty, encoded);
			return Z.GetZoneProperty(FoundingHeartReceiptProperty, null) == encoded;
		}

		private static bool EnsureFoundingHeartZoneTruth(Zone Z, KingdomFoundingHeartPlan Plan)
		{
			return ExactOrWriteZoneProperty(Z, RiteXProperty, Plan.RiteX)
				&& ExactOrWriteZoneProperty(Z, RiteYProperty, Plan.RiteY)
				&& ExactOrWriteZoneProperty(Z, SurveyX1Property, Plan.SurveyX1)
				&& ExactOrWriteZoneProperty(Z, SurveyY1Property, Plan.SurveyY1)
				&& ExactOrWriteZoneProperty(Z, SurveyX2Property, Plan.SurveyX2)
				&& ExactOrWriteZoneProperty(Z, SurveyY2Property, Plan.SurveyY2);
		}

		private static bool ExactFoundingHeartReceipt(Zone Z, KingdomFoundingHeartPlan Plan)
		{
			string expected = KingdomFoundingHeartRules.Encode(Plan);
			string raw = Z?.GetZoneProperty(FoundingHeartReceiptProperty, null);
			return expected != null && raw == expected
				&& KingdomFoundingHeartRules.TryDecode(raw, out KingdomFoundingHeartPlan read)
				&& KingdomFoundingHeartRules.Encode(read) == expected;
		}

		private static bool ExactFoundingHeartZoneTruth(Zone Z, KingdomFoundingHeartPlan Plan)
		{
			return ExactZoneProperty(Z, RiteXProperty, Plan.RiteX)
				&& ExactZoneProperty(Z, RiteYProperty, Plan.RiteY)
				&& ExactZoneProperty(Z, SurveyX1Property, Plan.SurveyX1)
				&& ExactZoneProperty(Z, SurveyY1Property, Plan.SurveyY1)
				&& ExactZoneProperty(Z, SurveyX2Property, Plan.SurveyX2)
				&& ExactZoneProperty(Z, SurveyY2Property, Plan.SurveyY2);
		}

		private static bool HasReceiptlessFoundingHeartEvidence(KingdomSystem System, Zone Z)
		{
			if (Z == null) return true;
			foreach (string key in new string[] { FoundingHeartSealProperty, RiteXProperty, RiteYProperty, SurveyX1Property,
				SurveyY1Property, SurveyX2Property, SurveyY2Property })
				if (!string.IsNullOrEmpty(Z.GetZoneProperty(key, null))) return true;
			if (HasFoundingHeartEvidenceInZone(Z)) return true;
			return TryFoundingHeartTransaction(System, Z, out string transaction)
				&& HasGlobalFoundingHeartTransactionEvidence(transaction, Z.ZoneID);
		}

		internal static bool EnsureFoundingRiteGround(Zone Z, int RiteX, int RiteY)
		{
			return Z?.GetCell(RiteX, RiteY) != null
				&& ExactOrWriteZoneProperty(Z, RiteXProperty, RiteX)
				&& ExactOrWriteZoneProperty(Z, RiteYProperty, RiteY);
		}

		private static bool ExactOrWriteZoneProperty(Zone Z, string Key, int Expected)
		{
			string raw = Z?.GetZoneProperty(Key, null);
			string exact = Expected.ToString(global::System.Globalization.CultureInfo.InvariantCulture);
			if (!string.IsNullOrEmpty(raw)) return raw == exact;
			Z.SetZoneProperty(Key, exact);
			return Z.GetZoneProperty(Key, null) == exact;
		}

		private static bool ExactZoneProperty(Zone Z, string Key, int Expected)
		{
			return Z?.GetZoneProperty(Key, null) == Expected.ToString(
				global::System.Globalization.CultureInfo.InvariantCulture);
		}
	}
}
