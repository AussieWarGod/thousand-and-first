using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomMaterials
	{
		// --- Callback-free observation ----------------------------------------------------------
		//
		// Everything here answers a question WITHOUT asking the engine anything that can answer
		// back. The settlement counts ordinarily everywhere else; a delivery counts raw, because
		// its readings licence a destruction, an insertion, or a credit, and the last thing it
		// looks at must not be the thing that moved the bundle.

		/// <summary>
		/// The count standing on one object, taken RAW and NOT normalised. <c>GameObject.Count</c>
		/// reaches <c>Stacker.Number</c>, which repairs a nonpositive count by assigning one and
		/// sends <c>StackCountChangedEvent</c> for it; the <c>StackCount</c> getter beside it
		/// simply returns the field. A delivery's proofs read the field, so the last thing it
		/// looks at before destroying or inserting a body can never be the thing that moved it.
		/// <para>
		/// A malformed count comes back AS IT IS &mdash; zero, or negative. This is the reading a
		/// PROOF uses, and a proof must be able to fail: the engine's own stacking adds the
		/// incoming <c>StackCount</c> to the stack it merges into, so a body carrying minus one
		/// would take a unit OUT of a stack already lying there. A thing that does not stack is
		/// one thing.
		/// </para>
		/// </summary>
		internal static int RawPhysicalCountOf(GameObject Item)
		{
			if (Item == null)
			{
				return 0;
			}
			Stacker stacker = Item.Stacker;
			return (stacker == null) ? 1 : stacker.StackCount;
		}

		/// <summary>
		/// What one thing standing in a store OCCUPIES, for a census. Here, and only here, a
		/// malformed count reads as one: a broken stack is still a thing lying in the chest taking
		/// up a place, and the engine's own repair would make it one the moment anybody asked. It
		/// is not written back, and it is NOT a proof that anything may be inserted &mdash; that
		/// is <see cref="RawPhysicalCountOf"/>, which lets a malformed body fail.
		/// </summary>
		internal static int RawCensusCountOf(GameObject Item)
		{
			int raw = RawPhysicalCountOf(Item);
			return (raw > 0) ? raw : 1;
		}

		/// <summary>
		/// The same room, taken RAW: capacity off the blueprint tag, less a hold counted without
		/// asking any object a question that could answer back. This is the reading a batch is
		/// finally judged against, and it is a whole-occupancy reading exactly as the ordinary one
		/// is, because everything in a chest takes up the space it takes up (ruling 5).
		/// </summary>
		internal static int DepositRawRoomNow(GameObject Container)
		{
			if (!GameObject.Validate(Container) || Container.Inventory == null
				|| !IsStockpile(Container))
			{
				return 0;
			}
			int room = KingdomSurvey.StockCapacityOf(Container) - RawStockHeldIn(Container);
			return (room > 0) ? room : 0;
		}

		/// <summary>
		/// What a store physically holds, counted RAW and in one pass. The vocabulary is the
		/// survey's own, so this and <see cref="KingdomSurvey.StockHeldIn"/> answer the same
		/// question about the same things; only the count read differs, and only here, where the
		/// answer licences a destruction or an insertion.
		/// </summary>
		private static int RawStockHeldIn(GameObject Container)
		{
			int held = 0;
			List<GameObject> objects = Container.Inventory.Objects;
			for (int i = 0; i < objects.Count; i++)
			{
				GameObject item = objects[i];
				if (item == null)
				{
					continue;
				}
				// Eligibility only, and callback-free: TryMaterialOf and TryExoticOf read a tag
				// and a blueprint, and UnitBits asks what ONE of a thing is worth, which is a
				// part lookup and a bit-cost table. TryBitsOf beside it multiplies that by the
				// thing's ORDINARY count, which repairs a nonpositive one and dispatches for it
				// -- inside a walk, where the handler can move a row already counted, or the row
				// the walk is standing on, and leave this number describing no store that exists.
				if (TryMaterialOf(item, out _) || TryExoticOf(item, out _)
					|| !UnitBits(item).IsEmpty())
				{
					held += RawCensusCountOf(item);
				}
			}
			return held;
		}

		/// <summary>
		/// Units of ONE blueprint standing in an exact destination right now, and nothing at all
		/// once it has stopped being a destination. Read either side of an insertion, this is how
		/// the delivery learns what a store actually gained when the bundle it made stopped being
		/// the thing the units arrived in.
		/// <para>
		/// Deliberately NARROWER than the room reading beside it. Room is whole-occupancy, because
		/// everything in a chest takes up the space it takes up; but a gain is evidence about ONE
		/// material, and a handler that retires the timber and drops an equal count of stone would
		/// otherwise pay this delivery in full for timber that never arrived.
		/// </para>
		/// </summary>
		internal static int DepositMaterialHeldNow(GameObject Container, string Blueprint)
		{
			if (!GameObject.Validate(Container) || Container.Inventory == null
				|| !IsStockpile(Container) || string.IsNullOrEmpty(Blueprint))
			{
				return 0;
			}
			return CountBlueprint(Container.Inventory.Objects, Blueprint, Container, null);
		}

		/// <summary>The same reading for open ground, which has no capacity and no designation:
		/// units of one blueprint standing in an exact cell right now.</summary>
		internal static int GroundMaterialHeldNow(Cell Ground, string Blueprint)
		{
			return (Ground != null && !string.IsNullOrEmpty(Blueprint))
				? CountBlueprint(Ground.Objects, Blueprint, null, Ground) : 0;
		}

		/// <summary>
		/// Units of one blueprint PROVED to be standing in an exact destination, counting a stack
		/// of twenty as twenty and a thing with no count at all as one.
		/// <para>
		/// Membership of the destination's own list is not the proof, because a list can hold a
		/// body that is no longer there. <c>Cell.AddObject</c> runs <c>Physics.EnterCell</c>
		/// BEFORE it appends, and a handler on the environmental update inside it may move the
		/// object to another cell; the append then happens anyway, and the cell-entry stacking
		/// that follows merges the body into a stack in the cell it actually reached and
		/// obliterates it. The requested cell is left holding a dead entry, and counting that
		/// entry would pay this delivery for a landing somewhere else. So each entry answers for
		/// itself: it exists, and its OWN custody names this destination.
		/// </para>
		/// <para>
		/// The whole pass is callback-free: <see cref="RawCensusCountOf"/> reads the field, and custody
		/// is three field reads and an int property. Nothing inside the walk can dispatch, so no
		/// row's reading can move an earlier row out from under a count already taken, and the
		/// number that comes back describes the store as it stood at one instant.
		/// </para>
		/// </summary>
		private static int CountBlueprint(IReadOnlyList<GameObject> Objects, string Blueprint,
			GameObject Container, Cell Ground)
		{
			int held = 0;
			if (Objects == null)
			{
				return 0;
			}
			for (int i = 0; i < Objects.Count; i++)
			{
				GameObject item = Objects[i];
				if (!GameObject.Validate(item) || item.Blueprint != Blueprint
					|| !StandsIn(item, Container, Ground))
				{
					continue;
				}
				held += RawCensusCountOf(item);
			}
			return held;
		}

		/// <summary>Whether one object's OWN custody names the exact destination being read. A
		/// container is named by the physics inventory back-reference and no cell; a cell by the
		/// object's current cell and no holder of any kind.</summary>
		private static bool StandsIn(GameObject Item, GameObject Container, Cell Ground)
		{
			if (Container != null)
			{
				return Item.Physics != null
					&& ReferenceEquals(Item.Physics.InInventory, Container)
					&& Item.CurrentCell == null;
			}
			return Ground != null && ReferenceEquals(Item.CurrentCell, Ground)
				&& Item.Holder == null;
		}
	}
}
