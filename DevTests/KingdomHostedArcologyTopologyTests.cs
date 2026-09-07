#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public sealed class KingdomHostedArcologyTopologyTests
	{
		[Test]
		public void SchemaEnumeratesExactlyTwentySevenDistinctProgrammedZones()
		{
			List<KingdomArcologyCoordinate> all = KingdomHostedArcologyTopology.AllCoordinates();
			ClassicAssert.AreEqual(KingdomHostedArcologyTopology.ZoneCount, all.Count);
			HashSet<KingdomArcologyCoordinate> coordinates =
				new HashSet<KingdomArcologyCoordinate>();
			HashSet<KingdomArcologyProgramme> programmes =
				new HashSet<KingdomArcologyProgramme>();
			for (int i = 0; i < all.Count; i++)
			{
				ClassicAssert.IsTrue(coordinates.Add(all[i]), all[i].ToString());
				KingdomArcologyProgramme programme = KingdomHostedArcologyTopology.ProgrammeAt(
					all[i].X, all[i].Y, all[i].Z);
				ClassicAssert.AreNotEqual((KingdomArcologyProgramme)0, programme, all[i].ToString());
				ClassicAssert.IsTrue(programmes.Add(programme), programme.ToString());
				ClassicAssert.IsNotEmpty(KingdomHostedArcologyTopology.ProgrammeName(programme));
			}
			ClassicAssert.AreEqual(27, programmes.Count);
			ClassicAssert.AreEqual((KingdomArcologyProgramme)0,
				KingdomHostedArcologyTopology.ProgrammeAt(-1, 1, 10));
		}

		[Test]
		public void EveryHorizontalThresholdIsReciprocalAndEveryOuterEdgeIsSealed()
		{
			List<KingdomArcologyCoordinate> all = KingdomHostedArcologyTopology.AllCoordinates();
			for (int i = 0; i < all.Count; i++)
			{
				KingdomArcologyCoordinate at = all[i];
				foreach (KingdomArcologyDirection direction in
					Enum.GetValues(typeof(KingdomArcologyDirection)))
				{
					KingdomArcologyCoordinate neighbour;
					bool open = KingdomHostedArcologyTopology.TryHorizontalNeighbour(
						at.X, at.Y, at.Z, direction, out neighbour);
					bool expected = direction == KingdomArcologyDirection.North ? at.Y > 0
						: direction == KingdomArcologyDirection.East ? at.X < 2
						: direction == KingdomArcologyDirection.South ? at.Y < 2
						: at.X > 0;
					ClassicAssert.AreEqual(expected, open, at + " " + direction);
					if (!open) continue;
					KingdomArcologyCoordinate returned;
					ClassicAssert.IsTrue(KingdomHostedArcologyTopology.TryHorizontalNeighbour(
						neighbour.X, neighbour.Y, neighbour.Z, Opposite(direction), out returned));
					ClassicAssert.AreEqual(at, returned);
				}
			}
		}

		[Test]
		public void AlignedVerticalPairsAndThresholdsConnectTheWholeSchema()
		{
			List<KingdomArcologyCoordinate> all = KingdomHostedArcologyTopology.AllCoordinates();
			HashSet<KingdomArcologyCoordinate> visited =
				new HashSet<KingdomArcologyCoordinate>();
			Queue<KingdomArcologyCoordinate> pending =
				new Queue<KingdomArcologyCoordinate>();
			KingdomArcologyCoordinate entry = new KingdomArcologyCoordinate(1, 1, 10);
			visited.Add(entry);
			pending.Enqueue(entry);
			while (pending.Count > 0)
			{
				KingdomArcologyCoordinate at = pending.Dequeue();
				foreach (KingdomArcologyDirection direction in
					Enum.GetValues(typeof(KingdomArcologyDirection)))
				{
					KingdomArcologyCoordinate next;
					if (KingdomHostedArcologyTopology.TryHorizontalNeighbour(
						at.X, at.Y, at.Z, direction, out next) && visited.Add(next))
						pending.Enqueue(next);
				}
				if (KingdomHostedArcologyTopology.HasStairsUp(at.Z))
					Add(new KingdomArcologyCoordinate(at.X, at.Y, at.Z - 1), visited, pending);
				if (KingdomHostedArcologyTopology.HasStairsDown(at.Z))
					Add(new KingdomArcologyCoordinate(at.X, at.Y, at.Z + 1), visited, pending);
			}
			ClassicAssert.AreEqual(all.Count, visited.Count);
			for (int i = 0; i < all.Count; i++)
			{
				KingdomArcologyCoordinate at = all[i];
				ClassicAssert.AreEqual(at.Z > 9, KingdomHostedArcologyTopology.HasStairsUp(at.Z));
				ClassicAssert.AreEqual(at.Z < 11, KingdomHostedArcologyTopology.HasStairsDown(at.Z));
				if (KingdomHostedArcologyTopology.HasStairsDown(at.Z))
					ClassicAssert.AreEqual(KingdomHostedArcologyTopology.StairsDownX(at.Z),
						KingdomHostedArcologyTopology.StairsUpX(at.Z + 1));
			}
			ClassicAssert.AreEqual(-1, KingdomHostedArcologyTopology.StairsUpX(9));
			ClassicAssert.AreEqual(-1, KingdomHostedArcologyTopology.StairsDownX(11));
		}

		[Test]
		public void SurfaceExitAndPaidWorksOwnOnlyTheirDeclaredZones()
		{
			int exits = 0;
			int wards = 0;
			int terraces = 0;
			foreach (KingdomArcologyCoordinate at in KingdomHostedArcologyTopology.AllCoordinates())
			{
				if (KingdomHostedArcologyTopology.IsSurfaceExit(at.X, at.Y, at.Z)) exits++;
				string lot = KingdomHostedArcologyTopology.HostedLotAt(at.X, at.Y, at.Z);
				if (lot == KingdomHostedArcologyTopology.WardLotKey) wards++;
				if (lot == KingdomHostedArcologyTopology.TerraceLotKey) terraces++;
				if (!string.IsNullOrEmpty(lot))
					ClassicAssert.IsTrue(KingdomHostedArcologyTopology.IsHostedLotZone(
						lot, at.X, at.Y, at.Z));
			}
			ClassicAssert.AreEqual(1, exits);
			ClassicAssert.AreEqual(1, wards);
			ClassicAssert.AreEqual(1, terraces);
			ClassicAssert.AreEqual(KingdomHostedArcologyTopology.TerraceLotKey,
				KingdomHostedArcologyTopology.HostedLotAt(1, 1, 9));
			ClassicAssert.AreEqual(KingdomHostedArcologyTopology.WardLotKey,
				KingdomHostedArcologyTopology.HostedLotAt(0, 1, 11));
		}

		[Test]
		public void NativeReviewTargetsNameExactTopologyProgrammes()
		{
			AssertTarget(1, 1, 10, KingdomArcologyProgramme.InheritedCourt, "");
			AssertTarget(1, 0, 10, KingdomArcologyProgramme.TeachingHall, "");
			AssertTarget(1, 1, 9, KingdomArcologyProgramme.HydroponicTerrace,
				KingdomHostedArcologyTopology.TerraceLotKey);
			AssertTarget(0, 1, 11, KingdomArcologyProgramme.LodgingWard,
				KingdomHostedArcologyTopology.WardLotKey);
			ClassicAssert.IsTrue(KingdomHostedArcologyTopology.IsSurfaceExit(1, 1, 10));
			ClassicAssert.IsFalse(KingdomHostedArcologyTopology.IsSurfaceExit(1, 0, 10));
		}

		[Test]
		public void SemanticRolesAreCoordinateAndRootScoped()
		{
			HashSet<string> roles = new HashSet<string>(StringComparer.Ordinal);
			HashSet<string> identities = new HashSet<string>(StringComparer.Ordinal);
			foreach (KingdomArcologyCoordinate at in KingdomHostedArcologyTopology.AllCoordinates())
			{
				string role = KingdomHostedArcologyTopology.StableRole(
					at.X, at.Y, at.Z, "anchor");
				ClassicAssert.IsTrue(roles.Add(role), role);
				ClassicAssert.IsTrue(identities.Add(
					KingdomHostedArcologyRules.StableChildId("root", role)));
			}
			ClassicAssert.AreEqual("", KingdomHostedArcologyTopology.StableRole(3, 1, 10, "anchor"));
			ClassicAssert.AreNotEqual(
				KingdomHostedArcologyRules.StableChildId("root-a", rolesString(roles)),
				KingdomHostedArcologyRules.StableChildId("root-b", rolesString(roles)));
		}

		private static string rolesString(HashSet<string> Roles)
		{
			foreach (string role in Roles) return role;
			return "";
		}

		private static void AssertTarget(int X, int Y, int Z,
			KingdomArcologyProgramme Programme, string LotKey)
		{
			ClassicAssert.AreEqual(Programme, KingdomHostedArcologyTopology.ProgrammeAt(X, Y, Z));
			ClassicAssert.AreEqual(LotKey, KingdomHostedArcologyTopology.HostedLotAt(X, Y, Z));
		}

		private static void Add(KingdomArcologyCoordinate Coordinate,
			HashSet<KingdomArcologyCoordinate> Visited,
			Queue<KingdomArcologyCoordinate> Pending)
		{
			if (Visited.Add(Coordinate)) Pending.Enqueue(Coordinate);
		}

		private static KingdomArcologyDirection Opposite(KingdomArcologyDirection Direction)
		{
			return Direction == KingdomArcologyDirection.North ? KingdomArcologyDirection.South
				: Direction == KingdomArcologyDirection.East ? KingdomArcologyDirection.West
				: Direction == KingdomArcologyDirection.South ? KingdomArcologyDirection.North
				: KingdomArcologyDirection.East;
		}
	}
}
#endif
