using System.Collections.Generic;

using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	/// <summary>
	/// The class ladder, mapped onto the risk split the precedent whitelist already keeps
	/// (DIVERSITY &sect;3.4). The numbers are the rung vocabulary the founder reads, so they are
	/// stable and are never renumbered (STANDARDS &sect;9).
	/// </summary>
	public enum LabClass : byte
	{
		/// <summary>Attack riders. The hall's ordinary work.</summary>
		Rider = 1,

		/// <summary>Defences and utility. The hall's ordinary work with teeth.</summary>
		Defence = 2,

		/// <summary>A new limb at a named slot, with whatever it brings. The theatre's work.</summary>
		Limb = 3,

		/// <summary>One of the four, once ever, found in the world and never listed before it is
		/// (DIVERSITY &sect;3.7; Addendum 14 at full strength, Addendum 20's hidden clause).</summary>
		Named = 4
	}
}
