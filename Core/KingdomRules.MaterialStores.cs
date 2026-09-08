namespace ThousandAndFirst
{
	public static partial class KingdomRules
	{
		// --- Material stores: how much one dedicated stockpile holds --------------------------
		//
		// Units, not weight and not slots. A material is one stacked object whose Count IS the
		// unit count, so a slot limit would cap nothing; and the mod reads Physics.Weight
		// nowhere, so a weight limit would be a second, invisible economy the founder could not
		// read off a chest. The food side already settled this shape for a pantry
		// (LarderCapacityTag / DefaultLarderCapacity); this is that shape for stone and timber.

		/// <summary>
		/// Blueprint tag a container declares its material capacity with, in units. Absent on an
		/// ordinary chest the founder walked up to and dedicated, which then gets
		/// <see cref="DefaultStockpileCapacity"/>.
		/// <para>
		/// A SEPARATE account from <see cref="LarderCapacityTag"/> on purpose: one chest may be a
		/// larder and a stockpile at once, and "how many servings" and "how many units of stone"
		/// are two questions with two answers.
		/// </para>
		/// </summary>
		public const string StockpileCapacityTag = "r_KingdomStockpileCapacity";

		/// <summary>Set on a dedicated stockpile once the founder has been told it will take no
		/// more. Cleared by the next delivery that finds room in it, so a store that fills twice
		/// is spoken about twice and one that stays full is spoken about once (STANDARDS 7b).
		/// </summary>
		public const string StockpileFullAnnouncedProperty = "KingdomStockpileFullAnnounced";

		/// <summary>What a stockpile with no declared capacity holds. A chest the founder walked
		/// up to and dedicated: one early building bill's worth of stone, not a programme's.
		/// </summary>
		public const int DefaultStockpileCapacity = 32;

		/// <summary>The camp heart's own store. Deliberately the same as a hand-dedicated chest:
		/// the camp is a camp, and a commissioned storehouse is the answer to wanting more.
		/// </summary>
		public const int HeartStockpileCapacity = 32;

		/// <summary>The first commissioned store.</summary>
		public const int StorehouseCapacity = 96;

		/// <summary>The commissioned store's second rung.</summary>
		public const int StoreyardCapacity = 192;

		/// <summary>The commissioned store's third rung.</summary>
		public const int StorehallCapacity = 384;

		/// <summary>A dry shelf added to a roofed camp.</summary>
		public const int ShelfCapacity = 48;

		/// <summary>A locker added to a roofed camp.</summary>
		public const int LockerCapacity = 64;

		/// <summary>
		/// A declared stockpile capacity, read back safely. Zero, absent, or negative is a
		/// container that never said, and gets <see cref="DefaultStockpileCapacity"/> &mdash;
		/// never zero, because a dedicated stockpile that could hold nothing would refuse every
		/// delivery the settlement ever earned and look, from outside, like a broken haul.
		/// </summary>
		public static int StockpileCapacity(int Declared)
		{
			return (Declared > 0) ? Declared : DefaultStockpileCapacity;
		}

		/// <summary>
		/// How many units one delivery may put into a store in a single insertion: what is left to
		/// deliver, the room the store had when the delivery chose it, the room it has RIGHT NOW
		/// re-read off the store itself, and whether the thing being placed stacks at all.
		/// <para>
		/// The live reading may only ever LOWER the batch. Creating an item and putting it into an
		/// inventory both run other people's callbacks, and a handler that drops something into
		/// this same store mid-delivery has already spent room the entry number still claims;
		/// paying the rest of the delivery out of the stale number is how a store ends up over its
		/// stated size. A live reading that is HIGHER is not taken either: this delivery was
		/// granted the room it was granted, and room something else released while it ran belongs
		/// to the next delivery to find.
		/// </para>
		/// </summary>
		/// <param name="Remaining">Units still to deliver. Nothing left places nothing.</param>
		/// <param name="Room">Room the store had when the delivery chose it.</param>
		/// <param name="LiveRoom">Room proved off the store after the last callback.</param>
		/// <param name="Stackable">Whether the item stacks. One that does not carries one unit.
		/// </param>
		/// <returns>Units for this one insertion, never above either room and never negative.
		/// </returns>
		public static int DepositBatch(int Remaining, int Room, int LiveRoom, bool Stackable)
		{
			int room = (Room < LiveRoom) ? Room : LiveRoom;
			if (Remaining < 1 || room < 1)
			{
				return 0;
			}
			if (!Stackable || Remaining < 2 || room < 2)
			{
				return 1;
			}
			return (Remaining < room) ? Remaining : room;
		}

		/// <summary>
		/// Whether a bundle that has ALREADY been stamped with its count may still be inserted,
		/// judged on the count now standing on it and the room its destination proves after the
		/// stamp.
		/// <para>
		/// Stamping a count is not a quiet assignment: it runs the engine's stack-count handlers,
		/// so every number read before the stamp is a number from before somebody else ran. A
		/// store filled to its last unit while the stamp ran refuses the whole bundle, and so does
		/// one whose count a handler moved out from under the delivery.
		/// </para>
		/// <para>
		/// A refused bundle is never re-stamped smaller and offered again: every stamp runs those
		/// same handlers, and one that keeps taking room could be asked forever. Nothing is lost
		/// by refusing &mdash; the units are still to deliver, and go to the next store with room
		/// or on the ground.
		/// </para>
		/// </summary>
		/// <param name="Batch">Units the delivery chose for this insertion.</param>
		/// <param name="Stamped">Count now standing on the bundle, read back after the stamp.
		/// </param>
		/// <param name="LiveRoom">Room the destination proves after the stamp.</param>
		public static bool DepositStampHolds(int Batch, int Stamped, int LiveRoom)
		{
			return Batch >= 1 && Stamped == Batch && LiveRoom >= Batch;
		}

		/// <summary>
		/// Units an insertion may be COUNTED for, which is what the destination ended up holding
		/// and never what the call returned. A bundle proved standing in its destination with the
		/// count it was stamped with is worth exactly that bundle. Anything else is worth only
		/// what the store itself gained while the insertion ran: a handler that merged the bundle
		/// into a stack already standing there did deliver the units, and a handler that refused,
		/// replaced, or carried it off delivered none.
		/// </summary>
		/// <param name="Batch">Units stamped on the bundle. Nothing at all counts as nothing.
		/// </param>
		/// <param name="Proved">Whether the exact bundle was proved in the exact destination.
		/// </param>
		/// <param name="HeldBefore">What the destination physically held before the insertion.
		/// </param>
		/// <param name="HeldAfter">What it physically holds after it, and after any withdrawal of
		/// a bundle that reached nobody.</param>
		/// <returns>Units to count as placed, never above the batch and never negative.</returns>
		public static int DepositLandedUnits(int Batch, bool Proved, int HeldBefore, int HeldAfter)
		{
			if (Batch < 1)
			{
				return 0;
			}
			if (Proved)
			{
				return Batch;
			}
			int gained = HeldAfter - HeldBefore;
			return (gained < 1) ? 0 : ((gained < Batch) ? gained : Batch);
		}
	}
}
