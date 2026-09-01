using ThousandAndFirst;
using XRL.World;

namespace XRL.World.ZoneBuilders
{
	/// <summary>One exact, root-owned placement in a hosted programme.</summary>
	internal sealed class KingdomArcologyFixtureSpec
	{
		internal readonly int X;
		internal readonly int Y;
		internal readonly string Blueprint;
		internal readonly string Role;

		internal KingdomArcologyFixtureSpec(int X, int Y, string Blueprint, string Role)
		{
			this.X = X;
			this.Y = Y;
			this.Blueprint = Blueprint;
			this.Role = Role;
		}
	}

	/// <summary>Deterministic spatial programmes; decorative props mint no civic state.</summary>
	internal static class KingdomHostedArcologyProgrammeBuilder
	{
		private static readonly KingdomArcologyFixtureSpec[] Ward = new KingdomArcologyFixtureSpec[] {
			F(8,5,"r_KingdomFixtureBedMetal","bed01"), F(8,19,"r_KingdomFixtureBedMetal","bed02"),
			F(25,5,"r_KingdomFixtureBedMetal","bed03"), F(25,19,"r_KingdomFixtureBedMetal","bed04"),
			F(54,5,"r_KingdomFixtureBedMetal","bed05"), F(54,19,"r_KingdomFixtureBedMetal","bed06"),
			F(71,5,"r_KingdomFixtureBedMetal","bed07"), F(71,19,"r_KingdomFixtureBedMetal","bed08"),
			F(15,8,"r_KingdomFixtureLockerScrap","locker01"),
			F(64,16,"r_KingdomFixtureLockerScrap","locker02"),
			F(34,8,"r_KingdomArcologyWardAmenity","amenity"),
			F(32,8,"r_KingdomFixtureChairMetal","chair01"),
			F(38,8,"r_KingdomFixtureChairMetal","chair02"),
			F(42,8,"r_KingdomFixtureChairMetal","chair03")
		};

		private static readonly KingdomArcologyFixtureSpec[] Terrace = new KingdomArcologyFixtureSpec[] {
			F(9,5,"r_KingdomArcologyGrowbed","bed01"), F(16,5,"r_KingdomArcologyGrowbed","bed02"),
			F(23,5,"r_KingdomArcologyGrowbed","bed03"), F(30,5,"r_KingdomArcologyGrowbed","bed04"),
			F(49,5,"r_KingdomArcologyGrowbed","bed05"), F(56,5,"r_KingdomArcologyGrowbed","bed06"),
			F(63,5,"r_KingdomArcologyGrowbed","bed07"), F(70,5,"r_KingdomArcologyGrowbed","bed08"),
			F(9,19,"r_KingdomArcologyGrowbed","bed09"), F(16,19,"r_KingdomArcologyGrowbed","bed10"),
			F(23,19,"r_KingdomArcologyGrowbed","bed11"), F(30,19,"r_KingdomArcologyGrowbed","bed12"),
			F(49,19,"r_KingdomArcologyGrowbed","bed13"), F(70,19,"r_KingdomArcologyGrowbed","bed14"),
			F(39,8,"r_KingdomArcologyRiser","riser"), F(39,16,"r_KingdomArcologyConduit","tap"),
			F(34,12,"r_KingdomFixtureTableStone","table"),
			F(48,12,"r_KingdomFixtureChairMetal","chair")
		};

		internal static string FloorFor(int Z)
		{
			return Z == KingdomHostedArcologyTopology.CivicZ
				? "GreyMarbleFloor" : Z == KingdomHostedArcologyTopology.UpperZ
					? "FoamcreteFloor" : "SmallHexFloor";
		}

		internal static string MaterialHistoryFor(int Z)
		{
			return Z == KingdomHostedArcologyTopology.UpperZ
				? "reclad foamcrete and ceramic cultivation galleries"
				: Z == KingdomHostedArcologyTopology.CivicZ
					? "surviving grey marble held inside poured concrete ribs"
					: "rusted Eater service steel repaired behind open screens";
		}

