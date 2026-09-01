using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomArchitectureStamper
	{
		private static bool TryAcceptUpgradeHeaderPrefix(GameObject Owner, GameObject Target,
			KingdomArchitectureIntent Successor, string Lot, ArchitectureLayoutDelta Delta,
			out bool Complete, out string Failure)
		{
			Complete = false;
			Failure = null;
			if (Owner.HasStringProperty(UpgradeSchemaProperty))
				return UpgradeQuarantine(Owner,
					"authored upgrade schema has an opposite-type collision", out Failure);
			if (Owner.HasIntProperty(UpgradeSchemaProperty))
			{
				if (Owner.GetIntProperty(UpgradeSchemaProperty) != UpgradeSchema
						|| !TryReadUpgradeReceipt(Owner, Target, Successor, Lot, Delta,
							out Failure))
					return UpgradeQuarantine(Owner, Failure
						?? "authored upgrade carries another committed header", out Failure);
				Complete = true;
				return true;
			}
			if (!UpgradeStringPrefix(Owner, UpgradeTargetProperty, Target.IDIfAssigned)
				|| !UpgradeStringPrefix(Owner, UpgradeHashProperty, Successor.SnapshotHash)
				|| !UpgradeStringPrefix(Owner, UpgradeLotProperty, Lot)
				|| !UpgradeIntPrefix(Owner, UpgradePhaseProperty, 0)
				|| Owner.HasStringProperty(UpgradeFaultProperty)
				|| Owner.HasIntProperty(UpgradeFaultProperty))
				return UpgradeQuarantine(Owner,
					"authored upgrade header prefix carries a third or opposite-type value",
					out Failure);
			for (int i = 0; i < Delta.Removed.Count; i++)
				if (!UpgradeIntPrefix(Owner, UpgradeRemove(Delta.Removed[i]), 0))
					return UpgradeQuarantine(Owner,
						"authored removal receipt prefix carries a third value", out Failure);
			for (int i = 0; i < Delta.Retained.Count; i++)
				if (!UpgradeIntPrefix(Owner, UpgradeRetain(Delta.Retained[i]), 0))
					return UpgradeQuarantine(Owner,
						"authored retain receipt prefix carries a third value", out Failure);
			return true;
		}

		private static bool UpgradeStringPrefix(GameObject Owner, string Property,
			string Expected)
		{
			return KingdomArchitectureReceiptPrefixRules.ExactOrAbsentString(
				Owner.HasStringProperty(Property), Owner.GetStringProperty(Property),
				Owner.HasIntProperty(Property), Expected);
		}

		private static bool UpgradeIntPrefix(GameObject Owner, string Property, int Expected)
		{
			return KingdomArchitectureReceiptPrefixRules.ExactOrAbsentInt(
				Owner.HasIntProperty(Property), Owner.GetIntProperty(Property),
				Owner.HasStringProperty(Property), Expected);
		}

		private static ArchitectureOutputPrefix RetainedTargetPrefix(GameObject Target,
			ArchitecturePlacement Placement, string ExpectedId)
		{
			return OwnerOutputPrefix(Target, Placement, ExpectedId);
		}

		private static bool TrySetUpgradeInt(GameObject Target,
			string Property, int Value, string Context, out string Failure)
		{
			Failure = null;
			try { Target.SetIntProperty(Property, Value); }
			catch (System.Exception exception)
			{
				return Fail(Context + " remains retryable: " + exception.Message, out Failure);
			}
			return true;
		}

		private static bool TrySetUpgradeString(GameObject Target,
			string Property, string Value, string Context, out string Failure)
		{
			Failure = null;
			try { Target.SetStringProperty(Property, Value); }
			catch (System.Exception exception)
			{
				return Fail(Context + " remains retryable: " + exception.Message, out Failure);
			}
			return true;
		}

		private static string UpgradeRemove(ArchitecturePlacement Placement)
		{
			return UpgradeRemovePrefix + PropertySlot(Placement.Slot);
		}

		private static string UpgradeRetain(ArchitecturePlacement Placement)
		{
			return UpgradeRetainPrefix + PropertySlot(Placement.Slot);
		}

		internal static bool IsUpgradeQuarantined(GameObject Owner, out string Reason)
		{
			Reason = null;
			if (Owner == null) return false;
			ArchitectureUpgradeFaultEvidence evidence =
				KingdomArchitectureReceiptPrefixRules.ClassifyUpgradeFault(
					Owner.HasStringProperty(UpgradeFaultProperty),
					Owner.GetStringProperty(UpgradeFaultProperty),
					Owner.HasIntProperty(UpgradeFaultProperty));
			if (evidence == ArchitectureUpgradeFaultEvidence.None) return false;
			if (evidence == ArchitectureUpgradeFaultEvidence.Message)
				Reason = Bounded(Owner.GetStringProperty(UpgradeFaultProperty));
			else if (evidence == ArchitectureUpgradeFaultEvidence.Collision)
				Reason = "authored upgrade fault carries an int/string collision";
			else if (evidence == ArchitectureUpgradeFaultEvidence.Integer)
				Reason = "authored upgrade fault carries integer evidence";
			else Reason = "authored upgrade fault carries empty or malformed string evidence";
			return true;
		}

		internal static bool TryQuarantineUpgrade(GameObject Owner, string Message,
			out string Failure)
		{
			return UpgradeQuarantine(Owner, Message, out Failure);
		}

		private static bool UpgradeQuarantine(GameObject Owner, string Message, out string Failure)
		{
			if (Owner == null)
			{
				Failure = Bounded(Message ?? "authored upgrade refused without a reason");
				return false;
			}
			if (Owner.HasIntProperty(UpgradeFaultProperty))
			{
				Failure = "authored upgrade fault carries an opposite-type collision";
				return false;
			}
			string existing = Owner.GetStringProperty(UpgradeFaultProperty);
			if (!string.IsNullOrEmpty(existing))
			{
				Failure = Bounded(existing);
				return false;
			}
			Failure = Bounded(Message ?? "authored upgrade refused without a reason");
			try { Owner.SetStringProperty(UpgradeFaultProperty, Failure); } catch { }
			return false;
		}

		private static bool SameRect(KingdomPlotRules.PlotRect A,
			KingdomPlotRules.PlotRect B)
		{
			return A.X1 == B.X1 && A.Y1 == B.Y1 && A.X2 == B.X2 && A.Y2 == B.Y2;
		}
	}
}
