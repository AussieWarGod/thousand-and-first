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

	public static partial class KingdomUpgrade
	{
		private static bool ExpectedImprovementScaffold(GameObject Scaffold, Cell Cell,
			KingdomRules.BuildEntry Successor, KingdomConstructionJob Job)
		{
			return ExpectedImprovementScaffold(Scaffold, Cell, Successor, Job, null, false);
		}

		private static bool ExpectedImprovementScaffold(GameObject Scaffold, Cell Cell,
			KingdomRules.BuildEntry Successor, KingdomConstructionJob Job,
			KingdomArchitectureIntent Architecture,
			bool Authored)
		{
			r_KingdomScaffold part = GameObject.Validate(Scaffold)
				? Scaffold.GetPart<r_KingdomScaffold>() : null;
			return part != null && Scaffold.CurrentCell == Cell && Successor != null
				&& Scaffold.GetStringProperty(BuildKeyProperty) == Successor.Key
				&& part.TargetBlueprint == Successor.Blueprint
				&& (KingdomConstruction.BuildTruthMatches(Scaffold, Job)
					|| KingdomConstruction.LegacyProjectedBuildTruthMatchesUnknownPlot(
						Scaffold, Job))
				&& (!Authored || Architecture != null
					&& KingdomArchitectureRuntime.TryRead(Scaffold,
						out KingdomArchitectureIntent frozen, out _)
					&& frozen.SnapshotHash == Architecture.SnapshotHash);
		}

		private static KingdomPhysicalLookupState FindImprovementScaffold(Cell Cell,
			KingdomRules.BuildEntry Successor, KingdomConstructionJob Job,
			out GameObject Scaffold)
		{
			return FindImprovementScaffold(Cell, Successor, Job, null, false, out Scaffold);
		}

		private static KingdomPhysicalLookupState FindImprovementScaffold(Cell Cell,
			KingdomRules.BuildEntry Successor, KingdomConstructionJob Job,
			KingdomArchitectureIntent Architecture, bool Authored,
			out GameObject Scaffold)
		{
			Scaffold = null;
			if (Cell == null || Successor == null || Job == null
				|| string.IsNullOrEmpty(Job.OutputId)) return KingdomPhysicalLookupState.Absent;
			GameObject found = null;
			int count = 0;
			foreach (GameObject item in Cell.GetObjects())
			{
				if (!KingdomConstruction.HasReceipt(item, Job)) continue;
				count++;
				if (count > 1 || item.IDIfAssigned != Job.OutputId
					|| !ExpectedImprovementScaffold(item, Cell, Successor, Job,
						Architecture, Authored))
					return KingdomPhysicalLookupState.Ambiguous;
				found = item;
			}
			GameObject global;
			KingdomPhysicalLookupState globalState = KingdomConstruction.FindExactId(
				Cell.ParentZone, Job.OutputId, out global);
			if (count == 0)
				return globalState == KingdomPhysicalLookupState.Absent
					? KingdomPhysicalLookupState.Absent : KingdomPhysicalLookupState.Ambiguous;
			if (globalState != KingdomPhysicalLookupState.Exact
				|| !ReferenceEquals(global, found)) return KingdomPhysicalLookupState.Ambiguous;
			Scaffold = found;
			return KingdomPhysicalLookupState.Exact;
		}

		private static bool IsImprovementPredecessorIdentity(KingdomSystem System, Zone Z,
			GameObject Work, KingdomConstructionJob Job)
		{
			Cell cell = Z == null || Job == null ? null : Z.GetCell(Job.X, Job.Y);
			KingdomArchitectureIntent architecture;
			bool authored;
			string architectureFailure;
			return GameObject.Validate(Work) && cell != null
				&& KingdomConstruction.Owns(System, Z, Job)
				&& Work.IDIfAssigned == Job.SubjectId && Work.CurrentZone == Z && Work.CurrentCell == cell
				&& Work.GetIntProperty(BuiltProperty) == 1
				&& TryReadImprovementArchitecture(Work, Job, out architecture, out authored,
					out architectureFailure);
		}

		private static bool EnsureExactImprovementPredecessor(KingdomSystem System, Zone Z,
			GameObject Work, KingdomConstructionJob Job)
		{
			if (!IsImprovementPredecessorIdentity(System, Z, Work, Job)
				|| !KingdomConstruction.IsCurrent(Job)) return false;
			GameObject global;
			if (KingdomConstruction.FindExactId(Z, Job.SubjectId, out global)
					!= KingdomPhysicalLookupState.Exact
				|| !ReferenceEquals(global, Work)) return false;
			string receipt = Work.GetStringProperty(KingdomConstruction.ReceiptProperty);
			if (string.IsNullOrEmpty(receipt)) KingdomConstruction.Bind(Work, Job);
			return KingdomConstruction.HasReceipt(Work, Job);
		}

		private static bool RemoveCreatedProjection(GameObject Object)
		{
			Zone zone = GameObject.Validate(Object) ? Object.CurrentZone : null;
			try
			{
				return !GameObject.Validate(Object)
					|| (Object.Obliterate(null, Silent: true) && !GameObject.Validate(Object));
			}
			catch
			{
				return false;
			}
			finally
			{
				if (zone != null) KingdomSurvey.ObserveCurrentTopologyInActive(zone, Object);
			}
		}

	}
}
