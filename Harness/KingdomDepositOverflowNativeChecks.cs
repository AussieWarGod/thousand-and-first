using System;
using System.Collections.Generic;
using System.Text;
using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Machine witness for the raw delivery overflow refusal (issue #111,
	/// <c>Growth/KingdomMaterials.RawObservation.cs</c>, <c>Core/KingdomDepositEngine.cs</c>).
	/// Four cases, all against the real adapters and real bodies: an ordinary delivered parcel
	/// into a real stockpile and onto real ground; a real held stack at exactly
	/// <c>int.MaxValue</c> that still reads and still credits; and a total past
	/// <c>int.MaxValue</c> refusing on the stockpile host and on the ground host, granting no
	/// credit and leaving every standing body untouched.
	/// <para>
	/// SYNTHETIC SETUP, DISCLOSED. The camp is founded by the harness and every large stack is a
	/// fixture body whose count the harness assigned directly and which carries
	/// <c>NeverStack</c>. No claim is made that ordinary play reaches such a count.
	/// </para>
	/// </summary>
	internal static partial class KingdomDepositOverflowNativeChecks
	{
		private static Frame Retained;

		internal static bool Vacant { get { return Retained == null; } }

		internal static string Run(string Verb, XRLGame Game, Zone Zone, out bool Complete)
		{
			Complete = false;
			if (Verb == KingdomDepositOverflowNativeProvider.SetupVerb)
			{
				Require(Retained == null, "a deposit-overflow attempt is already retained");
				Retained = new Frame(Game, Zone);
				Retained.Start();
			}
			else
			{
				Require(Retained != null, "deposit-overflow setup is absent");
				Retained.Check();
			}
			Complete = Retained.Done;
			return (Complete ? "native-deposit-overflow cases=4 passed=4 failed=0"
				: "native-deposit-overflow phase=" + Retained.Phase)
				+ "; synthetic-camp=true; synthetic-stacks=true; synthetic-neverstack=true"
				+ "; synthetic-id-allocation=true; ids allocated="
				+ (Retained == null ? 0 : Retained.IdentitiesAllocated)
				+ "; ordinary-reachability=untested; charter=untested; save-load=untested"
				+ Retained.Evidence;
		}

		internal static string Fail(Exception Error)
		{
			if (Retained != null) Retained.Armed = false;
			return "native-deposit-overflow cases=4 passed=0 failed=1; evidence retained: "
				+ KingdomScenarioRules.Bounded(Error.GetType().Name + ": " + Error.Message)
				+ Retained?.Evidence;
		}

		private static void Require(bool Value, string Failure)
		{
			KingdomDepositOverflowNativeProvider.Require(Value, Failure);
		}

		private sealed partial class Frame
		{
			private readonly XRLGame Game;
			private readonly Zone Zone;
			private KingdomSystem System;
			private GameObject Container;
			private string Blueprint;
			private Cell PlainGround, BoundaryGround, OverflowGround;
			private readonly List<long> Reserved = new List<long>();
			private Body Boundary;
			private Body[] StoreStacks, GroundStacks;
			internal bool Armed, Done;
			/// <summary>Fixture bodies the harness asked the engine to identify. Disclosed.
			/// </summary>
			internal int IdentitiesAllocated;

			/// <summary>Every identity this fixture has been allocated, shared across the stacks
			/// AND the store, so "no two fixture bodies share an id" stands on its own.</summary>
			private readonly HashSet<string> Identities = new HashSet<string>();
			internal int Phase;
			internal readonly StringBuilder Evidence = new StringBuilder();

			internal Frame(XRLGame Game, Zone Zone) { this.Game = Game; this.Zone = Zone; }

			/// <summary>Real founding, one really DEDICATED stockpile of the fixture's own, and
			/// three DISTINCT bare cells reserved up front &mdash; one per ground case, so no case
			/// can inherit another's hold.</summary>
			internal void Start()
			{
				System = KingdomNativeCampFounding.Found(Game, Zone, Require);
				Require(System.ClaimedZones.Contains(Zone.ZoneID),
					"the real founding did not claim this ground");
				Blueprint = KingdomMaterials.BlueprintFor(KingdomMaterial.Brush);
				Require(!string.IsNullOrEmpty(Blueprint), "brush has no shipped blueprint");
				PlainGround = ReserveCell();
				BoundaryGround = ReserveCell();
				OverflowGround = ReserveCell();
				Require(KingdomDepositOverflowReservation.AllDistinct(Reserved)
					&& Reserved.Count == 3,
					"the three ground cases did not reserve three distinct cells");
				Require(!ReferenceEquals(PlainGround, BoundaryGround)
					&& !ReferenceEquals(PlainGround, OverflowGround)
					&& !ReferenceEquals(BoundaryGround, OverflowGround),
					"two ground cases were handed the same cell");
				DedicateStore();
				Armed = true;
				Phase = 1;
				Evidence.Append("\nfounded tick=").Append(Game.TimeTicks)
					.Append("; zone=").Append(Zone.ZoneID)
					.Append("; blueprint=").Append(Blueprint)
					.Append("; synthetic store=").Append(Container.IDIfAssigned)
					.Append("; ids allocated so far=").Append(IdentitiesAllocated)
					.Append("; capacity=").Append(KingdomSurvey.StockCapacityOf(Container))
					.Append("; plain=").Append(Where(PlainGround))
					.Append("; boundary=").Append(Where(BoundaryGround))
					.Append("; overflow=").Append(Where(OverflowGround));
			}

			internal void Check()
			{
				Require(Armed && !Done, "the deposit-overflow frame is not armed");
				switch (Phase)
				{
					case 1: OrdinaryParcel(); break;
					case 2: RepresentableBoundary(); break;
					case 3: StockpileOverflow(); break;
					default: GroundOverflow(); Done = true; break;
				}
				Phase++;
			}

			private static string Where(Cell Cell)
			{
				return (Cell == null) ? "-" : ("(" + Cell.X + "," + Cell.Y + ")");
			}

			/// <summary>One bare cell nothing else has claimed. The reservation is remembered
			/// BEFORE the ground is used, because every candidate is still empty at this point and
			/// a "first bare cell" search would otherwise hand back the same cell every time.
			/// </summary>
			private Cell ReserveCell()
			{
				Cell cell = KingdomNativeCampFounding.Clear(Zone);
				while (cell != null && Reserved.Contains(KeyOf(cell))) cell = NextBare(cell);
				Require(cell != null, "no unreserved clear cell was available for a case");
				// The key is scoped to THIS frame's one zone -- the reservation never leaves it --
				// and the cell is proved to belong to that exact zone object before it is keyed,
				// so no hash of a zone id stands between a reservation and the ground it names.
				Require(ReferenceEquals(cell.ParentZone, Zone),
					"a reserved cell belongs to a different zone than this frame's");
				long key;
				Require(KingdomDepositOverflowReservation.TryReserve(Reserved,
					new[] { KeyOf(cell) }, out key) && key == KeyOf(cell),
					"the reservation refused a cell nothing had claimed");
				return cell;
			}

			/// <summary>The next cell after this one that is bare BY THE SAME PREDICATE the shared
			/// helper uses, so a reserved cell is stepped over rather than handed out twice and
			/// the space a case claims is as clear as the space the helper would have given it.
			/// </summary>
			private Cell NextBare(Cell After)
			{
				bool past = false;
				for (int y = 1; y < Zone.Height - 1; y++)
					for (int x = 1; x < Zone.Width - 1; x++)
					{
						Cell cell = Zone.GetCell(x, y);
						if (cell == null) continue;
						if (!past) { past = ReferenceEquals(cell, After); continue; }
						if (Bare(cell)) return cell;
					}
				return null;
			}

			/// <summary>Exactly the predicate <c>KingdomNativeCampFounding.Clear</c> applies:
			/// empty, passable, no open liquid, and every object standing in it a valid,
			/// non-creature, bare piece of ground.</summary>
			private static bool Bare(Cell Cell)
			{
				if (Cell == null || !Cell.IsEmpty() || !Cell.IsPassable()
					|| Cell.HasOpenLiquidVolume()) return false;
				foreach (GameObject row in Cell.Objects)
					if (!GameObject.Validate(row) || row.IsCreature
						|| KingdomPlots.ReadObject(row) != KingdomPlotRules.GroundKind.Bare)
						return false;
				return true;
			}

			private static long KeyOf(Cell Cell)
			{
				return KingdomDepositOverflowReservation.Key(Cell.X, Cell.Y);
			}

			private static void Require(bool Value, string Failure)
			{
				KingdomDepositOverflowNativeChecks.Require(Value, Failure);
			}
		}
	}
}
