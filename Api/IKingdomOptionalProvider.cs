namespace ThousandAndFirst.Api
{
	/// <summary>Optional dependency availability for ownership and footprint providers. Return
	/// false only when the dependency is absent or disabled; it then does not register. An enabled
	/// dependency with missing capabilities must remain registered and explain its refusal through
	/// its ordinary observation contract. This property must not change the dependency or world.</summary>
	public interface IKingdomOptionalProvider
	{
		bool IsAvailable { get; }
	}
}
