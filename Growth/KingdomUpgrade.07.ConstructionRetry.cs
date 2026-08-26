using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

using ThousandAndFirst;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	/// <summary>
	/// Works that get better. A design may name what it grows into; when the settlement has
	/// earned it, the settlement raises the new work itself, out of what its stores can spare,
	/// through the same scaffold every commission uses &mdash; so an improvement is visibly work
	/// happening on the ground, not a number changing.
	/// <para>
	/// Improvements are AUTOMATIC, with a standing opt-out. That is a deliberate choice against
	/// the alternative of offering each one:
	/// </para>
	/// <list type="bullet">
	/// <item><description>Clicking through a confirmation for every work at every stage is the
	/// foreman's job the mod refuses to hand the player. The founder sets intent; the settlement
	/// acts on it.</description></item>
	/// <item><description>An improvement only ever adds. Everything the old work held and
	/// everything the settlement had marked on it is carried across, so a founder who never
	/// opens the Charter loses nothing they had and simply comes home to a better
	/// settlement.</description></item>
	/// <item><description>It is never a surprise, because it is announced three times before it
	/// is a fact: once per game when the settlement first becomes able to do this at all, once
	/// when a particular work starts, and continuously by the scaffold standing in the
	/// cell.</description></item>
	/// <item><description>It can always be refused, permanently, without losing anything: a
	/// single work, or this whole ground, can be held as it is, and that choice persists and is
	/// visible on the object itself.</description></item>
	/// <item><description>It cannot cause a thirst. The cost never draws the stores below the
	/// reserve the settlement lives on, and it never spends a settler who is doing something
	/// else.</description></item>
	/// </list>
	/// <para>
	/// The arithmetic and every refusal sentence are in <see cref="KingdomUpgradeRules"/>.
	/// </para>
	/// </summary>
	public static partial class KingdomUpgrade
	{
		internal static void RetryConstruction(KingdomSystem System, Zone Z, KingdomConstructionJob Job)
		{
			if (System == null || Z == null || Job == null
				|| Job.Route != KingdomConstructionRoute.Improvement
				|| !KingdomData.TryGetBuilding(Job.TargetKey, out var successor))
			{
				return;
			}
			GameObject work;
			KingdomPhysicalLookupState workState = KingdomConstruction.FindExactId(
				Z, Job.SubjectId, out work);
			if (workState == KingdomPhysicalLookupState.Ambiguous)
			{
				KingdomConstructionJob duplicate = Job;
				KingdomConstruction.Quarantine(ref duplicate,
					"The improvement predecessor ID resolves to more than one loaded object.");
				return;
			}
			if (!EnsureExactImprovementPredecessor(System, Z, work, Job))
			{
				KingdomConstructionJob complete = Job;
				GameObject result;
				int results = r_KingdomScaffold.FindExactSuccessors(Z, Job,
					successor.Blueprint, null, out result);
				if (results > 1)
				{
					KingdomConstruction.Quarantine(ref complete,
						"More than one exact improvement successor carries this receipt.");
					return;
				}
				if (results != 1 || !r_KingdomScaffold.HasRemovalProof(result, Job.SubjectId))
				{
					KingdomConstruction.Quarantine(ref complete,
						"The improvement predecessor is not exact and no proved successor replaces it.");
					return;
				}
				if (KingdomConstruction.Complete(ref complete))
					r_KingdomScaffold.TellCompletion(System, result, complete);
				return;
			}
			r_KingdomImprovement improvement = work.GetPart<r_KingdomImprovement>();
			GameObject finished = null;
			KingdomPhysicalLookupState finishedState = improvement == null
				? KingdomPhysicalLookupState.Absent
				: improvement.FindSuccessor(work.CurrentCell, out finished);
			if (finishedState == KingdomPhysicalLookupState.Ambiguous)
			{
				KingdomConstructionJob ambiguous = Job;
				KingdomConstruction.Quarantine(ref ambiguous,
					"The improvement successor ID is duplicated or malformed.");
				return;
			}
			if (finishedState == KingdomPhysicalLookupState.Exact)
			{
				KingdomConstruction.Bind(finished, Job);
				HandOver(work, finished, Job.TargetKey);
				return;
			}
			if (improvement != null && improvement.Working)
			{
				if (!ExpectedImprovementScaffold(improvement.Scaffold, work.CurrentCell, successor, Job)
					|| !KingdomConstruction.HasReceipt(improvement.Scaffold, Job))
				{
					KingdomConstructionJob ambiguous = Job;
					KingdomConstruction.Quarantine(ref ambiguous,
						"The linked improvement scaffold is absent, moved, changed, or unreceipted.");
					return;
				}
				r_KingdomScaffold scaffoldPart = improvement.Scaffold.GetPart<r_KingdomScaffold>();
				if (scaffoldPart.RemainingTicks <= 0 && scaffoldPart.LastWorkedTick > 0)
					scaffoldPart.RetryDurable(System, Z, Job);
				else
				{
					KingdomConstructionJob working = Job;
					KingdomConstruction.FinishProjection(ref working, true, true);
				}
				return;
			}
			ProjectImprovement(System, work, successor, Job, out _, out _);
		}

	}
}
