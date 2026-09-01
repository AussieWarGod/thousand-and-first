using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using XRL;
using XRL.Rules;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomFounding
	{
		private const string PendingFactionProperty = "TAFFoundingPending";
		private const string FoundingStepProperty = "TAFFoundingStep";
		private const string FoundingChronicleEventProperty = "TAFFoundingChronicleEvent_v1";
		private const string FoundingChronicleStageProperty = "TAFFoundingChronicleStage_v1";
		private const string FoundingChronicleDispositionProperty =
			"TAFFoundingChronicleDisposition_v1";
		private const string FoundingStandingsProperty = "TAFFoundingStandings_v1";
		private const int MaxFoundingStandingsLength = 262144;

		internal static bool DirectionalAuthorityPublished(Faction Faction)
		{
			return Faction != null &&
				(Faction.GetIntProperty(PendingFactionProperty) != 1 ||
				 Faction.GetIntProperty(FoundingStepProperty) >= 2);
		}

		/// <summary>
		/// Founds the player's kingdom: creates and registers a runtime faction following the
		/// engine's village-faction recipe, leaves both civic directions unspecified, grants the
		/// Charter ability, and opens the chronicle. Personal regard is never copied into civic policy.
		/// </summary>
		/// <param name="Name">Settlement display name. New engine factions use the immutable
		/// namespaced realm id as their key.</param>
		/// <returns>The new faction; the existing one if a kingdom is already founded (not an
		/// error); or null when a faction of that name is already registered, in which case
		/// nothing has changed.</returns>
		public static Faction Found(string Name)
		{
			return KingdomFoundingTransaction.TryFoundFirstWithoutWater(Name,
				The.Player?.CurrentZone, out var faction, out var failure)
				? faction : null;
		}

		/// <summary>Transaction overload binding terrain and rite placement to the prepared site,
		/// not wherever the player happens to stand during a recovery attempt.</summary>
		internal static Faction Found(string Name, Zone FoundingZone, Cell RiteCell,
			string TransactionID, string Authority)
		{
			if (string.IsNullOrEmpty(Name) || FoundingZone == null || RiteCell == null ||
				RiteCell.ParentZone != FoundingZone ||
				!KingdomFoundingTransactionRules.TryParseAuthority(Authority,
					out var parsedAuthority) ||
				parsedAuthority.Kind != KingdomFoundingKind.FirstCity ||
				parsedAuthority.TransactionID != TransactionID ||
				!KingdomIdentityRules.FirstFactionKeyMatches(parsedAuthority.RealmFaction,
					TransactionID, Name, AllowLegacy: true) ||
				parsedAuthority.ZoneID != FoundingZone.ZoneID ||
				parsedAuthority.RiteX != RiteCell.X || parsedAuthority.RiteY != RiteCell.Y ||
				!KingdomFoundingTransaction.AuthorityIsSynchronouslyInFlight(Authority) ||
				!KingdomFoundingTransaction.GlobalReservationMatches(Authority) ||
				!KingdomFoundingTransaction.SiteReservationMatches(FoundingZone, Authority))
			{
				return null;
			}
			string factionId = parsedAuthority.RealmFaction;
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			string identityFailure;
			// Plain persisted fields only: immutable realm + first-city ids exist and read back
			// before faction registration, reputation, zone properties, journal, or any step marker.
			if (!system.TryBindFirstFoundingIdentity(TransactionID, FoundingZone.ZoneID,
				out identityFailure))
			{
				KingdomLog.Log("founding identity refused: " + identityFailure);
				return null;
			}
			if (system.Founded)
			{
				Faction current = Factions.GetIfExists(system.KingdomFactionName);
				if (current != null &&
					!KingdomFactionEmblemPresentation.TryApply(current, current.Name))
				{
					return null;
				}
				// Ordinary repeated debug calls keep their old idempotent meaning. The sole exception
				// is an interrupted first founding, whose faction is deliberately marked until the
				// basin has verified claim, ability, placement, and seal.
				if (current == null || current.GetIntProperty(PendingFactionProperty) != 1 ||
					system.KingdomFactionName != factionId)
				{
					return current;
				}
				if (!KingdomFoundingTransaction.FactionRegistryCoherent(factionId, current) ||
					string.IsNullOrEmpty(TransactionID) ||
					current.GetStringProperty(
						KingdomFoundingTransaction.PendingFactionTransactionProperty, null) !=
						TransactionID)
				{
					return null;
				}
				return CompleteFirstPublication(system, current, factionId, Name,
					FoundingZone, RiteCell, TransactionID, Authority);
			}
			if (string.IsNullOrEmpty(Name) || string.IsNullOrEmpty(TransactionID))
			{
				return null;
			}
			// Factions.AddNewFaction is a Dictionary.Add (XRL/World/Factions.cs:270) and a runtime
			// faction can never be removed or renamed - so after an expulsion the old realm's name
			// is taken forever. Refuse before the rite commits anything rather than throwing part
			// way through it.
			Faction faction;
			if (Factions.Exists(factionId))
			{
				// AddNewFaction cannot be reversed. A faction carrying this marker is the exact
				// recoverable state left if the engine threw after Dictionary.Add; any other existing
				// faction is somebody else's identity and remains an absolute refusal.
				faction = Factions.GetIfExists(factionId);
				if (faction == null || faction.GetIntProperty(PendingFactionProperty) != 1 ||
					faction.GetIntProperty("PlayerKingdom") != 1 ||
					faction.GetStringProperty(
						KingdomFoundingTransaction.PendingFactionTransactionProperty, null) !=
						TransactionID ||
					faction.GetStringProperty(
						KingdomFoundingTransaction.PendingFactionAuthorityProperty, null) !=
						Authority ||
					faction.GetStringProperty(FoundingChronicleEventProperty, null) !=
						KingdomFoundingTransaction.FoundingEventID(
							KingdomFoundingKind.FirstCity, TransactionID, "chronicle") ||
					!KingdomFoundingTransaction.RepairPendingFactionRegistry(factionId,
						TransactionID, Authority))
				{
					return null;
				}
				if (!KingdomFactionEmblemPresentation.TryApply(faction, faction.Name))
				{
					return null;
				}
			}
			else
			{
				if (!KingdomFoundingTransaction.FactionNameAvailable(factionId))
				{
					return null;
				}
				faction = new Faction();
				faction.Old = false;
				faction.ExtradimensionalVersions = false;
				faction.Visible = true;
				faction.Name = factionId;
				faction.DisplayName = Name;
				faction.PositiveSound = "Sounds/Reputation/sfx_reputation_village_positive";
				faction.NegativeSound = "Sounds/Reputation/sfx_reputation_village_negative";
				faction.SetProperty("PlayerKingdom", 1);
				faction.SetProperty("Village", 1);
				faction.SetProperty(PendingFactionProperty, 1);
				faction.SetProperty(
					KingdomFoundingTransaction.PendingFactionTransactionProperty,
					TransactionID);
				faction.SetProperty(
					KingdomFoundingTransaction.PendingFactionAuthorityProperty, Authority);
				faction.SetProperty(
					KingdomFoundingTransaction.RealmReservationProperty, Authority);
				faction.SetProperty(FoundingStepProperty, 0);
				faction.SetProperty(FoundingChronicleEventProperty,
					KingdomFoundingTransaction.FoundingEventID(
						KingdomFoundingKind.FirstCity, TransactionID, "chronicle"));
				faction.SetProperty(FoundingChronicleStageProperty, 0);
				faction.SetProperty(FoundingChronicleDispositionProperty,
					(int)KingdomChronicleDisposition.None);
				faction.WaterRitualLiquid = "water";
				if (!KingdomFactionEmblemPresentation.TryApply(faction, faction.Name))
				{
					return null;
				}
				faction.SetFactionFeeling("Player", 100);
				Factions.AddNewFaction(faction);
				if (!system.FirstIdentityMatches(TransactionID, FoundingZone.ZoneID) ||
					!KingdomFoundingTransaction.FactionRegistryCoherent(factionId, faction))
				{
					return null;
				}
			}
			return CompleteFirstPublication(system, faction, factionId, Name,
				FoundingZone, RiteCell, TransactionID, Authority);
		}

		internal static bool TryGetRecoverableFirstPublication(KingdomSystem System,
			string Name, out Faction Faction, out string TransactionID)
		{
			Faction = null;
			TransactionID = null;
			foreach (Faction candidate in Factions.GetList())
			{
				if (candidate == null || candidate.DisplayName != Name ||
					candidate.GetIntProperty(PendingFactionProperty) != 1 ||
					candidate.GetIntProperty("PlayerKingdom") != 1) continue;
				if (Faction != null) return false;
				Faction = candidate;
			}
			TransactionID = Faction?.GetStringProperty(
				KingdomFoundingTransaction.PendingFactionTransactionProperty, null);
			return System != null && !string.IsNullOrEmpty(Name) &&
				Faction != null && !string.IsNullOrEmpty(TransactionID) &&
				Faction.GetIntProperty(PendingFactionProperty) == 1 &&
				Faction.GetIntProperty("PlayerKingdom") == 1 &&
				KingdomFoundingTransaction.FactionRegistryCoherent(Faction.Name, Faction) &&
				KingdomIdentityRules.FirstFactionKeyMatches(Faction.Name, TransactionID,
					Name, AllowLegacy: true) &&
				(!System.Founded || System.KingdomFactionName == Faction.Name);
		}

	}

	/// <summary>Deterministic TAF faction mark. Qud's village helper always paints Joppa's
	/// terrain tile; a glyph-only emblem stays visible in tile and text UI without borrowing a
	/// particular settlement's identity.</summary>
	internal static class KingdomFactionEmblemPresentation
	{
		internal const string Glyph = "Ø";

		internal static bool TryApply(Faction Faction, string Seed)
		{
			if (Faction == null || string.IsNullOrEmpty(Seed)) return false;
			Random random = Stat.GetSeededRandomGenerator(Seed);
			string foreground = Crayons.GetRandomColorAll(random);
			if (string.IsNullOrEmpty(foreground)) return false;
			string detail = Crayons.GetRandomColorExcept(candidate =>
				string.IsNullOrEmpty(candidate) || candidate[0] == foreground[0], random);
			if (string.IsNullOrEmpty(detail)) return false;

			FactionEmblem emblem = Faction.Emblem ?? new FactionEmblem();
			emblem.Tile = null;
			emblem.RenderString = Glyph;
			emblem.ColorString = "&" + foreground;
			emblem.TileColor = emblem.ColorString;
			emblem.DetailColor = detail[0];
			Faction.Emblem = emblem;
			return true;
		}
	}
}
