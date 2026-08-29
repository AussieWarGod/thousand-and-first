using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	/// <summary>
	/// The settlement as a place, not a screen: staffs the gathering bench with a keeper whose
	/// talk reflects what is actually happening, and brings the occasional traveller through who
	/// is offered water and leaves &mdash; never enrolled, never housed, never counted.
	/// <para>
	/// Runs from the kingdom's one <c>ZoneActivatedEvent</c> pass, after growth and raids have
	/// already settled the state of the turn (<see cref="KingdomSystem"/>'s wiring calls this
	/// last), and reads that state rather than keeping any clock of its own. Guest arrival and
	/// unattended departure are both decided here, on activation, for the same reason the rest of
	/// the mod avoids a per-turn tick: time away must catch up in one pass, not accrue while
	/// nobody is watching.
	/// </para>
	/// </summary>
	public static partial class KingdomLocus
	{
		public static bool Enabled => Options.GetOption("r_TAF_OptionLocus") != "No";

		/// <summary>The one owned communal-work blueprint. The keeper pass finds exact built
		/// instances in the already-bounded survey, including legacy benches before their missing
		/// staff declaration has been adopted.</summary>
		public const string BenchBlueprint = "r_KingdomBench";

		/// <summary>Population the keeper had last seen, read fresh each pass to decide
		/// <see cref="KingdomLocusRules.KeeperMood.Growing"/>.</summary>
		private const string KeeperLastPopulationProperty = "KingdomKeeperLastPopulation";

		private const string KeeperMoodProperty = "KingdomKeeperMood";

		public const string CausalPilgrimProperty = "r_TAF_CausalPilgrim";

		public const string PilgrimSequenceProperty = "r_TAF_PilgrimSequence";

		public const string PilgrimCauseProperty = "r_TAF_PilgrimCause";

		public static void OnZoneActivated(KingdomSystem System, Zone Z, KingdomSurvey Survey)
		{
			if (!KingdomMaster.AutomaticWorkAllowed(System)) return;
			if (System == null || !System.Founded || Z == null || Survey == null
				|| !System.ClaimedZones.Contains(Z.ZoneID))
			{
				return;
			}
			long timeTicks = The.Game.TimeTicks;
			// Keeper and locus truth are civic-service state, not a traveller. Reconcile them
			// before the timed PlainGuest lane can wait, recover, disable, or return. An open guest
			// receipt therefore never strands a keeper or leaves a stale ambient hook behind.
			RunKeeperPass(System, Z, Survey, timeTicks);
			if (!KingdomGuestLifecycle.ObserveOption(System,
				KingdomLifecycleLane.PlainGuest, Enabled, timeTicks, out bool allowNew)) return;
			if (KingdomGuestLifecycle.Open(System, KingdomLifecycleLane.PlainGuest) != null)
			{
				KingdomGuestLifecycle.Drive(System, Z, KingdomLifecycleLane.PlainGuest);
				if (KingdomGuestLifecycle.Open(System, KingdomLifecycleLane.PlainGuest) != null) return;
			}
			if (!allowNew) return;
			// Guests belong at the gate/rite heart, not on a random claimed parasang. This also
			// keeps the city's one patience clock bound to the one zone which owns it.
			if (!KingdomPlots.TryRiteGround(Z, out _, out _)) return;
			if (!RunPilgrimPass(System, Z, Survey, timeTicks))
			{
				RunGuestPass(System, Z, Survey, timeTicks);
			}
		}

	}
}
