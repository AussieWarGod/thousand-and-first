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
	}
}
