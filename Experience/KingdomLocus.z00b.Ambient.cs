using System;
using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public static partial class KingdomLocus
	{
		/// <summary>Handles one engine-supplied idle actor. It resolves only the exact keeper ID
		/// stamped by the active-ground pass and never walks a zone, schedules a goal, or changes
		/// semantic state.</summary>
		internal static bool TryClaimAmbient(GameObject Bench, r_KingdomLocusAmbient Part,
			GameObject Actor, long NowTick, out bool RetireHook)
		{
			RetireHook = !AmbientAuthorityCurrent(Bench, Part, NowTick, out GameObject keeper,
				out KingdomSystem system);
			if (RetireHook) return false;
			int residentId = KingdomResidents.IdOf(Actor);
			bool resident = GameObject.Validate(Actor) && Actor.IsAlive
				&& Actor.Brain != null && Actor.GetIntProperty("KingdomBorn") == 1
				&& residentId > 0 && KingdomCitizenship.BelongsTo(system, Actor);
			bool sameGround = resident && ReferenceEquals(Actor.CurrentZone, Bench.CurrentZone)
				&& Actor.CurrentCell != null;
			int distance = sameGround ? Actor.DistanceTo(Bench) : int.MaxValue;
			if (!KingdomLocusRules.MayClaim(Part.AuthorityEnabled, resident, sameGround,
				ReferenceEquals(Actor, keeper), KingdomStations.PostOf(Actor) != 0,
				KingdomPhysicalHappenings.IsStaged(Actor), Actor?.IsPlayer() == true,
				Actor?.IsPlayerLed() == true, distance, Part.HasUsed, Part.LastUseTick, NowTick))
				return false;

			KingdomLocusRules.AmbientCue cue = KingdomLocusRules.Cue(
				KingdomLocusRules.AmbientUseFor(residentId));
			if (!cue.Exists) return false;
			// Explicit velocity avoids ParticleText's RNG-bearing convenience overload. Both
			// positive cues are cosmetic; Bored owns the ordinary idle-turn energy payment.
			Actor.ParticleText(cue.Text, 0f, -0.2f, cue.Color, IgnoreVisibility: false);
			Part.HasUsed = true;
			Part.LastUseTick = NowTick;
			return true;
		}

		private static bool AmbientAuthorityCurrent(GameObject Bench,
			r_KingdomLocusAmbient Part, long NowTick, out GameObject Keeper,
			out KingdomSystem System)
		{
			Keeper = null;
			System = The.Game?.GetSystem<KingdomSystem>();
			if (!GameObject.Validate(Bench) || Part == null || !Part.AuthorityEnabled
				|| System == null || !System.Founded || !KingdomMaster.AutomaticWorkAllowed(System)
				|| NowTick < 0L || Part.ConfiguredTick < 0L || NowTick < Part.ConfiguredTick
				|| Bench.Blueprint != BenchBlueprint || !KingdomUpgrade.IsFunctionallyBuilt(Bench)
				|| Bench.GetIntProperty("KingdomStaffNeeded") != 1
				|| Bench.GetIntProperty("KingdomStaffed") != 1 || Bench.CurrentCell == null
				|| !Enabled || Options.GetOption(KingdomExperienceOptions.AmbientOptionId,
					"Yes") == "No" || Options.DisableAllIdleTileAnimations
				|| Options.DisableTextAnimationEffects) return false;
			Zone zone = Bench.CurrentZone;
			int locusWorkId = KingdomLocusRules.SelectLocusWork(System.City?.WorkIds,
				System.City?.WorkDesignKeys, BenchBlueprint);
			if (zone == null || !ReferenceEquals(zone, The.Player?.CurrentZone)
				|| !System.ClaimedZones.Contains(zone.ZoneID)
				|| !string.Equals(zone.ZoneID, Part.OwnerZoneId, StringComparison.Ordinal)
				|| !string.Equals(System.RealmId, Part.OwnerRealmId, StringComparison.Ordinal)
				|| !string.Equals(System.City?.SettlementId, Part.OwnerSettlementId,
					StringComparison.Ordinal)
				|| Part.WorkId != locusWorkId
				|| Part.WorkId != KingdomCityRules.StableId(Bench.ID)
				|| !KingdomExperienceRules.CanEmit(System.Experience,
					KingdomExperienceOptionKind.AmbientUse, NowTick)) return false;

			Keeper = GameObject.FindByID(Part.KeeperObjectId);
			return GameObject.Validate(Keeper) && Keeper.IsAlive && Keeper.Brain != null
				&& ReferenceEquals(Keeper.CurrentZone, zone) && Keeper.CurrentCell != null
				&& Keeper.DistanceTo(Bench) <= KingdomLocusRules.AmbientDistance
				&& KingdomResidents.IdOf(Keeper) == Part.KeeperResidentId
				&& Keeper.GetIntProperty("KingdomKeeper") == 1
				&& Keeper.GetIntProperty("KingdomBorn") == 1
				&& KingdomStations.PostOf(Keeper) == Part.WorkId
				&& !KingdomPhysicalHappenings.IsStaged(Keeper)
				&& !Keeper.IsPlayer() && !Keeper.IsPlayerLed()
				&& KingdomCitizenship.BelongsTo(System, Keeper);
		}
	}
}
