using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>The standing-body snapshot for <see cref="KingdomDepositOverflowNativeChecks"/>,
	/// kept in its own shard so the checks shard stays under the line cap.</summary>
	internal static partial class KingdomDepositOverflowNativeChecks
	{
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
	}
}
