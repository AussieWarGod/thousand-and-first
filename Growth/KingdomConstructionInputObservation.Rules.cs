using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static class KingdomConstructionInputObservationRules
	{
		public const int Schema = 1;
		public const int MaxZones = 64;
		public const int MaxLines = 4096;
		public const int MaxCells = 4096;
		public const int MaxPayloadBytes = 1048576;

		public static bool Valid(KingdomConstructionInputObservationBook book)
		{
			if (book == null || book.Schema != Schema || !Id(book.RealmId)
				|| book.RealmEpoch < 0L || book.ZoneCount > MaxZones) return false;
			HashSet<string> zones = new HashSet<string>(StringComparer.Ordinal);
			int lines = 0;
			for (int i = 0; i < book.ZoneCount; i++)
			{
				KingdomConstructionInputZoneObservation zone = book.ZoneAt(i);
				if (!Valid(zone) || !zones.Add(zone.ZoneId)
					|| lines > MaxLines - zone.LineCount) return false;
				lines += zone.LineCount;
			}
			return true;
		}

		public static bool Valid(KingdomConstructionInputZoneObservation zone)
		{
			if (zone == null || !Id(zone.SettlementId) || !Id(zone.ZoneId)
				|| zone.ObservedTick < 0L || zone.DailyWaterUpkeep < 0
				|| zone.Width <= 0 || zone.Height <= 0
				|| (long)zone.Width * zone.Height > MaxCells
				|| zone.LineCount > MaxLines) return false;
			byte[] passable = zone.CopyPassable();
			byte[] paved = zone.CopyPaved();
			int cells = zone.Width * zone.Height;
			if (passable == null || paved == null || passable.Length != cells
				|| paved.Length != cells) return false;
			for (int i = 0; i < cells; i++)
				if (passable[i] > 1 || paved[i] > 1 || paved[i] != 0 && passable[i] == 0)
					return false;
			HashSet<string> identities = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < zone.LineCount; i++)
			{
				KingdomConstructionInputObservationLine line = zone.LineAt(i);
				if (!Valid(line, zone.Width, zone.Height)
					|| !identities.Add(line.HolderId + "\0" + line.SourceObjectId)) return false;
			}
			return true;
		}

		private static bool Valid(KingdomConstructionInputObservationLine line,
			int width, int height)
		{
			if (line == null
				|| (int)line.Kind < (int)KingdomConstructionInputKind.Water
				|| (int)line.Kind > (int)KingdomConstructionInputKind.Exotic
				|| !Text(line.Classification, 512) || !Id(line.HolderId)
				|| !Id(line.SourceObjectId) || !Text(line.Blueprint, 160)
				|| line.X < 0 || line.X >= width || line.Y < 0 || line.Y >= height
				|| line.Count <= 0 || line.DedicationOrdinal < 0) return false;
			return line.Kind == KingdomConstructionInputKind.Water
				? line.Topology == KingdomConstructionInputTopology.LiquidVessel
					&& line.HolderId == line.SourceObjectId
					&& line.Classification == KingdomConstructionInputRules.WaterClassification
				: line.Topology == KingdomConstructionInputTopology.ContainerInventory;
		}

		internal static bool Id(string value) { return Text(value, 128); }
		internal static bool Text(string value, int max)
		{
			return !string.IsNullOrEmpty(value) && value.Length <= max
				&& value.IndexOf('\0') < 0;
		}
	}
}