		internal static bool Build(Zone Z, KingdomArcologyCoordinate At,
			KingdomArcologyProgramme Programme, string RootId)
		{
			if (KingdomHostedArcologyTopology.ProgrammeAt(At.X, At.Y, At.Z) != Programme)
				return false;
			string[] decor = DecorFor(Programme);
			if (decor == null) return false;
			int archetype = ((int)Programme - 1) % 9;
			string primary = PrimaryFor(At.Z);
			string accent = AccentFor(At.Z);
			Z.SetZoneProperty("TAFArcologyArchetype", ArchetypeName(archetype));
			Z.SetZoneProperty("TAFArcologyMaterialHistory", MaterialHistoryFor(At.Z));
			Z.SetZoneProperty("TAFArcologyPlanSignature", ArchetypeName(archetype)
				+ "|" + primary + "|" + accent + "|" + decor[0] + "|" + decor[1]
				+ "|" + decor[2]);
			return BuildArchetype(Z, At, RootId, archetype, primary, accent)
				&& AddProgrammeProps(Z, At, RootId, decor)
				&& AddRouteLights(Z, At, RootId);
		}

		internal static bool TryPaidFixtures(string LotKey, KingdomArcologyProgramme Programme,
			out KingdomArcologyFixtureSpec[] Fixtures)
		{
			Fixtures = LotKey == KingdomHostedArcologyTopology.TerraceLotKey
				&& Programme == KingdomArcologyProgramme.HydroponicTerrace ? Terrace
				: LotKey == KingdomHostedArcologyTopology.WardLotKey
					&& Programme == KingdomArcologyProgramme.LodgingWard ? Ward : null;
			return Fixtures != null;
		}

		private static bool BuildArchetype(Zone Z, KingdomArcologyCoordinate At,
			string RootId, int Kind, string A, string B)
		{
			switch (Kind)
			{
				case 0: return Cellular(Z, At, RootId, A, B);
				case 1: return Nave(Z, At, RootId, A, B);
				case 2: return Comb(Z, At, RootId, A, B);
				case 3: return Courts(Z, At, RootId, A, B);
				case 4: return Terraces(Z, At, RootId, A, B);
				case 5: return Workbays(Z, At, RootId, A, B);
				case 6: return Aisles(Z, At, RootId, A, B);
				case 7: return Branches(Z, At, RootId, A, B);
				case 8: return Lightwell(Z, At, RootId, A, B);
				default: return false;
			}
		}

		private static bool Cellular(Zone z, KingdomArcologyCoordinate at, string root, string a, string b)
		{
			return V(z,at,root,16,3,8,a,"cell-nw") && V(z,at,root,31,3,8,b,"cell-nc")
				&& V(z,at,root,48,3,8,b,"cell-ne") && V(z,at,root,63,3,8,a,"cell-nf")
				&& V(z,at,root,16,16,21,a,"cell-sw") && V(z,at,root,31,16,21,b,"cell-sc")
				&& V(z,at,root,48,16,21,b,"cell-se") && V(z,at,root,63,16,21,a,"cell-sf");
		}

		private static bool Nave(Zone z, KingdomArcologyCoordinate at, string root, string a, string b)
		{
			return H(z,at,root,5,30,7,a,"nave-nw") && H(z,at,root,49,74,7,a,"nave-ne")
				&& H(z,at,root,5,30,17,a,"nave-sw") && H(z,at,root,49,74,17,a,"nave-se")
				&& V(z,at,root,20,3,6,b,"pier-nw") && V(z,at,root,59,3,6,b,"pier-ne")
				&& V(z,at,root,20,18,21,b,"pier-sw") && V(z,at,root,59,18,21,b,"pier-se");
		}

		private static bool Comb(Zone z, KingdomArcologyCoordinate at, string root, string a, string b)
		{
			return H(z,at,root,5,30,8,a,"comb-n") && H(z,at,root,49,74,16,a,"comb-s")
				&& V(z,at,root,10,3,7,b,"tooth-n1") && V(z,at,root,20,3,7,b,"tooth-n2")
				&& V(z,at,root,30,3,7,b,"tooth-n3") && V(z,at,root,49,17,21,b,"tooth-s1")
				&& V(z,at,root,59,17,21,b,"tooth-s2") && V(z,at,root,69,17,21,b,"tooth-s3");
		}

