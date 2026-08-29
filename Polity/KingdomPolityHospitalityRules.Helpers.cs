using System.Collections.Generic;
using System.Globalization;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityHospitalityRules
	{
		internal static bool SameTransaction(KingdomPolityHospitalityTransaction A,
			KingdomPolityHospitalityTransaction B)
		{
			return A != null && B != null && A.TransactionId == B.TransactionId &&
				A.TermsPlanId == B.TermsPlanId && A.SurfaceRef == B.SurfaceRef &&
				A.ZoneId == B.ZoneId && A.PlannedTick == B.PlannedTick &&
				A.PlanDigest == B.PlanDigest && SameLines(A.Lines, B.Lines);
		}

		private static bool ValidLines(IList<KingdomPolityHospitalityDebitLine> Lines)
		{
			if (Lines == null || Lines.Count != RequiredDebitLines) return false;
			for (int i = 0; i < Lines.Count; i++)
			{
				KingdomPolityHospitalityDebitLine line = Lines[i];
				KingdomPolityHospitalityDebitKind expected = i == 0
					? KingdomPolityHospitalityDebitKind.Food
					: KingdomPolityHospitalityDebitKind.Water;
				if (line == null || line.Kind != expected ||
					!KingdomPolityRules.Text(line.ContainerId, true) ||
					!KingdomPolityRules.Text(line.ObjectId, true) ||
					!KingdomPolityRules.Text(line.Blueprint, true) || line.Before < 1 ||
					line.Before > KingdomPolityRules.MaxValueBudget ||
					line.After != line.Before - 1) return false;
				if (line.Kind == KingdomPolityHospitalityDebitKind.Food &&
					(line.Capacity != 0 || line.ContainerId == line.ObjectId)) return false;
				if (line.Kind == KingdomPolityHospitalityDebitKind.Water &&
					(line.ContainerId != line.ObjectId || line.Capacity < line.Before ||
					 line.Capacity > KingdomPolityRules.MaxValueBudget)) return false;
			}
			return true;
		}

		private static bool SameLines(IList<KingdomPolityHospitalityDebitLine> A,
			IList<KingdomPolityHospitalityDebitLine> B)
		{
			if (A == null || B == null || A.Count != B.Count) return false;
			for (int i = 0; i < A.Count; i++)
				if (A[i] == null || B[i] == null || A[i].Kind != B[i].Kind ||
					A[i].ContainerId != B[i].ContainerId || A[i].ObjectId != B[i].ObjectId ||
					A[i].Blueprint != B[i].Blueprint || A[i].Before != B[i].Before ||
					A[i].After != B[i].After || A[i].Capacity != B[i].Capacity) return false;
			return true;
		}

		private static List<KingdomPolityHospitalityDebitLine> CopyLines(
			IList<KingdomPolityHospitalityDebitLine> Values)
		{
			List<KingdomPolityHospitalityDebitLine> copy =
				new List<KingdomPolityHospitalityDebitLine>();
			for (int i = 0; Values != null && i < Values.Count; i++)
				copy.Add(Values[i]?.Copy());
			return copy;
		}

		private static string PlanDigest(KingdomPolityHospitalityTransaction T)
		{
			List<string> values = new List<string>
			{
				T.TransactionId ?? "", T.TermsPlanId ?? "", T.SurfaceRef ?? "",
				T.ZoneId ?? "", T.PlannedTick.ToString(CultureInfo.InvariantCulture)
			};
			for (int i = 0; T.Lines != null && i < T.Lines.Count; i++)
			{
				KingdomPolityHospitalityDebitLine line = T.Lines[i];
				values.Add(((byte)(line?.Kind ??
					KingdomPolityHospitalityDebitKind.None)).ToString(CultureInfo.InvariantCulture));
				values.Add(line?.ContainerId ?? ""); values.Add(line?.ObjectId ?? "");
				values.Add(line?.Blueprint ?? "");
				values.Add((line?.Before ?? -1).ToString(CultureInfo.InvariantCulture));
				values.Add((line?.After ?? -1).ToString(CultureInfo.InvariantCulture));
				values.Add((line?.Capacity ?? -1).ToString(CultureInfo.InvariantCulture));
			}
			return KingdomPolityRules.ActivationDigest("polity-hospitality-plan-v1", values);
		}

		private static KingdomPolityHospitalityTransaction CloneAsPlanned(
			KingdomPolityHospitalityTransaction T)
		{
			return new KingdomPolityHospitalityTransaction
			{
				TransactionId = T.TransactionId, TermsPlanId = T.TermsPlanId,
				SurfaceRef = T.SurfaceRef, ZoneId = T.ZoneId,
				Phase = KingdomPolityHospitalityPhase.Planned,
				PlannedTick = T.PlannedTick, Lines = CopyLines(T.Lines),
				PlanDigest = T.PlanDigest
			};
		}

		private static bool Fail(string Message, out string Failure)
		{
			Failure = Message;
			return false;
		}
	}
}
