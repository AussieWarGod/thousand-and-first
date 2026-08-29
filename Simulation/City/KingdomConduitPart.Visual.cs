using ThousandAndFirst;
using ThousandAndFirst.Simulation.City;
using XRL.World;

namespace XRL.World.Parts
{
	public partial class r_KingdomLiquidConduit
	{
		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade) || ID == GetInventoryActionsEvent.ID
				|| ID == InventoryActionEvent.ID || ID == GetShortDescriptionEvent.ID;
		}

		public override bool FinalRender(RenderEvent E)
		{
			KingdomLiquidVisualCue cue;
			KingdomLiquidVisualRules.TryCue(Joins, KingdomLiquidVisualRules.IsBrine(Liquid), out cue);
			E.Tile = null;
			E.RenderString = ((char)cue.Glyph).ToString();
			return base.FinalRender(E);
		}

		public override bool HandleEvent(GetShortDescriptionEvent E)
		{
			E.Postfix.Append(KingdomLiquidConfigurationRules.Status(Liquid, Joins, false));
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(GetInventoryActionsEvent E)
		{
			if (E.Actor != null && E.Actor.IsPlayer())
				E.AddAction("Configure", "choose this main's frozen joined faces",
					"r_ConfigureLiquidMain", null, 'c', FireOnActor: false, 20);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(InventoryActionEvent E)
		{
			if (E.Command == "r_ConfigureLiquidMain" && E.Actor != null && E.Actor.IsPlayer())
			{
				KingdomLiquidConfiguration.Open(this, E.Actor);
				E.RequestInterfaceExit();
				return true;
			}
			return base.HandleEvent(E);
		}
	}

	public partial class r_KingdomLiquidCrossover
	{
		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade) || ID == GetInventoryActionsEvent.ID
				|| ID == InventoryActionEvent.ID || ID == GetShortDescriptionEvent.ID;
		}

		public override bool FinalRender(RenderEvent E)
		{
			int glyph;
			bool freshVertical;
			KingdomLiquidVisualRules.TryCrossingCue(Pairs, out glyph, out freshVertical);
			E.Tile = null;
			E.RenderString = ((char)glyph).ToString();
			return base.FinalRender(E);
		}

		public override bool HandleEvent(GetShortDescriptionEvent E)
		{
			E.Postfix.Append(KingdomLiquidConfigurationRules.CrossingStatus(Pairs));
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(GetInventoryActionsEvent E)
		{
			if (E.Actor != null && E.Actor.IsPlayer())
				E.AddAction("Configure", "choose which isolated pair carries fresh water",
					"r_ConfigureLiquidCrossing", null, 'c', FireOnActor: false, 20);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(InventoryActionEvent E)
		{
			if (E.Command == "r_ConfigureLiquidCrossing" && E.Actor != null
				&& E.Actor.IsPlayer())
			{
				KingdomLiquidConfiguration.Open(this, E.Actor);
				E.RequestInterfaceExit();
				return true;
			}
			return base.HandleEvent(E);
		}
	}

	public partial class r_KingdomLiquidTap
	{
		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade) || ID == GetInventoryActionsEvent.ID
				|| ID == InventoryActionEvent.ID || ID == GetShortDescriptionEvent.ID;
		}

		public override bool FinalRender(RenderEvent E)
		{
			KingdomLiquidVisualCue cue;
			KingdomLiquidVisualRules.TryCue(Joins, KingdomLiquidVisualRules.IsBrine(Liquid), out cue);
			E.Tile = null;
			E.RenderString = ((char)cue.Glyph).ToString();
			return base.FinalRender(E);
		}

		public override bool HandleEvent(GetShortDescriptionEvent E)
		{
			E.Postfix.Append(KingdomLiquidConfigurationRules.Status(Liquid, Joins, true));
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(GetInventoryActionsEvent E)
		{
			if (E.Actor != null && E.Actor.IsPlayer())
				E.AddAction("Configure", "choose this tap's frozen joined faces",
					"r_ConfigureLiquidTap", null, 'c', FireOnActor: false, 20);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(InventoryActionEvent E)
		{
			if (E.Command == "r_ConfigureLiquidTap" && E.Actor != null && E.Actor.IsPlayer())
			{
				KingdomLiquidConfiguration.Open(this, E.Actor);
				E.RequestInterfaceExit();
				return true;
			}
			return base.HandleEvent(E);
		}
	}
}