		private static bool Courts(Zone z, KingdomArcologyCoordinate at, string root, string a, string b)
		{
			return Corner(z,at,root,7,22,3,7,a,b,"court-nw")
				&& Corner(z,at,root,57,72,3,7,a,b,"court-ne")
				&& Corner(z,at,root,7,22,17,21,a,b,"court-sw")
				&& Corner(z,at,root,57,72,17,21,a,b,"court-se");
		}

		private static bool Terraces(Zone z, KingdomArcologyCoordinate at, string root, string a, string b)
		{
			return H(z,at,root,4,30,8,a,"terrace-nw") && H(z,at,root,49,75,8,b,"terrace-ne")
				&& H(z,at,root,4,30,16,b,"terrace-sw") && H(z,at,root,49,75,16,a,"terrace-se");
		}

		private static bool Workbays(Zone z, KingdomArcologyCoordinate at, string root, string a, string b)
		{
			return Bay(z,at,root,6,27,3,8,a,b,"bay-nw")
				&& Bay(z,at,root,52,73,3,8,a,b,"bay-ne")
				&& Bay(z,at,root,6,27,16,21,a,b,"bay-sw")
				&& Bay(z,at,root,52,73,16,21,a,b,"bay-se");
		}

		private static bool Aisles(Zone z, KingdomArcologyCoordinate at, string root, string a, string b)
		{
			return V(z,at,root,12,3,8,a,"aisle-n1") && V(z,at,root,24,3,8,b,"aisle-n2")
				&& V(z,at,root,55,3,8,b,"aisle-n3") && V(z,at,root,67,3,8,a,"aisle-n4")
				&& V(z,at,root,12,16,21,b,"aisle-s1") && V(z,at,root,24,16,21,a,"aisle-s2")
				&& V(z,at,root,55,16,21,a,"aisle-s3") && V(z,at,root,67,16,21,b,"aisle-s4");
		}

		private static bool Branches(Zone z, KingdomArcologyCoordinate at, string root, string a, string b)
		{
			return H(z,at,root,5,30,6,a,"branch-nw") && H(z,at,root,49,74,6,a,"branch-ne")
				&& H(z,at,root,5,30,18,b,"branch-sw") && H(z,at,root,49,74,18,b,"branch-se")
				&& V(z,at,root,15,7,8,b,"drop-nw") && V(z,at,root,64,7,8,b,"drop-ne")
				&& V(z,at,root,15,16,17,a,"rise-sw") && V(z,at,root,64,16,17,a,"rise-se");
		}

		private static bool Lightwell(Zone z, KingdomArcologyCoordinate at, string root, string a, string b)
		{
			return H(z,at,root,20,30,7,a,"well-nw") && H(z,at,root,49,59,7,a,"well-ne")
				&& H(z,at,root,20,30,17,b,"well-sw") && H(z,at,root,49,59,17,b,"well-se")
				&& V(z,at,root,30,4,6,b,"well-n1") && V(z,at,root,49,4,6,b,"well-n2")
				&& V(z,at,root,30,18,20,a,"well-s1") && V(z,at,root,49,18,20,a,"well-s2");
		}

		private static bool Corner(Zone z, KingdomArcologyCoordinate at, string root,
			int x1, int x2, int y1, int y2, string a, string b, string role)
		{
			return H(z,at,root,x1,x2,y2,a,role+":h")
				&& V(z,at,root,x2,y1,y2-1,b,role+":v");
		}

		private static bool Bay(Zone z, KingdomArcologyCoordinate at, string root,
			int x1, int x2, int y1, int y2, string a, string b, string role)
		{
			int cap = y1 < 10 ? y1 : y2;
			int start = cap == y1 ? y1 + 1 : y1;
			int end = cap == y2 ? y2 - 1 : y2;
			return H(z,at,root,x1,x2,cap,a,role+":cap")
				&& V(z,at,root,x1,start,end,b,role+":l")
				&& V(z,at,root,x2,start,end,b,role+":r");
		}

