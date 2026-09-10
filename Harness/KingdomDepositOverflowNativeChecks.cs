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

		/// <summary>
		/// One standing body and everything about it a refused delivery must leave alone.
		/// <para>
		/// The proof is REFERENCES, not text. A body's id, a zone's id and a container's id are
		/// all strings the engine can hand to a different object: a replacement holder carrying
		/// the same id, or a rebuilt zone with the same <c>ZoneID</c>, would pass a text
		/// comparison while the body had in fact been re-homed. So the exact <c>Zone</c>,
		/// <c>Cell</c> and holder objects are held here and re-proved with
		/// <c>ReferenceEquals</c>; the strings survive only as journal diagnostics and are never
		/// the proof.
		/// </para>
		/// <para>
		/// Custody is also EXCLUSIVE, and admission checks it. A body stands in a cell or in a
		/// container, never both and never neither, and it is admitted only in the mode the case
		/// placed it in. A body with no assigned id, or carrying a nonpositive raw count, is not a
		/// standing body at all and is refused before it can be snapshotted.
		/// </para>
		/// </summary>
		private sealed class Body
		{
			internal readonly GameObject Item;
			internal readonly Zone Zone;
			internal readonly Cell Cell;
			internal readonly GameObject Holder;
			internal readonly string Id;
			internal readonly string Blueprint;
			internal readonly int RawCount;

			private Body(GameObject Item, Zone Zone, Cell Cell, GameObject Holder)
			{
				this.Item = Item;
				this.Zone = Zone;
				this.Cell = Cell;
				this.Holder = Holder;
				Id = Item.IDIfAssigned;
				Blueprint = Item.Blueprint;
				RawCount = KingdomMaterials.RawPhysicalCountOf(Item);
			}

			/// <summary>A body admitted as standing on EXACTLY this ground: in this cell, in this
			/// cell's own zone, and in nobody's inventory.</summary>
			internal static Body OnGround(GameObject Item, Cell Ground)
			{
				Admit(Item);
				Require(Ground != null && Ground.ParentZone != null,
					"a ground body was offered no cell to stand in");
				Require(ReferenceEquals(Item.CurrentCell, Ground),
					"a ground body does not stand in the exact cell it was placed in");
				Require(Item.Physics != null && Item.Physics.InInventory == null,
					"a ground body is also in somebody's inventory, which is mixed custody");
				Require(ReferenceEquals(Item.CurrentZone, Ground.ParentZone),
					"a ground body's zone is not the zone of the cell it stands in");
				return new Body(Item, Ground.ParentZone, Ground, null);
			}

			/// <summary>A body admitted as standing in EXACTLY this container: in its inventory,
			/// in the container's own zone, and in no cell of its own.</summary>
			internal static Body InStore(GameObject Item, GameObject Container)
			{
				Admit(Item);
				Require(GameObject.Validate(Container) && Container.Inventory != null,
					"a stored body was offered no container to stand in");
				Require(Item.Physics != null
					&& ReferenceEquals(Item.Physics.InInventory, Container),
					"a stored body is not in the exact container it was placed in");
				Require(Item.CurrentCell == null,
					"a stored body also stands in a cell, which is mixed custody");
				Zone zone = Container.CurrentZone;
				Require(zone != null, "the container a stored body stands in is in no zone");
				return new Body(Item, zone, null, Container);
			}

			/// <summary>What every standing body must be before anything is recorded about it.
			/// </summary>
			private static void Admit(GameObject Item)
			{
				Require(GameObject.Validate(Item), "a body offered for snapshot does not exist");
				Require(!string.IsNullOrEmpty(Item.IDIfAssigned),
					"a body with no assigned id cannot be proved unchanged later");
				Require(!string.IsNullOrEmpty(Item.Blueprint),
					"a body with no blueprint cannot be proved unchanged later");
				Require(KingdomMaterials.RawPhysicalCountOf(Item) > 0,
					"a body carrying a nonpositive raw count is not a standing stack");
			}

			/// <summary>
			/// The same body, the same stuff, the same count, and the SAME zone, cell and holder
			/// objects. Every place check is by reference, so a replacement object wearing the
			/// same id fails here; the exclusivity re-check catches a body that acquired a second
			/// custody without losing the first.
			/// </summary>
			internal void RequireUnchanged(string What)
			{
				Require(GameObject.Validate(Item),
					What + " stopped existing across a refused delivery");
				Require(Item.IDIfAssigned == Id,
					What + " changed identity across a refused delivery");
				Require(Item.Blueprint == Blueprint,
					What + " changed blueprint across a refused delivery");
				Require(KingdomMaterials.RawPhysicalCountOf(Item) == RawCount,
					What + " changed its raw count across a refused delivery");
				GameObject holder = Item.Physics?.InInventory;
				Require(ReferenceEquals(Item.CurrentCell, Cell),
					What + " changed cell across a refused delivery");
				Require(ReferenceEquals(holder, Holder),
					What + " changed holder across a refused delivery");
				Require((Cell == null) != (Holder == null),
					What + " was snapshotted without exactly one custody");
				Require((Item.CurrentCell == null) != (holder == null),
					What + " no longer has exactly one custody");
				Zone zone = (Item.CurrentCell != null)
					? Item.CurrentZone : holder?.CurrentZone;
				Require(ReferenceEquals(zone, Zone),
					What + " changed zone across a refused delivery");
			}

			/// <summary>Journal text only. Never a proof: ids are strings the engine can hand to
			/// another object.</summary>
			internal string Evidence
			{
				get
				{
					return Id + "/" + Blueprint + "x" + RawCount + "@zone:"
						+ (Zone == null ? "-" : Zone.ZoneID)
						+ "/cell:" + (Cell == null ? "-" : (Cell.X + "," + Cell.Y))
						+ "/in:" + (Holder == null ? "-" : Holder.IDIfAssigned);
				}
			}
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
