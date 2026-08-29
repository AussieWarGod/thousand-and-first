using System;
using System.Collections.Generic;
using XRL;
using XRL.UI;
using XRL.World;

using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	/// <summary>Charter surface and exact city/resident selection for one named cook per city.</summary>
	public static partial class KingdomNamedCook
	{
		private sealed class CityContext
		{
			internal KingdomCityBook Book;
			internal string SettlementId;
			internal string SettlementName;
		}

		private sealed class ResidentChoice
		{
			internal int ResidentId;
			internal string Name;
		}

		public static void Open(KingdomSystem System, GameObject Founder)
		{
			CityContext city;
			string failure;
			if (!TryCurrentCity(System, Founder, out city, out failure))
			{
				Popup.Show(failure);
				return;
			}
			Reconcile(System, city.Book, true, out failure);
			KingdomNamedCookReceipt receipt = city.Book.NamedCook;
			if (receipt.Phase == KingdomNamedCookPhase.Quarantined)
			{
				Popup.Show("The named-cook receipt is quarantined. Nothing was overwritten.\n\n"
					+ receipt.Fault);
				return;
			}
			if (receipt.Phase == KingdomNamedCookPhase.Prepared
				|| KingdomNamedCookRules.IsVacancyPrepared(receipt.Phase))
			{
				Popup.Show("The appointment has an unfinished exact projection. "
					+ (failure ?? "Bring the named resident back onto the roll and ask again."));
				return;
			}

			ResidentChoice chosen = null;
			if (receipt.Phase == KingdomNamedCookPhase.Applied)
			{
				string[] options = new string[]
				{
					"Keep " + KingdomPresentation.Rich(receipt.ResidentName) + " as named cook",
					"Let " + KingdomPresentation.Rich(receipt.ResidentName)
						+ " retire; leave the hearth vacant",
					"Ask for a deliberate handoff to another resident"
				};
				int action = Popup.PickOption(Title: "Named cook of "
					+ KingdomPresentation.Rich(city.SettlementName),
					Intro: KingdomPresentation.Rich(receipt.ResidentName) + " teaches "
						+ receipt.RecipeDisplayName + " through Qud's ordinary water ritual. "
						+ "A recipe already learned remains learned after release.",
					Options: options, AllowEscape: true);
				if (action <= 0) return;
				if (action == 2 && !TryChooseResident(city.Book, city.SettlementName,
					receipt.ResidentId, out chosen)) return;
				KingdomNamedCookVacancyCause cause = action == 1
					? KingdomNamedCookVacancyCause.VoluntaryRetirement
					: KingdomNamedCookVacancyCause.Handoff;
				bool released = TryRelease(System, city.Book, cause,
					out bool releaseMutation, out failure);
				if (releaseMutation)
					KingdomGovernanceScope.Commit(action == 1
						? "retire named cook" : "handoff named cook");
				if (!released)
				{
					Popup.Show((failure ?? "The exact appointment could not be released.")
						+ (releaseMutation ? " The prepared vacancy remains visible for recovery." : ""));
					return;
				}
				if (action == 1)
				{
					Popup.Show(receipt.ResidentName + " retired from the named hearth. Learned "
						+ "recipes, roles, and belongings were left alone; the vacancy has no deadline.");
					return;
				}
				bool appointed = TryDesignate(System, city.Book, chosen.ResidentId,
					out bool appointmentMutation, out failure);
				if (!releaseMutation && appointmentMutation)
					KingdomGovernanceScope.Commit("appoint named cook after handoff");
				if (!appointed)
				{
					Popup.Show((failure ?? "The deliberate successor could not be appointed.")
						+ " The hearth remains vacant or prepared; no recipe, item, or role was gifted.");
					return;
				}
				Popup.Show(KingdomPresentation.Rich(chosen.Name) + " now keeps the named hearth. "
					+ "The predecessor's learned recipe remains vanilla-owned; this appointment grants "
					+ "no free serving or inherited belongings.");
				return;
			}

			if (!TryChooseResident(city.Book, city.SettlementName, 0, out chosen)) return;
			bool designated = TryDesignate(System, city.Book, chosen.ResidentId,
				out bool designationMutation, out failure);
			if (designationMutation) KingdomGovernanceScope.Commit("appoint named cook");
			if (!designated)
			{
				Popup.Show((failure ?? "The appointment did not change.")
					+ (designationMutation ? " Its prepared receipt remains visible for recovery." : ""));
				return;
			}
			Popup.Show(KingdomPresentation.Rich(chosen.Name) + " is now the named cook of "
				+ KingdomPresentation.Rich(city.SettlementName) + ". Their city recipe is offered "
				+ "through the ordinary water ritual.");
		}

		private static bool TryCurrentCity(KingdomSystem System, GameObject Founder,
			out CityContext Context, out string Failure)
		{
			Context = null;
			Failure = "Stand on the held ground of the city whose cook you mean to appoint.";
			Zone zone = Founder?.CurrentZone;
			if (System == null || !System.Founded || Founder == null || !Founder.IsPlayer()
				|| zone == null || !System.OwnedZone(zone.ZoneID)) return false;
			bool seated = System.ClaimedZones != null && System.ClaimedZones.Contains(zone.ZoneID);
			KingdomSettlement other = seated ? null : System.FindNonSeatSettlementByZone(zone.ZoneID);
			KingdomCityBook book = seated ? System.City : other?.City;
			string id = seated ? System.City?.SettlementId : other?.City?.SettlementId;
			string name = seated ? System.SeatName : other?.SettlementName;
			if (book == null || string.IsNullOrEmpty(id) || string.IsNullOrEmpty(name))
			{
				Failure = "The exact city book and identity cannot be proved.";
				return false;
			}
			book.Normalize();
			Context = new CityContext { Book = book, SettlementId = id,
				SettlementName = name };
			return true;
		}

		private static bool TryChooseResident(KingdomCityBook Book, string CityName,
			int ExcludedResidentId, out ResidentChoice Choice)
		{
			Choice = null;
			List<ResidentChoice> rows = new List<ResidentChoice>();
			Dictionary<string, int> names = new Dictionary<string, int>(StringComparer.Ordinal);
			List<KingdomResidentRow> residents = KingdomResidents.RollRows(Book);
			for (int i = 0; i < residents.Count; i++)
			{
				KingdomResidentRow resident = residents[i];
				if (resident.Standing != KingdomResidentStanding.Resident
					|| resident.ResidentId == ExcludedResidentId) continue;
				string name = resident.Name ?? "";
				rows.Add(new ResidentChoice { ResidentId = resident.ResidentId, Name = name });
				names[name] = names.ContainsKey(name) ? names[name] + 1 : 1;
			}
			if (rows.Count == 0)
			{
				Popup.Show("No named resident currently stands on this city's roll.");
				return false;
			}
			string[] options = new string[rows.Count];
			for (int i = 0; i < rows.Count; i++)
			{
				options[i] = KingdomPresentation.Rich(rows[i].Name)
					+ (names[rows[i].Name] > 1 ? " {{K|[roll " + rows[i].ResidentId + "]}}" : "");
			}
			int pick = Popup.PickOption(Title: "Named cook of "
				+ KingdomPresentation.Rich(CityName),
				Intro: "Choose one exact standing resident. Existing native recipe teachers and "
					+ "followers are left untouched.", Options: options, AllowEscape: true);
			if (pick < 0 || pick >= rows.Count) return false;
			Choice = rows[pick];
			return true;
		}
	}
}