		private static bool H(Zone z, KingdomArcologyCoordinate at, string root,
			int x1, int x2, int y, string blueprint, string role)
		{
			for (int x = x1; x <= x2; x++)
				if (!Add(z,at,root,x,y,blueprint,"fabric:"+role+":"+x)) return false;
			return true;
		}

		private static bool V(Zone z, KingdomArcologyCoordinate at, string root,
			int x, int y1, int y2, string blueprint, string role)
		{
			for (int y = y1; y <= y2; y++)
				if (!Add(z,at,root,x,y,blueprint,"fabric:"+role+":"+y)) return false;
			return true;
		}

		private static bool AddProgrammeProps(Zone z, KingdomArcologyCoordinate at,
			string root, string[] decor)
		{
			return Add(z,at,root,11,5,decor[0],"programme:nw")
				&& Add(z,at,root,68,5,decor[1],"programme:ne")
				&& Add(z,at,root,11,19,decor[2],"programme:sw")
				&& Add(z,at,root,68,19,decor[0],"programme:se");
		}

		private static bool AddRouteLights(Zone z, KingdomArcologyCoordinate at, string root)
		{
			return Add(z,at,root,10,10,"Techlight1","light:west")
				&& Add(z,at,root,30,10,"Techlight1","light:midwest")
				&& Add(z,at,root,49,14,"Techlight1","light:mideast")
				&& Add(z,at,root,69,14,"Techlight1","light:east")
				&& Add(z,at,root,40,7,"Techlight1","light:north")
				&& Add(z,at,root,40,17,"Techlight1","light:south");
		}

		private static bool Add(Zone z, KingdomArcologyCoordinate at, string root,
			int x, int y, string blueprint, string role)
		{
			return KingdomHostedArcologyBuilder.AddStable(z,x,y,blueprint,root,
				KingdomHostedArcologyTopology.StableRole(at.X,at.Y,at.Z,role)) != null;
		}

		private static string PrimaryFor(int z) { return z == 9 ? "r_KingdomStructureLowConcrete"
			: z == 10 ? "r_KingdomStructureHalfStone" : "r_KingdomStructureLowMetalScreen"; }
		private static string AccentFor(int z) { return z == 9 ? "r_KingdomStructureHalfStone"
			: z == 10 ? "r_KingdomStructureLowConcrete" : "r_KingdomStructureRustedMetalWall"; }
		private static string ArchetypeName(int kind) { return new string[] { "cellular", "nave",
			"comb", "courts", "terraces", "workbays", "aisles", "branches", "lightwell" }[kind]; }
		private static KingdomArcologyFixtureSpec F(int x,int y,string bp,string role)
		{ return new KingdomArcologyFixtureSpec(x,y,bp,role); }
		private static string[] Set(string a,string b,string c) { return new string[] { a,b,c }; }

