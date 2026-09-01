using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public static partial class KingdomCeremony
	{

		// ==================================================================================
		// Notable tastes and leader traits
		// ==================================================================================

		/// <summary>
		/// Ceremony for a settling notable: states one or two tastes in prose and carries one
		/// virtue and one flaw, drawn once and never rerolled. A caller may use it only after the
		/// explicit office appointment has named its exact holder &mdash; never on vacancy, which
		/// names nobody to settle in.
		/// </summary>
		/// <param name="System">The realm.</param>
		/// <param name="Z">The zone the new holder was found standing in, read for which
		/// building categories already stand there. May be null; every taste then reads unmet.</param>
		/// <param name="Title">The office's own title, for the leader-trait line.</param>
		/// <param name="HolderName">The settler now holding office.</param>
		/// <param name="Holder">Legacy API parameter retained for binary/source compatibility;
		/// narrative office naming does not score the holder.</param>
		/// <param name="QuartersKey">Legacy API parameter retained for compatibility; housing
		/// preferences do not price a title into settlement capacity.</param>
		/// <remarks>The ceremony is narrative only. It explicitly retires the read-compatible
		/// <c>KingdomSystem.NotableShade</c> carrier to zero; an optional civic title grants no
		/// capacity, service, capability, or succession claim.</remarks>
		public static void OnOfficeHolderNamed(KingdomSystem System, Zone Z, string Title, string HolderName, GameObject Holder = null, string QuartersKey = null)
		{
			if (!Enabled || System == null || !System.Founded || string.IsNullOrEmpty(HolderName))
			{
				return;
			}
			KingdomSystem.Guard("ceremony: notable settled", delegate
			{
				string settlementId = KingdomChronicle.SettlementId(System);
				if (!KingdomIdentityRules.IsSettlementId(settlementId)) return;
				ulong ordinal = CurrentOrdinal();

				int virtueIndex;
				int flawIndex;
				KingdomCeremonyRules.ChooseLeaderTraits(settlementId, ordinal, out virtueIndex, out flawIndex);
				string shownHolder = KingdomPresentation.Rich(HolderName);
				KingdomChronicle.Record(System, KingdomCeremonyRules.LeaderTraitChronicle(
					Title, shownHolder, KingdomPresentation.Rich(System.SeatName), virtueIndex,
					flawIndex));
				KingdomLog.Log("ceremony: leader traits " + HolderName + " virtue=" + virtueIndex + " flaw=" + flawIndex);

				List<int> tastes = KingdomCeremonyRules.ChooseTastes(settlementId, ordinal);
				List<bool> met = KingdomCeremonyRules.TastesMet(tastes, TasteOfferIn(Z));
				string tasteLine = KingdomCeremonyRules.TasteChronicle(shownHolder, tastes, met);
				KingdomChronicle.Record(System, tasteLine);
				MessageQueue.AddPlayerMessage("{{W|" + XRL.Language.Grammar.InitCap(tasteLine) + ".}}");
				// Older builds priced this title into settlement capacity. Preserve the serialized
				// carrier for save compatibility, but retire it at every legacy naming entry point.
				// Tastes remain visible fiction above; they are not an invisible reward.
				System.NotableShade = 0;
				KingdomLog.Log("ceremony: tastes " + HolderName + " (title-only; shade=0)");
			});
		}

		/// <summary>
		/// What this settlement offers a notable's stated tastes: one tag per built structure's
		/// category, read off the same <c>KingdomBuildKey</c>/<c>KingdomData</c> lookup the rest
		/// of the mod uses to recognise a completed work. Addendum 4's re-basing &mdash; the taste
		/// and the building meet in the shared vocabulary (<c>KingdomCeremonyRules.TastesMet</c>)
		/// rather than by a category-string comparison private to this file.
		/// </summary>
		/// <returns>Never null; empty for a zone with nothing standing, which meets no taste.
		/// </returns>
		private static string[] TasteOfferIn(Zone Z)
		{
			if (Z == null)
			{
				return KingdomQolRules.NoTags;
			}
			List<string> offer = new List<string>();
			KingdomSurvey survey = KingdomSurvey.ActiveFor(Z) ?? KingdomSurvey.Take(Z);
			foreach (GameObject item in survey.Built)
			{
				if (!KingdomUpgrade.IsFunctionallyBuilt(item))
				{
					continue;
				}
				string buildKey = item.GetStringProperty(KingdomUpgrade.BuildKeyProperty);
				if (string.IsNullOrEmpty(buildKey))
				{
					continue;
				}
				KingdomRules.BuildEntry entry;
				if (!KingdomData.TryGetBuilding(buildKey, out entry))
				{
					continue;
				}
				string tag = KingdomCeremonyRules.CategoryTag(entry.Category);
				if (!string.IsNullOrEmpty(tag) && !offer.Contains(tag))
				{
					offer.Add(tag);
				}
			}
			return (offer.Count == 0) ? KingdomQolRules.NoTags : offer.ToArray();
		}

		// ==================================================================================
		// The pattern-book
		// ==================================================================================

		/// <summary>
		/// Freezes the optional offer into its owning CharterDelivery before that operation mutates
		/// resources. Only the seated city's stored keeper roster participates; catalogue rows are
		/// copied now, so reload and later catalogue drift cannot reroll labels or keys.
		/// </summary>
		public static KingdomTradePatternReceipt FreezePatternBook(KingdomSystem System,
			string SettlementId, long OperationSequence)
		{
			List<KingdomTradePatternDesign> frozen = new List<KingdomTradePatternDesign>();
			if (Enabled && System != null && System.Founded
				&& System.City != null && string.Equals(System.City.SettlementId,
					SettlementId, StringComparison.Ordinal))
			{
				List<KingdomCeremonyRules.BuildingKnowledge> knowledge =
					new List<KingdomCeremonyRules.BuildingKnowledge>();
				foreach (KingdomRules.BuildEntry entry in KingdomData.Buildings)
				{
					knowledge.Add(new KingdomCeremonyRules.BuildingKnowledge
					{
						Key = entry.Key,
						Knowledge = KingdomZoning.GateFor(entry.Key).Knowledge,
						Label = entry.Name
					});
				}
				List<string> stored = KingdomZoningRules.DecodeRoster(System.KeepersRoster);
				List<KingdomCeremonyRules.ForeignDesign> candidates =
					KingdomCeremonyRules.ForeignDesigns(knowledge, stored);
				for (int i = 0; i < candidates.Count; i++)
				{
					KingdomCeremonyRules.ForeignDesign candidate = candidates[i];
					frozen.Add(new KingdomTradePatternDesign
					{
						BuildingKey = candidate.BuildingKey,
						LearnName = candidate.LearnName,
						Label = string.IsNullOrEmpty(candidate.Label)
							? candidate.LearnName : candidate.Label
					});
				}
			}
			return KingdomTradePatternRules.Freeze(SettlementId, OperationSequence, frozen);
		}

		/// <summary>One UI callback over the already-frozen operation-owned offer.</summary>
		public static int PickPatternBook(KingdomTradePatternReceipt Receipt)
		{
			if (Receipt?.Offers == null || Receipt.Offers.Count == 0) return -1;
			string[] options = new string[Receipt.Offers.Count];
			for (int i = 0; i < Receipt.Offers.Count; i++)
				options[i] = "{{W|" + Receipt.Offers[i].Label
					+ "}} {{K|(a foreign pattern)}}";
			return Popup.PickOption(Title: "A pattern-book, offered",
				Intro: "A caravan's driver spreads up to three foreign patterns and offers this settlement its pick of one. Nothing carried is spent, and the settlement's own catalogue loses nothing either way.",
				Options: options, AllowEscape: true);
		}

		private static ulong CurrentOrdinal()
		{
			return CurrentTicks() > 0L ? (ulong)CurrentTicks() : 0uL;
		}

		private static long CurrentTicks()
		{
			return (The.Game != null) ? The.Game.TimeTicks : 0L;
		}
	}
}
