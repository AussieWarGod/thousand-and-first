using XRL.World;
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
			return Target != null && (Target.Volume == 0 || Target.IsFreshWater());
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
		/// <summary>
		/// Pours water out onto the ground as a real pool, the way vanilla does when a container
		/// is emptied on a cell: a <c>Water</c> object whose <c>LiquidVolume</c> has
		/// <c>MaxVolume = -1</c>. This mod's own survey already classifies exactly that as open
		/// water (<see cref="KingdomSurvey"/>), so anything poured here is water the settlement
		/// can fetch back into its stores once there is room for it.
		/// <para>
		/// Used where water would otherwise have to be destroyed. The treasury is casks you can
		/// walk up to, and water nobody can see is water this mod has quietly invented a second
		/// place to keep &mdash; so the surplus becomes a puddle on the ground instead.
		/// </para>
		/// </summary>
		/// <param name="Where">Cell to pour on. Null pours nothing.</param>
		/// <param name="Drams">Amount to pour.</param>
		/// <returns>Drams actually poured, measured from the pool rather than assumed.</returns>
		public static int PourOnGround(Cell Where, int Drams)
		{
			if (Where == null || Drams <= 0)
			{
				return 0;
			}
			foreach (GameObject existing in Where.GetObjectsWithPart("LiquidVolume"))
			{
				LiquidVolume pool = existing.GetPart<LiquidVolume>();
				if (pool != null && pool.MaxVolume < 0 && CanReceiveFreshWater(pool))
				{
					return Fill(pool, "water", Drams);
				}
			}
			GameObject made = GameObject.Create("Water");
			if (made == null)
			{
				return 0;
			}
			int poured = Fill(made.GetPart<LiquidVolume>(), "water", Drams);
			if (poured <= 0)
			{
				made.Destroy(null, Silent: true);
				return 0;
			}
			Where.AddObject(made);
			return poured;
		}

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
