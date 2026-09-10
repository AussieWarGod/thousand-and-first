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
		/// One standing body and everything about it a refused delivery must leave alone: which
		/// body it is, what it is, how many units it carries RAW, and exactly where it stands.
		/// <para>
		/// "Where" is a zone, a cell AND a holder, all three, always. Cell coordinates alone are
		/// not a place: the same coordinates in another zone are different ground, and a body
		/// carried out of a chest into a cell at the chest's own coordinates would otherwise read
		/// as unmoved. Recording only whichever of the two happens to be set would hide exactly
		/// the move that matters.
		/// </para>
		/// </summary>
		private sealed class Body
		{
			internal readonly GameObject Item;
			internal readonly string Id;
			internal readonly string Blueprint;
			internal readonly int RawCount;
			internal readonly string ZoneId;
			internal readonly string CellKey;
			internal readonly string HolderId;

			internal Body(GameObject Item)
			{
				this.Item = Item;
				Id = Item.IDIfAssigned;
				Blueprint = Item.Blueprint;
				RawCount = KingdomMaterials.RawPhysicalCountOf(Item);
				ZoneId = ZoneOf(Item);
				CellKey = CellOf(Item);
				HolderId = HolderOf(Item);
			}

			/// <summary>The zone this body is really in: its own if it stands in a cell, and
			/// otherwise the zone of whoever is holding it. A body nowhere at all reads as
			/// nowhere, which is never equal to a zone.</summary>
			internal static string ZoneOf(GameObject Item)
			{
				if (!GameObject.Validate(Item)) return "gone";
				Zone own = Item.CurrentZone;
				if (own != null) return own.ZoneID;
				Zone held = Item.Physics?.InInventory?.CurrentZone;
				return (held == null) ? "-" : held.ZoneID;
			}

			internal static string CellOf(GameObject Item)
			{
				if (!GameObject.Validate(Item)) return "gone";
				Cell cell = Item.CurrentCell;
				return (cell == null) ? "-" : (cell.X + "," + cell.Y);
			}

			internal static string HolderOf(GameObject Item)
			{
				if (!GameObject.Validate(Item)) return "gone";
				GameObject holder = Item.Physics?.InInventory;
				return (holder == null) ? "-" : holder.IDIfAssigned;
			}

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
				Require(ZoneOf(Item) == ZoneId,
					What + " changed zone across a refused delivery");
				Require(CellOf(Item) == CellKey,
					What + " changed cell across a refused delivery");
				Require(HolderOf(Item) == HolderId,
					What + " changed holder across a refused delivery");
			}

			internal string Evidence
			{
				get
				{
					return Id + "/" + Blueprint + "x" + RawCount + "@zone:" + ZoneId
						+ "/cell:" + CellKey + "/in:" + HolderId;
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
				long key;
				Require(KingdomDepositOverflowReservation.TryReserve(Reserved,
					new[] { KeyOf(cell) }, out key) && key == KeyOf(cell),
					"the reservation refused a cell nothing had claimed");
				return cell;
			}

			/// <summary>The next bare cell after this one in the same scan order the shared
			/// helper uses, so a reserved cell is stepped over rather than handed out twice.
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
						if (cell.IsEmpty() && cell.IsPassable() && !cell.HasOpenLiquidVolume())
							return cell;
					}
				return null;
			}

			private long KeyOf(Cell Cell)
			{
				return KingdomDepositOverflowReservation.Key(
					Zone.ZoneID.GetHashCode(), Cell.X, Cell.Y);
			}

			private static void Require(bool Value, string Failure)
			{
				KingdomDepositOverflowNativeChecks.Require(Value, Failure);
			}
		}
	}
}
