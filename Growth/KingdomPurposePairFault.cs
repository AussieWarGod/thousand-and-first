namespace ThousandAndFirst
{
	public enum KingdomPurposePairFault : byte
	{
		None = 0,
		Malformed = 1,
		UnknownKind = 2,
		Incompatible = 3,
		WrongRecipe = 4,
		Accounting = 5,
		Identity = 6,
		Phase = 7,
		Transition = 8,
		Bounds = 9,
		Canonical = 10
	}
}
