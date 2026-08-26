using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>
	/// Persistable measured claims. Material lanes use
	/// <see cref="KingdomMaterialDebitCost.ToClaimString"/>; live engine receipts never cross a
	/// save. <see cref="Exact"/> is false only when an engine receipt could not prove its physical
	/// aftermath, so uncertainty is explicit rather than silently rounded to success or refusal.
	/// </summary>
	public sealed class KingdomConstructionClaims
	{
		public int WaterRequested;
		public int WaterSpent;
		public int WaterOutstanding;
		public int WaterLost;
		public bool Exact;
		public string MaterialRequested;
		public string MaterialSpent;
		public string MaterialOutstanding;
		public string MaterialLost;

		public KingdomConstructionClaims Copy()
		{
			return new KingdomConstructionClaims
			{
				WaterRequested = WaterRequested,
				WaterSpent = WaterSpent,
				WaterOutstanding = WaterOutstanding,
				WaterLost = WaterLost,
				Exact = Exact,
				MaterialRequested = MaterialRequested,
				MaterialSpent = MaterialSpent,
				MaterialOutstanding = MaterialOutstanding,
				MaterialLost = MaterialLost
			};
		}
	}
}
