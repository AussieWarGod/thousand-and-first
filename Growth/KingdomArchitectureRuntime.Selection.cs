using System;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomArchitectureRuntime
	{
		private static bool TrySelectionContext(KingdomSystem System, Zone Z,
			out ArchitectureSelectionContext Context, out string Failure)
		{
			Context = null;
			Failure = null;
			if (string.IsNullOrWhiteSpace(System.Style)
				|| System.Style.Length > KingdomArchitectureRules.MaxSelectorChars
				|| HasControl(System.Style))
				return Fail("the settlement style is absent or over the architecture selector bound",
					out Failure);
			if (!KingdomRules.IsKnownStage(System.Stage))
				return Fail("the settlement has an unknown growth stage", out Failure);
			TechLevel tech = KingdomZoning.Tech(System);
			if (!KingdomZoningRules.IsKnownTechLevel(tech))
				return Fail("the settlement has an unknown craft rung", out Failure);

			string terrain = null;
			try
			{
				GameObject current = Z.GetTerrainObject();
				terrain = current == null ? null : current.Blueprint;
			}
			catch
			{
				// The persisted founding evidence remains the exact fallback authority.
			}
			if (string.IsNullOrEmpty(terrain)) terrain = System.FoundingTerrainBlueprint;
			if (terrain != null && (terrain.Length > KingdomArchitectureRules.MaxSelectorChars
				|| HasControl(terrain)))
				return Fail("terrain evidence is over the architecture selector bound", out Failure);
			string creed = KingdomCreed.SeatCreed(System);
			if (creed != null && (creed.Length > KingdomArchitectureRules.MaxSelectorChars
				|| HasControl(creed)))
				return Fail("the dominant seat creed is over the architecture selector bound", out Failure);

			Context = new ArchitectureSelectionContext
			{
				Style = System.Style,
				Creed = creed,
				Cultures = KingdomResidentIdentityRules.FactNames(System.CultureCounts,
					KingdomZoningRules.KindCulture),
				Species = KingdomResidentIdentityRules.FactNames(System.SpeciesCounts,
					KingdomZoningRules.KindSpecies),
				Genotypes = KingdomResidentIdentityRules.IdentityNames(System.IdentityCounts,
					KingdomResidentIdentityRules.KindGenotype),
				Bodies = KingdomResidentIdentityRules.IdentityNames(System.IdentityCounts,
					KingdomResidentIdentityRules.KindBody),
				Terrain = terrain,
				Stratum = KingdomZoningRules.StratumOfGround(
					Z.Z > KingdomRules.SurfaceZLevel),
				Stage = (int)System.Stage,
				Tech = (int)tech
			};
			return true;
		}

		// --- Durable named receipt ---------------------------------------------------------

		/// <summary>
		/// Freezes a fully validated intent. Schema is removed to invalidate any old receipt, every
		/// field is written, and schema is written last as the sole commit marker.
		/// </summary>
	}
}
