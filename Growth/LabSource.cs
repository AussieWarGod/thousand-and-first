using System.Collections.Generic;

using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	/// <summary>What the founder must bring, and by which of vanilla's three write paths it lands
	/// (DIVERSITY-AND-TECH-TREES &sect;3.4's source table).</summary>
	public enum LabSource : byte
	{
		/// <summary>A preserved part from a creature that carried the named <c>IPart</c>. Granted
		/// with <c>IPart.DeepCopy</c> (<c>D/XRL/World/IPart.cs:401-435</c>), so the source
		/// instance's own field values are the numbers the founder gets.</summary>
		Part = 0,

		/// <summary>A preserved severed limb, carrying <c>DismemberedProperties</c>
		/// (<c>D/XRL/World/Parts/Body.cs:2557</c>). Granted with <c>BodyPart.AddPartAt</c>.</summary>
		Limb = 1,

		/// <summary>A preserved gland or organ from a mutation-bearing creature. Granted with
		/// <c>Mutations.AddMutation</c>, and never at the source's own level
		/// (<see cref="KingdomProcedureRules.GrantedMutationLevel"/>).</summary>
		Mutation = 2
	}
}
