using System;
using System.Collections.Generic;

using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>
	/// Read-only capture of one realized architecture lot, shared by every path that builds one.
	/// <para>
	/// An ordinary commission and the review gallery both stamp through
	/// <see cref="KingdomArchitectureStamper"/>, so both mark their components with the same lot id,
	/// snapshot hash, and component schema. This reader gathers exactly those, bounded to the
	/// owner's own lot rect, and derives every cell fact from them. That is what lets a differential
	/// ask whether two builds are the same REALIZED result rather than merely the same frozen
	/// receipt, while staying comparable between an inhabited settlement and an empty gallery.
	/// </para>
	/// <para>
	/// Aggregate cell predicates are never consulted. The engine's own passability and open-liquid
	/// tests on <c>Cell</c> scan every object standing in the cell, so a wandering
	/// resident or an unrelated puddle would move a digest that claims to measure architecture, and
	/// would contradict this reader's own exclusion of that same resident.
	/// </para>
	/// <para>
	/// Read-only: nothing here creates, moves, destroys, or writes a property.
	/// </para>
	/// </summary>
	public static partial class KingdomRealizedArchitectureCapture
	{
		/// <summary>
		/// Measures the realized lot around an architecture owner. Fails rather than narrowing when
		/// the owner is not exact, its lot cannot be bounded, or the lot's component authority is
		/// moved, duplicated, partial, foreign, or unreadable.
		/// </summary>
		public static bool TryCapture(GameObject Owner, out string Digest, out int Width,
			out int Height, out string Failure, bool Stable = false)
		{
			Digest = null;
			Width = 0;
			Height = 0;
			Failure = null;
			if (!GameObject.Validate(Owner)) return Fail("the architecture owner is not valid", out Failure);
			Zone zone = Owner.CurrentZone;
			if (zone == null) return Fail("the architecture owner is not in a loaded zone", out Failure);
			KingdomArchitectureIntent intent;
			ArchitectureLayoutSnapshot snapshot;
			string lot;
			if (!KingdomArchitectureStamper.TryReadOwner(Owner, out intent, out snapshot, out lot,
				out Failure)) return false;
			if (intent == null) return Fail("the owner carries no architecture intent", out Failure);
			if (string.IsNullOrEmpty(lot)) return Fail("the owner carries no lot id", out Failure);
			if (string.IsNullOrEmpty(intent.SnapshotHash))
				return Fail("the owner carries no snapshot hash to bind its components to", out Failure);
			int x1 = intent.Rect.X1;
			int y1 = intent.Rect.Y1;
			// Each dimension is computed and bounded in long BEFORE it becomes an int: a hostile
			// rect whose subtraction overflows would otherwise arrive as a small positive width.
			long width = (long)intent.Rect.X2 - (long)x1 + 1L;
			long height = (long)intent.Rect.Y2 - (long)y1 + 1L;
			if (width < 1L || height < 1L) return Fail("the lot rect is empty", out Failure);
			// Bound each dimension before multiplying: two unbounded long dimensions can overflow
			// their own product, which is the same defect one level up.
			if (width > KingdomRealizedCaptureRules.MaxCells
				|| height > KingdomRealizedCaptureRules.MaxCells
				|| width * height > KingdomRealizedCaptureRules.MaxCells)
				return Fail("the lot exceeds the bounded capture size", out Failure);
			Width = (int)width;
			Height = (int)height;
			List<KingdomRealizedObjectFact> objects;
			if (!TryObjects(zone, Owner, intent, snapshot, lot, x1, y1, Width, Height, out objects,
				out Failure)) return false;
			// Stable: a before/after comparison across a walk forgets the look of what is expected
			// to move (stateful fixtures, doors); the digest of record stays the live one.
			if (Stable) objects = KingdomRealizedCaptureRules.Stabilized(objects);
			List<KingdomRealizedCellFact> cells = Cells(Width, Height, objects);
			Digest = KingdomRealizedCaptureRules.Digest(Width, Height, cells, objects);
			if (Digest == null) return Fail("the realized lot could not be digested", out Failure);
			return true;
		}

		/// <summary>
		/// One row per in-bounds coordinate, built ONLY from the architecture facts standing on it.
		/// An empty coordinate is still recorded, so a component removed between two builds moves the
		/// digest instead of shortening the list.
		/// </summary>
		private static List<KingdomRealizedCellFact> Cells(int Width, int Height,
			IList<KingdomRealizedObjectFact> Objects)
		{
			KingdomRealizedCellFact[] grid = new KingdomRealizedCellFact[Width * Height];
			for (int y = 0; y < Height; y++)
				for (int x = 0; x < Width; x++)
					grid[(y * Width) + x] = new KingdomRealizedCellFact { X = x, Y = y };
			for (int i = 0; i < Objects.Count; i++)
			{
				KingdomRealizedObjectFact item = Objects[i];
				KingdomRealizedCellFact cell = grid[(item.Y * Width) + item.X];
				if (item.Owner) cell.Owner = true;
				else cell.Components++;
				if (item.Door) cell.Door = true;
				else if (item.Solid) cell.Blocking = true;
				if (item.Liquid != null) cell.Liquid = true;
			}
			return new List<KingdomRealizedCellFact>(grid);
		}

		private static bool Fail(string Message, out string Failure)
		{
			Failure = Message;
			return false;
		}
	}
}
