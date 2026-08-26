using System;
using System.Collections.Generic;
using System.Reflection;
using Qud.API;
using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.AI;
using XRL.World.Parts;
using XRL.World.Tinkering;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public sealed partial class KingdomSuccession
	{
		private static void RecordFounderDeath(KingdomSystem System, string FounderName, AfterDieEvent E)
		{
			string cause = KingdomPresentation.Rich(DeathCause(E));
			KingdomChronicle.RecordDisputed(System,
				KingdomSuccessionRules.FallenChronicle(KingdomPresentation.Rich(FounderName), KingdomPresentation.Rich(System.SeatName), cause),
				KingdomSuccessionRules.FallenRumour(KingdomPresentation.Rich(FounderName), KingdomPresentation.Rich(System.SeatName)));
		}

		private static string DeathCause(AfterDieEvent E)
		{
			string cause = !string.IsNullOrEmpty(E?.ThirdPersonReason) ? E.ThirdPersonReason
				: (!string.IsNullOrEmpty(E?.Reason) ? E.Reason
					: "died, and no one living can say how");
			return ConsoleLib.Console.ColorUtility.StripFormatting(cause);
		}

		private void PublishFounderDeath(KingdomSystem System, string FounderName, AfterDieEvent E)
		{
			if (DeathChroniclePublished || System == null || E == null)
			{
				return;
			}
			DeathChroniclePublished = true;
			KingdomSystem.Guard("succession founder death", delegate
			{
				RecordFounderDeath(System, FounderName, E);
			});
		}

		private void EndDynasty(KingdomSystem System, string FounderName, SuccessionVerdict Verdict,
			AfterDieEvent Death)
		{
			PublishFounderDeath(System, FounderName, Death);
			KingdomChronicle.Record(System,
				KingdomSuccessionRules.DynastyEndChronicle(KingdomPresentation.Rich(System.SeatName), KingdomPresentation.Rich(FounderName)));
			string sealFailure;
			if (!KingdomSeal.TryTerminalFromSuccession(Death, LineEnded: true, out sealFailure))
			{
				KingdomLog.Log("succession: terminal seal attempt failed closed ("
					+ (string.IsNullOrEmpty(sealFailure) ? "unknown failure" : sealFailure) + ")");
			}
			Popup.Show(KingdomSuccessionRules.DynastyEndPopup(KingdomPresentation.Rich(System.SeatName), Verdict));
			KingdomLog.Log("succession: terminal verdict " + Verdict + "; player body unchanged");
		}

		private void TryTerminalAfterOuterFailure(AfterDieEvent Death)
		{
			try
			{
				XRLGame game = The.Game;
				KingdomSystem system = game?.GetSystem<KingdomSystem>();
				GameObject founder = Death?.Dying;
				if (game == null || system == null || !system.Founded || founder == null
					|| !ReferenceEquals(The.Player, founder)
					|| !KingdomSuccessionRules.ModeOn(game.gameMode,
						game.GetBooleanGameState(KingdomSuccessionRules.ModeFlagStateKey)))
				{
					return;
				}
				PublishFounderDeath(system, founder.BaseDisplayNameStripped, Death);
				string failure;
				if (!KingdomSeal.TryTerminalFromSuccession(Death, LineEnded: true, out failure))
				{
					KingdomLog.Log("succession: outer-failure terminal seal attempt failed closed ("
						+ (string.IsNullOrEmpty(failure) ? "unknown failure" : failure) + ")");
				}
			}
			catch (Exception ex)
			{
				MetricsManager.LogError("ThousandAndFirst: outer succession terminal attempt failed", ex);
			}
		}

		private static void PrepareSuccessor(GameObject Heir)
		{
			The.Game.PlayerName = Heir.Render.DisplayName;
			Heir.SetIntProperty("Renamed", 1);
			if (Heir.Brain != null)
			{
				// GamePlayer.SetBody owns the control transition and clears active AI goals itself.
				// Add only vanilla's player membership to the base set. Native memberships, every
				// temporary layer/reason/flag, leader, feeling, conversation and ownership survive.
				AllegianceSet baseSet = Heir.Brain.GetBaseAllegiance();
				if (baseSet == null)
					throw new InvalidOperationException("successor Brain has no base allegiance");
				baseSet["Player"] = 100;
			}
		}

		private static bool TryResetPersonalKnowledge(KingdomSystem System, string Token, int RealmRegard)
		{
			List<JournalSnapshot> journal = new List<JournalSnapshot>();
			foreach (IBaseJournalEntry entry in JournalAPI.GetAllNotes())
			{
				if (entry != null)
				{
					journal.Add(new JournalSnapshot(entry));
				}
			}
			List<TinkerData> recipes = (TinkerData.KnownRecipes == null)
				? new List<TinkerData>() : new List<TinkerData>(TinkerData.KnownRecipes);
			Reputation oldReputation = The.Game.PlayerReputation;
			string founderRites = The.Game.GetStringGameState(KingdomResearch.FounderRiteState, "");
			try
			{
				string attribute = KingdomSuccessionRules.FounderAttribute(Token);
				foreach (JournalSnapshot snapshot in journal)
				{
					IBaseJournalEntry entry = snapshot.Entry;
					if (!entry.Revealed || !KingdomSuccessionRules.Forgets(KindOf(entry), entry.Forgettable()))
					{
						continue;
					}
					if (entry.Attributes == null)
					{
						entry.Attributes = new List<string>();
					}
					if (!entry.Attributes.Contains(attribute))
					{
						entry.Attributes.Add(attribute);
					}
					entry.Forget(fast: true);
				}
				RevealRealmGround(System);
				TinkerData.KnownRecipes?.Clear();
				The.Game.SetStringGameState(KingdomResearch.FounderRiteState, "");
				if (!string.IsNullOrEmpty(The.Game.GetStringGameState(
					KingdomResearch.FounderRiteState, "")))
				{
					throw new InvalidOperationException("founder rite ledger did not clear");
				}
				Reputation next = new Reputation();
				next.Init();
				Faction realm = Factions.GetIfExists(System.KingdomFactionName);
				if (realm != null)
				{
					next.Set(realm, RealmRegard);
				}
				The.Game.PlayerReputation = next;
				next.InitFeeling();
				return true;
			}
			catch (Exception ex)
			{
				for (int i = 0; i < journal.Count; i++)
				{
					try
					{
						journal[i].Restore();
					}
					catch (Exception restoreEx)
					{
						MetricsManager.LogError("ThousandAndFirst: journal rollback entry failed", restoreEx);
					}
				}
				try
				{
					if (TinkerData.KnownRecipes != null)
					{
						TinkerData.KnownRecipes.Clear();
						TinkerData.KnownRecipes.AddRange(recipes);
					}
				}
				catch (Exception recipeEx)
				{
					MetricsManager.LogError("ThousandAndFirst: recipe rollback failed", recipeEx);
				}
				try
				{
					The.Game.SetStringGameState(KingdomResearch.FounderRiteState, founderRites);
					if (!string.Equals(The.Game.GetStringGameState(
						KingdomResearch.FounderRiteState, ""), founderRites,
						StringComparison.Ordinal))
					{
						throw new InvalidOperationException("founder rite ledger rollback did not stick");
					}
				}
				catch (Exception riteEx)
				{
					MetricsManager.LogError("ThousandAndFirst: founder rite rollback failed", riteEx);
				}
				try
				{
					The.Game.PlayerReputation = oldReputation;
					oldReputation?.InitFeeling();
				}
				catch (Exception reputationEx)
				{
					MetricsManager.LogError("ThousandAndFirst: reputation rollback failed", reputationEx);
				}
				MetricsManager.LogError("ThousandAndFirst: succession honesty reset rolled back", ex);
				return false;
			}
		}

		private static void RevealRealmGround(KingdomSystem System)
		{
			HashSet<string> ground = new HashSet<string>(StringComparer.Ordinal);
			if (System.ClaimedZones != null)
			{
				ground.UnionWith(System.ClaimedZones);
			}
			if (System.Away != null && System.Away.ClaimedZones != null)
			{
				ground.UnionWith(System.Away.ClaimedZones);
			}
			foreach (JournalMapNote note in JournalAPI.MapNotes)
			{
				if (note != null && ground.Contains(note.ZoneID) && !note.Revealed)
				{
					note.Reveal("the kingdom's chart", Silent: true);
				}
			}
		}

		private static JournalKind KindOf(IBaseJournalEntry Entry)
		{
			if (Entry is JournalAccomplishment) return JournalKind.Accomplishment;
			if (Entry is JournalMapNote) return JournalKind.MapNote;
			if (Entry is JournalGeneralNote) return JournalKind.GeneralNote;
			if (Entry is JournalVillageNote) return JournalKind.VillageNote;
			if (Entry is JournalRecipeNote) return JournalKind.RecipeNote;
			if (Entry is JournalSultanNote) return JournalKind.SultanNote;
			return JournalKind.Observation;
		}

	}
}
