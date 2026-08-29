using XRL;
using XRL.UI;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public static partial class KingdomFirstFeastRuntime
	{
		public static void ReconcileBestEffort(KingdomSystem System)
		{
			if (System?.Experience?.FirstFeasts == null) return;
			long now = Now();
			KingdomGrowthFirstGuestTerminalReceipt guestTerminal =
				System.LifecycleBook?.Growth?.FirstGuestTerminal;
			if (guestTerminal != null
				&& !KingdomGuestFeastRuntime.TryObserveGrowthTerminalBestEffort(System,
					guestTerminal, out string terminalFailure))
				KingdomLog.Log("guest feast: terminal replay retained ("
					+ terminalFailure + ")");
			if (KingdomExperienceRuntime.TryObserveConfiguredOptions(System, now,
				out string optionFailure)
				&& !KingdomExperienceRules.CanEmit(System.Experience,
					KingdomExperienceOptionKind.CivicStory, now))
			{
				string[] settlements = new string[System.Experience.FirstFeasts.Count];
				for (int i = 0; i < settlements.Length; i++)
					settlements[i] = System.Experience.FirstFeasts[i].SettlementId;
				for (int i = 0; i < settlements.Length; i++)
				{
					if (!KingdomExperienceRules.TryGetFirstFeast(System.Experience,
						settlements[i], out KingdomFirstFeastReceipt offer,
						out string archiveFailure)) break;
					if (offer?.Phase == KingdomFirstFeastPhase.Offered
						&& !KingdomExperienceRules.TryArchiveFirstFeastOffer(
							System.Experience, System.Experience.Revision, settlements[i],
							now, out _, out _, out archiveFailure))
						KingdomLog.Log("first feast: disabled-offer archive retained ("
							+ archiveFailure + ")");
				}
			}
			for (int i = 0; i < System.Experience.FirstFeasts.Count; i++)
			{
				KingdomFirstFeastReceipt row = System.Experience.FirstFeasts[i];
				if ((KingdomFirstFeastRules.IsAffirmative(row)
						|| row.Phase == KingdomFirstFeastPhase.Refused
						|| row.Phase == KingdomFirstFeastPhase.Archived)
					&& !KingdomGuestFeastRuntime.TryObservePractice(System,
						The.Player?.CurrentZone, row, out string guestFailure))
					KingdomLog.Log("guest feast: load observation retained ("
						+ guestFailure + ")");
				if (KingdomFirstFeastRules.IsAffirmative(row) && !TellPractice(System, row))
					KingdomLog.Log("first feast: attributed telling remains pending for "
						+ row.SettlementId);
			}
			KingdomCommunalRiteRuntime.ReconcileBestEffort(System);
		}

		/// <summary>Read-only optional locus trace. The practice survives absent people and cook.</summary>
		public static bool TryDescribePractice(KingdomSystem System, string SettlementId,
			out string Description)
		{
			Description = null;
			if (System?.Experience == null
				|| !KingdomExperienceRules.TryGetFirstFeast(System.Experience, SettlementId,
					out KingdomFirstFeastReceipt row, out string _)
				|| !KingdomFirstFeastRules.IsAffirmative(row)) return false;
			Description = KingdomFirstFeastRules.RenderOutcome(row); return true;
		}

		private static void ShowExisting(KingdomSystem System, XRL.World.GameObject Founder,
			CityContext Context,
			KingdomFirstFeastReceipt Receipt)
		{
			bool history = !KingdomFirstFeastRules.IsAffirmative(Receipt)
				|| TellPractice(System, Receipt);
			string text = KingdomFirstFeastRules.RenderOutcome(Receipt);
			if (KingdomFirstFeastRules.IsAffirmative(Receipt))
			{
				if (!KingdomGuestFeastRuntime.TryObservePractice(System, Founder?.CurrentZone,
					Receipt, out string guestFailure))
					KingdomLog.Log("guest feast: practice review retained (" + guestFailure + ")");
				if (KingdomGuestFeastRuntime.TryTrace(System, Context.SettlementId,
					Receipt, out string trace)) text = trace;
				text += "\n\n" + RecipeStatus(Context.Book)
					+ (history ? "" : "\n\nIts attributed Chronicle telling remains pending recovery.");
				if (KingdomGuestFeastRuntime.TryDescribe(System, Context.SettlementId,
					out string guest)) text += "\n\n" + guest;
			}
			if (KingdomFirstFeastRules.IsAffirmative(Receipt))
				KingdomCommunalRiteRuntime.Open(System, Founder, Context, Receipt, text);
			else Popup.Show(text);
		}

		private static bool TellPractice(KingdomSystem System, KingdomFirstFeastReceipt Row)
		{
			string id = KingdomFirstFeastRules.ChronicleEventId(Row);
			string text = KingdomFirstFeastRules.ChronicleClause(Row);
			return id != null && text != null
				&& KingdomChronicle.RecordOnce(System, id, text);
		}

		private static string RecipeStatus(KingdomCityBook Book)
		{
			Book?.Normalize();
			KingdomNamedCookReceipt cook = Book?.NamedCook;
			bool available = KingdomNamedCookRules.Validate(cook, out string _)
				&& KingdomNamedCookRules.ServiceState(cook)
					== KingdomNamedCookServiceState.Available;
			return KingdomFirstFeastRules.RecipePolicyText(available,
				available ? cook.ResidentName : null,
				available ? cook.RecipeDisplayName : null);
		}

		private static long Now()
		{
			return The.Game == null || The.Game.TimeTicks < 0L ? 0L : The.Game.TimeTicks;
		}
	}
}
