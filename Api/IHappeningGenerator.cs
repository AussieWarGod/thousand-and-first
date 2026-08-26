using System;

namespace ThousandAndFirst.Api
{
	/// <summary>Canonical LIVING-CITY &sect;6.6 happening name. It inherits the v1 source contract,
	/// so old and new generators use the same live chronicle/telling consumer.</summary>
	public interface IHappeningGenerator : IKingdomHappeningSource
	{
	}
}
