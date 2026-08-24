using System;
using System.Globalization;
using XRL;
using XRL.Messages;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>
	/// Petitions are offers grounded in settlement state, not quests silently accepted at issue.
	/// Every offer snapshots its event, requester, origin, cause, and target; only an Accepted
	/// petition can resolve. Rendering is pure and a missing requester never blocks bookkeeping.
	/// </summary>
	public static class KingdomPetitions
	{
		public static bool Enabled => XRL.UI.Options.GetOption("r_TAF_OptionPetitions") != "No";

		/// <summary>
		/// Option-independent settlement entrypoint. Call from the canonical settlement pass even
		/// when Growth is disabled; this method owns its own option check.
		/// </summary>
		public static void OnSettlementPass(KingdomSystem System, Zone Z, KingdomSurvey Survey)
		{
			if (!Enabled || System == null || !System.Founded || Z == null || Survey == null
				|| !System.ClaimedZones.Contains(Z.ZoneID))
			{
				return;
			}
			long now = The.Game.TimeTicks;
			NormalizeLifecycle(System);
			if (KingdomPetitionRules.IsActive(System.PetitionState))
			{
				Check(System, Z, Survey);
				return;
			}
			if (!KingdomPetitionRules.CanOffer(now, System.LastPetitionMonthOrdinal,
				System.LastPetitionTick, System.PetitionState, System.PetitionKind))
			{
				return;
			}
			Issue(System, Z, Survey);
		}

		public static bool Issue(KingdomSystem System, Zone Z, KingdomSurvey Survey)
		{
			if (System == null || Z == null || Survey == null)
			{
				return false;
			}
			long now = The.Game.TimeTicks;
			NormalizeLifecycle(System);
			if (!KingdomPetitionRules.CanOffer(now, System.LastPetitionMonthOrdinal,
				System.LastPetitionTick, System.PetitionState, System.PetitionKind))
			{
				return false;
			}
			string worstFaction = null;
			int worstStanding = 0;
			foreach (System.Collections.Generic.KeyValuePair<string, int> standing in System.Standings)
			{
				if (standing.Value < worstStanding)
				{
					worstStanding = standing.Value;
					worstFaction = standing.Key;
				}
			}
			bool hasShrine = HasShrine(Z);
			KingdomRules.PetitionKind kind = KingdomRules.ChoosePetition(Survey.StoredWater,
				System.Population, Survey.Beds, System.IdleWorks, worstStanding, hasShrine, System.Dead);
			if (kind == KingdomRules.PetitionKind.None)
			{
				return false;
			}
			return Offer(System, kind, worstFaction, now, null);
		}

		/// <summary>
		/// Authored petition raised by a founder action. It shares the same one-per-month gate and
		/// lifecycle as condition-raised petitions.
		/// </summary>
		public static bool Raise(KingdomSystem System, KingdomRules.PetitionKind Kind, string Faction)
		{
			if (!Enabled || System == null || !System.Founded || Kind == KingdomRules.PetitionKind.None)
			{
				return false;
			}
			long now = The.Game.TimeTicks;
			NormalizeLifecycle(System);
			if (!KingdomPetitionRules.CanOffer(now, System.LastPetitionMonthOrdinal,
				System.LastPetitionTick, System.PetitionState, System.PetitionKind))
			{
				return false;
			}
			return Offer(System, Kind, Faction, now, null);
		}

		/// <summary>Caller-keyed authored petition. The supplied ID is published with the
		/// petition state before ledger/message callbacks, so retry can adopt the exact event after
		/// either callback throws and can never create a second petition.</summary>
		public static bool RaiseOnce(KingdomSystem System, KingdomRules.PetitionKind Kind,
			string Faction, string EventId)
		{
			if (!Enabled || System == null || !System.Founded
				|| Kind == KingdomRules.PetitionKind.None || string.IsNullOrEmpty(EventId)
				|| EventId.Length > 256) return false;
			NormalizeLifecycle(System);
			if (string.Equals(System.PetitionEventId, EventId, StringComparison.Ordinal))
			{
				return System.PetitionKind == Kind
					&& string.Equals(System.PetitionFaction, Faction, StringComparison.Ordinal)
					&& KingdomPetitionRules.IsActive(System.PetitionState);
			}
			long now = The.Game.TimeTicks;
			if (!KingdomPetitionRules.CanOffer(now, System.LastPetitionMonthOrdinal,
				System.LastPetitionTick, System.PetitionState, System.PetitionKind)) return false;
			return Offer(System, Kind, Faction, now, EventId);
		}

		private static bool Offer(KingdomSystem System, KingdomRules.PetitionKind Kind,
			string Faction, long NowTick, string EventId)
		{
			if (!KingdomPetitionRules.CanTransition(System.PetitionState, PetitionLifecycle.Offered))
			{
				return false;
			}
			string petitioner = (System.RosterNames != null && System.RosterNames.Count > 0)
				? System.RosterNames.GetRandomElement()
				: "a settler";
			if (string.IsNullOrEmpty(petitioner))
			{
				petitioner = "a settler";
			}
			string origin = OriginIdentity(System);
			if (!KingdomIdentityRules.IsSettlementId(origin)) return false;
			System.PetitionKind = Kind;
			System.PetitionPetitioner = petitioner;
			System.PetitionFaction = Faction;
			System.PetitionTarget = KingdomPetitionRules.SnapshotTarget(Kind, System.Population);
			System.PetitionIssuedTick = NowTick;
			System.PetitionOriginSettlementId = origin;
			System.PetitionCauseSnapshot = CauseSnapshot(Kind, Faction);
			System.PetitionEventId = string.IsNullOrEmpty(EventId)
				? (origin + ":" + NowTick.ToString(CultureInfo.InvariantCulture)
					+ ":" + ((int)Kind).ToString(CultureInfo.InvariantCulture))
				: EventId;
			System.PetitionState = PetitionLifecycle.Offered;
			System.LastPetitionMonthOrdinal = KingdomPetitionRules.CanonicalMonthOrdinal(NowTick);
			// Kept as migration evidence for builds that only knew this field. Offer time, not close
			// time, is what enforces one offer per month after decline or expiry.
			System.LastPetitionTick = NowTick;
			System.Ledger.Note("{{W|" + petitioner + " is waiting to speak with you.}}");
			MessageQueue.AddPlayerMessage("{{W|" + petitioner + " would have a word with you about "
				+ Subject(Kind) + ".}}");
			if (KingdomLog.Enabled)
			{
				KingdomLog.Log("petition offered: " + Kind + " id=" + System.PetitionEventId
					+ " target=" + System.PetitionTarget);
			}
			if (!string.IsNullOrEmpty(EventId))
			{
				return string.Equals(System.PetitionEventId, EventId, StringComparison.Ordinal)
					&& System.PetitionKind == Kind
					&& string.Equals(System.PetitionFaction, Faction, StringComparison.Ordinal)
					&& KingdomPetitionRules.IsActive(System.PetitionState);
			}
			return true;
		}

		/// <summary>
		/// Accepts the exact visible offer. State publishes before governance is marked; render and
		/// cancellation paths never call this method.
		/// </summary>
		public static bool Accept(KingdomSystem System)
		{
			if (System == null)
			{
				return false;
			}
			NormalizeLifecycle(System);
			if (System.PetitionKind == KingdomRules.PetitionKind.None
				|| !KingdomPetitionRules.CanTransition(System.PetitionState, PetitionLifecycle.Accepted))
			{
				return false;
			}
			System.PetitionState = PetitionLifecycle.Accepted;
			KingdomGovernanceScope.Commit("accept petition");
			System.Ledger.Note("{{W|The founder accepted " + Petitioner(System) + "'s petition.}}");
			return true;
		}

		/// <summary>Declines an Offered petition. Free bookkeeping; Accepted work cannot be erased.</summary>
		public static bool Decline(KingdomSystem System)
		{
			if (System == null)
			{
				return false;
			}
			NormalizeLifecycle(System);
			if (!KingdomPetitionRules.CanTransition(System.PetitionState, PetitionLifecycle.Declined))
			{
				return false;
			}
			string petitioner = Petitioner(System);
			System.PetitionState = PetitionLifecycle.Declined;
			System.PetitionKind = KingdomRules.PetitionKind.None;
			System.LastPetitionTick = The.Game.TimeTicks;
			KingdomChronicle.Record(System, petitioner + " was told the matter must wait");
			System.Ledger.Note("{{K|" + petitioner + " returned to work. The matter was not pressed.}}");
			return true;
		}

		/// <summary>Compatibility entrypoint for older callers. Only an Offered petition closes.</summary>
		public static void Close(KingdomSystem System)
		{
			Decline(System);
		}

		public static PetitionLifecycle Status(KingdomSystem System)
		{
			if (System == null)
			{
				return PetitionLifecycle.None;
			}
			return KingdomPetitionRules.NormalizeLegacy(System.PetitionState,
				System.PetitionKind);
		}

		public static bool IsAwaitingAnswer(KingdomSystem System)
		{
			return Status(System) == PetitionLifecycle.Offered;
		}

		public static bool IsAccepted(KingdomSystem System)
		{
			return Status(System) == PetitionLifecycle.Accepted;
		}

		public static void Check(KingdomSystem System, Zone Z, KingdomSurvey Survey)
		{
			if (System == null || Z == null || Survey == null)
			{
				return;
			}
			NormalizeLifecycle(System);
			if (!KingdomPetitionRules.IsActive(System.PetitionState)
				|| !OriginMatches(System, System.PetitionOriginSettlementId))
			{
				return;
			}
			int standing = (System.PetitionFaction != null) ? System.GetStanding(System.PetitionFaction) : 0;
			if (KingdomPetitionRules.CanResolve(System.PetitionState, System.PetitionKind,
				System.PetitionTarget, Survey.StoredWater, Survey.Beds, System.IdleWorks,
				standing, HasShrine(Z)))
			{
				Fulfil(System);
				return;
			}
			if (KingdomPetitionRules.IsExpired(The.Game.TimeTicks, System.PetitionIssuedTick,
				KingdomRules.PetitionLifetimeTicks))
			{
				Expire(System);
			}
		}

		private static bool Fulfil(KingdomSystem System)
		{
			if (System == null || System.PetitionState != PetitionLifecycle.Accepted
				|| System.PetitionKind == KingdomRules.PetitionKind.None)
			{
				return false;
			}
			string petitioner = Petitioner(System);
			string deed = Deed(System.PetitionKind, System.KingdomDisplayName);
			System.PetitionState = PetitionLifecycle.Resolved;
			System.PetitionKind = KingdomRules.PetitionKind.None;
			System.LastPetitionTick = The.Game.TimeTicks;
			System.PetitionsMet++;
			System.RecordDeed(deed);
			KingdomChronicle.Record(System, petitioner + " asked, and " + deed, Accomplishment: true);
			System.Ledger.Note("{{G|" + petitioner + " has what they asked for. Word of it will travel.}}");
			MessageQueue.AddPlayerMessage("{{G|" + petitioner + " thanks you. "
				+ XRL.Language.Grammar.InitCap(deed) + ".}}");
			return true;
		}

		private static void Expire(KingdomSystem System)
		{
			if (!KingdomPetitionRules.CanTransition(System.PetitionState, PetitionLifecycle.Expired))
			{
				return;
			}
			string petitioner = Petitioner(System);
			System.PetitionState = PetitionLifecycle.Expired;
			System.PetitionKind = KingdomRules.PetitionKind.None;
			System.LastPetitionTick = The.Game.TimeTicks;
			System.Ledger.Note("{{K|" + petitioner + " stopped asking. The matter was not pressed.}}");
		}

		private static void NormalizeLifecycle(KingdomSystem System)
		{
			System.PetitionState = KingdomPetitionRules.NormalizeLegacy(System.PetitionState,
				System.PetitionKind);
			if (!KingdomPetitionRules.IsActive(System.PetitionState))
			{
				if (System.PetitionKind != KingdomRules.PetitionKind.None)
				{
					System.PetitionKind = KingdomRules.PetitionKind.None;
				}
				return;
			}
			if (string.IsNullOrEmpty(System.PetitionPetitioner))
			{
				System.PetitionPetitioner = "a settler";
			}
			// An old active row with no immutable origin is preserved but never guessed from a
			// mutable name. OriginMatches consequently fails closed until inspected.
			if (string.IsNullOrEmpty(System.PetitionCauseSnapshot))
			{
				System.PetitionCauseSnapshot = CauseSnapshot(System.PetitionKind,
					System.PetitionFaction);
			}
			if (KingdomPetitionRules.TargetNeedsRepair(System.PetitionKind,
				System.PetitionTarget))
			{
				System.PetitionTarget = KingdomPetitionRules.SnapshotTarget(System.PetitionKind,
					System.Population);
			}
			if (string.IsNullOrEmpty(System.PetitionEventId))
			{
				System.PetitionEventId = System.PetitionOriginSettlementId + ":"
					+ System.PetitionIssuedTick.ToString(CultureInfo.InvariantCulture) + ":"
					+ ((int)System.PetitionKind).ToString(CultureInfo.InvariantCulture);
			}
			if (System.LastPetitionMonthOrdinal < 0L)
			{
				long evidence = (System.PetitionIssuedTick > 0L)
					? System.PetitionIssuedTick : System.LastPetitionTick;
				if (evidence > 0L)
				{
					System.LastPetitionMonthOrdinal = KingdomPetitionRules.CanonicalMonthOrdinal(evidence);
				}
			}
		}

		private static bool HasShrine(Zone Z)
		{
			foreach (GameObject item in Z.GetObjects())
			{
				if (item.GetIntProperty("KingdomBuilt") == 1 && item.HasPart("Shrine"))
				{
					return true;
				}
			}
			return false;
		}

		private static string OriginIdentity(KingdomSystem System)
		{
			return System?.CurrentSettlementId;
		}

		private static bool OriginMatches(KingdomSystem System, string Snapshot)
		{
			return KingdomPetitionRules.OriginMatches(Snapshot, OriginIdentity(System));
		}

		private static string CauseSnapshot(KingdomRules.PetitionKind Kind, string Faction)
		{
			if (string.IsNullOrEmpty(Faction))
			{
				return Subject(Kind);
			}
			try
			{
				return XRL.World.Faction.GetFormattedName(Faction);
			}
			catch
			{
				return Faction;
			}
		}

		private static string Petitioner(KingdomSystem System)
		{
			return string.IsNullOrEmpty(System.PetitionPetitioner)
				? "a settler" : System.PetitionPetitioner;
		}

		public static string Subject(KingdomRules.PetitionKind Kind)
		{
			switch (Kind)
			{
			case KingdomRules.PetitionKind.Thirst: return "the water";
			case KingdomRules.PetitionKind.Shelter: return "where people are sleeping";
			case KingdomRules.PetitionKind.Craft: return "the works standing idle";
			case KingdomRules.PetitionKind.Peace: return "the ones who hate us";
			case KingdomRules.PetitionKind.Memorial: return "the dead";
			case KingdomRules.PetitionKind.Flesh: return KingdomLabRules.SpokenAgainstSubject();
			case KingdomRules.PetitionKind.Chrome: return KingdomAnnexeRules.SpokenAboutSubject();
			default: return "the settlement";
			}
		}

		/// <summary>Pure rendering: calling repeatedly cannot accept, complete, or mutate anything.</summary>
		public static string Speech(KingdomSystem System)
		{
			switch (System.PetitionKind)
			{
			case KingdomRules.PetitionKind.Thirst:
				return "\"We are counting drams again. I am not asking for plenty — I am asking for " + System.PetitionTarget + " in the stores, so that when the month turns dry we do not have to decide who drinks.\"";
			case KingdomRules.PetitionKind.Shelter:
				return "\"There are more of us than there are beds, and the newest sleep where they can. Raise enough bunks that " + System.PetitionTarget + " sleepers have a place.\"";
			case KingdomRules.PetitionKind.Craft:
				return "\"We built the works and then left them standing. Every day I walk past a thing we paid water for that nobody is turning. Either find us hands, or let me pull it down for the timber.\"";
			case KingdomRules.PetitionKind.Peace:
				return "\"" + (System.PetitionCauseSnapshot ?? "They") + " will not hear us, and my people flinch at the road. I do not care how it is done — bought, begged, or drunk over. Just make it so they do not hate us.\"";
			case KingdomRules.PetitionKind.Memorial:
				return "\"We have buried people here now. There is nowhere to put a hand and say a name. Raise a shrine stone, and let the ground admit what it has taken.\"";
			case KingdomRules.PetitionKind.Flesh:
				return KingdomLabRules.SpokenAgainstSpeech(System.PetitionCauseSnapshot ?? "everyone");
			case KingdomRules.PetitionKind.Chrome:
				return KingdomAnnexeRules.SpokenAboutSpeech(System.PetitionCauseSnapshot ?? "the debt-minded");
			default:
				return "\"It is nothing. It has passed.\"";
			}
		}

		public static string Deed(KingdomRules.PetitionKind Kind, string Name)
		{
			switch (Kind)
			{
			case KingdomRules.PetitionKind.Thirst: return "the stores of " + Name + " were filled against the dry month";
			case KingdomRules.PetitionKind.Shelter: return "a bed was raised for every soul in " + Name;
			case KingdomRules.PetitionKind.Craft: return "the works of " + Name + " were set turning again";
			case KingdomRules.PetitionKind.Peace: return "the peace " + Name + " made with its enemies";
			case KingdomRules.PetitionKind.Memorial: return "the shrine " + Name + " raised over its dead";
			case KingdomRules.PetitionKind.Flesh: return KingdomLabRules.SpokenAgainstDeed(Name);
			case KingdomRules.PetitionKind.Chrome: return KingdomAnnexeRules.SpokenAboutDeed(Name);
			default: return "the matter was settled at " + Name;
			}
		}
	}
}
