using System;
using System.Collections.Generic;
using System.Globalization;

namespace ThousandAndFirst
{
	public enum KingdomArcologyDirection : byte
	{
		North = 1,
		East = 2,
		South = 3,
		West = 4
	}

	public enum KingdomArcologyProgramme : byte
	{
		SeedArchive = 1,
		SpectrumGallery = 2,
		CondenserWalk = 3,
		NurseryBowers = 4,
		HydroponicTerrace = 5,
		GraftingHouse = 6,
		DryingGallery = 7,
		IrrigationGallery = 8,
		LightwellGarden = 9,
		PublicKitchen = 10,
		TeachingHall = 11,
		Infirmary = 12,
		Exchange = 13,
		InheritedCourt = 14,
		Assembly = 15,
		Baths = 16,
		GuestHall = 17,
		GuardedApproach = 18,
		LodgingWard = 19,
		CisternPlant = 20,
		Stores = 21,
		MaintenanceConcourse = 22,
		FabricationBay = 23,
		FreightHall = 24,
		SanitationWorks = 25,
		ServiceBarracks = 26,
		RepairGallery = 27
	}

	public struct KingdomArcologyCoordinate : IEquatable<KingdomArcologyCoordinate>
	{
		public readonly int X;
		public readonly int Y;
		public readonly int Z;

		public KingdomArcologyCoordinate(int X, int Y, int Z)
		{
			this.X = X;
			this.Y = Y;
			this.Z = Z;
		}

		public bool Equals(KingdomArcologyCoordinate Other)
		{
			return X == Other.X && Y == Other.Y && Z == Other.Z;
		}

		public override bool Equals(object Value)
		{
			return Value is KingdomArcologyCoordinate
				&& Equals((KingdomArcologyCoordinate)Value);
		}

		public override int GetHashCode()
		{
			unchecked { return ((X * 397) ^ Y) * 397 ^ Z; }
		}

		public override string ToString()
		{
			return X.ToString(CultureInfo.InvariantCulture) + ","
				+ Y.ToString(CultureInfo.InvariantCulture) + ","
				+ Z.ToString(CultureInfo.InvariantCulture);
		}
	}

	/// <summary>Engine-free authority for the hosted arcology's one-parasang topology.</summary>
	public static class KingdomHostedArcologyTopology
	{
		public const string Schema = "TAFArcology";
		public const int MinX = 0;
		public const int MaxX = 2;
		public const int MinY = 0;
		public const int MaxY = 2;
		public const int UpperZ = 9;
		public const int CivicZ = 10;
		public const int LowerZ = 11;
		public const int EntryX = 1;
		public const int EntryY = 1;
		public const int EntryZ = CivicZ;
		public const int UpperLinkX = 36;
		public const int LowerLinkX = 44;
		public const int StairY = 12;
		public const int ZoneCount = 27;
		public const string TerraceLotKey = "arcologyterrace";
		public const string WardLotKey = "arcologyward";

		private static readonly KingdomArcologyProgramme[] Programmes =
			new KingdomArcologyProgramme[] {
				KingdomArcologyProgramme.SeedArchive,
				KingdomArcologyProgramme.SpectrumGallery,
				KingdomArcologyProgramme.CondenserWalk,
				KingdomArcologyProgramme.NurseryBowers,
				KingdomArcologyProgramme.HydroponicTerrace,
				KingdomArcologyProgramme.GraftingHouse,
				KingdomArcologyProgramme.DryingGallery,
				KingdomArcologyProgramme.IrrigationGallery,
				KingdomArcologyProgramme.LightwellGarden,
				KingdomArcologyProgramme.PublicKitchen,
				KingdomArcologyProgramme.TeachingHall,
				KingdomArcologyProgramme.Infirmary,
				KingdomArcologyProgramme.Exchange,
				KingdomArcologyProgramme.InheritedCourt,
				KingdomArcologyProgramme.Assembly,
				KingdomArcologyProgramme.Baths,
				KingdomArcologyProgramme.GuestHall,
				KingdomArcologyProgramme.GuardedApproach,
				KingdomArcologyProgramme.CisternPlant,
				KingdomArcologyProgramme.Stores,
				KingdomArcologyProgramme.FreightHall,
				KingdomArcologyProgramme.LodgingWard,
				KingdomArcologyProgramme.MaintenanceConcourse,
				KingdomArcologyProgramme.FabricationBay,
				KingdomArcologyProgramme.SanitationWorks,
				KingdomArcologyProgramme.ServiceBarracks,
				KingdomArcologyProgramme.RepairGallery
			};

		public static bool InBounds(int X, int Y, int Z)
		{
			return X >= MinX && X <= MaxX && Y >= MinY && Y <= MaxY
				&& Z >= UpperZ && Z <= LowerZ;
		}

		public static List<KingdomArcologyCoordinate> AllCoordinates()
		{
			List<KingdomArcologyCoordinate> result =
				new List<KingdomArcologyCoordinate>(ZoneCount);
			for (int z = UpperZ; z <= LowerZ; z++)
				for (int y = MinY; y <= MaxY; y++)
					for (int x = MinX; x <= MaxX; x++)
						result.Add(new KingdomArcologyCoordinate(x, y, z));
			return result;
		}

		public static KingdomArcologyProgramme ProgrammeAt(int X, int Y, int Z)
		{
			if (!InBounds(X, Y, Z)) return 0;
			return Programmes[(Z - UpperZ) * 9 + Y * 3 + X];
		}

