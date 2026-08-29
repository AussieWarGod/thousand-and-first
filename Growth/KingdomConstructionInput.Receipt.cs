using System;

namespace ThousandAndFirst
{
	/// <summary>Immutable copy-on-write parent receipt owning routed construction economics.</summary>
	public sealed class KingdomConstructionInputReceipt
	{
		public readonly int Schema;
		public readonly string ReceiptId;
		public readonly string ConstructionJobId;
		public readonly string OwnerKey;
		public readonly long OwnerEpoch;
		public readonly string TargetZoneId;
		public readonly int TargetX;
		public readonly int TargetY;
		public readonly string ConstructionIntentDigest;
		/// <summary>Compatibility view of the first required object, or null when absent.</summary>
		public readonly string RequiredObjectId;
		public readonly int WaterRequested;
		public readonly string MaterialRequestedClaim;
		public readonly int WaterReserveFloor;
		public readonly int MaterialReservePolicyVersion;
		public readonly int PriorWaterSpent;
		public readonly int PriorWaterLost;
		public readonly string PriorMaterialSpentClaim;
		public readonly string PriorMaterialLostClaim;
		public readonly KingdomConstructionInputTxPhase TxPhase;
		public readonly int Revision;
		public readonly string PlanDigest;
		public readonly long PauseStartedTick;
		public readonly long PausedTicks;

		private readonly KingdomConstructionInputSourceLine[] _sources;
		private readonly KingdomConstructionInputCargoLine[] _cargo;
		private readonly KingdomConstructionInputChild[] _children;
		private readonly string[] _requiredObjectIds;

		internal KingdomConstructionInputReceipt(int Schema, string ReceiptId,
			string ConstructionJobId, string OwnerKey, long OwnerEpoch, string TargetZoneId,
			int TargetX, int TargetY, string ConstructionIntentDigest,
			string[] RequiredObjectIds,
			int WaterRequested,
			string MaterialRequestedClaim, int WaterReserveFloor,
			int MaterialReservePolicyVersion, int PriorWaterSpent, int PriorWaterLost,
			string PriorMaterialSpentClaim, string PriorMaterialLostClaim,
			KingdomConstructionInputTxPhase TxPhase, int Revision, string PlanDigest,
			long PauseStartedTick, long PausedTicks,
			KingdomConstructionInputSourceLine[] Sources,
			KingdomConstructionInputCargoLine[] Cargo,
			KingdomConstructionInputChild[] Children)
		{
			this.Schema = Schema;
			this.ReceiptId = ReceiptId;
			this.ConstructionJobId = ConstructionJobId;
			this.OwnerKey = OwnerKey;
			this.OwnerEpoch = OwnerEpoch;
			this.TargetZoneId = TargetZoneId;
			this.TargetX = TargetX;
			this.TargetY = TargetY;
			this.ConstructionIntentDigest = ConstructionIntentDigest;
			_requiredObjectIds = RequiredObjectIds == null
				? new string[0] : (string[])RequiredObjectIds.Clone();
			this.RequiredObjectId = _requiredObjectIds.Length == 0
				? null : _requiredObjectIds[0];
			this.WaterRequested = WaterRequested;
			this.MaterialRequestedClaim = MaterialRequestedClaim;
			this.WaterReserveFloor = WaterReserveFloor;
			this.MaterialReservePolicyVersion = MaterialReservePolicyVersion;
			this.PriorWaterSpent = PriorWaterSpent;
			this.PriorWaterLost = PriorWaterLost;
			this.PriorMaterialSpentClaim = PriorMaterialSpentClaim;
			this.PriorMaterialLostClaim = PriorMaterialLostClaim;
			this.TxPhase = TxPhase;
			this.Revision = Revision;
			this.PlanDigest = PlanDigest;
			this.PauseStartedTick = PauseStartedTick;
			this.PausedTicks = PausedTicks;
			_sources = Sources == null ? null : (KingdomConstructionInputSourceLine[])Sources.Clone();
			_cargo = Cargo == null ? null : (KingdomConstructionInputCargoLine[])Cargo.Clone();
			_children = Children == null ? null : (KingdomConstructionInputChild[])Children.Clone();
		}

