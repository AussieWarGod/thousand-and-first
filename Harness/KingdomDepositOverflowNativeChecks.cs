using System;
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

		/// <summary>One large stack, and the two counts that must be identical afterwards. A
		/// refused delivery may not move, merge, renumber, or re-home anything already standing:
		/// the body is proved by its own assigned id, its raw count, and where it stands.
		/// </summary>
		private sealed class Body
		{
			internal readonly GameObject Item;
			internal readonly string Id;
			internal readonly int RawCount;
			internal readonly string Where;

			internal Body(GameObject Item)
			{
				this.Item = Item;
				Id = Item.IDIfAssigned;
				RawCount = KingdomMaterials.RawPhysicalCountOf(Item);
				Where = Place(Item);
			}

			/// <summary>Exactly where a body stands, as text: the cell it is in, or the container
			/// whose inventory holds it. A body that moved reads differently.</summary>
			internal static string Place(GameObject Item)
			{
				if (!GameObject.Validate(Item)) return "gone";
				Cell cell = Item.CurrentCell;
				if (cell != null) return "cell(" + cell.X + "," + cell.Y + ")";
				GameObject holder = Item.Physics?.InInventory;
				return (holder == null) ? "nowhere" : ("in:" + holder.IDIfAssigned);
			}

			internal void RequireUnchanged(string What)
			{
				KingdomDepositOverflowNativeChecks.Require(GameObject.Validate(Item),
					What + " stopped existing across a refused delivery");
				KingdomDepositOverflowNativeChecks.Require(Item.IDIfAssigned == Id,
					What + " changed identity across a refused delivery");
				KingdomDepositOverflowNativeChecks.Require(
					KingdomMaterials.RawPhysicalCountOf(Item) == RawCount,
					What + " changed its raw count across a refused delivery");
				KingdomDepositOverflowNativeChecks.Require(Place(Item) == Where,
					What + " changed custody across a refused delivery");
			}

			internal string Evidence
			{
				get { return Id + "x" + RawCount + "@" + Where; }
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
			private Body Boundary;
			private Body[] StoreStacks, GroundStacks;
			internal bool Armed, Done;
			internal int Phase;
			internal readonly StringBuilder Evidence = new StringBuilder();

			internal Frame(XRLGame Game, Zone Zone) { this.Game = Game; this.Zone = Zone; }

			/// <summary>Real founding, the camp's own dedicated stockpile, and three separate bare
			/// cells &mdash; one per ground case, so no case can inherit another's hold.</summary>
			internal void Start()
			{
				System = KingdomNativeCampFounding.Found(Game, Zone, Require);
				Require(System.ClaimedZones.Contains(Zone.ZoneID),
					"the real founding did not claim this ground");
				Blueprint = KingdomMaterials.BlueprintFor(KingdomMaterial.Brush);
				Require(!string.IsNullOrEmpty(Blueprint), "brush has no shipped blueprint");
				KingdomMaterials.MaterialStock stock = KingdomMaterials.Stock(Zone);
				Require(stock.Stockpiles.Count > 0,
					"the founded camp dedicated no stockpile to deliver into");
				Container = stock.Stockpiles[0];
				Require(KingdomMaterials.IsStockpile(Container) && Container.Inventory != null,
					"the camp's first store is not a dedicated container");
				PlainGround = ClearCell();
				BoundaryGround = ClearCell();
				OverflowGround = ClearCell();
				Armed = true;
				Phase = 1;
				Evidence.Append("\nfounded tick=").Append(Game.TimeTicks)
					.Append("; blueprint=").Append(Blueprint)
					.Append("; store=").Append(Container.IDIfAssigned)
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

			private Cell ClearCell()
			{
				Cell cell = KingdomNativeCampFounding.Clear(Zone);
				Require(cell != null, "no clear cell was available for a deposit-overflow case");
				return cell;
			}

			private static void Require(bool Value, string Failure)
			{
				KingdomDepositOverflowNativeChecks.Require(Value, Failure);
			}
		}
	}
}
