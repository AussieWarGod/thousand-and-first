using System;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomAdopt
	{
		internal const int PendingSchema = 1;
		internal const string PendingSchemaProperty = "r_TAF_AdoptionPendingSchema";
		internal const string PendingPhaseProperty = "r_TAF_AdoptionPendingPhase";
		internal const string PendingKeyProperty = "r_TAF_AdoptionPendingKey";
		internal const string PendingCreatedProperty = "r_TAF_AdoptionPendingCreated";
		internal const string PendingMarkProperty = "r_TAF_AdoptionPendingMark";

		private static bool BeginPending(GameObject Target, string Key, bool Created,
			out string Failure)
		{
			Failure = null;
			if (!GameObject.Validate(Target) || !KingdomDesignationRules.SafeToken(Key, 128))
				return FailPending("adoption transaction target is invalid", out Failure);
			if (Target.GetIntProperty(PendingSchemaProperty) == PendingSchema
				|| Target.GetIntProperty(AdoptedProperty) == 1) RecoverPending(Target);
			if (!GameObject.Validate(Target))
				return FailPending("adoption marker recovery removed an orphan", out Failure);
			if (Target.GetIntProperty(AdoptedProperty) == 1)
				return FailPending("this object already has a complete adoption", out Failure);
			try
			{
				Target.RequirePart<XRL.World.Parts.r_KingdomAdoptionRecovery>();
				ClearPendingProperties(Target);
				Target.SetStringProperty(PendingKeyProperty, Key);
				Target.SetIntProperty(PendingCreatedProperty, Created ? 1 : 0);
				Target.SetIntProperty(PendingPhaseProperty, 1);
				Target.SetIntProperty(PendingSchemaProperty, PendingSchema);
			}
			catch (Exception exception)
			{
				RollbackPending(Target);
				return FailPending("adoption transaction could not begin: "
					+ exception.GetType().Name, out Failure);
			}
			return Pending(Target, Key, 1);
		}

		private static bool AdvancePending(GameObject Target, string Key, int Phase,
			out string Failure)
		{
			Failure = null;
			if (!Pending(Target, Key, Phase - 1))
				return FailPending("adoption transaction lost its previous phase", out Failure);
			try { Target.SetIntProperty(PendingPhaseProperty, Phase); }
			catch (Exception exception)
			{
				return FailPending("adoption transaction phase threw "
					+ exception.GetType().Name, out Failure);
			}
			return Pending(Target, Key, Phase);
		}

		private static bool FinalizePending(GameObject Target, string Key, string Mark,
			out string Failure)
		{
			Failure = null;
			if (!Pending(Target, Key, 4))
				return FailPending("adoption transaction is not ready to publish", out Failure);
			try
			{
				Target.SetIntProperty(BuiltProperty, 1);
				Target.SetStringProperty(AdoptedKeyProperty, Key);
				Target.SetStringProperty(AdoptedMarkProperty, Mark ?? "");
				Target.SetStringProperty(PendingMarkProperty, Mark ?? "");
				Target.SetIntProperty(PendingPhaseProperty, 5);
				// Positive civic authority last. A reader never sees a partly published adoption.
				Target.SetIntProperty(AdoptedProperty, 1);
			}
			catch (Exception exception)
			{
				if (!CompleteEvidence(Target, Key))
					return FailPending("adoption publication threw "
						+ exception.GetType().Name, out Failure);
			}
			if (!CompleteEvidence(Target, Key))
				return FailPending("adoption publication did not read back exactly", out Failure);
			try { KingdomGovernanceScope.Commit("adopt building"); }
			catch (Exception exception)
			{
				if (!CompleteEvidence(Target, Key))
					return FailPending("adoption governance commit threw "
						+ exception.GetType().Name, out Failure);
			}
			ClearPending(Target); return true;
		}

		private static bool PrepareStorageMark(GameObject Target, string Key, string Mark,
			out string Failure)
		{
			Failure = null;
			if (!Pending(Target, Key, 4)
				|| (Mark != StoresProperty && Mark != LarderProperty && Mark != ""))
				return FailPending("adoption storage mark is invalid", out Failure);
			try
			{
				Target.SetStringProperty(PendingMarkProperty, Mark);
				if (!string.IsNullOrEmpty(Mark)) Target.SetIntProperty(Mark, 1);
			}
			catch (Exception exception)
			{
				return FailPending("adoption storage mark threw "
					+ exception.GetType().Name, out Failure);
			}
			return true;
		}

		internal static void RecoverPending(GameObject Target)
		{
			if (!GameObject.Validate(Target)) return;
			bool pending = Target.GetIntProperty(PendingSchemaProperty) == PendingSchema;
			if (!pending)
			{
				if (Target.Blueprint == WorkMarkerBlueprint
					&& Target.GetIntProperty(AdoptedProperty) != 1)
					Target.Obliterate(null, Silent: true);
				return;
			}
			string key = Target.GetStringProperty(PendingKeyProperty);
			if (Target.GetIntProperty(AdoptedProperty) == 1 && CompleteEvidence(Target, key))
			{
				ClearPending(Target); return;
			}
			RollbackPending(Target);
		}

		private static void RollbackPending(GameObject Target)
		{
			if (!GameObject.Validate(Target)) return;
			int phase = Target.GetIntProperty(PendingPhaseProperty);
			string mark = Target.GetStringProperty(PendingMarkProperty);
			KingdomAdoptionDesignation.Clear(Target);
			KingdomPlots.ReleaseAdoptedPlot(Target);
			if (phase >= 4) ClearTyped(Target, BuiltProperty);
			ClearTyped(Target, AdoptedProperty);
			ClearTyped(Target, AdoptedKeyProperty);
			ClearTyped(Target, AdoptedMarkProperty);
			if (mark == StoresProperty || mark == LarderProperty) ClearTyped(Target, mark);
			bool created = Target.GetIntProperty(PendingCreatedProperty) == 1;
			ClearPending(Target);
			if (created && Target.Blueprint == WorkMarkerBlueprint)
				Target.Obliterate(null, Silent: true);
		}

		private static bool CompleteEvidence(GameObject Target, string Key)
		{
			if (!GameObject.Validate(Target) || Target.GetIntProperty(AdoptedProperty) != 1
				|| Target.GetIntProperty(BuiltProperty) != 1
				|| Target.GetStringProperty(AdoptedKeyProperty) != Key
				|| Target.GetIntProperty(KingdomPlots.AdoptedPlotProperty) != 1
				|| !KingdomAdoptionDesignation.TryRead(Target, out _, out _)) return false;
			if (!KingdomData.TryGetBuilding(Key, out KingdomRules.BuildEntry entry)) return false;
			bool required = KingdomAdoptionOperationRules.RequiresContract(
				entry.Category, entry.Staff);
			return required
				? KingdomAdoptionOperation.TryRead(Target, out _, out _)
				: !KingdomAdoptionOperation.HasState(Target);
		}

		private static bool Pending(GameObject Target, string Key, int Phase)
		{
			return GameObject.Validate(Target)
				&& Target.GetIntProperty(PendingSchemaProperty) == PendingSchema
				&& Target.GetStringProperty(PendingKeyProperty) == Key
				&& Target.GetIntProperty(PendingPhaseProperty) == Phase;
		}

		private static void ClearPending(GameObject Target)
		{
			ClearPendingProperties(Target);
			if (GameObject.Validate(Target) && Target.Blueprint != WorkMarkerBlueprint)
				Target.RemovePart<XRL.World.Parts.r_KingdomAdoptionRecovery>();
		}

		private static void ClearPendingProperties(GameObject Target)
		{
			if (Target == null) return;
			ClearTyped(Target, PendingSchemaProperty);
			ClearTyped(Target, PendingPhaseProperty);
			ClearTyped(Target, PendingCreatedProperty);
			ClearTyped(Target, PendingKeyProperty);
			ClearTyped(Target, PendingMarkProperty);
		}

		private static void ClearTyped(GameObject Target, string Property)
		{
			Target.RemoveIntProperty(Property); Target.RemoveStringProperty(Property);
		}

		private static bool FailPending(string Message, out string Failure)
		{
			Failure = Message; return false;
		}
	}
}

namespace XRL.World.Parts
{
	/// <summary>Load/thaw recovery hook for one adoption transaction. It never changes player
	/// fabric; only TAF-owned receipt/projection fields and a mod-created marker are rolled back.</summary>
	[Serializable]
	public sealed class r_KingdomAdoptionRecovery : IPart
	{
		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade) || ID == ZoneActivatedEvent.ID
				|| ID == ZoneThawedEvent.ID;
		}

		public override bool HandleEvent(ZoneActivatedEvent E)
		{
			ThousandAndFirst.KingdomAdopt.RecoverPending(ParentObject);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(ZoneThawedEvent E)
		{
			ThousandAndFirst.KingdomAdopt.RecoverPending(ParentObject);
			return base.HandleEvent(E);
		}
	}
}