		/// <summary>Legacy constructor retained for schema-one tests and integrations.</summary>
		internal KingdomConstructionInputReceipt(int Schema, string ReceiptId,
			string ConstructionJobId, string OwnerKey, long OwnerEpoch, string TargetZoneId,
			int TargetX, int TargetY, string ConstructionIntentDigest, string RequiredObjectId,
			int WaterRequested, string MaterialRequestedClaim, int WaterReserveFloor,
			int MaterialReservePolicyVersion, int PriorWaterSpent, int PriorWaterLost,
			string PriorMaterialSpentClaim, string PriorMaterialLostClaim,
			KingdomConstructionInputTxPhase TxPhase, int Revision, string PlanDigest,
			long PauseStartedTick, long PausedTicks,
			KingdomConstructionInputSourceLine[] Sources,
			KingdomConstructionInputCargoLine[] Cargo,
			KingdomConstructionInputChild[] Children)
			: this(Schema, ReceiptId, ConstructionJobId, OwnerKey, OwnerEpoch,
				TargetZoneId, TargetX, TargetY, ConstructionIntentDigest,
				string.IsNullOrEmpty(RequiredObjectId) ? new string[0]
					: new[] { RequiredObjectId }, WaterRequested, MaterialRequestedClaim,
				WaterReserveFloor, MaterialReservePolicyVersion, PriorWaterSpent,
				PriorWaterLost, PriorMaterialSpentClaim, PriorMaterialLostClaim,
				TxPhase, Revision, PlanDigest, PauseStartedTick, PausedTicks,
				Sources, Cargo, Children)
		{
		}

		public int SourceCount { get { return _sources == null ? 0 : _sources.Length; } }
		public int CargoCount { get { return _cargo == null ? 0 : _cargo.Length; } }
		public int ChildCount { get { return _children == null ? 0 : _children.Length; } }
		public int RequiredObjectCount { get { return _requiredObjectIds.Length; } }
		public bool Paused { get { return PauseStartedTick >= 0L; } }

		public string RequiredObjectAt(int index)
		{
			if (index < 0 || index >= _requiredObjectIds.Length)
				throw new ArgumentOutOfRangeException("index");
			return _requiredObjectIds[index];
		}

		public bool RequiresObject(string objectId)
		{
			if (string.IsNullOrEmpty(objectId)) return false;
			for (int i = 0; i < _requiredObjectIds.Length; i++)
				if (_requiredObjectIds[i] == objectId) return true;
			return false;
		}

		public KingdomConstructionInputSourceLine SourceAt(int index)
		{
			if (_sources == null || index < 0 || index >= _sources.Length)
				throw new ArgumentOutOfRangeException("index");
			return _sources[index];
		}

		public KingdomConstructionInputCargoLine CargoAt(int index)
		{
			if (_cargo == null || index < 0 || index >= _cargo.Length)
				throw new ArgumentOutOfRangeException("index");
			return _cargo[index];
		}

		public KingdomConstructionInputChild ChildAt(int index)
		{
			if (_children == null || index < 0 || index >= _children.Length)
				throw new ArgumentOutOfRangeException("index");
			return _children[index];
		}

		internal KingdomConstructionInputSourceLine[] CopySources()
		{
			return _sources == null ? null : (KingdomConstructionInputSourceLine[])_sources.Clone();
		}

		internal KingdomConstructionInputCargoLine[] CopyCargo()
		{
			return _cargo == null ? null : (KingdomConstructionInputCargoLine[])_cargo.Clone();
		}

		internal KingdomConstructionInputChild[] CopyChildren()
		{
			return _children == null ? null : (KingdomConstructionInputChild[])_children.Clone();
		}

		internal string[] CopyRequiredObjectIds()
		{
			return (string[])_requiredObjectIds.Clone();
		}

		internal KingdomConstructionInputReceipt Copy(KingdomConstructionInputTxPhase phase,
			int revision, long pauseStartedTick, long pausedTicks,
			KingdomConstructionInputSourceLine[] sources,
			KingdomConstructionInputCargoLine[] cargo,
			KingdomConstructionInputChild[] children)
		{
			return new KingdomConstructionInputReceipt(Schema, ReceiptId, ConstructionJobId,
				OwnerKey, OwnerEpoch, TargetZoneId, TargetX, TargetY, ConstructionIntentDigest,
				CopyRequiredObjectIds(), WaterRequested, MaterialRequestedClaim, WaterReserveFloor,
				MaterialReservePolicyVersion, PriorWaterSpent, PriorWaterLost,
				PriorMaterialSpentClaim, PriorMaterialLostClaim, phase, revision, PlanDigest,
				pauseStartedTick, pausedTicks, sources ?? CopySources(), cargo ?? CopyCargo(),
				children ?? CopyChildren());
		}
	}
}
