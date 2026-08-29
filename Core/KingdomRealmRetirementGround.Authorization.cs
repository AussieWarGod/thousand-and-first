using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	internal static partial class KingdomRealmRetirementGround
	{
		internal static bool TryAuthorizeRecords(KingdomRealmRetirementState State,
			KingdomRealmRemovalGroundPlan Plan, long Tick, out string Failure)
		{
			Failure = null;
			if (State == null || Plan == null
				|| !TryBuildObjectReceipts(State, Plan, out Failure)) return false;
			Plan.ObjectRecord = Plan.ObjectCompletionRecords[0].Clone();
			KingdomRealmRetirementState preview = State;
			List<KingdomRemovalRecord> rows = new List<KingdomRemovalRecord>();
			rows.AddRange(Plan.ObjectPreviewRecords);
			rows.AddRange(Plan.ObjectCompletionRecords);
			for (int i = 0; i < rows.Count; i++)
			{
				if (!KingdomRealmRetirementRules.TryRecord(preview, preview.Revision,
					rows[i], Tick, out KingdomRealmRetirementState next, out Failure)) return false;
				preview = next;
			}
			return KingdomRealmRemovalRetryRules.FenceCapacityReserved(preview.Records.Count)
				|| Fail("ground roster leaves no capacity for the terminal fence", out Failure);
		}

		private static bool TryBuildObjectReceipts(KingdomRealmRetirementState State,
			KingdomRealmRemovalGroundPlan Plan, out string Failure)
		{
			Failure = null;
			string generation = "v2";
			KingdomRemovalRecord firstCompletion = GroundRecord(State,
				"taf:ground-complete:" + Plan.Zone.ZoneID + ":v2");
			KingdomRemovalLocator locator = State.Locators.Find(row =>
				row.ZoneId == Plan.Zone.ZoneID);
			if (locator?.State == KingdomRemovalLocatorState.Cleaned && firstCompletion != null)
			{
				if (!TryActualGroundEvidence(Plan, out string firstActual, out Failure)) return false;
				if (firstActual != firstCompletion.AfterDigest) generation = "v3";
			}
			string previewId = "taf:ground-preview:" + Plan.Zone.ZoneID + ":" + generation;
			string completionId = "taf:ground-complete:" + Plan.Zone.ZoneID + ":" + generation;
			if (!TryGroundRows(Plan, false, out List<string> liveRows, out Failure)
				|| !TryGroundRows(Plan, true, out List<string> projectedRows, out Failure)) return false;
			string before = KingdomRetirementDigestRules.Evidence("ground-before-v2", liveRows);
			string projected = KingdomRetirementDigestRules.Evidence("clean-ground-v2", projectedRows);
			KingdomRemovalRecord preview = GroundRecord(State, previewId);
			KingdomRemovalRecord committed = GroundRecord(State, completionId);
			if (preview == null)
				preview = new KingdomRemovalRecord
				{
					Kind = KingdomRemovalProjectionKind.Object, Id = previewId,
					Disposition = KingdomRemovalDisposition.Preserved,
					BeforeDigest = before, AfterDigest = projected,
					Amount = Plan.RetainedObjectCount,
					Detail = "bounded whole-zone roster frozen; legacy and shared values remain disclosed and preserved"
				};
			else if (preview.Disposition != KingdomRemovalDisposition.Preserved
				|| preview.AfterDigest != projected || preview.Amount != Plan.RetainedObjectCount)
				return Fail(Failure ?? "active ground diverged from its bounded aggregate preview",
					out Failure);
			if (preview.BeforeDigest != before
				&& (!TryActualGroundEvidence(Plan, out string actual, out Failure)
					|| (actual != preview.AfterDigest && !Plan.WitnessRetryProgress)))
				return Fail(Failure ?? "active ground diverged from its bounded aggregate preview",
					out Failure);
			KingdomRemovalRecord completion = new KingdomRemovalRecord
			{
				Kind = KingdomRemovalProjectionKind.Object, Id = completionId,
				Disposition = KingdomRemovalDisposition.Converted,
				BeforeDigest = preview.BeforeDigest, AfterDigest = preview.AfterDigest,
				Amount = preview.Amount,
				Detail = "owned blueprints converted; callback-bearing carriers and value metadata preserved"
			};
			if (committed != null && !ExactRecord(committed, completion))
				return Fail("ground completion differs from its aggregate preview", out Failure);
			if (committed != null
				&& (!TryActualGroundEvidence(Plan, out string terminal, out Failure)
					|| terminal != preview.AfterDigest))
				return Fail(Failure ?? "completed ground no longer matches its terminal aggregate",
					out Failure);
			Plan.ProjectedEvidenceDigest = preview.AfterDigest;
			Plan.ObjectPreviewRecords.Add(preview); Plan.ObjectCompletionRecords.Add(completion);
			return true;
		}

		private static KingdomRemovalRecord GroundRecord(KingdomRealmRetirementState State,
			string Id)
		{
			for (int i = 0; i < (State?.Records?.Count ?? 0); i++)
				if (State.Records[i].Kind == KingdomRemovalProjectionKind.Object
					&& State.Records[i].Id == Id) return State.Records[i].Clone();
			return null;
		}

		private static bool ExactRecord(KingdomRemovalRecord A, KingdomRemovalRecord B)
		{
			return A != null && B != null && A.Kind == B.Kind && A.Id == B.Id
				&& A.Disposition == B.Disposition && A.BeforeDigest == B.BeforeDigest
				&& A.AfterDigest == B.AfterDigest && A.Amount == B.Amount
				&& A.Detail == B.Detail;
		}

		private static string ProjectedObjectRow(GameObject Item,
			Dictionary<GameObject, GameObjectBlueprint> Fallbacks)
		{
			string blueprint = Fallbacks.TryGetValue(Item, out GameObjectBlueprint fallback)
				? fallback.Name : Item.Blueprint;
			return ObjectRosterRow(Item, blueprint, true, true);
		}

		internal static bool TryRevalidate(KingdomSystem System,
			KingdomRealmRemovalGroundPlan Expected, out string Failure)
		{
			Failure = null;
			if (!TryPrepare(System, Expected?.Zone, out KingdomRealmRemovalGroundPlan actual,
				out Failure)) return false;
			if (!SameReferences(Expected.Objects, actual.Objects)
				|| !SameReferences(Expected.ExactForeignCitizens, actual.ExactForeignCitizens)
				|| !SameWitnessPlan(Expected, actual)
				|| !SameFallbacks(Expected.Fallbacks, actual.Fallbacks)
				|| !SameReferences(new List<GameObject>(Expected.RemovedObjects),
					new List<GameObject>(actual.RemovedObjects))
				|| !SameExternalOwnership(Expected.ExternalOwnership,
					actual.ExternalOwnership)
				|| Expected.RecoveryDigest != actual.RecoveryDigest
				|| Expected.LegacyCitizenCount != actual.LegacyCitizenCount
				|| Expected.OwnedBlueprintCount != actual.OwnedBlueprintCount
				|| Expected.CustomPartCount != actual.CustomPartCount
				|| Expected.ObjectPropertyCount != actual.ObjectPropertyCount
				|| Expected.ZonePropertyCount != actual.ZonePropertyCount
				|| Expected.ZonePartCount != actual.ZonePartCount
				|| Expected.SharedFaction != actual.SharedFaction)
				return Fail("active ground changed after its destructive preview", out Failure);
			return true;
		}

		private static bool TryGroundRows(KingdomRealmRemovalGroundPlan Plan, bool Projected,
			out List<string> Rows, out string Failure)
		{
			Rows = new List<string>(); Failure = null;
			if (Plan?.Zone == null) return Fail("ground aggregate has no zone", out Failure);
			HashSet<GameObject> mutations = new HashSet<GameObject>(Plan.MutationObjects);
			for (int i = 0; i < Plan.Objects.Count; i++)
			{
				GameObject item = Plan.Objects[i]; bool removed = Plan.RemovedObjects.Contains(item);
				if (Projected && removed) continue;
				if (!Projected && removed && !GameObject.Validate(item)) continue;
				Rows.Add(Projected && mutations.Contains(item)
					? ProjectedObjectRow(item, Plan.Fallbacks) : ObjectRosterRow(item));
			}
			Rows.Add("zone=" + Plan.Zone.ZoneID); Rows.Add("recovery=" + Plan.RecoveryDigest);
			Rows.Add("legacy=" + Plan.LegacyCitizenCount); Rows.Add("shared="
				+ (Plan.SharedFaction ?? "<null>"));
			Rows.Sort(System.StringComparer.Ordinal); return true;
		}

		private static bool SameReferences(List<GameObject> Expected, List<GameObject> Actual)
		{
			if (Expected == null || Actual == null || Expected.Count != Actual.Count) return false;
			HashSet<GameObject> values = new HashSet<GameObject>(Expected);
			return values.Count == Expected.Count && values.SetEquals(Actual);
		}

		private static bool SameFallbacks(Dictionary<GameObject, GameObjectBlueprint> Expected,
			Dictionary<GameObject, GameObjectBlueprint> Actual)
		{
			if (Expected == null || Actual == null || Expected.Count != Actual.Count) return false;
			foreach (KeyValuePair<GameObject, GameObjectBlueprint> row in Expected)
				if (!Actual.TryGetValue(row.Key, out GameObjectBlueprint other)
					|| row.Value?.Name != other?.Name) return false;
			return true;
		}

		private static bool SameExternalOwnership(KingdomExternalOwnershipResetPlan Expected,
			KingdomExternalOwnershipResetPlan Actual)
		{
			if (Expected == null || Actual == null || Expected.ZoneId != Actual.ZoneId
				|| Expected.Values.Count != Actual.Values.Count) return false;
			foreach (KeyValuePair<string, string> row in Expected.Values)
				if (!Actual.Values.TryGetValue(row.Key, out string other)
					|| row.Value != other) return false;
			return true;
		}
	}
}
