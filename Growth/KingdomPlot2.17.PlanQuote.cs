using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.World;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomPlots
	{
		public static bool TryQuotePlan(KingdomSystem System, Zone Z,
			KingdomRules.BuildEntry Entry, string SkinKey, KingdomPlotRules.PlotSize Stake,
			Cell StakeCell, out KingdomPlotQuote Quote, out string Failure)
		{
			Quote = null;
			Failure = null;
			Failure = KingdomPurpose.PlanRefusal(Entry?.Key);
			if (Failure != null) return false;
			if (System == null || Z == null || Entry == null || StakeCell == null
				|| StakeCell.ParentZone != Z || !TryGetSpec(Entry.Key, out var spec))
			{
				Failure = "The plan needs an exact plotted design and a survey stake on this ground.";
				return false;
			}
			if (KingdomPlotRules.HeartRungOf(Entry.Key) > 0)
			{
				Failure = KingdomPlotRules.RefuseSecondHeart(KingdomPresentation.Rich(System.SeatName));
				return false;
			}
			Failure = KingdomCommission.StageRefusal(System, Entry);
			if (Failure != null || !KingdomZoning.Permits(System, Z.ZoneID, Entry, out Failure))
				return false;
			KingdomPlotRules.PlotSize staked = StakedSize(spec, Stake);
			if (!KingdomPlotRules.Allows(System.Stage, staked))
			{
				Failure = KingdomPlotRules.RefuseStage(staked, KingdomPresentation.Rich(System.SeatName), System.Stage);
				return false;
			}
			Failure = KingdomDelve.Refusal(System, Z.ZoneID, Entry.Key, Entry.Name);
			if (Failure != null) return false;
			bool carved = KingdomPlotRules.IsUnderground(Z.Z);
			if (carved && spec.RequiresSky)
			{
				Failure = KingdomPlotRules.RefuseSky(Entry.Name);
				return false;
			}
			if (KingdomPlotRules.RoofRefusesSky(spec))
			{
				Failure = KingdomPlotRules.RefuseRoofSky(Entry.Name, spec.Roof);
				return false;
			}
			if (KingdomPlotRules.WouldExceedBudget(ReadPlots(Z), staked, Z.Width, Z.Height))
			{
				Failure = KingdomPlotRules.RefuseBudget(KingdomPresentation.Rich(System.SeatName));
				return false;
			}
			GroundGrid grid = new GroundGrid(Z, StakeCell.X, StakeCell.Y);
			if (!TryFindRect(Z, System, Entry, spec, staked, grid, StakeCell,
				out KingdomPlotRules.PlotRect rect, out KingdomLayoutRules.LayoutOutcome outcome,
				out Failure)) return false;
			if (rect.Contains(StakeCell.X, StakeCell.Y))
			{
				Failure = "The survey stake would stand inside its own reserved lot.";
				return false;
			}
			if (!TryPreparePlotPayload(System, Z, rect, Entry.Key, Entry.Category, SkinKey,
				out KingdomArchitectureIntent architecture, out string payload, out Failure))
				return false;
			Cell main = Z.GetCell(architecture.MainWorldX, architecture.MainWorldY);
			if (main == null || KingdomConstruction.HasActiveAt(System, Z, main))
			{
				Failure = main == null
					? "The authored building's main anchor is outside its reserved lot."
					: "That lot's authored main ground already has paid construction in hand.";
				return false;
			}
			long total = KingdomPlotRules.RaiseTicks(
				KingdomCommission.CraftBuildTicks(Entry.BuildTicks, System.ZoneDistricts.Values),
				grid.CellsOf(rect), PlannedFootprint(Z, rect, spec),
				KingdomPlotRules.RoofOnGround(spec.Roof, carved), carved);
			if (total < 1L)
			{
				Failure = "The exact plan labour quote is empty.";
				return false;
			}
			Quote = new KingdomPlotQuote
			{
				Rect = rect, StakedSize = staked, Outcome = outcome,
				Architecture = architecture, Payload = payload, LabourTicks = total,
				WaterDrams = Entry.CostDrams,
				MaterialClaim = new KingdomMaterialDebitCost(KingdomMaterials.CostFor(Entry.Key),
					KingdomMaterials.BitCostFor(Entry.Key), KingdomMaterials.ExoticCostFor(Entry.Key)),
				MainX = architecture.MainWorldX, MainY = architecture.MainWorldY
			};
			return true;
		}

		/// <summary>Freezes a quote onto a detached marker. Schema is written last.</summary>
		public static bool TryFreezePlan(GameObject Marker, KingdomRules.BuildEntry Entry,
			KingdomPlotQuote Quote, out string Failure)
		{
			Failure = null;
			if (Marker == null || Entry == null || Quote == null || Quote.Architecture == null
				|| Quote.WaterDrams < 0 || Quote.LabourTicks < 1L || Quote.MaterialClaim == null
				|| Quote.Architecture.BuildKey != Entry.Key
				|| Quote.Payload == null
				|| !TryDecodePlotPayload(Quote.Payload, out KingdomPlotRules.PlotRect decoded,
					out _, out KingdomArchitectureIntent architecture, out bool legacy, out Failure)
				|| legacy || !SameRect(decoded, Quote.Rect)
				|| !SameIntent(architecture, Quote.Architecture))
			{
				if (Failure == null) Failure = "The plan quote is absent, legacy, or internally inconsistent.";
				return false;
			}
			string labour = Quote.LabourTicks.ToString(
				global::System.Globalization.CultureInfo.InvariantCulture);
			string material = Quote.MaterialClaim.ToClaimString();
			try
			{
				Marker.RemoveIntProperty(PlanSchemaProperty);
				StampRect(Marker, Quote.Rect);
				Marker.SetStringProperty(PlanPayloadProperty, Quote.Payload);
				Marker.SetStringProperty(PlanLabourProperty, labour);
				Marker.SetIntProperty(PlanWaterProperty, Quote.WaterDrams);
				Marker.SetStringProperty(PlanMaterialProperty, material);
				Marker.SetIntProperty(PlanSchemaProperty, PlanSchema);
			}
			catch (Exception exception)
			{
				try { Marker.RemoveIntProperty(PlanSchemaProperty); } catch { }
				Failure = "The plan receipt could not be frozen: " + exception.Message;
				return false;
			}
			return TryReadFrozenPlan(Marker, Entry, false, out _, out _, out _, out _,
				out _, out Failure);
		}

		/// <summary>Reads a new frozen plan without consulting current costs or architecture data.</summary>
		internal static bool TryReadFrozenPlan(GameObject Marker, KingdomRules.BuildEntry Entry,
			bool RequireWorld, out KingdomPlotRules.PlotRect Rect, out string Payload,
			out long LabourTicks, out int WaterDrams, out KingdomMaterialDebitCost Material,
			out string Failure)
		{
			Rect = default(KingdomPlotRules.PlotRect);
			Payload = null;
			LabourTicks = 0L;
			WaterDrams = 0;
			Material = null;
			Failure = null;
			if (Marker == null || Entry == null || Marker.HasStringProperty(PlanSchemaProperty)
				|| !Marker.HasIntProperty(PlanSchemaProperty)
				|| Marker.GetIntProperty(PlanSchemaProperty) != PlanSchema
				|| Marker.GetPart<r_KingdomPlanMarker>() == null
				|| Marker.GetPart<r_KingdomPlanMarker>().DesignKey != Entry.Key
				|| !TryReadRect(Marker, out Rect))
			{
				Failure = "The frozen plan receipt is absent, partial, or unknown.";
				return false;
			}
			Payload = Marker.GetStringProperty(PlanPayloadProperty);
			if (!TryDecodePlotPayload(Payload, out KingdomPlotRules.PlotRect decoded, out _,
				out KingdomArchitectureIntent architecture, out bool legacy, out Failure)
				|| legacy || architecture.BuildKey != Entry.Key || !SameRect(decoded, Rect)
				|| !KingdomArchitectureRules.IsCurrentSnapshotEncoding(architecture.EncodedSnapshot)
				|| !long.TryParse(Marker.GetStringProperty(PlanLabourProperty),
					global::System.Globalization.NumberStyles.None,
					global::System.Globalization.CultureInfo.InvariantCulture, out LabourTicks)
				|| LabourTicks < 1L
				|| !KingdomMaterialDebitCost.TryParseClaim(
					Marker.GetStringProperty(PlanMaterialProperty), out Material))
			{
				if (Failure == null) Failure = "The frozen plan map, price, or labour receipt is malformed.";
				return false;
			}
			WaterDrams = Marker.GetIntProperty(PlanWaterProperty);
			if (WaterDrams < 0)
			{
				Failure = "The frozen plan has a negative water price.";
				return false;
			}
			if (RequireWorld)
			{
				Zone zone = Marker.CurrentZone;
				Cell stake = Marker.CurrentCell;
				if (zone == null || stake == null || RectOutsideZone(Rect, zone)
					|| Rect.Contains(stake.X, stake.Y))
				{
					Failure = "The frozen plan is not standing beside its exact reserved lot.";
					return false;
				}
			}
			return true;
		}

	}
}
