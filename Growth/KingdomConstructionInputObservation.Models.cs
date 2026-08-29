using System;

namespace ThousandAndFirst
{
	/// <summary>Durable fact recorded while one exact source zone was attended.</summary>
	public sealed class KingdomConstructionInputObservationLine
	{
		public readonly KingdomConstructionInputKind Kind;
		public readonly string Classification;
		public readonly string HolderId;
		public readonly string SourceObjectId;
		public readonly KingdomConstructionInputTopology Topology;
		public readonly int X, Y;
		public readonly string Blueprint;
		public readonly int Count;
		public readonly int DedicationOrdinal;
		public readonly bool AlwaysStack;
		public readonly bool ProtectedCargo;

		public KingdomConstructionInputObservationLine(KingdomConstructionInputKind kind,
			string classification, string holderId, string sourceObjectId,
			KingdomConstructionInputTopology topology, int x, int y, string blueprint,
			int count, int dedicationOrdinal, bool alwaysStack, bool protectedCargo)
		{
			Kind = kind; Classification = classification; HolderId = holderId;
			SourceObjectId = sourceObjectId; Topology = topology; X = x; Y = y;
			Blueprint = blueprint; Count = count; DedicationOrdinal = dedicationOrdinal;
			AlwaysStack = alwaysStack; ProtectedCargo = protectedCargo;
		}
	}

	/// <summary>Exact source objects plus route ground measured during one attended survey.</summary>
	public sealed class KingdomConstructionInputZoneObservation
	{
		public readonly string SettlementId;
		public readonly string ZoneId;
		public readonly long ObservedTick;
		public readonly int DailyWaterUpkeep;
		public readonly int Width, Height;
		private readonly byte[] _passable, _paved;
		private readonly KingdomConstructionInputObservationLine[] _lines;

		public KingdomConstructionInputZoneObservation(string settlementId, string zoneId,
			long observedTick, int dailyWaterUpkeep, int width, int height,
			byte[] passable, byte[] paved, KingdomConstructionInputObservationLine[] lines)
		{
			SettlementId = settlementId; ZoneId = zoneId; ObservedTick = observedTick;
			DailyWaterUpkeep = dailyWaterUpkeep; Width = width; Height = height;
			_passable = passable == null ? null : (byte[])passable.Clone();
			_paved = paved == null ? null : (byte[])paved.Clone();
			_lines = lines == null ? null
				: (KingdomConstructionInputObservationLine[])lines.Clone();
		}

		public int LineCount { get { return _lines == null ? 0 : _lines.Length; } }
		public KingdomConstructionInputObservationLine LineAt(int index)
		{
			if (_lines == null || index < 0 || index >= _lines.Length)
				throw new ArgumentOutOfRangeException("index");
			return _lines[index];
		}
		internal byte[] CopyPassable() { return _passable == null ? null : (byte[])_passable.Clone(); }
		internal byte[] CopyPaved() { return _paved == null ? null : (byte[])_paved.Clone(); }
		internal KingdomConstructionInputObservationLine[] CopyLines()
		{
			return _lines == null ? null
				: (KingdomConstructionInputObservationLine[])_lines.Clone();
		}
	}

	public sealed class KingdomConstructionInputObservationBook
	{
		public readonly int Schema;
		public readonly string RealmId;
		public readonly long RealmEpoch;
		private readonly KingdomConstructionInputZoneObservation[] _zones;

		public KingdomConstructionInputObservationBook(int schema, string realmId,
			long realmEpoch, KingdomConstructionInputZoneObservation[] zones)
		{
			Schema = schema; RealmId = realmId; RealmEpoch = realmEpoch;
			_zones = zones == null ? null
				: (KingdomConstructionInputZoneObservation[])zones.Clone();
		}

		public int ZoneCount { get { return _zones == null ? 0 : _zones.Length; } }
		public KingdomConstructionInputZoneObservation ZoneAt(int index)
		{
			if (_zones == null || index < 0 || index >= _zones.Length)
				throw new ArgumentOutOfRangeException("index");
			return _zones[index];
		}
	}
}
