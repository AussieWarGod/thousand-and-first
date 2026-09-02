using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>
	/// The in-repository designation path. A source this mod itself ships may report the full
	/// internal row, including covered and interior cell use, because its geometry is proved by the
	/// same build; everything discovered only through <c>ThousandAndFirst.Api</c> reports Api rows
	/// and is translated and restricted at the seam instead.
	/// </summary>
	internal interface IKingdomTrustedDesignationSource
	{
		bool TryObserveTrusted(Zone ActiveZone, out KingdomBenefitDesignation[] Designations,
			out string Failure);
	}
}
