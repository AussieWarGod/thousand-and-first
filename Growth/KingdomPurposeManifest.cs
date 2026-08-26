using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>
	/// Immutable manifest published before production is funded. The output ID is deliberately
	/// absent: construction publishes that identity before the first physical AddObject callback.
	/// </summary>
	public sealed class KingdomPurposeManifest
	{
		public const int Schema = 1;
		public string BuildKey;
		public KingdomPurposeKind Kind;
		public KingdomPurposeSite Site;
		public string CargoKey;
		public string CargoName;
		public KingdomMaterial CargoMaterial;
		public int CargoWater;
		public string CargoCostClaim;
		public string OriginSettlementId;
		public string OriginCity;
		public string OriginZoneId;
		public string SourceGateKey;
		public string DestinationSettlementId;
		public string DestinationCity;
		public string DestinationZoneId;
		public string DestinationGateKey;
		public string ProducerProof;
		public string Effect;
	}
}
