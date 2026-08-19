using XRL.World.Parts;

namespace ThousandAndFirst
{
	/// <summary>Small, explicit adapters around Qud's liquid API.</summary>
	public static class KingdomLiquids
	{
		/// <summary>
		/// Removes up to <paramref name="Drams"/> and returns the amount actually removed.
		/// LiquidVolume.UseDrams returns whether liquid remains, not whether removal succeeded,
		/// so its boolean result must never be used for accounting.
		/// </summary>
		public static int Drain(LiquidVolume Source, int Drams)
		{
			if (Source == null || Drams <= 0 || Source.Volume <= 0)
			{
				return 0;
			}
			int before = Source.Volume;
			Source.UseDrams(Drams);
			int removed = before - Source.Volume;
			return (removed > 0) ? removed : 0;
		}
	}
}
