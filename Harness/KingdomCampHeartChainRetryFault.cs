using System;
using HarmonyLib;
using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	internal static class KingdomCampHeartChainRetryFault
	{
		private static bool Injected, Refused, Outstanding, Retried;
		private static string JobId, SubjectId, OutputId, Materials, Refusal;
		internal static bool Proved => Injected && Refused && Outstanding && Retried;

		internal static GameObject Place(GameObject Owner, GameObject Target, Zone Z,
			KingdomArchitectureIntent Successor)
		{
			var system = The.Game?.GetSystem<KingdomSystem>();
			if (Injected || !KingdomCampHeartChainTrace.Active(system)
				|| Successor?.BuildKey != "heartmoot") return null;
			Require(KingdomSurvey.ActiveFor(Z) != null, "retry fault lacks the production survey");
			if (!KingdomArchitectureStamper.TryProveEnvelopeGrowth(system, Z, Owner, Target,
				Successor, true, out _)) return null;
			Require(KingdomArchitectureRuntime.TryRead(Owner, out var before, out string failure), failure);
			Require(KingdomArchitectureStamper.TryPlacementPassability(Successor, Z,
				out var slots, out failure), failure);
			Cell at = null;
			foreach (var slot in slots)
			{
				int x = slot.Key % Z.Width, y = slot.Key / Z.Width;
				if (!before.Rect.Contains(x, y) && slot.Value == ArchitecturePassability.Blocked)
				{
					Cell cell = Z.GetCell(x, y);
					if (cell != null && cell.IsPassable() && !cell.HasOpenLiquidVolume())
					{ at = cell; break; }
				}
			}
			Require(at != null, "retry fault lacks a clear annexed wall slot");
			JobId = Owner.GetStringProperty(KingdomConstruction.ReceiptProperty);
			Require(KingdomConstruction.TryFind(JobId, out var job) && job != null
				&& job.Route == KingdomConstructionRoute.Improvement
				&& job.SubjectId == Owner.IDIfAssigned && job.OutputId == Target.IDIfAssigned
				&& job.PhysicalPhase == KingdomPhysicalPhase.None && KingdomConstruction.Owns(system, Z, job),
				"retry fault lacks exact paid endpoints");
			SubjectId = job.SubjectId; OutputId = job.OutputId; Materials = job.Claims.MaterialSpent;
			GameObject body = GameObject.Create("NPC");
			try
			{
				Require(body != null && body.IsCreature && !KingdomCitizenship.BelongsTo(system, body)
					&& ReferenceEquals(at.AddObject(body, NoStack: true), body) && body.CurrentCell == at,
					"retry fault did not place its exact synthetic stranger");
				Injected = true;
				Refusal = "a living occupant stands on plot-envelope growth ground at " + at.X + "," + at.Y;
				return body;
			}
			catch { if (GameObject.Validate(body)) body.Obliterate(null, Silent: true); throw; }
		}

		internal static void AfterApply(GameObject Body, bool Accepted, string Failure)
		{
			if (Body == null) return;
			try
			{
				Require(Injected && !Refused && !Accepted && Failure == Refusal && GameObject.Validate(Body),
					"controlled obstruction did not cause the exact retryable layout refusal: " + Failure);
				Refused = true;
			}
			finally { Body.Obliterate(null, Silent: true); }
			Require(!GameObject.Validate(Body) && Body.CurrentCell == null, "retry obstacle cleanup failed");
			Record("camp-heart-chain-retry-obstruction", "strict-baseline=true; synthetic-body=true; physical-refusal=true; removed=true");
		}

		internal static void AfterHandover()
		{
			if (!Refused || Outstanding) return;
			Require(KingdomConstruction.TryFind(JobId, out var job) && job != null
				&& job.Phase == KingdomConstructionPhase.Outstanding && job.Failure == Refusal
				&& job.PhysicalPhase == KingdomPhysicalPhase.None && SamePaidJob(job),
				"physical obstruction did not retain an exact Outstanding paid improvement");
			Outstanding = true;
			Record("camp-heart-chain-retry-outstanding", "physical-refusal=true; phase=Outstanding; same-paid-job=true; job=" + JobId);
		}

		internal static void ObserveRemoval(KingdomConstructionJob Job, bool Accepted)
		{
			if (!Outstanding || Retried || Job?.Id != JobId) return;
			Require(Accepted && Job.Phase == KingdomConstructionPhase.Outstanding && SamePaidJob(Job),
				"Outstanding handover could not reprove its committed scaffold removal");
			Retried = true;
			Record("camp-heart-chain-retry-removal", "phase=Outstanding; committed-removal-reproved=true; same-paid-job=true; job=" + JobId);
		}

		private static bool SamePaidJob(KingdomConstructionJob Job)
			=> Job.Id == JobId && Job.SubjectId == SubjectId && Job.OutputId == OutputId
				&& Job.Claims.MaterialSpent == Materials && KingdomConstruction.IsCurrent(Job);
		private static void Require(bool Value, string Failure)
			=> KingdomCampHeartNativeProvider.Require(Value, Failure);
		private static void Record(string Name, string Detail)
			=> Require(KingdomScenarioJournal.Append(Name, true, Detail) == null, "retry journal unavailable");
		internal static void Fault(Exception Error)
			=> KingdomScenarioJournal.Append("camp-heart-chain-retry", false, Error.GetType().Name + ": " + Error.Message);
	}

	[HarmonyPatch(typeof(KingdomArchitectureStamper), nameof(KingdomArchitectureStamper.TryApplyUpgrade))]
	internal static class KingdomCampHeartChainRetryPlacementPatch
	{
		[HarmonyPrefix]
		internal static void Prefix(GameObject Owner, GameObject Target, Zone Z,
			KingdomArchitectureIntent Successor, out GameObject __state)
		{
			__state = null;
			try { __state = KingdomCampHeartChainRetryFault.Place(Owner, Target, Z, Successor); }
			catch (Exception error) { KingdomCampHeartChainRetryFault.Fault(error); }
		}
		[HarmonyPostfix]
		internal static void Postfix(GameObject __state, bool __result, string Failure)
		{
			try { KingdomCampHeartChainRetryFault.AfterApply(__state, __result, Failure); }
			catch (Exception error) { KingdomCampHeartChainRetryFault.Fault(error); }
		}
	}

	[HarmonyPatch(typeof(KingdomUpgrade), nameof(KingdomUpgrade.HandOver))]
	internal static class KingdomCampHeartChainRetryHandoverPatch
	{
		[HarmonyPostfix]
		internal static void Postfix()
		{
			try { KingdomCampHeartChainRetryFault.AfterHandover(); }
			catch (Exception error) { KingdomCampHeartChainRetryFault.Fault(error); }
		}
	}
}
