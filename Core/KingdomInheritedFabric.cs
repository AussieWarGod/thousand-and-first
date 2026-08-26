using System;

using XRL.World;

namespace XRL.World.Parts
{
	/// <summary>Stateless visual reader for exact, empty fabric reconstructed from a legacy seal.</summary>
	[Serializable]
	public sealed class r_KingdomInheritedFabric : IPart
	{
		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade) || ID == GetDisplayNameEvent.ID
				|| ID == GetShortDescriptionEvent.ID;
		}

		public override bool FinalRender(RenderEvent E)
		{
			int wear = Wear();
			if (wear > 0)
			{
				ThousandAndFirst.KingdomVisualCue cue =
					ThousandAndFirst.KingdomVisualStateRules.Cue(
						ThousandAndFirst.KingdomInheritanceFabricRules.VisualStateFor(wear));
				E.RenderEffectIndicator(cue.Glyph, cue.Tile, cue.ColorString, cue.DetailColor,
					24, 36);
			}
			return base.FinalRender(E);
		}

		public override bool HandleEvent(GetDisplayNameEvent E)
		{
			string adjective = ThousandAndFirst.KingdomMaterialRules.ConditionAdjective(Wear());
			if (!string.IsNullOrEmpty(adjective)) E.AddAdjective(adjective);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(GetShortDescriptionEvent E)
		{
			string look = ThousandAndFirst.KingdomMaterialRules.ConditionLook(Wear());
			if (!string.IsNullOrEmpty(look)) E.Postfix.Append("\n").Append(look);
			return base.HandleEvent(E);
		}

		private int Wear()
		{
			return ThousandAndFirst.KingdomInheritanceFabricRules.WearFor(
				(ThousandAndFirst.KingdomInheritWorkState)ParentObject.GetIntProperty(
					ThousandAndFirst.KingdomInheritEngine.ObjectStateProperty, -1),
				ParentObject.GetIntProperty(
					ThousandAndFirst.KingdomInheritEngine.ObjectConditionProperty, -1));
		}
	}
}
