using System;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>Public petition facade. Runtime truth lives only in LifecycleBook.Petition.</summary>
	public static class KingdomPetitions
	{
		public static bool Enabled
		{
			get
			{
				try { return XRL.UI.Options.GetOption("r_TAF_OptionPetitions") != "No"; }
				catch (Exception error)
				{
					MetricsManager.LogError("ThousandAndFirst petition option", error);
					return false;
				}
			}
		}

		public static void OnSettlementPass(KingdomSystem System, Zone Z, KingdomSurvey Survey)
		{
			if (!KingdomMaster.AutomaticWorkAllowed(System)) return;
			Guard("settlement pass", delegate
			{
				KingdomPetitionLifecycle.OnSettlementPass(System, Z, Survey, Enabled,
					The.Game.TimeTicks);
			});
		}

		public static bool Issue(KingdomSystem System, Zone Z, KingdomSurvey Survey)
		{
			if (!KingdomMaster.NewWorkAllowed(System)) return false;
			return Guard("issue", delegate
			{
				return KingdomPetitionLifecycle.Issue(System, Z, Survey, Enabled,
					The.Game.TimeTicks);
			});
		}

		/// <summary>Raises an authored petition from the exact currently seated ground.</summary>
		public static bool Raise(KingdomSystem System, KingdomRules.PetitionKind Kind,
			string Faction)
		{
			return RaiseOnce(System, Kind, Faction, null);
		}

		/// <summary>Caller-keyed authored petition. Retry adopts only the exact active event.</summary>
		public static bool RaiseOnce(KingdomSystem System, KingdomRules.PetitionKind Kind,
			string Faction, string EventId)
		{
			if (!KingdomMaster.NewWorkAllowed(System)) return false;
			return Guard("raise", delegate
			{
				Zone zone = The.Player?.CurrentZone;
				KingdomSurvey survey = zone == null ? null : KingdomSurvey.Take(zone, System);
				return KingdomPetitionLifecycle.Raise(System, zone, survey, Kind, Faction,
					EventId, Enabled, The.Game.TimeTicks);
			});
		}

		public static bool Accept(KingdomSystem System)
		{
			if (!KingdomMaster.NewWorkAllowed(System)) return false;
			return Guard("accept", delegate
			{
				return KingdomPetitionLifecycle.Accept(System, The.Game.TimeTicks);
			});
		}

		public static bool Decline(KingdomSystem System)
		{
			if (!KingdomMaster.NewWorkAllowed(System)) return false;
			return Guard("decline", delegate
			{
				return KingdomPetitionLifecycle.Decline(System, The.Game.TimeTicks);
			});
		}

		/// <summary>Compatibility entrypoint. Accepted work is never erased.</summary>
		public static void Close(KingdomSystem System)
		{
			Decline(System);
		}

		public static PetitionLifecycle Status(KingdomSystem System)
		{
			try { return KingdomPetitionLifecycle.Status(System); }
			catch (Exception error)
			{
				MetricsManager.LogError("ThousandAndFirst petition status", error);
				return PetitionLifecycle.None;
			}
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
			if (!KingdomMaster.AutomaticWorkAllowed(System)) return;
			Guard("check", delegate
			{
				KingdomPetitionLifecycle.Check(System, Z, Survey, The.Game.TimeTicks);
			});
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

		/// <summary>Pure rendering. Repeated reads never accept or resolve a petition.</summary>
		public static string Speech(KingdomSystem System)
		{
			try { return SpeechCore(System); }
			catch (Exception error)
			{
				MetricsManager.LogError("ThousandAndFirst petition speech", error);
				return "\"It is nothing. It has passed.\"";
			}
		}

		private static string SpeechCore(KingdomSystem System)
		{
			KingdomLifecycleOperation op = KingdomPetitionLifecycle.Open(System);
			if (!KingdomPetitionRules.FrozenSnapshotValid(op))
				return "\"It is nothing. It has passed.\"";
			switch ((KingdomRules.PetitionKind)op.Kind)
			{
			case KingdomRules.PetitionKind.Thirst:
				return "\"We are counting drams again. I am not asking for plenty — I am asking for "
					+ op.Target + " in the stores, so that when the month turns dry we do not have to decide who drinks.\"";
			case KingdomRules.PetitionKind.Shelter:
				return "\"There are more of us than there are beds, and the newest sleep where they can. Raise enough bunks that "
					+ op.Target + " sleepers have a place.\"";
			case KingdomRules.PetitionKind.Craft:
				return "\"We built the works and then left them standing. Every day I walk past a thing we paid water for that nobody is turning. Either find us hands, or let me pull it down for the timber.\"";
			case KingdomRules.PetitionKind.Peace:
				return "\"" + KingdomPresentation.Rich(op.Detail)
					+ " will not hear us, and my people flinch at the road. I do not care how it is done — bought, begged, or drunk over. Just make it so they do not hate us.\"";
			case KingdomRules.PetitionKind.Memorial:
				return "\"We have buried people here now. There is nowhere to put a hand and say a name. Raise a shrine stone, and let the ground admit what it has taken.\"";
			case KingdomRules.PetitionKind.Flesh:
				return KingdomLabRules.SpokenAgainstSpeech(KingdomPresentation.Rich(op.Detail));
			case KingdomRules.PetitionKind.Chrome:
				return KingdomAnnexeRules.SpokenAboutSpeech(KingdomPresentation.Rich(op.Detail));
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

		private static void Guard(string Label, Action Work)
		{
			try { Work(); }
			catch (Exception error) { MetricsManager.LogError("ThousandAndFirst petition " + Label, error); }
		}

		private static bool Guard(string Label, Func<bool> Work)
		{
			try { return Work(); }
			catch (Exception error)
			{
				MetricsManager.LogError("ThousandAndFirst petition " + Label, error);
				return false;
			}
		}
	}
}
