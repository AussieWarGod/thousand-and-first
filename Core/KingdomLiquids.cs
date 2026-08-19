using XRL.World.Parts;

namespace ThousandAndFirst
{
	/// <summary>Small, explicit adapters around Qud's liquid API.</summary>
	public static class KingdomLiquids
	{
		/// <summary>
		/// True only for a positive volume of pure fresh water. Water being the primary
		/// component is not sufficient: Qud's salt pools are water-primary mixtures.
		/// </summary>
		public static bool HasFreshWater(LiquidVolume Source)
		{
			return Source != null && Source.Volume > 0 && Source.IsFreshWater();
		}

		/// <summary>Whether an empty or already-pure vessel may receive fresh water.</summary>
		public static bool CanReceiveFreshWater(LiquidVolume Target)
		{
			return Target != null && Target.IsFreshWater(AllowEmpty: true);
		}

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

		/// <summary>
		/// Adds up to <paramref name="Drams"/> and returns the amount actually added.
		/// LiquidVolume.AddDrams silently clamps to the space available and returns true
		/// regardless, so its boolean must not be used for accounting either. Measuring the
		/// delta also survives the mixing path, which can accept a different amount again.
		/// </summary>
		public static int Fill(LiquidVolume Target, string Liquid, int Drams)
		{
			if (Target == null || Drams <= 0)
			{
				return 0;
			}
			int before = Target.Volume;
			Target.AddDrams(Liquid, Drams);
			int added = Target.Volume - before;
			return (added > 0) ? added : 0;
		}
	}
}
