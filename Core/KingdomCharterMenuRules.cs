using System;

namespace ThousandAndFirst
{
	/// <summary>Every verb that was reachable from the original flat Charter menu.</summary>
	public enum KingdomCharterAction
	{
		HearPetition = 0,
		Status = 1,
		Homecoming = 2,
		ChronicleAndDynasty = 3,
		OutsiderChronicle = 4,
		Standings = 5,
		SettlerRoll = 6,
		StandingPolicy = 7,
		DesignateDistrict = 8,
		CommissionBuilding = 9,
		AnswerThreat = 10,
		DedicateStores = 11,
		StrikeTradeCharter = 12,
		SendManifest = 13,
		ShareMeal = 14,
		CertifyMachine = 15,
		SetWaterDetail = 16,
		ManagePlans = 17,
		AdoptBuilding = 18,
		ReleaseAdoption = 19,
		ManageCreed = 20,
		KeepersKnowledge = 21,
		WorksAndTrades = 22,
		NameBuilding = 23,
		GroundWork = 24,
		StrikeBuilding = 25,
		PostPrice = 26,
		ConvertPlot = 27,
		RedressBuilding = 28,
		ConsecrateShrine = 29,
		ShareWater = 30,
		ClaimGround = 31,
		CityBook = 32,
		TechMap = 33,
		CityAsks = 34,
		SalvageExpedition = 35
	}

	public enum KingdomCharterChapter
	{
		PeopleAndBelief = 0,
		WorksAndGround = 1,
		PlansAndHoldings = 2,
		StoresAndRoutes = 3,
		ThreatsAndDiplomacy = 4,
		DynastyAndLegacy = 5,
		CityReadings = 6
	}

	public enum KingdomCharterRouteKind
	{
		Action = 0,
		Chapter = 1,
		Back = 2
	}

	/// <summary>Engine-free row used by both popup wiring and exhaustive routing tests.</summary>
	public sealed class KingdomCharterMenuRoute
	{
		public readonly string Label;
		public readonly char Hotkey;
		public readonly KingdomCharterRouteKind Kind;
		public readonly KingdomCharterAction Action;
		public readonly KingdomCharterChapter Chapter;

		private KingdomCharterMenuRoute(string Label, char Hotkey,
			KingdomCharterRouteKind Kind, KingdomCharterAction Action,
			KingdomCharterChapter Chapter)
		{
			this.Label = Label;
			this.Hotkey = Hotkey;
			this.Kind = Kind;
			this.Action = Action;
			this.Chapter = Chapter;
		}

		public static KingdomCharterMenuRoute ForAction(string Label, char Hotkey,
			KingdomCharterAction Action)
		{
			return new KingdomCharterMenuRoute(Label, Hotkey,
				KingdomCharterRouteKind.Action, Action, default(KingdomCharterChapter));
		}

		public static KingdomCharterMenuRoute ForChapter(string Label, char Hotkey,
			KingdomCharterChapter Chapter)
		{
			return new KingdomCharterMenuRoute(Label, Hotkey,
				KingdomCharterRouteKind.Chapter, default(KingdomCharterAction), Chapter);
		}

		public static KingdomCharterMenuRoute Back()
		{
			return new KingdomCharterMenuRoute("{{K|Back to the Charter}}", 'x',
				KingdomCharterRouteKind.Back, default(KingdomCharterAction),
				default(KingdomCharterChapter));
		}
	}

	/// <summary>
	/// One bounded navigation table for the Charter. Entering a chapter or taking its Back row
	/// is navigation only; only an Action route may open a governance scope.
	/// </summary>
	public static class KingdomCharterMenuRules
	{
		private static readonly KingdomCharterMenuRoute[] Root = new KingdomCharterMenuRoute[]
		{
			KingdomCharterMenuRoute.ForAction("{{W|Status and next need}}", 's', KingdomCharterAction.Status),
			KingdomCharterMenuRoute.ForChapter("People & belief", 'p', KingdomCharterChapter.PeopleAndBelief),
			KingdomCharterMenuRoute.ForChapter("Works & ground", 'w', KingdomCharterChapter.WorksAndGround),
			KingdomCharterMenuRoute.ForChapter("Plans & holdings", 'h', KingdomCharterChapter.PlansAndHoldings),
			KingdomCharterMenuRoute.ForChapter("Stores & routes", 'r', KingdomCharterChapter.StoresAndRoutes),
			KingdomCharterMenuRoute.ForChapter("Threats & diplomacy", 't', KingdomCharterChapter.ThreatsAndDiplomacy),
			KingdomCharterMenuRoute.ForChapter("Dynasty & legacy", 'd', KingdomCharterChapter.DynastyAndLegacy),
			KingdomCharterMenuRoute.ForChapter("The city in full", 'c', KingdomCharterChapter.CityReadings)
		};

