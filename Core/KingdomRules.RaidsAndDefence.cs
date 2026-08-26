namespace ThousandAndFirst
{
	public static partial class KingdomRules
	{
		/// <summary>
		/// How a raid resolves against what the settlement built. Fortification is the fourth
		/// answer to a threat, beside paying, talking, and meeting it in the field &mdash; and
		/// unlike the other three it works while the founder is elsewhere.
		/// </summary>
		public enum RaidOutcome
		{
			Overrun,
			Plundered,
			Repelled
		}

		public const int DefenceToRepel = 12;

		/// <summary>
		/// Resolves a raid against the settlement's defence. Works only count while crewed, so
		/// a watchtower with nobody in it defends nothing.
		/// </summary>
		/// <param name="Defence">Sum of crewed defensive works.</param>
		/// <param name="RaidSize">Number of raiders.</param>
		public static RaidOutcome ResolveRaid(int Defence, int RaidSize)
		{
			int pressure = RaidSize * 3;
			if (Defence >= DefenceToRepel && Defence >= pressure)
			{
				return RaidOutcome.Repelled;
			}
			if (Defence <= 0)
			{
				return RaidOutcome.Overrun;
			}
			return RaidOutcome.Plundered;
		}

		/// <summary>Percent of a raiding band turned back at the perimeter, per point of defence.</summary>
		public const int RaidTurnedBackPercentPerDefence = 6;

		/// <summary>
		/// Ceiling on how much of a band the walls turn back. Walls are a perimeter, not an
		/// answer: past this, someone still climbs over and has to be met.
		/// </summary>
		public const int MaxRaidersTurnedBackPercent = 60;

		/// <summary>
		/// How many raiders actually reach the settlement's ground. Defence is a perimeter
		/// rather than a damage number: crewed works and a garrison district turn part of the
		/// band back before any fighting starts, and a wall strong enough to
		/// <see cref="RaidOutcome.Repelled"/> turns all of it back. A band that does get through
		/// is never fewer than one &mdash; being well-walled is not the same as being spared.
		/// </summary>
		/// <param name="RaidSize">Raiders who set out.</param>
		/// <param name="Defence">Sum of crewed defensive works plus district bonus.</param>
		/// <param name="Outcome">Result of <see cref="ResolveRaid"/> for this defence and size.</param>
		public static int RaidingPartySize(int RaidSize, int Defence, RaidOutcome Outcome)
		{
			if (RaidSize <= 0 || Outcome == RaidOutcome.Repelled)
			{
				return 0;
			}
			if (Defence <= 0)
			{
				return RaidSize;
			}
			int turned = Defence * RaidTurnedBackPercentPerDefence;
			if (turned > MaxRaidersTurnedBackPercent)
			{
				turned = MaxRaidersTurnedBackPercent;
			}
			int through = RaidSize * (100 - turned) / 100;
			return (through < 1) ? 1 : through;
		}

		/// <summary>
		/// Drams a raid carries off. Defence buys down the loss proportionally, and a repelled
		/// raid takes nothing &mdash; but walls never make a settlement free, only expensive.
		/// </summary>
		public static int RaidPlunder(int BaseDrams, int Defence, RaidOutcome Outcome)
		{
			if (Outcome == RaidOutcome.Repelled)
			{
				return 0;
			}
			if (Defence <= 0)
			{
				return BaseDrams;
			}
			int reduction = Defence * 6;
			if (reduction > 80)
			{
				reduction = 80;
			}
			return BaseDrams * (100 - reduction) / 100;
		}

		/// <summary>
		/// Defence a wall gains from ground already broken into worked stone: exactly
		/// <see cref="RuinTerrainBlueprints"/>'s two blueprints, plus the wider "Ruins" region tag
		/// shared by <c>TerrainGritGate</c>, <c>TerrainRustWell</c>, <c>TerrainGolgotha</c>, and
		/// <c>TerrainRustedArchway</c> (StreamingAssets/Base/ObjectBlueprints/WorldTerrain.xml).
		/// The region match is deliberately wider than <see cref="IsRuinSite"/>'s: that gate
		/// decides whether the founding rite itself restores a ruin, and stays exact so an
		/// ordinary founding never mistakenly reads as one; this one only asks whether there is
		/// cut stone lying around to build with, which any of those sites offer.
		/// </summary>
		private static readonly string[] WorkedStoneGround = new string[1] { "Ruins" };

		/// <summary>
		/// Defence a wall gains from ground with rock underfoot that nobody has cut yet:
		/// <c>TerrainMountains</c>/<c>TerrainBethesdaSusa</c> (region "Mountains") and
		/// <c>TerrainHills</c>/<c>TerrainAsphaltMines</c>/<c>TerrainCraters</c> (region "Hills").
		/// Worth less than <see cref="WorkedStoneGround"/>: the stone is there, but the
		/// settlement's own masons have to quarry it first.
		/// </summary>
		private static readonly string[] QuarriableGround = new string[2] { "Mountains", "Hills" };

		/// <summary>Defence a wall built on <see cref="WorkedStoneGround"/> gains.</summary>
		public const int WorkedStoneWallBonus = 2;

		/// <summary>Defence a wall built on <see cref="QuarriableGround"/> gains.</summary>
		public const int QuarriableWallBonus = 1;

		/// <summary>
		/// Extra defence a wall draws from what the settlement's ground offers to build with.
		/// Reads the exact blueprint first and the region tag only if the blueprint names
		/// nothing recognised &mdash; the same order <see cref="StyleForSite"/> reads ground in,
		/// for the same reason: a ruin sitting inside some other region is still a ruin. Ground
		/// that offers nothing to quarry (a salt flat, a jungle floor, a spore mat) answers zero,
		/// never a negative number &mdash; this is a bonus ladder, and its floor is the wall a
		/// settlement builds today.
		/// </summary>
		/// <param name="TerrainBlueprint">Founding site's terrain blueprint
		/// (<see cref="KingdomSystem.FoundingTerrainBlueprint"/>), or null.</param>
		/// <param name="RegionName">Founding site's terrain region
		/// (<see cref="KingdomSystem.FoundingRegionName"/>), or null.</param>
		public static int GroundWallBonus(string TerrainBlueprint, string RegionName)
		{
			if (GroundMatches(TerrainBlueprint, WorkedStoneGround) || GroundMatches(RegionName, WorkedStoneGround))
			{
				return WorkedStoneWallBonus;
			}
			if (GroundMatches(TerrainBlueprint, QuarriableGround) || GroundMatches(RegionName, QuarriableGround))
			{
				return QuarriableWallBonus;
			}
			return 0;
		}

		/// <summary>As <see cref="ContainsAny"/>, but null- and empty-safe: <see cref="StyleForGround"/>
		/// guards this at its one call site, and this ladder needs the same guard at two.</summary>
		private static bool GroundMatches(string Ground, string[] Needles)
		{
			return !string.IsNullOrEmpty(Ground) && ContainsAny(Ground, Needles);
		}

		/// <summary>Defence a wall gains from a founder who knows the Tinkering skill at all
		/// &mdash; enough to keep a joint sound, even without a schematic for it.</summary>
		public const int TinkeringWallBonus = 1;

		/// <summary>
		/// Defence a wall gains, on top of <see cref="TinkeringWallBonus"/>, from a founder who
		/// has gone on to Tinker I (<c>Tinkering_Tinker1</c>): the point the skill actually
		/// teaches building from a schematic rather than only examining and repairing one.
		/// </summary>
		public const int AdvancedTinkeringWallBonus = 1;

		/// <summary>
		/// Extra defence a wall draws from what the founder standing at the commission actually
		/// knows how to build. Purely additive over the two skill checks, and never negative: an
		/// unskilled founder answers zero, the wall a settlement builds today.
		/// </summary>
		/// <param name="HasTinkering">Whether the founder holds the base Tinkering skill.</param>
		/// <param name="HasAdvancedTinkering">Whether the founder holds Tinker I.</param>
		public static int KnowledgeWallBonus(bool HasTinkering, bool HasAdvancedTinkering)
		{
			int bonus = 0;
			if (HasTinkering)
			{
				bonus += TinkeringWallBonus;
			}
			if (HasAdvancedTinkering)
			{
				bonus += AdvancedTinkeringWallBonus;
			}
			return bonus;
		}

		/// <summary>
		/// The defence a commissioned wall actually rises with: its design's own
		/// <see cref="BuildEntry.Defence"/>, plus what the ground offers to quarry
		/// (<see cref="GroundWallBonus"/>) and what the founder knows how to build
		/// (<see cref="KnowledgeWallBonus"/>). Purely additive and gated on the design already
		/// being defensive &mdash; a design with no <c>Defence</c> of its own (an ordinary
		/// building) never gains any, because this ladder makes walls stronger, it does not make
		/// anything into a wall.
		/// </summary>
		/// <param name="BaseDefence">The commissioned design's own <c>Defence</c> attribute.
		/// Zero or negative for anything that is not a defensive work.</param>
		/// <param name="TerrainBlueprint">Founding site's terrain blueprint, or null.</param>
		/// <param name="RegionName">Founding site's terrain region, or null.</param>
		/// <param name="HasTinkering">Whether the founder holds the base Tinkering skill.</param>
		/// <param name="HasAdvancedTinkering">Whether the founder holds Tinker I.</param>
		/// <returns><paramref name="BaseDefence"/> unchanged for a non-defensive design, poor
		/// ground, and an unskilled founder alike &mdash; the bonus only ever adds.</returns>
		public static int WallDefence(int BaseDefence, string TerrainBlueprint, string RegionName, bool HasTinkering, bool HasAdvancedTinkering)
		{
			if (BaseDefence <= 0)
			{
				return BaseDefence;
			}
			return BaseDefence + GroundWallBonus(TerrainBlueprint, RegionName) + KnowledgeWallBonus(HasTinkering, HasAdvancedTinkering);
		}

		/// <summary>Whether a defensive design is a free-standing perimeter work rather than a
		/// building on a reserved plot. Defence is an effect; plot ownership is geometry. Keeping
		/// those facts separate lets a watch-lodge or guarded shrine defend the settlement without
		/// turning its whole authored building into one wall-line cell.</summary>
		public static bool IsFrontierWork(int Defence, bool HasPlot)
		{
			return Defence > 0 && !HasPlot;
		}

		/// <summary>Defence frozen onto a completed work. Only free-standing perimeter works receive
		/// quarry-ground and founder-knowledge wall bonuses; a defensive plotted building carries its
		/// authored rating exactly.</summary>
		public static int BuiltDefence(int BaseDefence, bool HasPlot, string TerrainBlueprint,
			string RegionName, bool HasTinkering, bool HasAdvancedTinkering)
		{
			if (BaseDefence <= 0)
			{
				return BaseDefence;
			}
			return IsFrontierWork(BaseDefence, HasPlot)
				? WallDefence(BaseDefence, TerrainBlueprint, RegionName,
					HasTinkering, HasAdvancedTinkering)
				: BaseDefence;
		}

		/// <summary>The drams a thirst petition asks the stores to reach.</summary>
		public static int ThirstPetitionTarget(int Population)
		{
			int num = UpkeepDrams(Population) * 8;
			if (num < 16)
			{
				num = 16;
			}
			return num;
		}

		/// <summary>Whether an open petition has been answered by the settlement's own state.</summary>
		public static bool IsPetitionMet(PetitionKind Kind, int Target, int StoredWater, int Population, int Beds, int IdleWorks, int Standing, bool HasShrine)
		{
			switch (Kind)
			{
			case PetitionKind.Thirst:
				return StoredWater >= Target;
			case PetitionKind.Shelter:
				return Beds > Population;
			case PetitionKind.Memorial:
				return HasShrine;
			case PetitionKind.Peace:
				return Standing >= Target;
			case PetitionKind.Craft:
				return IdleWorks == 0;
			case PetitionKind.Flesh:
				// The one kind no STATE answers. There is nothing the founder can build, fill or
				// mend that settles "say out loud what you have built here" — DIVERSITY §3.6 is
				// explicit that there is no correct answer. Hearing the speech supplies this
				// frozen target; acceptance separately gates resolution in CanResolve.
				return Target > 0;
			case PetitionKind.Chrome:
				// Flesh's twin, met the same way and for the same reason: the debt is not a bill
				// the founder can pay, it is a thing said to their face.
				return Target > 0;
			default:
				return false;
			}
		}
	}
}
