using System;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// What a network carries. The first four are vanilla's own transmission families, named the
	/// same way vanilla names them so a reader can put our row beside the engine's part and see one
	/// vocabulary; the fifth is ours, because vanilla has no liquid carrier that moves a dram.
	/// <para>
	/// The engine's five concrete families are <c>ElectricalPowerTransmission</c>
	/// (<c>D/XRL/World/Parts/ElectricalPowerTransmission.cs:6</c>),
	/// <c>HydraulicPowerTransmission</c> (<c>:6</c>), <c>MechanicalPowerTransmission</c> (<c>:6</c>),
	/// <c>BiomechanicalPowerTransmission</c> (<c>:6</c>) and <c>GenericPowerTransmission</c>
	/// (<c>:6</c>), each overriding <c>GetPowerTransmissionType()</c> with its own type string.
	/// <c>IPowerTransmission.GetCorrespondingPart</c> (<c>:1075-1087</c>) matches on that string, so
	/// <b>vanilla itself will not join two families</b> — the typed-line law below is the same rule
	/// carried one level further, down to the liquid.
	/// </para>
	/// </summary>
	internal enum KingdomNetworkKind : byte
	{
		Electrical = 0,
		Hydraulic = 1,
		Mechanical = 2,
		Biomechanical = 3,

		/// <summary>Ours. A typed liquid line: one liquid, declared, never inferred.</summary>
		Liquid = 4
	}
}
