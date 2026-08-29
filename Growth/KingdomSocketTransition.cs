namespace ThousandAndFirst
{
	/// <summary>One explicit, directional same-set plan-change declaration.</summary>
	public sealed class KingdomSocketTransition
	{
		private readonly KingdomMaterialTally materials;

		public string Key { get; private set; }
		public string FromBuildKey { get; private set; }
		public string ToBuildKey { get; private set; }
		public string LotType { get; private set; }
		public ArchitectureLotSize LotSize { get; private set; }
		public int WaterDrams { get; private set; }
		public long WorkTicks { get; private set; }

		/// <summary>Detached price copy. Callers cannot mutate declaration authority.</summary>
		public KingdomMaterialTally Materials
		{
			get { return materials == null ? null : materials.Copy(); }
		}

		internal KingdomSocketTransition(string Key, string FromBuildKey, string ToBuildKey,
			string LotType, ArchitectureLotSize LotSize, int WaterDrams,
			KingdomMaterialTally Materials, long WorkTicks)
		{
			this.Key = Key;
			this.FromBuildKey = FromBuildKey;
			this.ToBuildKey = ToBuildKey;
			this.LotType = LotType;
			this.LotSize = LotSize;
			this.WaterDrams = WaterDrams;
			this.materials = Materials == null ? null : Materials.Copy();
			this.WorkTicks = WorkTicks;
		}

		internal bool HasMaterials()
		{
			return materials != null;
		}

		internal int MaterialUnits(KingdomMaterial Material)
		{
			return materials == null ? 0 : materials.Get(Material);
		}
	}

	/// <summary>Observed engine-property shape for one durable transition receipt.</summary>
	public struct KingdomSocketTransitionReceiptShape
	{
		public bool SchemaHasInt;
		public bool SchemaHasString;
		public int Schema;
		public bool KeyHasInt;
		public bool KeyHasString;
		public string Key;
		public bool DeclarationHasInt;
		public bool DeclarationHasString;
		public string DeclarationDigest;
		public bool BeforeHasInt;
		public bool BeforeHasString;
		public string BeforeHash;
		public bool AfterHasInt;
		public bool AfterHasString;
		public string AfterHash;
		public bool JobHasInt;
		public bool JobHasString;
		public string JobId;
	}
}