		public static string ProgrammeName(KingdomArcologyProgramme Programme)
		{
			switch (Programme)
			{
				case KingdomArcologyProgramme.SeedArchive: return "seed archive";
				case KingdomArcologyProgramme.SpectrumGallery: return "spectrum gallery";
				case KingdomArcologyProgramme.CondenserWalk: return "condenser walk";
				case KingdomArcologyProgramme.NurseryBowers: return "nursery bowers";
				case KingdomArcologyProgramme.HydroponicTerrace: return "hydroponic terrace";
				case KingdomArcologyProgramme.GraftingHouse: return "grafting house";
				case KingdomArcologyProgramme.DryingGallery: return "drying gallery";
				case KingdomArcologyProgramme.IrrigationGallery: return "irrigation gallery";
				case KingdomArcologyProgramme.LightwellGarden: return "lightwell garden";
				case KingdomArcologyProgramme.PublicKitchen: return "public kitchens";
				case KingdomArcologyProgramme.TeachingHall: return "teaching hall";
				case KingdomArcologyProgramme.Infirmary: return "infirmary";
				case KingdomArcologyProgramme.Exchange: return "exchange";
				case KingdomArcologyProgramme.InheritedCourt: return "inherited court";
				case KingdomArcologyProgramme.Assembly: return "assembly hall";
				case KingdomArcologyProgramme.Baths: return "public baths";
				case KingdomArcologyProgramme.GuestHall: return "guest hall";
				case KingdomArcologyProgramme.GuardedApproach: return "guarded approach";
				case KingdomArcologyProgramme.LodgingWard: return "vertical lodging ward";
				case KingdomArcologyProgramme.CisternPlant: return "cistern plant";
				case KingdomArcologyProgramme.Stores: return "sealed stores";
				case KingdomArcologyProgramme.MaintenanceConcourse: return "maintenance concourse";
				case KingdomArcologyProgramme.FabricationBay: return "fabrication bay";
				case KingdomArcologyProgramme.FreightHall: return "freight hall";
				case KingdomArcologyProgramme.SanitationWorks: return "sanitation works";
				case KingdomArcologyProgramme.ServiceBarracks: return "service barracks";
				case KingdomArcologyProgramme.RepairGallery: return "repair gallery";
				default: return "";
			}
		}

		public static bool HasHorizontalNeighbour(int X, int Y, int Z,
			KingdomArcologyDirection Direction)
		{
			KingdomArcologyCoordinate ignored;
			return TryHorizontalNeighbour(X, Y, Z, Direction, out ignored);
		}

		public static bool TryHorizontalNeighbour(int X, int Y, int Z,
			KingdomArcologyDirection Direction, out KingdomArcologyCoordinate Neighbour)
		{
			int nx = X;
			int ny = Y;
			if (Direction == KingdomArcologyDirection.North) ny--;
			else if (Direction == KingdomArcologyDirection.East) nx++;
			else if (Direction == KingdomArcologyDirection.South) ny++;
			else if (Direction == KingdomArcologyDirection.West) nx--;
			else { Neighbour = new KingdomArcologyCoordinate(); return false; }
			Neighbour = new KingdomArcologyCoordinate(nx, ny, Z);
			return InBounds(nx, ny, Z);
		}

		public static bool HasStairsUp(int Z) { return Z > UpperZ && Z <= LowerZ; }
		public static bool HasStairsDown(int Z) { return Z >= UpperZ && Z < LowerZ; }
		public static int StairsUpX(int Z)
		{
			return Z == CivicZ ? UpperLinkX : Z == LowerZ ? LowerLinkX : -1;
		}
		public static int StairsDownX(int Z)
		{
			return Z == UpperZ ? UpperLinkX : Z == CivicZ ? LowerLinkX : -1;
		}

		public static bool IsSurfaceExit(int X, int Y, int Z)
		{
			return X == EntryX && Y == EntryY && Z == EntryZ;
		}

		public static string HostedLotAt(int X, int Y, int Z)
		{
			if (X == 1 && Y == 1 && Z == UpperZ) return TerraceLotKey;
			if (X == 0 && Y == 1 && Z == LowerZ) return WardLotKey;
			return "";
		}

		public static bool IsHostedLotZone(string LotKey, int X, int Y, int Z)
		{
			return string.Equals(HostedLotAt(X, Y, Z), LotKey,
				StringComparison.Ordinal);
		}

		public static bool TryHostedLotCoordinate(string LotKey,
			out KingdomArcologyCoordinate Coordinate)
		{
			if (LotKey == TerraceLotKey)
			{
				Coordinate = new KingdomArcologyCoordinate(1, 1, UpperZ); return true;
			}
			if (LotKey == WardLotKey)
			{
				Coordinate = new KingdomArcologyCoordinate(0, 1, LowerZ); return true;
			}
			Coordinate = new KingdomArcologyCoordinate(); return false;
		}

		public static string StableRole(int X, int Y, int Z, string Role)
		{
			if (!InBounds(X, Y, Z) || string.IsNullOrEmpty(Role)) return "";
			return "zone:" + X.ToString(CultureInfo.InvariantCulture) + ":"
				+ Y.ToString(CultureInfo.InvariantCulture) + ":"
				+ Z.ToString(CultureInfo.InvariantCulture) + ":" + Role;
		}
	}
}
