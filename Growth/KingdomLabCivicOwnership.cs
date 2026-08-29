using System;
using System.Collections.Generic;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	internal static partial class KingdomLabCivicRuntime
	{
		internal const string OwnerStateKey = "r_TAF_LabCivicOwners_v1";

		private static bool TryCanonicalOwner(KingdomSystem System, Zone Z,
			KingdomSurvey Survey, bool AllowClaim, out GameObject Work,
			out r_KingdomLabCivicFriction Part,
			out KingdomLabCivicOwnerRow Owner, out string Failure)
		{
			Work = null; Part = null; Owner = null; Failure = null;
			string realm = System?.CurrentRealmId;
			string settlement = System?.SettlementIdForOwnedZone(Z?.ZoneID);
			if (Survey == null || !ReferenceEquals(Survey.Ground, Z)
				|| string.IsNullOrEmpty(realm) || string.IsNullOrEmpty(settlement)
				|| settlement != System.CurrentSettlementId)
				return Fail("The current active settlement identity cannot own a laboratory cause.",
					out Failure);
			if (!TryReadOwners(out string raw, out KingdomLabCivicOwnerBook book, out Failure))
				return false;
			KingdomLabCivicOwnerRow held = KingdomLabCivicOwnerRules.Find(book, settlement);
			List<GameObject> candidates = CivicWorks(Survey);
			if (held != null)
			{
				if (held.RealmId != realm)
					return Fail("Another realm incarnation owns this settlement's laboratory pin.",
						out Failure);
				Owner = held.Copy();
				if (held.ZoneId != Z.ZoneID)
					return Fail("The settlement's canonical laboratory stands on another ground; it was not loaded.", out Failure);
				int matches = 0;
				for (int i = 0; i < candidates.Count; i++)
					if (candidates[i].IDIfAssigned == held.OwnerObjectId) { Work = candidates[i]; matches++; }
				if (matches > 1)
					return Fail("Duplicate live laboratory IDs collide with the canonical owner.", out Failure);
				if (matches == 0)
				{
					ObserveMissingOwner(System, Z, held);
					if (!TryReleaseOwner(raw, book, held, out Failure)) return false;
					Owner = null;
					return Fail("The visited ground proves the old laboratory owner gone; its pin was released. Ask again on a later pass.", out Failure);
				}
				Part = Work.RequirePart<r_KingdomLabCivicFriction>();
				return Part != null || Fail("The canonical laboratory rejected its receipt carrier.", out Failure);
			}
			if (candidates.Count == 0)
				return Fail("No completed grafting hall or chimeric theatre stands on this ground.", out Failure);
			if (!AllowClaim)
				return Fail("New civic work is fenced; no laboratory owner was claimed.", out Failure);
			for (int i = 1; i < candidates.Count; i++)
				if (candidates[i - 1].IDIfAssigned == candidates[i].IDIfAssigned)
					return Fail("Two active laboratories expose one object ID; no cause was minted.", out Failure);
			Work = candidates[0];
			KingdomLabCivicOwnerRow claim = new KingdomLabCivicOwnerRow
			{
				RealmId = realm, SettlementId = settlement, ZoneId = Z.ZoneID,
				OwnerObjectId = Work.IDIfAssigned
			};
			if (!KingdomLabCivicOwnerRules.TryClaim(book, claim,
				out KingdomLabCivicOwnerBook next)
				|| !TryPublishOwners(raw, next, out Failure)) return false;
			Owner = claim;
			Part = Work.RequirePart<r_KingdomLabCivicFriction>();
			return Part != null || Fail("The claimed laboratory rejected its receipt carrier.", out Failure);
		}

		private static List<GameObject> CivicWorks(KingdomSurvey Survey)
		{
			List<GameObject> result = new List<GameObject>();
			for (int i = 0; Survey != null && i < Survey.Built.Count; i++)
			{
				GameObject work = Survey.Built[i];
				if (!GameObject.Validate(work) || work.GetIntProperty(KingdomUpgrade.BuiltProperty) != 1
					|| work.GetPart<r_KingdomGraftingHall>() == null
					&& work.GetPart<r_KingdomChimericTheatre>() == null) continue;
				if (!string.IsNullOrEmpty(work.IDIfAssigned)) result.Add(work);
			}
			result.Sort((a, b) => string.CompareOrdinal(a.IDIfAssigned, b.IDIfAssigned));
			return result;
		}

		private static bool TryReadOwners(out string Raw, out KingdomLabCivicOwnerBook Book,
			out string Failure)
		{
			Raw = null; Book = null; Failure = null;
			if (The.Game == null)
				return Fail("No live game can read the laboratory owner register.", out Failure);
			Raw = The.Game.GetStringGameState(OwnerStateKey, "") ?? "";
			return KingdomLabCivicOwnerRules.TryDecode(Raw, out Book)
				|| Fail("The laboratory owner register is malformed and quarantined.", out Failure);
		}

		private static bool TryPublishOwners(string Expected,
			KingdomLabCivicOwnerBook Next, out string Failure)
		{
			Failure = null;
			if (The.Game == null || Next == null)
				return Fail("No live game can publish the laboratory owner register.", out Failure);
			string current = The.Game.GetStringGameState(OwnerStateKey, "") ?? "";
			string wire = Next.Rows.Count == 0 ? "" : KingdomLabCivicOwnerRules.Encode(Next);
			if (current != (Expected ?? "") || wire == null)
				return Fail("The laboratory owner register changed after observation.", out Failure);
			The.Game.SetStringGameState(OwnerStateKey, wire);
			return (The.Game.GetStringGameState(OwnerStateKey, "") ?? "") == wire
				|| Fail("The laboratory owner register did not persist exactly.", out Failure);
		}

		private static bool TryReleaseOwner(string Raw, KingdomLabCivicOwnerBook Book,
			KingdomLabCivicOwnerRow Expected, out string Failure)
		{
			if (!KingdomLabCivicOwnerRules.TryRelease(Book, Expected,
				out KingdomLabCivicOwnerBook next))
				return Fail("The exact laboratory owner pin could not be released.", out Failure);
			return TryPublishOwners(Raw, next, out Failure);
		}

		private static bool ReleaseExact(KingdomLabCivicOwnerRow Expected, out string Failure)
		{
			if (!TryReadOwners(out string raw, out KingdomLabCivicOwnerBook book, out Failure))
				return false;
			KingdomLabCivicOwnerRow held = KingdomLabCivicOwnerRules.Find(book,
				Expected?.SettlementId);
			if (held == null) return true;
			if (!KingdomLabCivicOwnerRules.Same(held, Expected))
				return Fail("A different exact laboratory owns the settlement pin.", out Failure);
			return TryReleaseOwner(raw, book, held, out Failure);
		}
	}
}
