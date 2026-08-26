using System.Collections.Generic;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	/// <summary>
	/// The engine-free arithmetic and prose behind the five co-opted ceremonies
	/// (<c>KingdomCeremony</c> is the engine-coupled shell): the surveyor's plan staked ahead of
	/// a building, the raising ceremony that closes it, the tastes and traits a settling notable
	/// carries, and the pattern-book a chartered caravan occasionally opens. Every kernel draw
	/// here follows the split <c>KingdomVoiceRules.ChooseSpeaker</c> already established: the
	/// caller supplies a settlement id and an ordinal (both plain data, no engine reference
	/// required to produce them), and the draw itself &mdash; kernel included &mdash; is pure and
	/// unit-testable. A key that cannot be built or a kernel that refuses always falls back to a
	/// fixed, still-correct answer (index zero, "no"), never to an exception.
	/// </summary>
	public static partial class KingdomCeremonyRules
	{
		private const int CeremonyRulesVersion = 1;

		/// <summary>Fixed, all-zero seed, exactly as <c>KingdomChronicle</c> and
		/// <c>KingdomVoiceRules</c> use: domain separation comes entirely from the settlement id,
		/// stream, kind, and ordinal folded into each draw's key, so a shared all-zero seed can
		/// never alias two different ceremonies onto the same roll.</summary>
		private static readonly KernelSeed128 CeremonySeed = default(KernelSeed128);

		// ==================================================================================
		// The surveyor's plan
		// ==================================================================================

		/// <summary>
		/// Composes the plan a founder reads on a freshly staked marker, framed as intention
		/// rather than as the finished building's own description. One hand-written template per
		/// <see cref="KingdomRules.BuildEntry.Category"/> family, each carrying a tier slot (the
		/// design's own <see cref="GrowthStage"/>) and a material slot (a skin key, a future
		/// material name, or any other short flavour word a caller has on hand).
		/// <para>
		/// A category this file does not recognise &mdash; a third-party mod's own invention
		/// &mdash; falls back to a plain, honest line rather than a templated sentence wearing
		/// the wrong family's clothes. The fallback names no family, no tier, and no material: it
		/// is "plain stakes," not filler dressed up as a template.
		/// </para>
		/// </summary>
		/// <param name="Category">The design's <c>Category</c> attribute. Case-insensitive;
		/// null or unrecognised both fall back to plain stakes.</param>
		/// <param name="BuildingName">The design's display name. Null or empty reads as "the
		/// work".</param>
		/// <param name="Tier">The design's minimum growth stage, spoken as an adjective
		/// ("a steading's", "a city's").</param>
		/// <param name="MaterialFlavor">A short material or skin word, or null/empty to fall back
		/// to "plain stock" within the template &mdash; still a real sentence, just an
		/// unspecified material rather than missing text.</param>
		/// <returns>A capitalised, single-sentence description ending in a period, fit for a
		/// <c>Description.Short</c>. Never null or empty.</returns>
		public static string SurveyorsPlanText(string Category, string BuildingName, GrowthStage Tier, string MaterialFlavor)
		{
			string name = string.IsNullOrEmpty(BuildingName) ? "the work" : BuildingName;
			string tier = TierWord(Tier);
			string material = string.IsNullOrEmpty(MaterialFlavor) ? "plain stock" : MaterialFlavor;
			switch (Normalize(Category))
			{
			case "food":
				return "The plan for " + name + " is staked: " + tier + " table, raised in " + material + ", meant to keep the larder honest.";
			case "storage":
				return "The plan for " + name + " is staked: " + tier + " keeping-place, walled in " + material + ", meant to hold what the settlement cannot yet spend.";
			case "civic":
				return "The plan for " + name + " is staked: " + tier + " gathering-ground, built of " + material + ", meant for the business no one settler can do alone.";
			case "craft":
				return "The plan for " + name + " is staked: " + tier + " working-floor, framed in " + material + ", meant to turn hands into goods.";
			case "power":
				return "The plan for " + name + " is staked: " + tier + " engine-house, set in " + material + ", meant to carry a load no back should.";
			case "faith":
				return "The plan for " + name + " is staked: " + tier + " quiet room, raised in " + material + ", meant for whatever the settlement still believes.";
			case "memorial":
				return "The plan for " + name + " is staked: " + tier + " remembering-place, cut in " + material + ", meant to outlast whoever asked for it.";
			case "housing":
				return "The plan for " + name + " is staked: " + tier + " roof, walled in " + material + ", meant for a household that has not moved in yet.";
			case "defense":
				return "The plan for " + name + " is staked: " + tier + " standing wall, built of " + material + ", meant to cost a raider more than it cost the settlement.";
			case "knowledge":
				return "The plan for " + name + " is staked: " + tier + " keeping of what is known, written in " + material + ", meant to outlive the keeper who writes it.";
			default:
				return "The plan for " + name + " is staked: plain stakes in the ground, and nothing more written yet.";
			}
		}

		private static string TierWord(GrowthStage Tier)
		{
			switch (Tier)
			{
			case GrowthStage.Camp:
				return "a camp's";
			case GrowthStage.Steading:
				return "a steading's";
			case GrowthStage.Village:
				return "a village's";
			case GrowthStage.Town:
				return "a town's";
			case GrowthStage.City:
				return "a city's";
			default:
				return "the settlement's";
			}
		}

		private static string Normalize(string Text)
		{
			return string.IsNullOrEmpty(Text) ? null : Text.Trim().ToLowerInvariant();
		}

	}
}
