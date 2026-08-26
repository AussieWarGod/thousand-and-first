using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	internal enum KingdomInheritanceSpatialCaptureResult
	{
		Captured = 0,
		Unavailable = 1,
		Malformed = 2
	}

	/// <summary>Native, read-only witness adapter for one loaded old seat. It resolves no current
	/// architecture mapping: only roots already carrying a complete frozen receipt may contribute
	/// authored geometry.</summary>
	internal static class KingdomInheritanceSpatial
	{
		private sealed class SourceWork
		{
			internal int WorkId;
			internal string Blueprint;
			internal int X;
			internal int Y;
		}

		internal static KingdomInheritanceSpatialCaptureResult TryCapture(
			Simulation.City.KingdomCityBook Book, KingdomSealRecord Record, Zone Active,
			out string Failure)
		{
			Failure = "";
			if (Book == null || Record == null || Active == null
				|| Active.ZoneID != Record.GroundZoneId)
				return KingdomInheritanceSpatialCaptureResult.Unavailable;
			if (Active.Width != KingdomInheritanceSpatialRules.Width
				|| Active.Height != KingdomInheritanceSpatialRules.Height)
				return Malformed("the witnessed seat is not eighty by twenty-five", out Failure);

			List<SourceWork> source;
			if (!TrySourceRows(Book, Record, out source, out Failure))
				return KingdomInheritanceSpatialCaptureResult.Malformed;
			List<string> snapshots = new List<string>(source.Count);
			List<string> hashes = new List<string>(source.Count);
			List<KingdomInheritanceSpatialRules.Rect> rects =
				new List<KingdomInheritanceSpatialRules.Rect>(source.Count);
			for (int i = 0; i < source.Count; i++)
			{
				SourceWork row = source[i];
				GameObject root;
				if (!TryExactRoot(Active, row, out root, out Failure))
					return KingdomInheritanceSpatialCaptureResult.Malformed;
				if (!HasArchitectureEvidence(root))
				{
					snapshots.Add("");
					hashes.Add("");
					int width;
					int height;
					if (!KingdomInheritRules.TryFootprint(Record.WorkKeys[i], out width, out height))
					{
						width = 1;
						height = 1;
					}
					rects.Add(new KingdomInheritanceSpatialRules.Rect
					{
						X1 = row.X - (width - 1) / 2,
						Y1 = row.Y - (height - 1) / 2,
						X2 = row.X - (width - 1) / 2 + width - 1,
						Y2 = row.Y - (height - 1) / 2 + height - 1
					});
					continue;
				}

				KingdomArchitectureIntent intent;
				ArchitectureLayoutSnapshot snapshot;
				if (!KingdomArchitectureRuntime.TryRead(root, out intent, out Failure)
					|| !KingdomArchitectureRuntime.TryDecode(intent, out snapshot, out Failure)
					|| !KingdomArchitectureStamper.TryVerifyComplete(root, Active, out Failure)
					|| intent.EncodedSnapshot.Length > KingdomInheritanceSpatialRules.MaxSnapshotChars
					|| intent.MainWorldX != row.X || intent.MainWorldY != row.Y
					|| root.CurrentCell != Active.GetCell(row.X, row.Y))
					return Malformed("an authored work has incomplete or changed frozen evidence: "
						+ Failure, out Failure);
				KingdomInheritanceSpatialRules.Rect rect;
				if (!KingdomInheritanceSpatialRules.TrySnapshotRect(snapshot, row.X, row.Y,
					out rect) || rect.X1 != intent.Rect.X1 || rect.Y1 != intent.Rect.Y1
					|| rect.X2 != intent.Rect.X2 || rect.Y2 != intent.Rect.Y2)
					return Malformed("an authored work's root no longer matches its frozen lot",
						out Failure);
				if (!KingdomArchitectureRules.IsCurrentSnapshotEncoding(intent.EncodedSnapshot))
				{
					KingdomInheritanceSpatialRules.Rect proxy;
					if (!KingdomInheritanceSpatialRules.TryLegacyRect(Record.WorkKeys[i], row.X,
						row.Y, out proxy) || proxy.X1 != rect.X1 || proxy.Y1 != rect.Y1
						|| proxy.X2 != rect.X2 || proxy.Y2 != rect.Y2)
						return Malformed("a legacy authored work cannot be represented by its bounded anchor proxy",
							out Failure);
					snapshots.Add("");
					hashes.Add("");
					rects.Add(proxy);
					continue;
				}
				snapshots.Add(intent.EncodedSnapshot);
				hashes.Add(intent.SnapshotHash);
				rects.Add(rect);
			}

			bool[,] roads;
			if (!TryRoadEvidence(Active, rects, out roads, out Failure))
				return KingdomInheritanceSpatialCaptureResult.Malformed;
			int entryX;
			int entryY;
			int entrySide;
			List<int> streetX;
			List<int> streetY;
			SelectBoundaryComponent(roads, out entrySide, out entryX, out entryY,
				out streetX, out streetY);

			KingdomInheritanceSpatialFault fault;
			if (!KingdomInheritanceSpatialRules.TryValidate(Record.WorkKeys, Record.WorkX,
				Record.WorkY, Record.WorkConditions, snapshots, hashes,
				KingdomInheritanceSpatialRules.Width, KingdomInheritanceSpatialRules.Height,
				entrySide, entryX, entryY, streetX, streetY, out fault))
				return Malformed("the witnessed architecture and street graph do not form a safe "
					+ "spatial seal: " + fault, out Failure);

			Record.SpatialVersion = KingdomInheritanceSpatialRules.SpatialVersion;
			Record.SpatialWidth = KingdomInheritanceSpatialRules.Width;
			Record.SpatialHeight = KingdomInheritanceSpatialRules.Height;
			Record.SpatialEntrySide = entrySide;
			Record.SpatialEntryX = entryX;
			Record.SpatialEntryY = entryY;
			Record.WorkSnapshots = snapshots;
			Record.WorkSnapshotHashes = hashes;
			Record.StreetX = streetX;
			Record.StreetY = streetY;
			return KingdomInheritanceSpatialCaptureResult.Captured;
		}

		internal static void CopyEvidence(KingdomSealRecord Source, KingdomSealRecord Target)
		{
			if (Source == null || Target == null) return;
			Target.SpatialVersion = Source.SpatialVersion;
			Target.SpatialWidth = Source.SpatialWidth;
			Target.SpatialHeight = Source.SpatialHeight;
			Target.SpatialEntrySide = Source.SpatialEntrySide;
			Target.SpatialEntryX = Source.SpatialEntryX;
			Target.SpatialEntryY = Source.SpatialEntryY;
			Target.WorkSnapshots = new List<string>(Source.WorkSnapshots);
			Target.WorkSnapshotHashes = new List<string>(Source.WorkSnapshotHashes);
			Target.StreetX = new List<int>(Source.StreetX);
			Target.StreetY = new List<int>(Source.StreetY);
		}

		private static bool TrySourceRows(Simulation.City.KingdomCityBook Book,
			KingdomSealRecord Record, out List<SourceWork> Rows, out string Failure)
		{
			Rows = new List<SourceWork>();
			Failure = "";
			for (int i = 0; i < Book.WorkIds.Count && Rows.Count < KingdomSealRecord.MaxWorks; i++)
			{
				if (i >= Book.WorkZoneIds.Count || Book.WorkZoneIds[i] != Record.GroundZoneId)
					continue;
				if (i >= Book.WorkDesignKeys.Count || i >= Book.WorkAnchorsX.Count
					|| i >= Book.WorkAnchorsY.Count || i >= Book.WorkConditions.Count) continue;
				string key;
				string design = Book.WorkDesignKeys[i];
				if (!KingdomInheritRules.TrySemanticKeyForBlueprint(design, out key))
				{
					key = KingdomSealRules.SanitizeToken(design, KingdomSealRecord.MaxIdChars);
					if (!KingdomInheritRules.IsStableSemanticKey(key)) continue;
				}
				int x = Book.WorkAnchorsX[i];
				int y = Book.WorkAnchorsY[i];
				if (x < 0 || x > 255 || y < 0 || y > 255) continue;
				int at = Rows.Count;
				if (at >= Record.WorkKeys.Count || Record.WorkKeys[at] != key
					|| Record.WorkX[at] != x || Record.WorkY[at] != y)
				{
					Failure = "the city book changed while its spatial seal was witnessed";
					Rows = null;
					return false;
				}
				Rows.Add(new SourceWork
				{
					WorkId = Book.WorkIds[i], Blueprint = design, X = x, Y = y
				});
			}
			if (Rows.Count != Record.WorkKeys.Count)
			{
				Failure = "the city book's spatial work rows are incomplete";
				Rows = null;
				return false;
			}
			return true;
		}

		private static bool TryExactRoot(Zone Zone, SourceWork Row, out GameObject Root,
			out string Failure)
		{
			Root = null;
			Failure = "";
			Cell cell = Zone.GetCell(Row.X, Row.Y);
			if (cell == null)
			{
				Failure = "a sealed work anchor is outside its witnessed zone";
				return false;
			}
			int count = 0;
			for (int i = 0; i < cell.Objects.Count; i++)
			{
				GameObject item = cell.Objects[i];
				if (!GameObject.Validate(item) || item.Blueprint != Row.Blueprint
					|| Simulation.City.KingdomCityRules.StableId(item.ID) != Row.WorkId) continue;
				Root = item;
				count++;
			}
			if (count != 1)
			{
				Root = null;
				Failure = "a sealed work root is absent, duplicated, moved, or changed";
				return false;
			}
			return true;
		}

		private static bool HasArchitectureEvidence(GameObject Root)
		{
			return Root.HasIntProperty(KingdomArchitectureRuntime.SchemaProperty)
				|| Root.HasStringProperty(KingdomArchitectureRuntime.SchemaProperty)
				|| Root.HasStringProperty(KingdomArchitectureRuntime.SnapshotProperty)
				|| Root.HasStringProperty(KingdomArchitectureRuntime.HashProperty)
				|| Root.HasStringProperty(KingdomArchitectureRuntime.PlanKeyProperty)
				|| Root.HasStringProperty(KingdomArchitectureRuntime.BindingKeyProperty);
		}

		private static bool TryRoadEvidence(Zone Zone,
			IList<KingdomInheritanceSpatialRules.Rect> Rects, out bool[,] Roads,
			out string Failure)
		{
			Roads = new bool[KingdomInheritanceSpatialRules.Width,
				KingdomInheritanceSpatialRules.Height];
			Failure = "";
			for (int y = 0; y < Zone.Height; y++)
			{
				for (int x = 0; x < Zone.Width; x++)
				{
					GameObject floor;
					KingdomPhysicalLookupState state = KingdomRoads.FindOurFloor(
						Zone.GetCell(x, y), out floor);
					if (state == KingdomPhysicalLookupState.Ambiguous)
					{
						Failure = "road evidence is physically ambiguous at " + x + "," + y;
						return false;
					}
					if (state == KingdomPhysicalLookupState.Exact) Roads[x, y] = true;
				}
			}
			List<KingdomRoadRules.WornCell> tally;
			string error;
			if (!KingdomRoadRules.TryDecode(Zone.GetZoneProperty(KingdomRoads.TallyProperty,
				null), out tally, out error))
			{
				Failure = error ?? "the road tally is malformed";
				return false;
			}
			for (int i = 0; i < tally.Count; i++)
			{
				KingdomRoadRules.WornCell cell = tally[i];
				if (cell.X >= 0 && cell.Y >= 0 && cell.X < Zone.Width && cell.Y < Zone.Height
					&& KingdomRoadRules.WearAt(cell.Traffic) > KingdomRoadRules.WearState.Untouched)
					Roads[cell.X, cell.Y] = true;
			}
			for (int y = 0; y < Zone.Height; y++)
				for (int x = 0; x < Zone.Width; x++)
					for (int i = 0; Roads[x, y] && i < Rects.Count; i++)
						if (Rects[i].Contains(x, y)) Roads[x, y] = false;
			return true;
		}

		private static void SelectBoundaryComponent(bool[,] Roads, out int EntrySide,
			out int EntryX, out int EntryY, out List<int> StreetX, out List<int> StreetY)
		{
			EntrySide = KingdomInheritanceSpatialRules.NoEntry;
			EntryX = 0;
			EntryY = 0;
			StreetX = new List<int>();
			StreetY = new List<int>();
			for (int y = 0; y < KingdomInheritanceSpatialRules.Height; y++)
			{
				for (int x = 0; x < KingdomInheritanceSpatialRules.Width; x++)
				{
					int side = KingdomInheritanceSpatialRules.SideOfBoundary(x, y);
					if (side == KingdomInheritanceSpatialRules.NoEntry || !Roads[x, y]) continue;
					EntrySide = side;
					EntryX = x;
					EntryY = y;
					y = KingdomInheritanceSpatialRules.Height;
					break;
				}
			}
			if (EntrySide == KingdomInheritanceSpatialRules.NoEntry) return;
			bool[,] reached = new bool[KingdomInheritanceSpatialRules.Width,
				KingdomInheritanceSpatialRules.Height];
			Queue<int> queue = new Queue<int>();
			reached[EntryX, EntryY] = true;
			queue.Enqueue(EntryY * KingdomInheritanceSpatialRules.Width + EntryX);
			int[] dx = new int[4] { 0, 1, 0, -1 };
			int[] dy = new int[4] { -1, 0, 1, 0 };
			while (queue.Count > 0)
			{
				int packed = queue.Dequeue();
				int x = packed % KingdomInheritanceSpatialRules.Width;
				int y = packed / KingdomInheritanceSpatialRules.Width;
				for (int d = 0; d < 4; d++)
				{
					int nx = x + dx[d];
					int ny = y + dy[d];
					if (nx < 0 || ny < 0 || nx >= KingdomInheritanceSpatialRules.Width
						|| ny >= KingdomInheritanceSpatialRules.Height || reached[nx, ny]
						|| !Roads[nx, ny]) continue;
					reached[nx, ny] = true;
					queue.Enqueue(ny * KingdomInheritanceSpatialRules.Width + nx);
				}
			}
			for (int y = 0; y < KingdomInheritanceSpatialRules.Height; y++)
				for (int x = 0; x < KingdomInheritanceSpatialRules.Width; x++)
					if (reached[x, y])
					{
						StreetX.Add(x);
						StreetY.Add(y);
					}
		}

		private static KingdomInheritanceSpatialCaptureResult Malformed(string Detail,
			out string Failure)
		{
			Failure = string.IsNullOrEmpty(Detail) ? "spatial inheritance evidence is malformed"
				: Detail;
			return KingdomInheritanceSpatialCaptureResult.Malformed;
		}
	}
}
