using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.Rules;
using XRL.UI;
using XRL.World;
using XRL.World.AI;
using XRL.World.Conversations;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomGrowth
	{

		// ==================================================================================
		// What the fields physically make, and where it goes. Crop cycles own creation; dedicated
		// containers own custody; explicit meal/industry transactions own consumption.
		// ==================================================================================

		/// <summary>
		/// Legacy city-rate seam. Food support remains physical-lane catalogue metadata, but it is
		/// neither population support nor an abstract daily item producer.
		/// <para>
		/// Fields already deliver real crop objects through their harvest cycle; mills transform
		/// exact crop objects through <see cref="GrindHarvest"/>; larders and granaries store those
		/// objects. Returning zero prevents any storage or support score from minting food while a
		/// zone is away. Kept as an API method so callers and old tests have one explicit answer.
		/// </para>
		/// </summary>
		/// <param name="Survey">The pass's survey. Null makes nothing.</param>
		public static int FoodMadePerDay(KingdomSurvey Survey)
		{
			return 0;
		}

		/// <summary>
		/// Puts a making into the larders and is honest about whatever would not fit
		/// (STANDARDS 7b). Loss, not a queue: a harvest with nowhere to go is left in the field,
		/// the same way water the casks cannot take runs into the ground.
		/// <para>
		/// Called only by a physical harvest landing that already owns exact item creation. It does
		/// not service the retired city-rate model. The once-per-block flag and harvest ledger remain
		/// here so every caller gets one conservation and telling rule.
		/// </para>
		/// </summary>
		/// <returns>What actually reached a larder.</returns>
		public static int StoreHarvest(KingdomSystem System, KingdomSurvey Survey, int Amount)
		{
			if (!KingdomMaster.NewWorkAllowed(System)) return 0;
			if (Amount <= 0)
			{
				// Nothing made, so nothing was lost. If there is room now the block is over
				// anyway, and 7b's "once" has to be able to become "once more" the next time the
				// sentence is actually true - otherwise a settlement whose fields were struck
				// while its larders were full would never be told again.
				if (Survey.FoodSpace > 0)
				{
					System.HarvestUnstoredAnnounced = false;
				}
				return 0;
			}
			int stored = Survey.StoreFood(Amount, KingdomData.CropForStyle(System.Style));
			System.Ledger.Harvested = KingdomCatalogueRules.SaturatingCounterAdd(
				System.Ledger.Harvested, stored);
			int lost = Amount - stored;
			if (lost <= 0)
			{
				// The block lifted: room was found, so the sentence below is unsaid and may be
				// said again the next time it is true.
				System.HarvestUnstoredAnnounced = false;
				return stored;
			}
			System.Ledger.HarvestLost = KingdomCatalogueRules.SaturatingCounterAdd(
				System.Ledger.HarvestLost, lost);
			if (System.HarvestUnstoredAnnounced)
			{
				return stored;
			}
			System.HarvestUnstoredAnnounced = true;
			// One flag for one block - "the harvest has nowhere to go" - with the sentence chosen
			// for which shape the block currently has. A founder who fixes the first by
			// dedicating a chest and then fills it hears the line once more only after the room
			// they made ran out and was found again.
			string realm = KingdomPresentation.Rich(System.KingdomDisplayName);
			string line = (Survey.FoodCapacity <= 0)
				? ("The fields of " + realm + " brought in a harvest and there was nowhere to put it. Dedicate a larder, or commission one, and it will be kept.")
				: ("The larders of " + realm + " are full, and " + lost + " of the harvest was left in the field. A granary is what makes a good year last into a bad one.");
			System.Ledger.Note("{{r|" + line + "}}");
			MessageQueue.AddPlayerMessage("{{r|" + line + "}}");
			if (KingdomLog.Enabled) KingdomLog.Log("harvest: made=" + Amount + " stored=" + stored + " lost=" + lost + " cap=" + Survey.FoodCapacity);
			return stored;
		}

		/// <summary>
		/// The industry half of Addendum 11(b): the settlement's mills eat food and produce
		/// things. Real crops leave the real larders and real preserved staples go back into them
		/// &mdash; the same physical honesty the harvest already keeps, and what Addendum 12(d)
		/// asks of any consumption that lands on containers a founder can walk up to and open.
		/// <para>
		/// <b>What the machine actually does, in vanilla's own numbers.</b> Vanilla's
		/// <c>Millstone</c> carries <c>Mill</c> with blank transformation targets, so its one item
		/// per powered turn falls through to <c>Campfire.PerformPreserve</c>: a vinewafer becomes
		/// three vinewafer sheaves (<c>B/ObjectBlueprints/Foods.xml:424</c>). Our mill books the
		/// same ratio, flat across styles &mdash; <c>KingdomRules.PreserveMultiple</c>, and the
		/// reasoning for the flatness is on that constant. Two crops in, six staples back, a net
		/// of four servings, which is exactly the <c>food:4</c> the grinding mill declares.
		/// </para>
		/// <para>
		/// <b>No hidden household reserve.</b> Food has no passive upkeep bill. This transformation
		/// can only take named raw crops physically present in dedicated larders, and its operating
		/// mill bounds the daily request.
		/// </para>
		/// <para>
		/// <b>The visible machine and the accounting are different stock, on purpose.</b> The
		/// <c>Mill</c> part on the object grinds what is in the MILL'S OWN inventory while a
		/// founder is standing there (<c>WorksOnInventory</c>, <c>D/…/Mill.cs:47-51</c>), at
		/// vanilla's own per-crop numbers; this grinds the settlement's larders on the
		/// settlement's own clock. Nothing is counted twice, and a founder who hand-feeds the
		/// millstone gets vanilla's answer for their own goods.
		/// </para>
		/// </summary>
		/// <param name="System">The kingdom. Its style names the crop, and its dish the staple.</param>
		/// <param name="Survey">The pass's survey, whose counters this keeps correct.</param>
		/// <param name="Days">Whole days since the food works were last paid. Zero grinds nothing.</param>
		private static void GrindHarvest(KingdomSystem System, KingdomSurvey Survey, int Days)
		{
			if (Survey == null || Days <= 0)
			{
				return;
			}
			int owed = KingdomCatalogueRules.SaturatingCounterMultiply(
				KingdomCrops.MilledFoodPerDay(Survey), Days);
			if (owed <= 0)
			{
				return;
			}
			string crop = KingdomData.CropForStyle(System.Style);
			string staple = KingdomCrops.StapleFor(crop);
			if (string.IsNullOrEmpty(staple))
			{
				// A crop nothing in the game can bind to keep. The mill stands and turns; it
				// simply has nothing to make out of this harvest, and says so in the log rather
				// than minting a serving from nowhere.
				if (KingdomLog.Enabled) KingdomLog.Log("mill: " + crop + " has no staple to bind into; nothing ground");
				return;
			}
			// Candidate stock is still narrowed to exact crop objects by ConsumeCrop below.
			int spare = KingdomRules.MillableStock(Survey.FoodStored, System.Population);
			int wanted = KingdomRules.CropsForGain(owed);
			if (wanted > spare)
			{
				wanted = spare;
			}
			int ground = (wanted > 0) ? Survey.ConsumeCrop(crop, wanted) : 0;
			if (ground <= 0)
			{
				return;
			}
			// What came back: the crops themselves, bound, plus the gain. Conservation is stated
			// here in one line so it cannot drift - out is IN TIMES the multiple, never a figure
			// arrived at some other way.
			int made = KingdomCatalogueRules.SaturatingCounterMultiply(
				ground, KingdomRules.PreserveMultiple);
			int stored = Survey.StoreFood(made, staple);
			System.Ledger.Milled = KingdomCatalogueRules.SaturatingCounterAdd(
				System.Ledger.Milled, (stored > ground) ? (stored - ground) : 0);
			int lost = made - stored;
			if (lost > 0)
			{
				// Nowhere to put it, exactly as a harvest with a full larder has nowhere to go.
				// The same once-flag speaks for both, because it is the same block: the pantries
				// are full, and the settlement is losing what it made.
				System.Ledger.HarvestLost = KingdomCatalogueRules.SaturatingCounterAdd(
					System.Ledger.HarvestLost, lost);
			}
			if (KingdomLog.Enabled) KingdomLog.Log("mill: days=" + Days + " owed=" + owed + " spare=" + spare + " ground=" + ground + " " + crop + " -> " + made + " " + staple + " stored=" + stored);
		}

		/// <summary>
		/// Dedicates every finished work the catalogue calls a pantry that is not dedicated
		/// already, and folds it into this pass's survey so a granary raised before today is a
		/// pantry from the moment the pass notices it.
		/// <para>
		/// STANDARDS 7 is the warrant and also the whole limit: only a <c>KingdomBuilt</c> work
		/// whose blueprint is one of <c>KingdomRules.CivicLarderBlueprints</c> is taken, so a
		/// chest the player carried in and set down is never swept up. Idempotent, and a repair
		/// as much as a rule &mdash; a granary raised by a build that only knew how to auto-flag
		/// the larder shed becomes a pantry the next time its city is walked into.
		/// </para>
		/// </summary>
		private static void AdoptCivicLarders(KingdomSurvey Survey)
		{
			for (int i = 0; i < Survey.Built.Count; i++)
			{
				GameObject work = Survey.Built[i];
				if (!GameObject.Validate(work) || work.Inventory == null || work.GetIntProperty("KingdomLarder") == 1)
				{
					continue;
				}
				if (KingdomRules.IsCivicLarderBlueprint(work.Blueprint) && Survey.AdoptLarder(work))
				{
					KingdomLog.Log("larder: dedicated commissioned " + work.Blueprint);
				}
			}
		}
	}
}
