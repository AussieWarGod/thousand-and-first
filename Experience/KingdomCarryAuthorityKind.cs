namespace ThousandAndFirst
{

	/// <summary>Which physical contract owns one carry operation. Value zero is the exact
	/// historical v5 remove/project graph and may only be decoded and reconciled. New carry-sign
	/// work always publishes <see cref="ExactManifest"/>.</summary>
	public enum KingdomCarryAuthorityKind : byte
	{
		LegacyMaterialProjection = 0,
		ExactManifest = 1
	}
}