		private static string[] DecorFor(KingdomArcologyProgramme p)
		{
			switch (p)
			{
				case KingdomArcologyProgramme.SeedArchive: return Set("r_KingdomArcologySeedCase","r_KingdomArcologySpectrumLamp","r_KingdomArcologyCeramicBed");
				case KingdomArcologyProgramme.SpectrumGallery: return Set("r_KingdomArcologySpectrumLamp","r_KingdomArcologyCeramicBed","r_KingdomArcologySeedCase");
				case KingdomArcologyProgramme.CondenserWalk: return Set("r_KingdomArcologyCondenserShell","r_KingdomArcologyConduit","r_KingdomArcologySpectrumLamp");
				case KingdomArcologyProgramme.NurseryBowers: return Set("r_KingdomArcologyCeramicBed","r_KingdomArcologySpectrumLamp","r_KingdomArcologyGraftingStand");
				case KingdomArcologyProgramme.HydroponicTerrace: return Set("r_KingdomArcologyCeramicBed","r_KingdomArcologyConduit","r_KingdomArcologySpectrumLamp");
				case KingdomArcologyProgramme.GraftingHouse: return Set("r_KingdomArcologyGraftingStand","r_KingdomArcologyCeramicBed","r_KingdomArcologySeedCase");
				case KingdomArcologyProgramme.DryingGallery: return Set("r_KingdomArcologyDryingRack","r_KingdomArcologySeedCase","r_KingdomArcologySpectrumLamp");
				case KingdomArcologyProgramme.IrrigationGallery: return Set("r_KingdomArcologyConduit","r_KingdomArcologyCondenserShell","r_KingdomArcologyCeramicBed");
				case KingdomArcologyProgramme.LightwellGarden: return Set("r_KingdomArcologySpectrumLamp","r_KingdomArcologyGraftingStand","r_KingdomArcologyCeramicBed");
				case KingdomArcologyProgramme.PublicKitchen: return Set("r_KingdomArcologyColdRange","r_KingdomFixtureTableStone","r_KingdomFixtureBenchTimber");
				case KingdomArcologyProgramme.TeachingHall: return Set("r_KingdomFixtureBenchTimber","r_KingdomFixtureTableTimber","r_KingdomFixtureChairTimber");
				case KingdomArcologyProgramme.Infirmary: return Set("r_KingdomArcologyInfirmaryCouch","r_KingdomFixtureTableStone","r_KingdomArcologyServiceCabinet");
				case KingdomArcologyProgramme.Exchange: return Set("r_KingdomFixtureTableTimber","r_KingdomFixtureBenchTimber","r_KingdomArcologyFreightPallet");
				case KingdomArcologyProgramme.InheritedCourt: return Set("r_KingdomFixtureChairMarble","r_KingdomFixtureTableMarble","r_KingdomFixtureBenchTimber");
				case KingdomArcologyProgramme.Assembly: return Set("r_KingdomFixtureBenchTimber","r_KingdomFixtureChairStone","r_KingdomFixtureTableStone");
				case KingdomArcologyProgramme.Baths: return Set("r_KingdomArcologyDryBasin","r_KingdomFixtureBenchTimber","r_KingdomArcologyScrubBank");
				case KingdomArcologyProgramme.GuestHall: return Set("r_KingdomArcologyDormantBunk","r_KingdomFixtureChairTimber","r_KingdomFixtureTableTimber");
				case KingdomArcologyProgramme.GuardedApproach: return Set("r_KingdomArcologyWatchPost","r_KingdomFixtureBenchTimber","r_KingdomArcologyServiceCabinet");
				case KingdomArcologyProgramme.LodgingWard: return Set("r_KingdomArcologyDormantBunk","r_KingdomArcologyServiceCabinet","r_KingdomArcologyWatchPost");
				case KingdomArcologyProgramme.CisternPlant: return Set("r_KingdomArcologyCondenserShell","r_KingdomArcologyConduit","r_KingdomArcologyScrubBank");
				case KingdomArcologyProgramme.Stores: return Set("r_KingdomArcologyServiceCabinet","r_KingdomArcologyFreightPallet","r_KingdomArcologyWatchPost");
				case KingdomArcologyProgramme.MaintenanceConcourse: return Set("r_KingdomArcologyServiceCabinet","r_KingdomArcologyRepairStand","r_KingdomArcologyConduit");
				case KingdomArcologyProgramme.FabricationBay: return Set("r_KingdomArcologyRepairStand","r_KingdomArcologyFreightPallet","r_KingdomArcologyServiceCabinet");
				case KingdomArcologyProgramme.FreightHall: return Set("r_KingdomArcologyFreightPallet","r_KingdomArcologyServiceCabinet","r_KingdomArcologyWatchPost");
				case KingdomArcologyProgramme.SanitationWorks: return Set("r_KingdomArcologyScrubBank","r_KingdomArcologyConduit","r_KingdomArcologyDryBasin");
				case KingdomArcologyProgramme.ServiceBarracks: return Set("r_KingdomArcologyDormantBunk","r_KingdomArcologyWatchPost","r_KingdomArcologyServiceCabinet");
				case KingdomArcologyProgramme.RepairGallery: return Set("r_KingdomArcologyRepairStand","r_KingdomArcologyServiceCabinet","r_KingdomArcologyFreightPallet");
				default: return null;
			}
		}
	}
}
