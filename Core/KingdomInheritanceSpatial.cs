using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>Native, read-only witness adapter for one loaded old seat. It resolves no current
	/// architecture mapping: only roots already carrying a complete frozen receipt may contribute
	/// authored geometry.</summary>
	internal static partial class KingdomInheritanceSpatial
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

	}
}
