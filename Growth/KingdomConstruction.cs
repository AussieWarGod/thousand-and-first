using System;
using System.Collections.Generic;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	/// <summary>
	/// Engine carrier for every costed construction route. Registry lives in already-serialized
	/// <c>XRLGame.StringGameState</c>; live water/material receipts never do. Each external debit
	/// and projection is bracketed by a persisted phase, and each involved object carries only a
	/// stable receipt property, also already serialized by <c>GameObject</c>.
	/// </summary>
	public static partial class KingdomConstruction
	{
		public const string RegistryStateKey = "r_TAF_ConstructionJobs";
		public const string ReceiptProperty = "KingdomConstructionReceipt";
		public const string PaidBuildSchemaProperty = "r_TAF_PaidBuildSchema";
		public const string PaidBuildWaterProperty = "r_TAF_PaidBuildWater";
		public const string PaidBuildMaterialProperty = "r_TAF_PaidBuildMaterial";
		public const string PaidBuildWorkProperty = "r_TAF_PaidBuildWork";
		public const int PaidBuildSchema = 1;
		private const int MaxLoadedLookupObjects = 4096;

		private static bool Resolving;

	}
}
