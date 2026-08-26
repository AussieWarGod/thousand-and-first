using System;
using System.Collections.Generic;
using XRL.World;
using ThousandAndFirst;

namespace XRL.World.Parts
{
	/// <summary>Stateless projection hook; all reload-safe state lives in named properties.</summary>
	[Serializable]
	public sealed class r_KingdomGatehouse : IPart
	{
		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade) || ID == EnteredCellEvent.ID;
		}

		public override bool HandleEvent(EnteredCellEvent E)
		{
			KingdomGatehouse.MaterializeFromEnteredCell(ParentObject, E.Cell);
			return base.HandleEvent(E);
		}
	}
}
