using System;
using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomResidentTransitionAuthority
	{
		private static bool TryProjectDurableClaims(KingdomSystem System, GameObject Body,
			int ResidentId, ref KingdomResidentTransitionClaim Claims)
		{
			List<KingdomCityBook> books = System.OwnedCityBooks();
			if (books == null || books.Count == 0) return false;
			int cookClaims = 0;
			for (int i = 0; i < books.Count; i++)
			{
				KingdomCityBook book = books[i];
				if (book == null || !TryProjectCityClaims(book, Body, ResidentId,
					ref Claims, ref cookClaims)) return false;
			}
			if (cookClaims > 1 || Body.GetPart<XRL.World.Parts.r_KingdomNamedCook>() != null
				&& cookClaims != 1)
				Claims |= KingdomResidentTransitionClaim.AuthorityUnproved;
			return TryProjectOfficeClaims(System, Body, ResidentId, ref Claims)
				&& TryProjectExpeditionClaims(System, Body, ResidentId, ref Claims)
				&& TryProjectLifecycleClaims(System, Body, ResidentId, ref Claims);
		}

		private static bool TryProjectCityClaims(KingdomCityBook Book, GameObject Body,
			int ResidentId, ref KingdomResidentTransitionClaim Claims, ref int CookClaims)
		{
			string objectId = Body.IDIfAssigned;
			KingdomNamedCookReceipt cook = Book.NamedCook;
			if (cook != null && cook.Phase != KingdomNamedCookPhase.None
				&& !KingdomNamedCookRules.IsVacant(cook.Phase)
				&& (cook.ResidentId == ResidentId
					|| string.Equals(cook.BodyObjectId, objectId, StringComparison.Ordinal)))
			{
				CookClaims++;
				Claims |= KingdomResidentTransitionClaim.NamedCook;
				XRL.World.Parts.r_KingdomNamedCook marker =
					Body.GetPart<XRL.World.Parts.r_KingdomNamedCook>();
				bool exact = KingdomNamedCookRules.Validate(cook, out string _)
					&& cook.ResidentId == ResidentId
					&& string.Equals(cook.BodyObjectId, objectId,
						StringComparison.Ordinal)
					&& marker != null && marker.Matches(cook, Body);
				if (!exact || cook.Phase != KingdomNamedCookPhase.Applied
					&& cook.Phase != KingdomNamedCookPhase.DepartureVacancyPrepared)
					Claims |= KingdomResidentTransitionClaim.AuthorityUnproved;
				else if (cook.Phase == KingdomNamedCookPhase.DepartureVacancyPrepared)
					Claims |= KingdomResidentTransitionClaim.CookDeparturePrepared;
			}

			KingdomAssentingMootReceipt moot = Book.AssentingMoot;
			if (moot != null && moot.Phase != KingdomAssentingMootPhase.None
				&& (MemberClaim(moot.AssentResidentIds, moot.AssentBodyObjectIds,
						ResidentId, objectId)
					|| MemberClaim(moot.ExemptResidentIds, moot.ExemptBodyObjectIds,
						ResidentId, objectId)))
				Claims |= KingdomResidentTransitionClaim.AssentingMoot;

			if (!KingdomHappeningLifecycleRules.TryDecode(Book.HappeningModel,
				out KingdomHappeningLifecycleBook happening,
				out KingdomHappeningLifecycleFault _)) return false;
			KingdomHappeningOperation active = happening.Active;
			for (int i = 0; active != null && i < active.Participants.Length; i++)
			{
				KingdomHappeningParticipant participant = active.Participants[i];
				if (participant.ResidentId == ResidentId
					|| string.Equals(participant.ObjectId, objectId,
						StringComparison.Ordinal))
				{
					Claims |= KingdomResidentTransitionClaim.PhysicalHappening;
					break;
				}
			}
			return true;
		}

		private static bool MemberClaim(IList<int> ResidentIds, IList<string> ObjectIds,
			int ResidentId, string ObjectId)
		{
			for (int i = 0; ResidentIds != null && i < ResidentIds.Count; i++)
				if (ResidentIds[i] == ResidentId) return true;
			for (int i = 0; ObjectIds != null && i < ObjectIds.Count; i++)
				if (string.Equals(ObjectIds[i], ObjectId, StringComparison.Ordinal)) return true;
			return false;
		}
	}
}
