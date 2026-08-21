using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	/// <summary>
	/// The engine-coupled shell for the five co-opted ceremonies
	/// (<see cref="KingdomCeremonyRules"/> owns the arithmetic and every hand-written line):
	/// the surveyor's plan staked ahead of a building and quoted when it rises, the raising
	/// ceremony that closes construction attended or not, the tastes and traits a settling
	/// notable carries, and the pattern-book a chartered caravan occasionally opens. Every entry
	/// point here is a single call for another system to make; none of them own a clock or a
	/// pass of their own.
	/// </summary>
	public static class KingdomCeremony
	{
		public static bool Enabled => Options.GetOption("r_TAF_OptionCeremony") != "No";

		/// <summary>String property carrying the surveyor's plan text from a staked marker
		/// through its scaffold to the moment the chronicle quotes it. Never present on a design
		/// raised without ever being staked (a direct commission) &mdash; absence is a normal
		/// state, not a fault.</summary>
		public const string SurveyorsPlanProperty = "KingdomSurveyorsPlan";

		// ==================================================================================
		// The surveyor's plan
		// ==================================================================================

		/// <summary>
		/// Writes the surveyor's plan onto a freshly staked marker: a lookable description
		/// framed as intention, and the same text stashed on a string property so it survives
		/// the marker's own destruction when the plan is realised. Call once, right after
		/// <c>r_KingdomPlanMarker.ApplyDesign</c>.
		/// </summary>
		/// <param name="Marker">The freshly created marker object.</param>
		/// <param name="Entry">The design staked.</param>
		/// <param name="SkinFlavor">The chosen skin's key, or null. Purely decorative &mdash;
		/// absence falls back to "plain stock" inside the template, never to missing text.</param>
		public static void StakePlan(GameObject Marker, KingdomRules.BuildEntry Entry, string SkinFlavor)
		{
			if (!Enabled || Marker == null || Entry == null)
			{
				return;
			}
			KingdomSystem.Guard("ceremony: stake plan", delegate
			{
				string text = KingdomCeremonyRules.SurveyorsPlanText(Entry.Category, Entry.Name, Entry.MinStage, SkinFlavor);
				Marker.SetStringProperty(SurveyorsPlanProperty, text);
				Marker.RequirePart<Description>().Short = text;
			});
		}

		/// <summary>
		/// Carries the staked plan's text from a marker onto the scaffold it becomes, the same
		/// way <c>KingdomDesign.StageSkin</c> carries the chosen skin. Call once, in
		/// <c>KingdomPlanMarker.Realize</c>, before the marker is destroyed.
		/// </summary>
		public static void TransferPlanQuote(GameObject Marker, GameObject Scaffold)
		{
			if (!Enabled || Marker == null || Scaffold == null)
			{
				return;
			}
			KingdomSystem.Guard("ceremony: transfer plan quote", delegate
			{
				string text = Marker.GetStringProperty(SurveyorsPlanProperty);
				if (!string.IsNullOrEmpty(text))
				{
					Scaffold.SetStringProperty(SurveyorsPlanProperty, text);
				}
			});
		}

		// ==================================================================================
		// The raising ceremony
		// ==================================================================================

		/// <summary>
		/// Closes construction: while attended, gathers whichever settlers are standing in the
		/// zone, shares a small measure of water, and chronicles who was there; while unattended,
		/// leaves a plainer chronicle line and a homecoming note instead. Replaces the deed and
		/// chronicle a scaffold's own completion used to write directly.
		/// </summary>
		/// <param name="System">The realm. Null or unfounded is a no-op &mdash; nothing here can
		/// fire before a settlement exists to own it.</param>
		/// <param name="Cell">The cell the finished building now stands on, read for its zone.
		/// May be null; the ceremony still records the deed with no crew found.</param>
		/// <param name="DisplayName">The finished building's name.</param>
		/// <param name="CompleteTick">The scaffold's own due tick, read before its destruction.</param>
		/// <param name="PlanQuote">The surveyor's plan text carried onto the scaffold, or null
		/// when this design was never staked as a plan.</param>
		public static void OnBuildingRaised(KingdomSystem System, Cell Cell, string DisplayName, long CompleteTick, string PlanQuote)
		{
			if (System == null || !System.Founded || string.IsNullOrEmpty(DisplayName))
			{
				return;
			}
			KingdomSystem.Guard("ceremony: raising", delegate
			{
				System.RecordDeed("the " + DisplayName + " raised at " + System.KingdomDisplayName);
				if (!Enabled)
				{
					KingdomChronicle.Record(System, "the " + DisplayName + " was raised at " + System.KingdomDisplayName);
					MessageQueue.AddPlayerMessage("{{G|The " + DisplayName + " is complete.}}");
					return;
				}
				Zone zone = (Cell != null) ? Cell.ParentZone : null;
				bool attended = KingdomCeremonyRules.IsAttended(CompleteTick, CurrentTicks());
				if (attended)
				{
					List<string> present = NearbyCitizenNames(zone);
					if (zone != null)
					{
						KingdomSurvey.Take(zone).Consume(KingdomCeremonyRules.RaisingShareDrams);
					}
					KingdomChronicle.Record(System, KingdomCeremonyRules.RaisingAttendedChronicle(DisplayName, System.SeatName, present, PlanQuote));
					MessageQueue.AddPlayerMessage(KingdomCeremonyRules.RaisingAttendedMessage(DisplayName, present));
				}
				else
				{
					KingdomChronicle.Record(System, KingdomCeremonyRules.RaisingUnattendedChronicle(DisplayName, System.SeatName, PlanQuote));
					System.Ledger.Note(KingdomCeremonyRules.RaisingLedgerNote(DisplayName));
					MessageQueue.AddPlayerMessage("{{G|The " + DisplayName + " is complete.}}");
				}
				KingdomLog.Log("ceremony: raised " + DisplayName + " attended=" + attended);
			});
		}

		/// <summary>Up to three named settlers standing in Z, for the raising ceremony's roll
		/// call. Not distance-scoped &mdash; the same zone-wide scope every other attended pass
		/// in this mod already uses (<c>KingdomOffices</c>, <c>KingdomLocus</c>).</summary>
		private static List<string> NearbyCitizenNames(Zone Z)
		{
			List<string> names = new List<string>();
			if (Z == null)
			{
				return names;
			}
			foreach (GameObject item in Z.GetObjects())
			{
				if (names.Count >= 3)
				{
					break;
				}
				if (item.GetIntProperty("KingdomBorn") != 1)
				{
					continue;
				}
				string name = item.GetStringProperty("KingdomName");
				if (!string.IsNullOrEmpty(name))
				{
					names.Add(name);
				}
			}
			return names;
		}

		// ==================================================================================
		// Notable tastes and leader traits
		// ==================================================================================

		/// <summary>
		/// Ceremony for a settling notable: states one or two tastes in prose and carries one
		/// virtue and one flaw, drawn once and never rerolled. Call from the office's own
		/// transition check (<c>KingdomOffices.UpdateOffice</c>) whenever a holder is newly
		/// named or the office passes to someone else &mdash; never on vacancy, which names
		/// nobody to settle in.
		/// </summary>
		/// <param name="System">The realm.</param>
		/// <param name="Z">The zone the new holder was found standing in, read for which
		/// building categories already stand there. May be null; every taste then reads unmet.</param>
		/// <param name="Title">The office's own title, for the leader-trait line.</param>
		/// <param name="HolderName">The settler now holding office.</param>
		/// <param name="Holder">The settler themselves, for the quality-of-life vocabulary's own
		/// Prefers (Addendum 4). Null skips that half and changes nothing else.</param>
		/// <param name="QuartersKey">Design key of what they were housed in. Null is a notable
		/// nobody has housed yet, whose Prefers are simply their default.</param>
		public static void OnOfficeHolderNamed(KingdomSystem System, Zone Z, string Title, string HolderName, GameObject Holder = null, string QuartersKey = null)
		{
			if (!Enabled || System == null || !System.Founded || string.IsNullOrEmpty(HolderName))
			{
				return;
			}
			KingdomSystem.Guard("ceremony: notable settled", delegate
			{
				string settlementId = KingdomChronicle.SettlementId(System.KingdomFactionName);
				ulong ordinal = CurrentOrdinal();

				int virtueIndex;
				int flawIndex;
				KingdomCeremonyRules.ChooseLeaderTraits(settlementId, ordinal, out virtueIndex, out flawIndex);
				KingdomChronicle.Record(System, KingdomCeremonyRules.LeaderTraitChronicle(Title, HolderName, System.SeatName, virtueIndex, flawIndex));
				KingdomLog.Log("ceremony: leader traits " + HolderName + " virtue=" + virtueIndex + " flaw=" + flawIndex + " shade=" + KingdomCeremonyRules.LeaderShade());

				List<int> tastes = KingdomCeremonyRules.ChooseTastes(settlementId, ordinal);
				List<bool> met = KingdomCeremonyRules.TastesMet(tastes, TasteOfferIn(Z));
				string tasteLine = KingdomCeremonyRules.TasteChronicle(HolderName, tastes, met);
				KingdomChronicle.Record(System, tasteLine);
				MessageQueue.AddPlayerMessage("{{W|" + XRL.Language.Grammar.InitCap(tasteLine) + ".}}");
				// Addendum 4 routes a resident's met Prefers through this same machinery rather than
				// opening a second road to equilibrium: one shade, and one balance to keep. The two
				// halves are shaded separately so each keeps its own cap, and the chronicle's own
				// met-list is left exactly as it was.
				int shade = KingdomCeremonyRules.TasteShade(met) + KingdomQol.PreferShade(Holder, QuartersKey);
				KingdomLog.Log("ceremony: tastes " + HolderName + " shade=" + shade);
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
			foreach (GameObject item in Z.GetObjects())
			{
				if (item.GetIntProperty("KingdomBuilt") != 1)
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
		/// Offers the founder a choice of one foreign design out of up to three, if this
		/// caravan's arrival happens to carry one. Call once per zone-activation pass that
		/// delivered under an active trade charter, after the deal loop. A no-op whenever there
		/// is no undiscovered pattern-book design to offer, so it never costs anything to check.
		/// </summary>
		/// <param name="System">The realm.</param>
		/// <param name="Z">The activated zone, for nothing but the popup's context; the offer
		/// itself is realm-wide.</param>
		public static void OnCaravanArrived(KingdomSystem System, Zone Z)
		{
			if (!Enabled || System == null || !System.Founded)
			{
				return;
			}
			KingdomSystem.Guard("ceremony: pattern-book", delegate
			{
				List<KingdomCeremonyRules.BuildingKnowledge> knowledge = new List<KingdomCeremonyRules.BuildingKnowledge>();
				foreach (KingdomRules.BuildEntry entry in KingdomData.Buildings)
				{
					knowledge.Add(new KingdomCeremonyRules.BuildingKnowledge
					{
						Key = entry.Key,
						Knowledge = KingdomZoning.GateFor(entry.Key).Knowledge
					});
				}
				List<KingdomCeremonyRules.ForeignDesign> candidates = KingdomCeremonyRules.ForeignDesigns(knowledge, KingdomZoning.Roster(System));
				if (candidates.Count == 0)
				{
					return;
				}
				string settlementId = KingdomChronicle.SettlementId(System.KingdomFactionName);
				ulong ordinal = CurrentOrdinal();
				if (!KingdomCeremonyRules.ShouldOfferPattern(settlementId, ordinal))
				{
					return;
				}
				List<KingdomCeremonyRules.ForeignDesign> remaining = new List<KingdomCeremonyRules.ForeignDesign>(candidates);
				List<KingdomCeremonyRules.ForeignDesign> offer = new List<KingdomCeremonyRules.ForeignDesign>();
				for (int step = 0; step < 3 && remaining.Count > 0; step++)
				{
					int index = KingdomCeremonyRules.PickPatternIndex(settlementId, ordinal, step, remaining.Count);
					if (index < 0 || index >= remaining.Count)
					{
						index = 0;
					}
					offer.Add(remaining[index]);
					remaining.RemoveAt(index);
				}
				if (offer.Count == 0)
				{
					return;
				}
				string[] options = new string[offer.Count];
				for (int i = 0; i < offer.Count; i++)
				{
					options[i] = "{{W|" + PatternLabel(offer[i]) + "}} {{K|(a foreign pattern)}}";
				}
				int pick = Popup.PickOption(Title: "A pattern-book, offered", Intro: "A caravan's driver spreads three foreign patterns and offers the settlement its pick of one. Nothing carried is spent, and the settlement's own catalogue loses nothing either way.", Options: options, AllowEscape: true);
				if (pick < 0 || pick >= offer.Count)
				{
					return;
				}
				string label = PatternLabel(offer[pick]);
				if (KingdomZoning.Learn(System, KingdomCeremonyRules.PatternKnowledgeKind, offer[pick].LearnName))
				{
					KingdomChronicle.Record(System, "the keepers of " + System.KingdomDisplayName + " learned " + XRL.Language.Grammar.A(label) + " from a caravan's pattern-book");
					MessageQueue.AddPlayerMessage("{{G|The pattern for " + label + " is learned.}}");
					KingdomLog.Log("ceremony: pattern learned " + offer[pick].LearnName + " for " + System.KingdomFactionName);
				}
			});
		}

		private static string PatternLabel(KingdomCeremonyRules.ForeignDesign Design)
		{
			KingdomRules.BuildEntry entry;
			if (Design != null && KingdomData.TryGetBuilding(Design.BuildingKey, out entry))
			{
				return entry.Name;
			}
			return (Design != null) ? Design.LearnName : "a pattern";
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
