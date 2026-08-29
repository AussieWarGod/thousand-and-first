using System;

namespace ThousandAndFirst
{
	public enum KingdomConstructionInputPlanFault : byte
	{
		None = 0,
		Null = 1,
		Bounds = 2,
		Identity = 3,
		Duplicate = 4,
		Claim = 5,
		InsufficientMaterial = 6,
		InsufficientWater = 7,
		RequiredObject = 8,
		UnsafeStack = 9,
		Child = 10,
		Receipt = 11
	}

	/// <summary>Engine-free observation of one exact dedicated source object.</summary>
	public sealed class KingdomConstructionInputCandidate
	{
		public readonly KingdomConstructionInputKind Kind;
		public readonly string Classification;
		public readonly string SourceSettlementId;
		public readonly string SourceZoneId;
		public readonly string HolderId;
		public readonly string SourceObjectId;
		public readonly KingdomConstructionInputTopology Topology;
		public readonly int X;
		public readonly int Y;
		public readonly string Blueprint;
		public readonly int Count;
		public readonly int HolderStockBefore;
		public readonly int PriorReserved;
		public readonly int ReserveFloor;
		public readonly int RouteCost;
		public readonly int DedicationOrdinal;
		public readonly bool AlwaysStack;

		public KingdomConstructionInputCandidate(KingdomConstructionInputKind Kind,
			string Classification, string SourceSettlementId, string SourceZoneId,
			string HolderId, string SourceObjectId, KingdomConstructionInputTopology Topology,
			int X, int Y, string Blueprint, int Count, int HolderStockBefore,
			int PriorReserved, int ReserveFloor, int RouteCost, int DedicationOrdinal,
			bool AlwaysStack)
		{
			this.Kind = Kind;
			this.Classification = Classification;
			this.SourceSettlementId = SourceSettlementId;
			this.SourceZoneId = SourceZoneId;
			this.HolderId = HolderId;
			this.SourceObjectId = SourceObjectId;
			this.Topology = Topology;
			this.X = X;
			this.Y = Y;
			this.Blueprint = Blueprint;
			this.Count = Count;
			this.HolderStockBefore = HolderStockBefore;
			this.PriorReserved = PriorReserved;
			this.ReserveFloor = ReserveFloor;
			this.RouteCost = RouteCost;
			this.DedicationOrdinal = DedicationOrdinal;
			this.AlwaysStack = AlwaysStack;
		}
	}

	/// <summary>One selected source mutation and its one freight object.</summary>
	public sealed class KingdomConstructionInputPlannedLine
	{
		public readonly KingdomConstructionInputCandidate Candidate;
		public readonly int Ordinal;
		public readonly int Before;
		public readonly int Take;
		public readonly string LineId;
		public readonly string CargoKey;
		public readonly string CreationMarker;
		public readonly string RemainderMarker;

		internal KingdomConstructionInputPlannedLine(KingdomConstructionInputCandidate candidate,
			int ordinal, int before, int take, string token)
		{
			Candidate = candidate;
			Ordinal = ordinal;
			Before = before;
			Take = take;
			LineId = "ci-source-" + token;
			CargoKey = "ci-cargo-" + token;
			CreationMarker = "ci-create-" + token;
			RemainderMarker = candidate.Kind != KingdomConstructionInputKind.Water
				&& take < before ? "ci-remain-" + token : null;
		}
	}

	/// <summary>One consecutive same-endpoint carrier batch.</summary>
	public sealed class KingdomConstructionInputPlannedChild
	{
		public readonly int Ordinal;
		public readonly int CargoStart;
		public readonly int CargoCount;
		public readonly string SourceObjectId;
		public readonly string SourceZoneId;
		public readonly int SourceX;
		public readonly int SourceY;

		internal KingdomConstructionInputPlannedChild(int ordinal, int start, int count,
			KingdomConstructionInputCandidate source)
		{
			Ordinal = ordinal;
			CargoStart = start;
			CargoCount = count;
			SourceObjectId = source.HolderId;
			SourceZoneId = source.SourceZoneId;
			SourceX = source.X;
			SourceY = source.Y;
		}
	}

	public sealed class KingdomConstructionInputPlan
	{
		public readonly string OperationId;
		public readonly int WaterRequested;
		public readonly string MaterialRequestedClaim;
		/// <summary>Compatibility view of the first required object, or null when absent.</summary>
		public readonly string RequiredObjectId;
		public readonly int DailyWaterUpkeep;
		private readonly KingdomConstructionInputPlannedLine[] _lines;
		private readonly KingdomConstructionInputPlannedChild[] _children;
		private readonly string[] _requiredObjectIds;

		internal KingdomConstructionInputPlan(string operationId, int water,
			string material, string[] required, int dailyWater,
			KingdomConstructionInputPlannedLine[] lines,
			KingdomConstructionInputPlannedChild[] children)
		{
			OperationId = operationId;
			WaterRequested = water;
			MaterialRequestedClaim = material;
			_requiredObjectIds = required == null ? new string[0] : (string[])required.Clone();
			RequiredObjectId = _requiredObjectIds.Length == 0 ? null : _requiredObjectIds[0];
			DailyWaterUpkeep = dailyWater;
			_lines = (KingdomConstructionInputPlannedLine[])lines.Clone();
			_children = (KingdomConstructionInputPlannedChild[])children.Clone();
		}

		public int LineCount { get { return _lines.Length; } }
		public int ChildCount { get { return _children.Length; } }
		public int RequiredObjectCount { get { return _requiredObjectIds.Length; } }
		public string RequiredObjectAt(int index)
		{
			if (index < 0 || index >= _requiredObjectIds.Length)
				throw new ArgumentOutOfRangeException("index");
			return _requiredObjectIds[index];
		}
		internal string[] CopyRequiredObjectIds()
		{ return (string[])_requiredObjectIds.Clone(); }
		public KingdomConstructionInputPlannedLine LineAt(int index)
		{
			if (index < 0 || index >= _lines.Length) throw new ArgumentOutOfRangeException("index");
			return _lines[index];
		}
		public KingdomConstructionInputPlannedChild ChildAt(int index)
		{
			if (index < 0 || index >= _children.Length) throw new ArgumentOutOfRangeException("index");
			return _children[index];
		}
	}
}