		private static readonly KingdomCharterMenuRoute[][] Chapters = new KingdomCharterMenuRoute[][]
		{
			new KingdomCharterMenuRoute[]
			{
				KingdomCharterMenuRoute.ForAction("Hear a petition", 'h', KingdomCharterAction.HearPetition),
				KingdomCharterMenuRoute.ForAction("The roll of settlers", 'r', KingdomCharterAction.SettlerRoll),
				KingdomCharterMenuRoute.ForAction("How your cities hold each other", 'c', KingdomCharterAction.ManageCreed),
				KingdomCharterMenuRoute.ForAction("Share a meal from the larder", 'm', KingdomCharterAction.ShareMeal),
				KingdomCharterMenuRoute.ForAction("Consecrate a shrine", 's', KingdomCharterAction.ConsecrateShrine),
				KingdomCharterMenuRoute.ForAction("Share water with a settler", 'w', KingdomCharterAction.ShareWater),
				KingdomCharterMenuRoute.Back()
			},
			new KingdomCharterMenuRoute[]
			{
				KingdomCharterMenuRoute.ForAction("Designate district", 'd', KingdomCharterAction.DesignateDistrict),
				KingdomCharterMenuRoute.ForAction("Commission a building", 'c', KingdomCharterAction.CommissionBuilding),
				KingdomCharterMenuRoute.ForAction("Certify a machine", 'm', KingdomCharterAction.CertifyMachine),
				KingdomCharterMenuRoute.ForAction("What the keepers know", 'k', KingdomCharterAction.KeepersKnowledge),
				KingdomCharterMenuRoute.ForAction("Your works, and what they become", 'w', KingdomCharterAction.WorksAndTrades),
				KingdomCharterMenuRoute.ForAction("Name a building", 'n', KingdomCharterAction.NameBuilding),
				KingdomCharterMenuRoute.ForAction("Set the crew on the ground", 'g', KingdomCharterAction.GroundWork),
				KingdomCharterMenuRoute.ForAction("Take down a building", 't', KingdomCharterAction.StrikeBuilding),
				KingdomCharterMenuRoute.ForAction("Claim this ground", 'l', KingdomCharterAction.ClaimGround),
				KingdomCharterMenuRoute.Back()
			},
			new KingdomCharterMenuRoute[]
			{
				KingdomCharterMenuRoute.ForAction("Plans staked for later", 'p', KingdomCharterAction.ManagePlans),
				KingdomCharterMenuRoute.ForAction("Adopt a building", 'a', KingdomCharterAction.AdoptBuilding),
				KingdomCharterMenuRoute.ForAction("Release an adoption", 'r', KingdomCharterAction.ReleaseAdoption),
				KingdomCharterMenuRoute.ForAction("Post a price at the heart", 'b', KingdomCharterAction.PostPrice),
				KingdomCharterMenuRoute.ForAction("Change what stands on a lot", 'c', KingdomCharterAction.ConvertPlot),
				KingdomCharterMenuRoute.ForAction("Give a building a new look", 'l', KingdomCharterAction.RedressBuilding),
				KingdomCharterMenuRoute.Back()
			},
			new KingdomCharterMenuRoute[]
			{
				KingdomCharterMenuRoute.ForAction("Dedicate a vessel, larder, or stockpile", 'd', KingdomCharterAction.DedicateStores),
				KingdomCharterMenuRoute.ForAction("Strike a trade charter", 't', KingdomCharterAction.StrikeTradeCharter),
				KingdomCharterMenuRoute.ForAction("Send a water manifest", 'm', KingdomCharterAction.SendManifest),
				KingdomCharterMenuRoute.ForAction("Commission a salvage expedition", 'e', KingdomCharterAction.SalvageExpedition),
				KingdomCharterMenuRoute.ForAction("Set the water detail", 'w', KingdomCharterAction.SetWaterDetail),
				KingdomCharterMenuRoute.Back()
			},
			new KingdomCharterMenuRoute[]
			{
				KingdomCharterMenuRoute.ForAction("Answer a threat", 't', KingdomCharterAction.AnswerThreat),
				KingdomCharterMenuRoute.ForAction("Standings", 's', KingdomCharterAction.Standings),
				KingdomCharterMenuRoute.ForAction("Standing policy", 'p', KingdomCharterAction.StandingPolicy),
				KingdomCharterMenuRoute.Back()
			},
			new KingdomCharterMenuRoute[]
			{
				KingdomCharterMenuRoute.ForAction("What happened while you were away", 'w', KingdomCharterAction.Homecoming),
				KingdomCharterMenuRoute.ForAction("The Chronicle and dynasty", 'c', KingdomCharterAction.ChronicleAndDynasty),
				KingdomCharterMenuRoute.ForAction("As others tell it", 'o', KingdomCharterAction.OutsiderChronicle),
				KingdomCharterMenuRoute.Back()
			},
			new KingdomCharterMenuRoute[]
			{
				KingdomCharterMenuRoute.ForAction("The book of the city", 'b', KingdomCharterAction.CityBook),
				KingdomCharterMenuRoute.ForAction("Where the keepers' craft could go", 'k', KingdomCharterAction.TechMap),
				KingdomCharterMenuRoute.ForAction("What the city is asking for", 'a', KingdomCharterAction.CityAsks),
				KingdomCharterMenuRoute.Back()
			}
		};

