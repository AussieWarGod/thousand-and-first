using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>
	/// One merged catalogue declaration. It describes the real consignment another city must
	/// produce and the distinct physical ground on which this purpose can be committed.
	/// </summary>
	public sealed class KingdomPurposeDefinition
	{
		public string BuildKey;
		public KingdomPurposeKind Kind;
		public KingdomPurposeSite Site;
		public string CargoKey;
		public string CargoName;
		public KingdomMaterial CargoMaterial;
		public int CargoWater;
		public KingdomMaterialTally CargoCost;
		public string ProducerSpec;
		public string Effect;

		public KingdomPurposeDefinition Copy()
		{
			return new KingdomPurposeDefinition
			{
				BuildKey = BuildKey,
				Kind = Kind,
				Site = Site,
				CargoKey = CargoKey,
				CargoName = CargoName,
				CargoMaterial = CargoMaterial,
				CargoWater = CargoWater,
				CargoCost = CargoCost?.Copy() ?? new KingdomMaterialTally(),
				ProducerSpec = ProducerSpec,
				Effect = Effect
			};
		}
	}
}
