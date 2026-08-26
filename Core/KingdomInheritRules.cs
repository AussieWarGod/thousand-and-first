using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	internal enum KingdomInheritWorkState
	{
		Standing = 0,
		Derelict = 1,
		Rubble = 2,
		Memory = 3
	}

	internal enum KingdomInheritFault
	{
		None = 0,
		NullInput = 1,
		RowCountMismatch = 2,
		TooManyWorks = 3,
		InvalidKey = 4,
		ConditionOutOfRange = 5,
		CoordinateOutOfRange = 6,
		RelativeRange = 7,
		InvalidState = 8,
		InterregnumRollOutOfRange = 9,
		ImpossibleFootprint = 10,
		Overlap = 11,
		NoEntry = 12,
		Malformed = 13
	}

	[Flags]
	internal enum KingdomInheritEngineCheck
	{
		None = 0,
		ConnectionCell = 1,
		Terrain = 2,
		ExistingObjects = 4,
		Stairs = 8,
		EntryToHeartPath = 16
	}

	internal sealed class KingdomInheritWork
	{
		internal readonly string Key;

		internal readonly int X;

		internal readonly int Y;

		internal readonly int Condition;

		internal readonly KingdomInheritWorkState State;

		internal readonly string ArchitectureSnapshot;

		internal readonly string ArchitectureHash;

		internal KingdomInheritWork(string Key, int X, int Y, int Condition, KingdomInheritWorkState State)
			: this(Key, X, Y, Condition, State, "", "")
		{
		}

		internal KingdomInheritWork(string Key, int X, int Y, int Condition,
			KingdomInheritWorkState State, string ArchitectureSnapshot, string ArchitectureHash)
		{
			this.Key = Key ?? "";
			this.X = X;
			this.Y = Y;
			this.Condition = Condition;
			this.State = State;
			this.ArchitectureSnapshot = ArchitectureSnapshot ?? "";
			this.ArchitectureHash = ArchitectureHash ?? "";
		}
	}

	internal sealed class KingdomInheritPlan
	{
		private readonly KingdomInheritWork[] _works;

		internal readonly int Width;

		internal readonly int Height;

		internal int Count
		{
			get { return _works.Length; }
		}

		internal KingdomInheritPlan(KingdomInheritWork[] Works, int Width, int Height)
		{
			_works = Works ?? new KingdomInheritWork[0];
			this.Width = Width;
			this.Height = Height;
		}

		internal KingdomInheritWork WorkAt(int Index)
		{
			return (Index >= 0 && Index < _works.Length) ? _works[Index] : null;
		}
	}

	internal sealed class KingdomInheritPlacement
	{
		private readonly KingdomInheritWork[] _works;

		internal readonly int EntryX;

		internal readonly int EntryY;

		internal readonly int CairnX;

		internal readonly int CairnY;

		internal readonly int HeartX;

		internal readonly int HeartY;

		internal readonly KingdomInheritEngineCheck RemainingEngineChecks;

		private readonly KingdomInheritWork[] _streets;

		internal readonly int SpatialVersion;

		internal int Count
		{
			get { return _works.Length; }
		}

		internal int StreetCount { get { return _streets.Length; } }

		internal KingdomInheritPlacement(KingdomInheritWork[] Works, int EntryX, int EntryY,
			int CairnX, int CairnY, int HeartX, int HeartY, KingdomInheritEngineCheck RemainingEngineChecks)
			: this(Works, EntryX, EntryY, CairnX, CairnY, HeartX, HeartY,
				RemainingEngineChecks, 0, null, null)
		{
		}

		internal KingdomInheritPlacement(KingdomInheritWork[] Works, int EntryX, int EntryY,
			int CairnX, int CairnY, int HeartX, int HeartY,
			KingdomInheritEngineCheck RemainingEngineChecks, int SpatialVersion,
			IList<int> StreetX, IList<int> StreetY)
		{
			_works = Works ?? new KingdomInheritWork[0];
			this.EntryX = EntryX;
			this.EntryY = EntryY;
			this.CairnX = CairnX;
			this.CairnY = CairnY;
			this.HeartX = HeartX;
			this.HeartY = HeartY;
			this.RemainingEngineChecks = RemainingEngineChecks;
			this.SpatialVersion = SpatialVersion;
			int count = StreetX == null || StreetY == null ? 0
				: Math.Min(StreetX.Count, StreetY.Count);
			_streets = new KingdomInheritWork[count];
			for (int i = 0; i < count; i++)
				_streets[i] = new KingdomInheritWork("inherit.street", StreetX[i], StreetY[i],
					0, KingdomInheritWorkState.Memory);
		}

		internal KingdomInheritWork WorkAt(int Index)
		{
			return (Index >= 0 && Index < _works.Length) ? _works[Index] : null;
		}

		internal int StreetXAt(int Index) { return _streets[Index].X; }

		internal int StreetYAt(int Index) { return _streets[Index].Y; }
	}

	internal static class KingdomInheritRules
	{
		private sealed class Definition
		{
			internal readonly string Key;
			internal readonly string Blueprint;
			internal readonly int Width;
			internal readonly int Height;

			internal Definition(string Key, string Blueprint, int Width, int Height)
			{
				this.Key = Key;
				this.Blueprint = Blueprint;
				this.Width = Width;
				this.Height = Height;
			}
		}

		private sealed class Candidate
		{
			internal string Key;
			internal int X;
			internal int Y;
			internal int Condition;
			internal KingdomInheritWorkState State;
			internal string ArchitectureSnapshot;
			internal string ArchitectureHash;
		}

		private struct Rect
		{
			internal int X1;
			internal int Y1;
			internal int X2;
			internal int Y2;
		}

		internal const int MaxWorks = 40;

		internal const int TargetWidth = 80;

		internal const int TargetHeight = 25;

		internal const int SafeMargin = 2;

		// Source construction owns the whole margin-two interior (76x21). The entry is
		// outside that interior and the cairn/inside pair is selected from unoccupied
		// cells, so inheritance must not silently shrink the lawful source envelope.
		internal const int WorkMargin = SafeMargin;

		internal const int MaxSourceCoordinateMagnitude = 1000000;

		internal const int MaxRelativeSpan = 255;

		internal const int HeldConditionCeiling = 80;

		internal const int FadedStandingConditionCeiling = 65;

		internal const int FadedDerelictConditionCeiling = 45;

		internal const int FadedDerelictPercent = 25;

		internal const int AbandonedDerelictConditionCeiling = 35;

		internal const int RuinsDerelictConditionCeiling = 20;

		internal const string RubbleKey = "inherit.rubble";

		internal const string MemoryKey = "inherit.memory";

		internal const string FounderCairnKey = "inherit.cairn";

		internal static readonly KingdomInheritEngineCheck RemainingEngineChecks =
			KingdomInheritEngineCheck.ConnectionCell
			| KingdomInheritEngineCheck.Terrain
			| KingdomInheritEngineCheck.ExistingObjects
			| KingdomInheritEngineCheck.Stairs
			| KingdomInheritEngineCheck.EntryToHeartPath;

		private static readonly Definition[] Definitions = new Definition[]
		{
			D("inherit.rubble", "r_KingdomRubbleWall", 1, 1),
			D("inherit.memory", "r_KingdomCairn", 1, 1),
			D("inherit.cairn", "r_KingdomCairn", 1, 1),
			D("tent", "r_KingdomTent", 3, 2),
			D("tentrow", "r_KingdomTentRow", 5, 2),
			D("hut", "r_KingdomHut", 4, 3),
			D("hutyard", "r_KingdomHutYard", 5, 4),
			D("house", "r_KingdomHouse", 8, 6),
			D("housecourt", "r_KingdomHouseCourt", 8, 6),
			D("terrace", "r_KingdomTerrace", 12, 9),
			D("finehouse", "r_KingdomFineHouse", 8, 6),
			D("manor", "r_KingdomManor", 12, 9),
			D("court", "r_KingdomCourt", 20, 14),
			D("saltpan", "r_KingdomSaltPan", 5, 4),
			D("saltterrace", "r_KingdomSaltTerrace", 5, 4),
			D("catchment", "r_KingdomCatchment", 5, 4),
			D("catchmentbank", "r_KingdomCatchmentBank", 5, 4),
			D("airwellcourt", "r_KingdomAirWellCourt", 8, 6),
			D("airwellfield", "r_KingdomAirWellField", 12, 9),
			D("weeptap", "r_KingdomWeepTap", 5, 4),
			D("weepgallery", "r_KingdomWeepGallery", 8, 6),
			D("cistern", "r_KingdomGreatCistern", 8, 6),
			D("cisternvault", "r_KingdomCisternVault", 8, 6),
			D("reservoir", "r_KingdomReservoir", 12, 9),
			D("waterworks", "r_KingdomWaterworks", 20, 14),
			D("condensery", "r_KingdomCondensery", 20, 14),
			D("larder", "r_KingdomLarder", 5, 4),
			D("plot", "r_KingdomPlot", 5, 4),
			D("plotrows", "r_KingdomPlotRows", 5, 4),
			D("field", "r_KingdomField", 8, 6),
			D("fieldrows", "r_KingdomFieldRows", 8, 6),
			D("granary", "r_KingdomGranary", 8, 6),
			D("grange", "r_KingdomGrange", 12, 9),
			D("homefarm", "r_KingdomHomeFarm", 20, 14),
			D("toolshed", "r_KingdomToolShed", 5, 4),
			D("chargingpost", "r_KingdomChargingPost", 5, 4),
			D("smithy", "r_KingdomSmithy", 8, 6),
			D("forge", "r_KingdomForge", 8, 6),
			D("grindmill", "r_KingdomGrindMill", 8, 6),
			D("workshop", "r_KingdomWorkshop", 8, 6),
			D("sawyeryard", "r_KingdomSawyerYard", 8, 6),
			D("masonyard", "r_KingdomMasonYard", 8, 6),
			D("smelter", "r_KingdomSmelter", 8, 6),
			D("oven", "r_KingdomOven", 5, 4),
			D("bench", "r_KingdomBench", 5, 4),
			D("hall", "r_KingdomHall", 8, 6),
			D("bazaar", "r_KingdomBazaar", 8, 6),
			D("bathhouse", "r_KingdomBathhouse", 12, 9),
			D("heartbasin", "r_KingdomRiteGround", 3, 3),
			D("heartwaterstone", "r_KingdomWaterstone", 6, 4),
			D("heartmoot", "r_KingdomMootYard", 8, 6),
			D("heartcourt", "r_KingdomGreatCourt", 16, 11),
			D("shrine", "r_KingdomShrine", 5, 4),
			D("shrinegarth", "r_KingdomShrineGarth", 5, 4),
			D("temple", "r_KingdomTemple", 12, 9),
			D("scriptorium", "r_KingdomScriptorium", 8, 6),
			D("palisade", "r_KingdomPalisade", 1, 1),
			D("rampart", "r_KingdomRampart", 1, 1),
			D("watchtower", "r_KingdomWatchtower", 1, 1),
			D("gatehouse", "r_KingdomGatehouse", 1, 1),
			D("barracks", "r_KingdomBarracks", 12, 9),
			D("cairn", "r_KingdomCairn", 5, 4),
			D("mill", "r_KingdomMill", 5, 4),
			D("waterwheel", "r_KingdomWaterWheel", 5, 4),
			D("sailvane", "r_KingdomSailvane", 5, 4),
			D("saltstore", "r_KingdomSaltStore", 8, 6),
			D("watermain", "r_KingdomWaterMain", 1, 1),
			D("brinemain", "r_KingdomBrineMain", 1, 1),
			D("liquidcrossing", "r_KingdomLiquidCrossing", 1, 1),
			D("watertap", "r_KingdomWaterTap", 1, 1),
			D("brinetap", "r_KingdomBrineTap", 1, 1),
			D("ydroofline", "r_KingdomHutYard", 5, 4),
			D("hindrenweavehall", "r_KingdomSmithy", 8, 6),
			D("mudhut", "r_KingdomMudHut", 4, 3),
			D("mudhutcourt", "r_KingdomMudHutCourt", 5, 4),
			D("caravanserai", "r_KingdomCaravanserai", 12, 9),
			D("stiltrow", "r_KingdomStiltRow", 8, 6),
			D("gravegrove", "r_KingdomGraveGrove", 5, 4),
			D("sporecellar", "r_KingdomSporeCellar", 8, 6),
			D("caproof", "r_KingdomCapRoof", 4, 3),
			D("bonefold", "r_KingdomBoneFold", 5, 4),
			D("sacramentcourt", "r_KingdomSacramentCourt", 12, 9),
			D("blockhut", "r_KingdomBlockHut", 4, 3),
			D("blockyard", "r_KingdomBlockYard", 5, 4),
			D("rubblewall", "r_KingdomRubbleWall", 1, 1),
			D("carvedcell", "r_KingdomCarvedCell", 4, 3),
			D("carvedgallery", "r_KingdomCarvedGallery", 8, 6),
			D("fungalvault", "r_KingdomFungalVault", 8, 6),
			D("vaultgalleries", "r_KingdomVaultGalleries", 12, 9),
			D("deepcut", "r_KingdomDeepCut", 8, 6),
			D("nichetomb", "r_KingdomNicheTomb", 5, 4),
			D("delve", "r_KingdomDelve", 8, 6),
			D("underbench", "r_KingdomUnderBench", 8, 6),
			D("reliquary", "r_KingdomReliquary", 12, 9),
			D("factorhouse", "r_KingdomFactorHouse", 8, 6),
			D("butcherslab", "r_KingdomButcherSlab", 5, 4),
			D("vathouse", "r_KingdomVatHouse", 8, 6),
			D("graftinghall", "r_KingdomGraftingHall", 12, 9),
			D("chimerictheatre", "r_KingdomChimericTheatre", 20, 14),
			D("becomingannexe", "r_KingdomBecomingAnnexe", 20, 14),
			D("mirrorgate", "r_KingdomMirrorGate", 11, 8),
			D("crownhall", "r_KingdomCrownHall", 14, 10),
			D("arcology", "r_KingdomArcology", 20, 14),
			D("arcologyward", "r_KingdomArcologyWard", 12, 9),
			D("arcologyterrace", "r_KingdomArcologyTerrace", 8, 6),
			D("hallsurgery", "r_KingdomHallSurgery", 8, 6),
			D("registryoffice", "r_KingdomRegistryOffice", 8, 6)
		};

		private static Definition D(string Key, string Blueprint, int Width, int Height)
		{
			return new Definition(Key, Blueprint, Width, Height);
		}

		internal static bool IsStableSemanticKey(string Key)
		{
			if (string.IsNullOrEmpty(Key) || Key.Length > 64)
			{
				return false;
			}
			for (int i = 0; i < Key.Length; i++)
			{
				char c = Key[i];
				if ((c < 'a' || c > 'z') && (c < '0' || c > '9') && c != '.' && c != '-' && c != '_')
				{
					return false;
				}
			}
			return true;
		}

		internal static bool IsInheritableKey(string Key)
		{
			return Find(Key) != null;
		}

		internal static bool IsFoundingHeartKey(string Key)
		{
			return Key == "heartbasin" || Key == "heartwaterstone"
				|| Key == "heartmoot" || Key == "heartcourt";
		}

		internal static bool TryResolveBlueprint(string Key, out string Blueprint)
		{
			Blueprint = null;
			Definition definition = Find(Key);
			if (definition == null || !IsTafBlueprint(definition.Blueprint))
			{
				return false;
			}
			Blueprint = definition.Blueprint;
			return true;
		}

		/// <summary>
		/// Converts the blueprint carried by a live city work row into the stable key
		/// written to a Seal. Definitions are ordered canonical-first: inheritance-only
		/// marker rows are skipped, then the base catalogue key wins over later cultural
		/// aliases that intentionally share a blueprint.
		/// </summary>
		internal static bool TrySemanticKeyForBlueprint(string Blueprint, out string Key)
		{
			Key = null;
			if (!IsTafBlueprint(Blueprint) || Blueprint.Length > 96)
			{
				return false;
			}
			for (int i = 0; i < Definitions.Length; i++)
			{
				Definition definition = Definitions[i];
				if (definition.Key.StartsWith("inherit.", StringComparison.Ordinal)
					|| !string.Equals(definition.Blueprint, Blueprint, StringComparison.Ordinal))
				{
					continue;
				}
				Key = definition.Key;
				return true;
			}
			return false;
		}

		internal static bool TryFootprint(string Key, out int Width, out int Height)
		{
			Width = 0;
			Height = 0;
			Definition definition = Find(Key);
			if (definition == null)
			{
				return false;
			}
			Width = definition.Width;
			Height = definition.Height;
			return Width > 0 && Height > 0;
		}

		internal static bool TryNormalize(IList<string> Keys, IList<int> X, IList<int> Y,
			IList<int> Conditions, out KingdomInheritPlan Plan, out KingdomInheritFault Fault)
		{
			Plan = EmptyPlan();
			Fault = KingdomInheritFault.None;
			try
			{
				return TryNormalizeCore(Keys, X, Y, Conditions, out Plan, out Fault);
			}
			catch
			{
				Plan = EmptyPlan();
				Fault = KingdomInheritFault.Malformed;
				return false;
			}
		}

		private static bool TryNormalizeCore(IList<string> Keys, IList<int> X, IList<int> Y,
			IList<int> Conditions, out KingdomInheritPlan Plan, out KingdomInheritFault Fault)
		{
			Plan = EmptyPlan();
			Fault = KingdomInheritFault.None;
			if (Keys == null || X == null || Y == null || Conditions == null)
			{
				Fault = KingdomInheritFault.NullInput;
				return false;
			}
			int count = Keys.Count;
			if (X.Count != count || Y.Count != count || Conditions.Count != count)
			{
				Fault = KingdomInheritFault.RowCountMismatch;
				return false;
			}
			if (count > MaxWorks)
			{
				Fault = KingdomInheritFault.TooManyWorks;
				return false;
			}
			Candidate[] candidates = new Candidate[count];
			for (int i = 0; i < count; i++)
			{
				string key = Keys[i];
				if (!IsStableSemanticKey(key))
				{
					Fault = KingdomInheritFault.InvalidKey;
					return false;
				}
				int condition = Conditions[i];
				if (condition < 0 || condition > 100)
				{
					Fault = KingdomInheritFault.ConditionOutOfRange;
					return false;
				}
				int x = X[i];
				int y = Y[i];
				if (!SourceCoordinate(x) || !SourceCoordinate(y))
				{
					Fault = KingdomInheritFault.CoordinateOutOfRange;
					return false;
				}
				bool known = IsInheritableKey(key) && key != RubbleKey
					&& key != MemoryKey && key != FounderCairnKey;
				candidates[i] = new Candidate
				{
					Key = known ? key : MemoryKey,
					X = x,
					Y = y,
					Condition = known ? condition : 0,
					State = known ? KingdomInheritWorkState.Standing : KingdomInheritWorkState.Memory
				};
			}
			Sort(candidates);
			int unique = Deduplicate(candidates);
			DegradeAmbiguousFootprints(candidates, unique);
			return TryBuildPlan(candidates, unique, out Plan, out Fault);
		}

		/// <summary>
		/// Old city books prove anchors, not the footprint version under which a work
		/// was built. Adjacent single-cell migrated works can therefore overlap only
		/// when interpreted through today's catalogue dimensions. Every member of such
		/// an ambiguous overlap becomes a one-cell memory at its exact old anchor; one
		/// local uncertainty must never invalidate the whole legacy.
		/// </summary>
		private static void DegradeAmbiguousFootprints(Candidate[] Candidates, int Count)
		{
			bool[] ambiguous = new bool[Count];
			for (int i = 0; i < Count; i++)
			{
				Rect current;
				if (!TryRect(Candidates[i].Key, Candidates[i].X, Candidates[i].Y, out current))
				{
					ambiguous[i] = true;
					continue;
				}
				for (int j = 0; j < i; j++)
				{
					Rect earlier;
					if (!TryRect(Candidates[j].Key, Candidates[j].X, Candidates[j].Y, out earlier)
						|| Overlaps(current, earlier))
					{
						ambiguous[i] = true;
						ambiguous[j] = true;
					}
				}
			}
			for (int i = 0; i < Count; i++)
			{
				if (ambiguous[i])
				{
					Candidates[i].Key = MemoryKey;
					Candidates[i].Condition = 0;
					Candidates[i].State = KingdomInheritWorkState.Memory;
				}
			}
		}

		internal static bool TryApplyState(KingdomInheritPlan Source, KingdomRules.InheritedState State,
			int InterregnumRoll,
			out KingdomInheritPlan Plan, out KingdomInheritFault Fault)
		{
			Plan = EmptyPlan();
			Fault = KingdomInheritFault.None;
			try
			{
				if (Source == null)
				{
					Fault = KingdomInheritFault.NullInput;
					return false;
				}
				if (!KingdomRules.IsKnownState(State))
				{
					Fault = KingdomInheritFault.InvalidState;
					return false;
				}
				if (InterregnumRoll < 0 || InterregnumRoll > 99)
				{
					Fault = KingdomInheritFault.InterregnumRollOutOfRange;
					return false;
				}
				bool[] fadedDerelict = (State == KingdomRules.InheritedState.Faded)
					? Select(Source, FadedDerelictPercent, InterregnumRoll, PreferHeart: false)
					: null;
				bool[] ruinsStanding = (State == KingdomRules.InheritedState.Ruins)
					? Select(Source, KingdomRules.StandingPercent(State, InterregnumRoll), InterregnumRoll, PreferHeart: true)
					: null;
				Candidate[] transformed = new Candidate[Source.Count];
				for (int i = 0; i < Source.Count; i++)
				{
					KingdomInheritWork work = Source.WorkAt(i);
					if (work == null)
					{
						Fault = KingdomInheritFault.Malformed;
						return false;
					}
					Candidate candidate = new Candidate
					{
						Key = work.Key,
						X = work.X,
						Y = work.Y,
						Condition = work.Condition,
						State = work.State,
						ArchitectureSnapshot = work.ArchitectureSnapshot,
						ArchitectureHash = work.ArchitectureHash
					};
					if (work.State != KingdomInheritWorkState.Memory)
					{
						if (State == KingdomRules.InheritedState.Held)
						{
							candidate.Condition = Min(work.Condition, HeldConditionCeiling);
							candidate.State = KingdomInheritWorkState.Standing;
						}
						else if (State == KingdomRules.InheritedState.Faded)
						{
							bool derelict = fadedDerelict[i];
							candidate.Condition = Min(work.Condition, derelict
								? FadedDerelictConditionCeiling
								: FadedStandingConditionCeiling);
							candidate.State = derelict ? KingdomInheritWorkState.Derelict : KingdomInheritWorkState.Standing;
						}
						else if (KingdomRules.AllWorksSurvive(State))
						{
							candidate.Condition = Min(work.Condition, AbandonedDerelictConditionCeiling);
							candidate.State = KingdomInheritWorkState.Derelict;
						}
						else if (ruinsStanding[i])
						{
							candidate.Condition = Min(work.Condition, RuinsDerelictConditionCeiling);
							candidate.State = KingdomInheritWorkState.Derelict;
						}
						else
						{
							candidate.Key = RubbleKey;
							candidate.Condition = 0;
							candidate.State = KingdomInheritWorkState.Rubble;
							candidate.ArchitectureSnapshot = "";
							candidate.ArchitectureHash = "";
						}
					}
					transformed[i] = candidate;
				}
				Sort(transformed);
				return TryBuildPlan(transformed, transformed.Length, out Plan, out Fault);
			}
			catch
			{
				Plan = EmptyPlan();
				Fault = KingdomInheritFault.Malformed;
				return false;
			}
		}

		internal static bool TryFit(KingdomInheritPlan Plan, int Width, int Height,
			out KingdomInheritPlacement Placement, out KingdomInheritFault Fault)
		{
			Placement = null;
			Fault = KingdomInheritFault.None;
			try
			{
				if (Plan == null)
				{
					Fault = KingdomInheritFault.NullInput;
					return false;
				}
				if (Width != TargetWidth || Height != TargetHeight)
				{
					Fault = KingdomInheritFault.ImpossibleFootprint;
					return false;
				}
				int usableWidth = Width - WorkMargin * 2;
				int usableHeight = Height - WorkMargin * 2;
				if (Plan.Width > usableWidth || Plan.Height > usableHeight)
				{
					Fault = KingdomInheritFault.ImpossibleFootprint;
					return false;
				}
				int offsetX = WorkMargin + (usableWidth - Plan.Width) / 2;
				int offsetY = WorkMargin + (usableHeight - Plan.Height) / 2;
				KingdomInheritWork[] translated = new KingdomInheritWork[Plan.Count];
				Rect[] occupied = new Rect[Plan.Count];
				for (int i = 0; i < Plan.Count; i++)
				{
					KingdomInheritWork work = Plan.WorkAt(i);
					if (work == null || !TryRect(work.Key, work.X + offsetX, work.Y + offsetY, out occupied[i]))
					{
						Fault = KingdomInheritFault.ImpossibleFootprint;
						return false;
					}
					if (occupied[i].X1 < WorkMargin || occupied[i].Y1 < WorkMargin
						|| occupied[i].X2 >= Width - WorkMargin || occupied[i].Y2 >= Height - WorkMargin)
					{
						Fault = KingdomInheritFault.ImpossibleFootprint;
						return false;
					}
					for (int j = 0; j < i; j++)
					{
						if (Overlaps(occupied[i], occupied[j]))
						{
							Fault = KingdomInheritFault.Overlap;
							return false;
						}
					}
					translated[i] = new KingdomInheritWork(work.Key, work.X + offsetX, work.Y + offsetY,
						work.Condition, work.State);
				}
				int heartX;
				int heartY;
				ChooseHeart(translated, Plan.Width, Plan.Height, offsetX, offsetY, out heartX, out heartY);
				int cairnX;
				int cairnY;
				int entryX;
				int entryY;
				if (!TryEntry(occupied, heartX, heartY, Width, Height, out cairnX, out cairnY, out entryX, out entryY))
				{
					Fault = KingdomInheritFault.NoEntry;
					return false;
				}
				KingdomInheritWork[] result = new KingdomInheritWork[translated.Length + 1];
				for (int i = 0; i < translated.Length; i++)
				{
					result[i] = translated[i];
				}
				result[translated.Length] = new KingdomInheritWork(FounderCairnKey, cairnX, cairnY, 0,
					KingdomInheritWorkState.Memory);
				if (translated.Length == 0)
				{
					heartX = cairnX;
					heartY = cairnY;
				}
				Placement = new KingdomInheritPlacement(result, entryX, entryY, cairnX, cairnY,
					heartX, heartY, RemainingEngineChecks);
				return true;
			}
			catch
			{
				Placement = null;
				Fault = KingdomInheritFault.Malformed;
				return false;
			}
		}

		internal static bool TryPrepare(IList<string> Keys, IList<int> X, IList<int> Y,
			IList<int> Conditions, KingdomRules.InheritedState State, int InterregnumRoll,
			out KingdomInheritPlacement Placement, out KingdomInheritFault Fault)
		{
			Placement = null;
			KingdomInheritPlan normalized;
			if (!TryNormalize(Keys, X, Y, Conditions, out normalized, out Fault))
			{
				return false;
			}
			KingdomInheritPlan inherited;
			if (!TryApplyState(normalized, State, InterregnumRoll, out inherited, out Fault))
			{
				return false;
			}
			return TryFit(inherited, TargetWidth, TargetHeight, out Placement, out Fault);
		}

		/// <summary>Current seals retain their witnessed zone-relative frame. Legacy spatial-v0
		/// records continue through the anchor-proxy path above.</summary>
		internal static bool TryPrepare(KingdomSealRecord Record,
			KingdomRules.InheritedState State, int InterregnumRoll,
			out KingdomInheritPlacement Placement, out KingdomInheritFault Fault)
		{
			Placement = null;
			Fault = KingdomInheritFault.None;
			if (Record == null)
			{
				Fault = KingdomInheritFault.NullInput;
				return false;
			}
			if (Record.SpatialVersion == 0)
				return TryPrepare(Record.WorkKeys, Record.WorkX, Record.WorkY,
					Record.WorkConditions, State, InterregnumRoll, out Placement, out Fault);
			return TryPrepareSpatial(Record, State, InterregnumRoll, out Placement, out Fault);
		}

		private static bool TryPrepareSpatial(KingdomSealRecord Record,
			KingdomRules.InheritedState State, int InterregnumRoll,
			out KingdomInheritPlacement Placement, out KingdomInheritFault Fault)
		{
			Placement = null;
			Fault = KingdomInheritFault.None;
			KingdomInheritanceSpatialFault spatialFault;
			if (!KingdomInheritanceSpatialRules.TryValidate(Record.WorkKeys, Record.WorkX,
				Record.WorkY, Record.WorkConditions, Record.WorkSnapshots,
				Record.WorkSnapshotHashes, Record.SpatialWidth, Record.SpatialHeight,
				Record.SpatialEntrySide, Record.SpatialEntryX, Record.SpatialEntryY,
				Record.StreetX, Record.StreetY, out spatialFault))
			{
				Fault = KingdomInheritFault.Malformed;
				return false;
			}
			if (!KingdomRules.IsKnownState(State))
			{
				Fault = KingdomInheritFault.InvalidState;
				return false;
			}
			if (InterregnumRoll < 0 || InterregnumRoll > 99)
			{
				Fault = KingdomInheritFault.InterregnumRollOutOfRange;
				return false;
			}

			KingdomInheritWork[] source = new KingdomInheritWork[Record.WorkKeys.Count];
			for (int i = 0; i < source.Length; i++)
			{
				string key = Record.WorkKeys[i];
				string encoded = Record.WorkSnapshots[i];
				string hash = Record.WorkSnapshotHashes[i];
				KingdomInheritWorkState workState = KingdomInheritWorkState.Standing;
				if (!IsInheritableKey(key))
				{
					key = MemoryKey;
					encoded = "";
					hash = "";
					workState = KingdomInheritWorkState.Memory;
				}
				else if (encoded.Length > 0)
				{
					ArchitectureLayoutSnapshot snapshot;
					if (!KingdomArchitectureRules.TryDecodeSnapshot(encoded, out snapshot, out _))
					{
						Fault = KingdomInheritFault.Malformed;
						return false;
					}
					// A first-basin binding proves old authority, not permission to mint that
					// authority in another world. The whole work becomes a named memory.
					if (IsFoundingHeartKey(key)
						|| KingdomInheritanceSpatialRules.HasExistingAuthority(snapshot))
					{
						key = MemoryKey;
						encoded = "";
						hash = "";
						workState = KingdomInheritWorkState.Memory;
					}
				}
				else if (IsFoundingHeartKey(key))
				{
					key = MemoryKey;
					workState = KingdomInheritWorkState.Memory;
				}
				source[i] = new KingdomInheritWork(key, Record.WorkX[i], Record.WorkY[i],
					workState == KingdomInheritWorkState.Memory ? 0 : Record.WorkConditions[i],
					workState, encoded, hash);
			}
			KingdomInheritPlan sourcePlan = new KingdomInheritPlan(source,
				KingdomInheritanceSpatialRules.Width, KingdomInheritanceSpatialRules.Height);
			bool[] faded = State == KingdomRules.InheritedState.Faded
				? Select(sourcePlan, FadedDerelictPercent, InterregnumRoll, false) : null;
			bool[] ruins = State == KingdomRules.InheritedState.Ruins
				? Select(sourcePlan, KingdomRules.StandingPercent(State, InterregnumRoll),
					InterregnumRoll, true) : null;
			KingdomInheritWork[] transformed = new KingdomInheritWork[source.Length];
			for (int i = 0; i < source.Length; i++)
			{
				KingdomInheritWork work = source[i];
				string key = work.Key;
				int condition = work.Condition;
				KingdomInheritWorkState workState = work.State;
				string encoded = work.ArchitectureSnapshot;
				string hash = work.ArchitectureHash;
				if (workState != KingdomInheritWorkState.Memory)
				{
					if (State == KingdomRules.InheritedState.Held)
					{
						condition = Min(condition, HeldConditionCeiling);
						workState = KingdomInheritWorkState.Standing;
					}
					else if (State == KingdomRules.InheritedState.Faded)
					{
						bool derelict = faded[i];
						condition = Min(condition, derelict
							? FadedDerelictConditionCeiling : FadedStandingConditionCeiling);
						workState = derelict ? KingdomInheritWorkState.Derelict
							: KingdomInheritWorkState.Standing;
					}
					else if (KingdomRules.AllWorksSurvive(State))
					{
						condition = Min(condition, AbandonedDerelictConditionCeiling);
						workState = KingdomInheritWorkState.Derelict;
					}
					else if (ruins[i])
					{
						condition = Min(condition, RuinsDerelictConditionCeiling);
						workState = KingdomInheritWorkState.Derelict;
					}
					else
					{
						key = RubbleKey;
						condition = 0;
						workState = KingdomInheritWorkState.Rubble;
						encoded = "";
						hash = "";
					}
				}
				transformed[i] = new KingdomInheritWork(key, work.X, work.Y, condition,
					workState, encoded, hash);
			}

			Rect[] occupied = new Rect[transformed.Length];
			for (int i = 0; i < transformed.Length; i++)
				if (!TryPreparedRect(transformed[i], out occupied[i]))
				{
					Fault = KingdomInheritFault.ImpossibleFootprint;
					return false;
				}
			int heartX;
			int heartY;
			ChooseHeart(transformed, KingdomInheritanceSpatialRules.Width,
				KingdomInheritanceSpatialRules.Height, 0, 0, out heartX, out heartY);
			int cairnX;
			int cairnY;
			int entryX = Record.SpatialEntryX;
			int entryY = Record.SpatialEntryY;
			if (Record.StreetX.Count > 0)
			{
				if (!TryStreetCairn(occupied, Record.StreetX, Record.StreetY,
					heartX, heartY, out cairnX, out cairnY))
				{
					Fault = KingdomInheritFault.NoEntry;
					return false;
				}
			}
			else if (!TryEntry(occupied, heartX, heartY, TargetWidth, TargetHeight,
				out cairnX, out cairnY, out entryX, out entryY))
			{
				Fault = KingdomInheritFault.NoEntry;
				return false;
			}
			KingdomInheritWork[] result = new KingdomInheritWork[transformed.Length + 1];
			Array.Copy(transformed, result, transformed.Length);
			result[transformed.Length] = new KingdomInheritWork(FounderCairnKey,
				cairnX, cairnY, 0, KingdomInheritWorkState.Memory);
			if (transformed.Length == 0) { heartX = cairnX; heartY = cairnY; }
			Placement = new KingdomInheritPlacement(result, entryX, entryY, cairnX, cairnY,
				heartX, heartY, RemainingEngineChecks, Record.SpatialVersion,
				Record.StreetX, Record.StreetY);
			return true;
		}

		private static bool TryPreparedRect(KingdomInheritWork Work, out Rect Rect)
		{
			Rect = default(Rect);
			if (Work == null) return false;
			if (Work.ArchitectureSnapshot.Length == 0)
				return TryRect(Work.Key, Work.X, Work.Y, out Rect);
			ArchitectureLayoutSnapshot snapshot;
			KingdomInheritanceSpatialRules.Rect exact;
			if (!KingdomArchitectureRules.TryDecodeSnapshot(Work.ArchitectureSnapshot,
				out snapshot, out _) || !KingdomInheritanceSpatialRules.TrySnapshotRect(snapshot,
					Work.X, Work.Y, out exact)) return false;
			Rect.X1 = exact.X1;
			Rect.Y1 = exact.Y1;
			Rect.X2 = exact.X2;
			Rect.Y2 = exact.Y2;
			return true;
		}

		private static bool TryStreetCairn(Rect[] Occupied, IList<int> StreetX,
			IList<int> StreetY, int HeartX, int HeartY, out int CairnX, out int CairnY)
		{
			CairnX = 0;
			CairnY = 0;
			bool[,] street = new bool[TargetWidth, TargetHeight];
			for (int i = 0; i < StreetX.Count; i++) street[StreetX[i], StreetY[i]] = true;
			int best = int.MaxValue;
			int[] dx = new int[4] { 0, 1, 0, -1 };
			int[] dy = new int[4] { -1, 0, 1, 0 };
			for (int i = 0; i < StreetX.Count; i++)
			{
				for (int d = 0; d < 4; d++)
				{
					int x = StreetX[i] + dx[d];
					int y = StreetY[i] + dy[d];
					if (x < 1 || y < 1 || x >= TargetWidth - 1
						|| y >= TargetHeight - 1 || street[x, y]
						|| IsOccupied(Occupied, x, y)) continue;
					int score = Distance(x, y, HeartX, HeartY);
					if (score < best || (score == best
						&& (y < CairnY || (y == CairnY && x < CairnX))))
					{
						best = score;
						CairnX = x;
						CairnY = y;
					}
				}
			}
			return best != int.MaxValue;
		}

		internal static string FailureLine(KingdomInheritFault Fault)
		{
			switch (Fault)
			{
				case KingdomInheritFault.None:
					return "";
				case KingdomInheritFault.NullInput:
					return "the inherited street plan is missing";
				case KingdomInheritFault.RowCountMismatch:
					return "the inherited street plan has torn rows";
				case KingdomInheritFault.TooManyWorks:
					return "the inherited street plan carries too many works";
				case KingdomInheritFault.InvalidKey:
					return "the inherited street plan carries a malformed semantic key";
				case KingdomInheritFault.ConditionOutOfRange:
					return "the inherited street plan carries an impossible condition";
				case KingdomInheritFault.CoordinateOutOfRange:
					return "the inherited street plan carries an impossible old coordinate";
				case KingdomInheritFault.RelativeRange:
					return "the inherited street plan is too wide to normalize safely";
				case KingdomInheritFault.InvalidState:
					return "the inherited settlement state is unknown";
				case KingdomInheritFault.InterregnumRollOutOfRange:
					return "the inherited settlement carries an impossible interregnum draw";
				case KingdomInheritFault.ImpossibleFootprint:
					return "the inherited footprint cannot fit this eighty-by-twenty-five zone";
				case KingdomInheritFault.Overlap:
					return "two inherited works claim the same ground";
				case KingdomInheritFault.NoEntry:
					return "the inherited plan leaves no safe entry and cairn pair";
				default:
					return "the inherited street plan is malformed";
			}
		}

		private static Definition Find(string Key)
		{
			if (Key == null)
			{
				return null;
			}
			for (int i = 0; i < Definitions.Length; i++)
			{
				if (string.Equals(Definitions[i].Key, Key, StringComparison.Ordinal))
				{
					return Definitions[i];
				}
			}
			return null;
		}

		private static bool IsTafBlueprint(string Blueprint)
		{
			return Blueprint != null && Blueprint.StartsWith("r_Kingdom", StringComparison.Ordinal);
		}

		private static bool SourceCoordinate(int Coordinate)
		{
			return Coordinate >= -MaxSourceCoordinateMagnitude && Coordinate <= MaxSourceCoordinateMagnitude;
		}

		private static int Deduplicate(Candidate[] Candidates)
		{
			int write = 0;
			for (int i = 0; i < Candidates.Length; i++)
			{
				if (write > 0 && Candidates[write - 1].X == Candidates[i].X && Candidates[write - 1].Y == Candidates[i].Y)
				{
					continue;
				}
				Candidates[write++] = Candidates[i];
			}
			return write;
		}

		private static bool TryBuildPlan(Candidate[] Candidates, int Count,
			out KingdomInheritPlan Plan, out KingdomInheritFault Fault)
		{
			Plan = EmptyPlan();
			Fault = KingdomInheritFault.None;
			if (Count == 0)
			{
				return true;
			}
			long minX = long.MaxValue;
			long minY = long.MaxValue;
			long maxX = long.MinValue;
			long maxY = long.MinValue;
			for (int i = 0; i < Count; i++)
			{
				Rect rect;
				if (Candidates[i] == null || !TryRect(Candidates[i].Key, Candidates[i].X, Candidates[i].Y, out rect))
				{
					Fault = KingdomInheritFault.ImpossibleFootprint;
					return false;
				}
				if (rect.X1 < minX) minX = rect.X1;
				if (rect.Y1 < minY) minY = rect.Y1;
				if (rect.X2 > maxX) maxX = rect.X2;
				if (rect.Y2 > maxY) maxY = rect.Y2;
				for (int j = 0; j < i; j++)
				{
					Rect earlier;
					if (!TryRect(Candidates[j].Key, Candidates[j].X, Candidates[j].Y, out earlier))
					{
						Fault = KingdomInheritFault.ImpossibleFootprint;
						return false;
					}
					if (Overlaps(rect, earlier))
					{
						Fault = KingdomInheritFault.Overlap;
						return false;
					}
				}
			}
			long width = maxX - minX + 1L;
			long height = maxY - minY + 1L;
			if (width < 1L || height < 1L || width > MaxRelativeSpan || height > MaxRelativeSpan)
			{
				Fault = KingdomInheritFault.RelativeRange;
				return false;
			}
			KingdomInheritWork[] works = new KingdomInheritWork[Count];
			for (int i = 0; i < Count; i++)
			{
				long relativeX = (long)Candidates[i].X - minX;
				long relativeY = (long)Candidates[i].Y - minY;
				if (relativeX < 0L || relativeX > MaxRelativeSpan || relativeY < 0L || relativeY > MaxRelativeSpan)
				{
					Fault = KingdomInheritFault.RelativeRange;
					return false;
				}
				works[i] = new KingdomInheritWork(Candidates[i].Key, (int)relativeX, (int)relativeY,
					Candidates[i].Condition, Candidates[i].State,
					Candidates[i].ArchitectureSnapshot, Candidates[i].ArchitectureHash);
			}
			Plan = new KingdomInheritPlan(works, (int)width, (int)height);
			return true;
		}

		private static bool TryRect(string Key, int AnchorX, int AnchorY, out Rect Rect)
		{
			Rect = default(Rect);
			Definition definition = Find(Key);
			if (definition == null || definition.Width < 1 || definition.Height < 1)
			{
				return false;
			}
			long x1 = (long)AnchorX - (definition.Width - 1) / 2;
			long y1 = (long)AnchorY - (definition.Height - 1) / 2;
			long x2 = x1 + definition.Width - 1L;
			long y2 = y1 + definition.Height - 1L;
			if (x1 < int.MinValue || y1 < int.MinValue || x2 > int.MaxValue || y2 > int.MaxValue)
			{
				return false;
			}
			Rect.X1 = (int)x1;
			Rect.Y1 = (int)y1;
			Rect.X2 = (int)x2;
			Rect.Y2 = (int)y2;
			return true;
		}

		private static bool Overlaps(Rect A, Rect B)
		{
			return A.X1 <= B.X2 && A.X2 >= B.X1 && A.Y1 <= B.Y2 && A.Y2 >= B.Y1;
		}

		private static bool IsOccupied(Rect[] Occupied, int X, int Y)
		{
			for (int i = 0; i < Occupied.Length; i++)
			{
				if (X >= Occupied[i].X1 && X <= Occupied[i].X2 && Y >= Occupied[i].Y1 && Y <= Occupied[i].Y2)
				{
					return true;
				}
			}
			return false;
		}

		private static void ChooseHeart(KingdomInheritWork[] Works, int PlanWidth, int PlanHeight,
			int OffsetX, int OffsetY, out int X, out int Y)
		{
			int centerX = OffsetX + PlanWidth / 2;
			int centerY = OffsetY + PlanHeight / 2;
			X = centerX;
			Y = centerY;
			int bestHeart = -1;
			int bestDistance = int.MaxValue;
			int bestIndex = -1;
			for (int i = 0; i < Works.Length; i++)
			{
				int heart = HeartRank(Works[i].Key);
				int distance = Distance(Works[i].X, Works[i].Y, centerX, centerY);
				if (heart > bestHeart || (heart == bestHeart && distance < bestDistance)
					|| (heart == bestHeart && distance == bestDistance
						&& (bestIndex < 0 || Before(Works[i], Works[bestIndex]))))
				{
					bestHeart = heart;
					bestDistance = distance;
					bestIndex = i;
					X = Works[i].X;
					Y = Works[i].Y;
				}
			}
		}

		private static int HeartRank(string Key)
		{
			switch (Key)
			{
				case "heartcourt": return 4;
				case "heartmoot": return 3;
				case "heartwaterstone": return 2;
				case "heartbasin": return 1;
				default: return 0;
			}
		}

		private static bool TryEntry(Rect[] Occupied, int HeartX, int HeartY, int Width, int Height,
			out int CairnX, out int CairnY, out int EntryX, out int EntryY)
		{
			CairnX = 0;
			CairnY = 0;
			EntryX = 0;
			EntryY = 0;
			int best = int.MaxValue;
			for (int y = SafeMargin; y < Height - SafeMargin; y++)
			{
				ConsiderEntry(Occupied, SafeMargin, y, SafeMargin + 1, y, 0, y,
					HeartX, HeartY, ref best, ref CairnX, ref CairnY, ref EntryX, ref EntryY);
				ConsiderEntry(Occupied, Width - 1 - SafeMargin, y, Width - 2 - SafeMargin, y, Width - 1, y,
					HeartX, HeartY, ref best, ref CairnX, ref CairnY, ref EntryX, ref EntryY);
			}
			for (int x = SafeMargin; x < Width - SafeMargin; x++)
			{
				ConsiderEntry(Occupied, x, SafeMargin, x, SafeMargin + 1, x, 0,
					HeartX, HeartY, ref best, ref CairnX, ref CairnY, ref EntryX, ref EntryY);
				ConsiderEntry(Occupied, x, Height - 1 - SafeMargin, x, Height - 2 - SafeMargin, x, Height - 1,
					HeartX, HeartY, ref best, ref CairnX, ref CairnY, ref EntryX, ref EntryY);
			}
			return best != int.MaxValue;
		}

		private static void ConsiderEntry(Rect[] Occupied, int CandidateCairnX, int CandidateCairnY,
			int InsideX, int InsideY, int CandidateEntryX, int CandidateEntryY, int HeartX, int HeartY,
			ref int Best, ref int CairnX, ref int CairnY, ref int EntryX, ref int EntryY)
		{
			if (IsOccupied(Occupied, CandidateCairnX, CandidateCairnY) || IsOccupied(Occupied, InsideX, InsideY))
			{
				return;
			}
			int score = Distance(CandidateCairnX, CandidateCairnY, HeartX, HeartY);
			if (score < Best)
			{
				Best = score;
				CairnX = CandidateCairnX;
				CairnY = CandidateCairnY;
				EntryX = CandidateEntryX;
				EntryY = CandidateEntryY;
			}
		}

		private static bool[] Select(KingdomInheritPlan Source, int Percent, int InterregnumRoll, bool PreferHeart)
		{
			bool[] selected = new bool[Source.Count];
			int eligible = 0;
			for (int i = 0; i < Source.Count; i++)
			{
				KingdomInheritWork work = Source.WorkAt(i);
				if (work != null && work.State != KingdomInheritWorkState.Memory)
				{
					eligible++;
				}
			}
			if (eligible == 0)
			{
				return selected;
			}
			int percent = Percent;
			if (percent < 0) percent = 0;
			if (percent > 100) percent = 100;
			int wanted = (eligible * percent + 50) / 100;
			if (wanted < 1) wanted = 1;
			if (wanted > eligible) wanted = eligible;
			int[] order = new int[eligible];
			int at = 0;
			for (int i = 0; i < Source.Count; i++)
			{
				KingdomInheritWork work = Source.WorkAt(i);
				if (work != null && work.State != KingdomInheritWorkState.Memory)
				{
					order[at++] = i;
				}
			}
			for (int i = 1; i < order.Length; i++)
			{
				int value = order[i];
				int write = i;
				while (write > 0 && SelectionBefore(Source.WorkAt(value), Source.WorkAt(order[write - 1]),
					InterregnumRoll, PreferHeart))
				{
					order[write] = order[write - 1];
					write--;
				}
				order[write] = value;
			}
			for (int i = 0; i < wanted; i++)
			{
				selected[order[i]] = true;
			}
			return selected;
		}

		private static bool SelectionBefore(KingdomInheritWork A, KingdomInheritWork B,
			int InterregnumRoll, bool PreferHeart)
		{
			int heartA = HeartRank(A.Key);
			int heartB = HeartRank(B.Key);
			if (heartA != heartB)
			{
				return PreferHeart ? heartA > heartB : heartA < heartB;
			}
			uint scoreA = SelectionScore(A, InterregnumRoll, PreferHeart);
			uint scoreB = SelectionScore(B, InterregnumRoll, PreferHeart);
			if (scoreA != scoreB)
			{
				return scoreA < scoreB;
			}
			return Before(A, B);
		}

		private static uint SelectionScore(KingdomInheritWork Work, int InterregnumRoll, bool PreferHeart)
		{
			uint hash = 2166136261U;
			for (int i = 0; i < Work.Key.Length; i++)
			{
				hash ^= Work.Key[i];
				hash *= 16777619U;
			}
			hash ^= (uint)Work.X;
			hash *= 16777619U;
			hash ^= (uint)Work.Y;
			hash *= 16777619U;
			hash ^= (uint)InterregnumRoll;
			hash *= 16777619U;
			hash ^= PreferHeart ? 0xA17E5EEDU : 0xFAD3D123U;
			hash *= 16777619U;
			return hash;
		}

		private static int Distance(int AX, int AY, int BX, int BY)
		{
			long dx = (long)AX - BX;
			long dy = (long)AY - BY;
			if (dx < 0L) dx = -dx;
			if (dy < 0L) dy = -dy;
			long distance = (dx > dy) ? dx : dy;
			return (distance > int.MaxValue) ? int.MaxValue : (int)distance;
		}

		private static int Min(int A, int B)
		{
			return (A < B) ? A : B;
		}

		private static bool Before(KingdomInheritWork A, KingdomInheritWork B)
		{
			if (A == null) return false;
			if (B == null) return true;
			if (A.Y != B.Y) return A.Y < B.Y;
			if (A.X != B.X) return A.X < B.X;
			return string.CompareOrdinal(A.Key, B.Key) < 0;
		}

		private static void Sort(Candidate[] Candidates)
		{
			for (int i = 1; i < Candidates.Length; i++)
			{
				Candidate value = Candidates[i];
				int at = i;
				while (at > 0 && CandidateBefore(value, Candidates[at - 1]))
				{
					Candidates[at] = Candidates[at - 1];
					at--;
				}
				Candidates[at] = value;
			}
		}

		private static bool CandidateBefore(Candidate A, Candidate B)
		{
			if (A.X != B.X) return A.X < B.X;
			if (A.Y != B.Y) return A.Y < B.Y;
			int key = string.CompareOrdinal(A.Key, B.Key);
			if (key != 0) return key < 0;
			if (A.State != B.State) return A.State > B.State;
			return A.Condition < B.Condition;
		}

		private static KingdomInheritPlan EmptyPlan()
		{
			return new KingdomInheritPlan(new KingdomInheritWork[0], 0, 0);
		}
	}
}
