using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public sealed partial class r_KingdomLegendaryMarketProjection
	{
		/// <summary>An empty civic legendary counter opens only while the physical provider and
		/// exact held office still prove authority. Personal trade after civic loss stays native.</summary>
		public override bool HandleEvent(AllowTradeWithNoInventoryEvent E)
		{
			KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
			if (ReferenceEquals(E.Trader, ParentObject) && Active(system, ParentObject)) return false;
			return base.HandleEvent(E);
		}
	}
}
