using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>
	/// Frozen commitment shown before a purposeful building is debited. It binds one exact delivered
	/// legacy body cargo and/or one reciprocal pair cargo to one distinct site reading.
	/// </summary>
	public sealed class KingdomPurposeCommitment
	{
		public const int Schema = 3;
		public string Manifest;
		public string ConsignmentId;
		public string CargoItemId;
		public string SiteProof;
		public string SpecialistId;
		public string SpecialistName;
		public string PortfolioPairId;
		public long PortfolioEpoch;
		public string PortfolioOperationId;
		public string ReciprocalCargoItemId;
		public string ReciprocalCargoReceipt;
		/// <summary>Exact portfolio-only shell commissioned before any pair exists.</summary>
		public string InitialBuildKey;
	}
}
