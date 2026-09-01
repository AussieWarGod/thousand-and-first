using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomGatehouse
	{
		/// <summary>Resolve road/frontier grammar and audit every owned/path cell before debit.</summary>
		public static bool TryPlan(Zone Z, KingdomSystem System, out KingdomGatehousePlan Plan,
			out string Failure)
		{
			Plan = null;
			Failure = null;
			if (Z == null || System == null)
			{
				Failure = "The gatehouse needs claimed ground to measure its road and frontier.";
				return false;
			}
			KingdomRules.Frontier edges = KingdomRules.FrontierEdges(Z.ZoneID,
				System.ClaimedZones);
			if (edges == KingdomRules.Frontier.None)
			{
				Failure = "This ground has no frontier edge for a gatehouse to cross.";
				return false;
			}
			bool hasRite = KingdomPlots.TryRiteGround(Z, out int riteX, out int riteY);
			if (!KingdomPlotRules.TryHeart(KingdomLayout.ReadMarks(Z), hasRite, riteX, riteY,
				out int heartX, out int heartY))
			{
				Failure = "The settlement has no heart from which to measure a road to the frontier.";
				return false;
			}
			if (!KingdomGatehouseRules.TryPlan(Z.Width, Z.Height, edges, heartX, heartY,
				System.Style, out Plan))
			{
				Failure = "The road reaches the frontier too near the zone edge for a gatehouse and its approaches.";
				return false;
			}
			return TryAudit(Z, Plan, null, null, out Failure);
		}

		/// <summary>Reserve the entire frozen footprint while the paid scaffold is standing.</summary>
		public static bool TryStageScaffold(GameObject Scaffold, KingdomGatehousePlan Plan)
		{
			if (!GameObject.Validate(Scaffold)
				|| !KingdomGatehouseRules.TryEncode(Plan, out string receipt)) return false;
			if (!TryStageRootPalette(Scaffold, Plan)) return false;
			Scaffold.SetStringProperty(PlanProperty, receipt);
			Scaffold.SetIntProperty(ReservationProperty, Schema);
			KingdomPlots.StampRect(Scaffold, new KingdomPlotRules.PlotRect(
				Plan.X1, Plan.Y1, Plan.X2, Plan.Y2));
			return Scaffold.GetIntProperty(ReservationProperty) == Schema
				&& !Scaffold.HasStringProperty(ReservationProperty)
				&& !Scaffold.HasIntProperty(PlanProperty)
				&& Scaffold.GetStringProperty(PlanProperty) == receipt
				&& ExactStagedRootPalette(Scaffold, Plan)
				&& ExactPlotRectMarks(Scaffold, Plan);
		}

		public static bool ScaffoldMatches(GameObject Scaffold, KingdomGatehousePlan Plan)
		{
			return GameObject.Validate(Scaffold)
				&& !Scaffold.HasStringProperty(ReservationProperty)
				&& Scaffold.GetIntProperty(ReservationProperty) == Schema
				&& !Scaffold.HasIntProperty(PlanProperty)
				&& KingdomGatehouseRules.TryEncode(Plan, out string receipt)
				&& Scaffold.GetStringProperty(PlanProperty) == receipt
				&& ExactStagedRootPalette(Scaffold, Plan)
				&& ExactPlotRectMarks(Scaffold, Plan);
		}

		private static bool TryStageRootPalette(GameObject Scaffold, KingdomGatehousePlan Plan)
		{
			if (Plan == null || Plan.ReceiptVersion != 2) return true;
			if (!KingdomGatehouseRules.TryRootRender(Plan, out string render,
				out string color, out _, out string detail, out string closedTile, out _)
				|| Scaffold.HasIntProperty(KingdomDesign.StagedColorStringProperty)
				|| Scaffold.HasIntProperty(KingdomDesign.StagedDetailColorProperty)
				|| Scaffold.HasIntProperty(KingdomDesign.StagedRenderStringProperty)
				|| Scaffold.HasIntProperty(KingdomDesign.StagedTileProperty)) return false;
			Scaffold.SetStringProperty(KingdomDesign.StagedColorStringProperty, color);
			Scaffold.SetStringProperty(KingdomDesign.StagedDetailColorProperty, detail);
			Scaffold.SetStringProperty(KingdomDesign.StagedRenderStringProperty, render);
			Scaffold.SetStringProperty(KingdomDesign.StagedTileProperty, closedTile);
			return ExactStagedRootPalette(Scaffold, Plan);
		}

		private static bool ExactStagedRootPalette(GameObject Scaffold,
			KingdomGatehousePlan Plan)
		{
			if (!GameObject.Validate(Scaffold) || Plan == null) return false;
			if (Plan.ReceiptVersion != 2) return true;
			return KingdomGatehouseRules.TryRootRender(Plan, out string render,
					out string color, out _, out string detail, out string closedTile, out _)
				&& !Scaffold.HasIntProperty(KingdomDesign.StagedColorStringProperty)
				&& !Scaffold.HasIntProperty(KingdomDesign.StagedDetailColorProperty)
				&& !Scaffold.HasIntProperty(KingdomDesign.StagedRenderStringProperty)
				&& !Scaffold.HasIntProperty(KingdomDesign.StagedTileProperty)
				&& Scaffold.GetStringProperty(KingdomDesign.StagedColorStringProperty) == color
				&& Scaffold.GetStringProperty(KingdomDesign.StagedDetailColorProperty) == detail
				&& Scaffold.GetStringProperty(KingdomDesign.StagedRenderStringProperty) == render
				&& Scaffold.GetStringProperty(KingdomDesign.StagedTileProperty) == closedTile;
		}

	}
}
