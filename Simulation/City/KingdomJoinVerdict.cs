using System;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// What one attempt to join two segments came to. The LIQUID LAW's whole surface
	/// (BUILDING-CATALOGUE-BRIEF, 2026-08-22): <i>connection is DECLARED, never inferred</i>.
	/// </summary>
	internal enum KingdomJoinVerdict : byte
	{
		/// <summary>The two declared toward each other and agree on what they carry.</summary>
		Joined = 0,

		/// <summary>They share ground and neither declared a join, so they pass without meeting.
		/// The crossover piece's whole behaviour, and the default for any two segments that only
		/// happen to be adjacent.</summary>
		Crossed = 1,

		/// <summary>Two different families. An axle will not carry amps, and vanilla will not join
		/// them either (<c>IPowerTransmission.GetCorrespondingPart</c>,
		/// <c>D/XRL/World/Parts/IPowerTransmission.cs:1075-1087</c>).</summary>
		RefusedKind = 2,

		/// <summary>Two liquid lines carrying different liquids. <b>Refused by name, never
		/// merged</b> — mixtures are a future mixing work, and the no-silent-merge rule never
		/// bends.</summary>
		RefusedLiquid = 3,

		/// <summary>A liquid line that never said what it carries. An untyped line joins nothing:
		/// the law is <i>declared, never inferred</i>, and a blank declaration is not a
		/// declaration.</summary>
		RefusedUntyped = 4
	}
}
