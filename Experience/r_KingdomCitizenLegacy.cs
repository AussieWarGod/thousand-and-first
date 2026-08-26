using System;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;
using ThousandAndFirst;

// The engine resolves an XML <part Name="X"/> as the single type "XRL.World.Parts.X" (see
// Growth/KingdomScaffold.cs for the full citation). r_KingdomCitizenLegacy is never named from
// XML - it is attached in code via RequirePart<T> - but it lives here regardless, so it is never
// the exception a future XML reference trips over.
namespace XRL.World.Parts
{
	/// <summary>
	/// Attached to every grown settler by <see cref="ThousandAndFirst.KingdomOffices"/> so the
	/// settlement learns of a citizen's death the moment the engine reports it, rather than
	/// inferring it from absence &mdash; a census cannot tell a dead settler from one who simply
	/// wandered to another claimed zone of the same territory. Carries no state of its own.
	/// </summary>
	[Serializable]
	public class r_KingdomCitizenLegacy : IPart
	{
		public override bool WantEvent(int ID, int cascade)
		{
			if (base.WantEvent(ID, cascade))
			{
				return true;
			}
			return ID == BeforeDeathRemovalEvent.ID;
		}

		public override bool HandleEvent(BeforeDeathRemovalEvent E)
		{
			KingdomOffices.RecordDeath(ParentObject, E.Killer);
			return base.HandleEvent(E);
		}
	}
}
