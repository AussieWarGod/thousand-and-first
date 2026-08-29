using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomRoads
	{
		internal static void RetryConstruction(KingdomSystem System, Zone Z, KingdomConstructionJob Job)
		{
			if (System == null || Z == null || Job == null
				|| Job.Route != KingdomConstructionRoute.RoadPaving)
			{
				return;
			}
			List<KingdomConstructionCell> cells;
			if (!KingdomConstructionRules.TryDecodeCells(Job.Payload, out cells)) return;
			ProjectPaving(Z, Job.TargetKey, cells, Job, out _, out _, out _);
		}

		internal static void InspectConstruction(KingdomSystem System, Zone Z,
			KingdomConstructionJob Job)
		{
			if (System == null || Z == null || Job == null
				|| Job.Route != KingdomConstructionRoute.RoadPaving) return;
			List<KingdomConstructionCell> cells;
			if (!KingdomConstructionRules.TryDecodeCells(Job.Payload, out cells)) return;
			KingdomConstructionJob inspected = Job;
			if (inspected.Phase == KingdomConstructionPhase.Complete)
			{
				if (inspected.PhysicalPhase == KingdomPhysicalPhase.RoadTallySettled)
					SettleRoadTerminal(System, Z, Job.TargetKey, cells, ref inspected);
				return;
			}
			if (inspected.Phase != KingdomConstructionPhase.InspectionRequired
				&& (inspected.Phase == KingdomConstructionPhase.ProjectionPending
					|| inspected.PhysicalPhase != KingdomPhysicalPhase.None))
				ProjectPaving(Z, Job.TargetKey, cells, inspected, out _, out _, out _);
		}

		/// <summary>Zone property carrying the worn-ground tally, written by
		/// <c>KingdomRoadRules.Encode</c>.</summary>
		public const string TallyProperty = "r_TAF_Roads";

		/// <summary>Zone property carrying the tick the ground was last walked, as a string
		/// because zone properties hold strings and a tick is not one.</summary>
		public const string WalkedProperty = "r_TAF_RoadsWalked";

		/// <summary>Bounded v1 option observation for this zone's own road clock. It also
		/// carries the last applied master-resume token, so master-off time cannot become
		/// walking when the global switch returns.</summary>
		public const string OptionStateProperty = "r_TAF_RoadsOption_v1";

		/// <summary>Exact immutable settlement owning <see cref="OptionStateProperty"/>.</summary>
		public const string OptionOwnerProperty = "r_TAF_RoadsOptionOwner_v1";

		/// <summary>Realm-wide option epoch. A transition first seen in one zone is therefore
		/// still observed as a mismatch when another claimed zone is loaded later.</summary>
		public const string GlobalOptionStatePrefix = "r_TAF_RoadsGlobalOption_v1:";

		/// <summary>Zone property set once the founder has been told the tally is full, so the
		/// reason is given once per stall rather than once per visit (STANDARDS 7b). Cleared the
		/// moment the tally has room again.</summary>
		public const string FullSaidProperty = "r_TAF_RoadsFull";

		/// <summary>Zone property carrying the highest rung of the ladder whose line has already
		/// been given, so the settlement remarks on its own paths once rather than every time a
		/// ninth cell crosses the same threshold.</summary>
		public const string SaidProperty = "r_TAF_RoadsSaid";

		/// <summary>
		/// Property marking a floor this system laid, and which rung of
		/// <c>KingdomRoadRules.WearState</c> it stands for. The whole of the mod's claim over
		/// these objects: nothing without this property is ever removed here, and everything with
		/// it was created here.
		/// </summary>
		public const string PathStateProperty = "KingdomPath";

		/// <summary>Vanilla's packed dirt floor, laid where the grass has gone
		/// (<c>ZoneTerrain.xml:932</c>).</summary>
		public const string TroddenBlueprint = "DirtFloor";

		/// <summary>Vanilla's dirt path, laid where a way has become a way
		/// (<c>ZoneTerrain.xml:937</c>) &mdash; the same floor <c>PlaceHut</c> lays inside a
		/// vanilla village hut.</summary>
		public const string PathBlueprint = "DirtPath";

		/// <summary>Whether ground wears at all. Its own toggle, because a player who likes the
		/// grass exactly as the world generator drew it should be able to keep it (STANDARDS 3).
		/// Defaults to on when the option is missing, so a build whose XML has not landed yet
		/// behaves like the shipped one.</summary>
		public static bool Enabled => Options.GetOption("r_TAF_OptionRoads") != "No";

		/// <summary>One errand: two ends and a reason.</summary>
		private struct Errand
		{
			public int FromX;

			public int FromY;

			public int ToX;

			public int ToY;

			public KingdomRoadRules.RouteKind Kind;

			/// <summary>Frozen authored intermediates for a DoorToLane route. Null alone means the
			/// receipt-less geometric compatibility path may search live walkable ground.</summary>
			public List<ArchitecturePoint> ExactRoute;

			public Errand(int FromX, int FromY, int ToX, int ToY, KingdomRoadRules.RouteKind Kind)
				: this(FromX, FromY, ToX, ToY, Kind, null)
			{
			}

			public Errand(int FromX, int FromY, int ToX, int ToY,
				KingdomRoadRules.RouteKind Kind, IList<ArchitecturePoint> ExactRoute)
			{
				this.FromX = FromX;
				this.FromY = FromY;
				this.ToX = ToX;
				this.ToY = ToY;
				this.Kind = Kind;
				this.ExactRoute = ExactRoute == null ? null : new List<ArchitecturePoint>(ExactRoute);
			}
		}

	}
}