		public static KingdomCharterMenuRoute[] RootEntries()
		{
			return Copy(Root);
		}

		public static KingdomCharterMenuRoute[] ChapterEntries(KingdomCharterChapter Chapter)
		{
			int index = (int)Chapter;
			if (index < 0 || index >= Chapters.Length)
			{
				return new KingdomCharterMenuRoute[0];
			}
			return Copy(Chapters[index]);
		}

		public static string ChapterTitle(KingdomCharterChapter Chapter)
		{
			switch (Chapter)
			{
			case KingdomCharterChapter.PeopleAndBelief: return "People & belief";
			case KingdomCharterChapter.WorksAndGround: return "Works & ground";
			case KingdomCharterChapter.PlansAndHoldings: return "Plans & holdings";
			case KingdomCharterChapter.StoresAndRoutes: return "Stores & routes";
			case KingdomCharterChapter.ThreatsAndDiplomacy: return "Threats & diplomacy";
			case KingdomCharterChapter.DynastyAndLegacy: return "Dynasty & legacy";
			case KingdomCharterChapter.CityReadings: return "The city in full";
			default: return "Charter";
			}
		}

		/// <summary>
		/// Reports and already-committed threat/petition recovery remain reachable while the realm
		/// master is paused. Every other verb can create work, spend value, or change governance and
		/// is therefore unavailable until resume.
		/// </summary>
		public static bool AvailableWhileSimulationPaused(KingdomCharterAction Action)
		{
			switch (Action)
			{
			case KingdomCharterAction.HearPetition:
			case KingdomCharterAction.Status:
			case KingdomCharterAction.Homecoming:
			case KingdomCharterAction.ChronicleAndDynasty:
			case KingdomCharterAction.OutsiderChronicle:
			case KingdomCharterAction.Standings:
			case KingdomCharterAction.SettlerRoll:
			case KingdomCharterAction.AnswerThreat:
			case KingdomCharterAction.CityBook:
			case KingdomCharterAction.TechMap:
			case KingdomCharterAction.CityAsks:
				return true;
			default:
				return false;
			}
		}

		/// <summary>Player wording for a founding stamp, without exposing engine ticks.</summary>
		public static string FoundedWhen(long Founded, long Now, long TicksPerDay)
		{
			if (Founded < 0L || Now < Founded || TicksPerDay <= 0L)
			{
				return "founding date needs inspection";
			}
			long days = (Now - Founded) / TicksPerDay;
			if (days <= 0L) return "founded today";
			if (days == 1L) return "founded yesterday";
			return "founded " + days + " days ago";
		}

		/// <summary>Player wording for a due stamp, preserving whether it has passed.</summary>
		public static string DueWhen(long Due, long Now, long TicksPerDay)
		{
			if (Due <= 0L) return "not yet scheduled";
			if (Now < 0L || TicksPerDay <= 0L) return "date needs inspection";
			if (Due == Now) return "due now";
			if (Due < Now)
			{
				long late = Now - Due;
				long lateDays = late / TicksPerDay;
				long lateRemainder = late % TicksPerDay;
				if (lateDays == 0L) return "overdue by less than a day";
				if (lateRemainder == 0L)
					return "overdue by " + lateDays + ((lateDays == 1L) ? " day" : " days");
				return "overdue by more than " + lateDays
					+ ((lateDays == 1L) ? " day" : " days");
			}
			long span = Due - Now;
			long days = span / TicksPerDay;
			long remainder = span % TicksPerDay;
			if (days == 0L) return "due within a day";
			if (remainder == 0L) return "due in " + days + ((days == 1L) ? " day" : " days");
			return "due in less than " + (days + 1L) + " days";
		}

		private static KingdomCharterMenuRoute[] Copy(KingdomCharterMenuRoute[] Source)
		{
			KingdomCharterMenuRoute[] copy = new KingdomCharterMenuRoute[Source.Length];
			Array.Copy(Source, copy, Source.Length);
			return copy;
		}
	}
}
