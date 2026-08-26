using System;
using System.Collections.Generic;

using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomDistanceRuntime
	{

		private static void Shafts(KingdomZoneGraph graph, int zoneIndex,
			out int upX, out int upY, out int downX, out int downY)
		{
			upX = upY = downX = downY = -1;
			KingdomZoneNode here;
			if (!graph.TryNode(zoneIndex, out here)) return;
			for (int i = 0; i < graph.Count; i++)
			{
				if (i == zoneIndex) continue;
				KingdomZoneNode other;
				KingdomZoneStep step;
				if (!graph.TryNode(i, out other) || !graph.TryStep(zoneIndex, i, out step)
					|| (step != KingdomZoneStep.Up && step != KingdomZoneStep.Down)) continue;
				KingdomZoneNode head = (here.Stratum < other.Stratum) ? here : other;
				KingdomZoneNode foot = (here.Stratum < other.Stratum) ? other : here;
				KingdomDelveLinkReceipt receipt;
				if (!KingdomDelveLink.TryReadPhysicalReceipt(head.ZoneId, out receipt)
					|| receipt.FootZoneId != foot.ZoneId) continue;
				if (step == KingdomZoneStep.Up) { upX = receipt.X; upY = receipt.Y; }
				else { downX = receipt.X; downY = receipt.Y; }
			}
		}

		private static void WriteBoundaries(KingdomDistanceZoneCache zone, bool[] passable,
			bool[] paved, int width, int height)
		{
			Array.Clear(zone.BoundaryPassable, 0, zone.BoundaryPassable.Length);
			Array.Clear(zone.BoundaryPaved, 0, zone.BoundaryPaved.Length);
			for (int x = 0; x < width; x++)
			{
				BoundaryBit(zone, KingdomZoneStep.North, x, passable[x], paved[x]);
				int south = (height - 1) * width + x;
				BoundaryBit(zone, KingdomZoneStep.South, x, passable[south], paved[south]);
			}
			for (int y = 0; y < height; y++)
			{
				int west = y * width;
				int east = west + width - 1;
				BoundaryBit(zone, KingdomZoneStep.West, y, passable[west], paved[west]);
				BoundaryBit(zone, KingdomZoneStep.East, y, passable[east], paved[east]);
			}
		}

		private static void BoundaryBit(KingdomDistanceZoneCache zone, KingdomZoneStep edge,
			int offset, bool passable, bool paved)
		{
			if (offset < 0 || offset >= 128) return;
			int at = (int)edge * 2 + offset / 64;
			ulong bit = 1UL << (offset % 64);
			if (passable) zone.BoundaryPassable[at] |= bit;
			if (paved) zone.BoundaryPaved[at] |= bit;
		}

		private static void ReconcilePortals(KingdomDistanceCache cache)
		{
			if (cache == null || cache.Matrix == null) return;
			for (int a = 0; a < cache.Matrix.ZoneCount; a++)
			for (int b = a + 1; b < cache.Matrix.ZoneCount; b++)
			{
				KingdomZoneStep ab;
				KingdomZoneStep ba;
				KingdomDistanceZoneCache left;
				KingdomDistanceZoneCache right;
				if (!cache.Matrix.Graph.TryStep(a, b, out ab)
					|| !cache.Matrix.Graph.TryStep(b, a, out ba)
					|| (ab == KingdomZoneStep.Up || ab == KingdomZoneStep.Down)
					|| !cache.TryZone(a, out left) || !cache.TryZone(b, out right)
					|| !left.BoundaryObserved || !right.BoundaryObserved) continue;
				int limit = (ab == KingdomZoneStep.North || ab == KingdomZoneStep.South)
					? Math.Min(left.Width, right.Width) : Math.Min(left.Height, right.Height);
				int offset = MutualOffset(left, ab, right, ba, limit, paved: true);
				if (offset < 0) offset = MutualOffset(left, ab, right, ba, limit, paved: false);
				int ax = -1, ay = -1, bx = -1, by = -1;
				if (offset >= 0)
				{
					PortalCell(left, ab, offset, out ax, out ay);
					PortalCell(right, ba, offset, out bx, out by);
				}
				SetPortal(cache, a, ab, ax, ay);
				SetPortal(cache, b, ba, bx, by);
			}
		}

		private static void SetDefaultPortal(KingdomDistanceCache cache, int zoneIndex,
			KingdomZoneStep edge)
		{
			KingdomDistanceZoneCache zone;
			if (!cache.TryZone(zoneIndex, out zone)) return;
			int limit = (edge == KingdomZoneStep.North || edge == KingdomZoneStep.South)
				? zone.Width : zone.Height;
			int offset = SingleOffset(zone, edge, limit, paved: true);
			if (offset < 0) offset = SingleOffset(zone, edge, limit, paved: false);
			int x, y;
			PortalCell(zone, edge, offset, out x, out y);
			SetPortal(cache, zoneIndex, edge, x, y);
		}

		private static int SingleOffset(KingdomDistanceZoneCache zone, KingdomZoneStep edge,
			int limit, bool paved)
		{
			if (limit <= 0) return -1;
			ulong[] words = paved ? zone.BoundaryPaved : zone.BoundaryPassable;
			for (int offset = 0; offset < limit; offset++)
			{
				ulong bit = 1UL << (offset % 64);
				if ((words[(int)edge * 2 + offset / 64] & bit) != 0) return offset;
			}
			return -1;
		}

		private static int MutualOffset(KingdomDistanceZoneCache left, KingdomZoneStep leftEdge,
			KingdomDistanceZoneCache right, KingdomZoneStep rightEdge, int limit, bool paved)
		{
			ulong[] l = paved ? left.BoundaryPaved : left.BoundaryPassable;
			ulong[] r = paved ? right.BoundaryPaved : right.BoundaryPassable;
			for (int offset = 0; offset < limit; offset++)
			{
				int word = offset / 64;
				ulong bit = 1UL << (offset % 64);
				if ((l[(int)leftEdge * 2 + word] & bit) != 0
					&& (r[(int)rightEdge * 2 + word] & bit) != 0) return offset;
			}
			return -1;
		}

		private static void PortalCell(KingdomDistanceZoneCache zone, KingdomZoneStep edge,
			int offset, out int x, out int y)
		{
			x = y = -1;
			if (offset < 0) return;
			if (edge == KingdomZoneStep.North) { x = offset; y = 0; }
			else if (edge == KingdomZoneStep.South) { x = offset; y = zone.Height - 1; }
			else if (edge == KingdomZoneStep.West) { x = 0; y = offset; }
			else if (edge == KingdomZoneStep.East) { x = zone.Width - 1; y = offset; }
		}

		private static void SetPortal(KingdomDistanceCache cache, int zoneIndex,
			KingdomZoneStep edge, int x, int y)
		{
			KingdomDistanceZoneCache zone;
			int at = (int)edge;
			if (cache == null || at < 0 || at >= KingdomDistanceRules.EdgesPerZone
				|| !cache.TryZone(zoneIndex, out zone)) return;
			short sx = (short)x;
			short sy = (short)y;
			if (zone.PortalX[at] == sx && zone.PortalY[at] == sy) return;
			zone.PortalX[at] = sx;
			zone.PortalY[at] = sy;
			zone.Observed = false;
			cache.Matrix.MarkDirty(zone.ZoneId);
		}

		private static void Mix(ref ulong a, ref ulong b, string value)
		{
			if (value == null) { Mix(ref a, ref b, -1); return; }
			Mix(ref a, ref b, value.Length);
			for (int i = 0; i < value.Length; i++) Mix(ref a, ref b, value[i]);
		}

		private static void Mix(ref ulong a, ref ulong b, int value)
		{
			unchecked
			{
				a = (a ^ (uint)value) * PrimeA;
				b = (b ^ ((uint)value + 0x9E3779B9u)) * PrimeB;
			}
		}
	}
}
