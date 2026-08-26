using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public static partial class KingdomLifecycleRules
	{
		private static void SetSlot(KingdomLifecycleBook Book, KingdomLifecycleLane Lane,
			KingdomLifecycleOperation Operation)
		{
			switch (Lane)
			{
			case KingdomLifecycleLane.PlainGuest: Book.PlainGuest = Operation; break;
			case KingdomLifecycleLane.NotableGuest: Book.NotableGuest = Operation; break;
			case KingdomLifecycleLane.Raid: Book.Raid = Operation; break;
			case KingdomLifecycleLane.Petition: Book.Petition = Operation; break;
			}
		}

		private static long GetNextSequence(KingdomLifecycleBook Book,
			KingdomLifecycleLane Lane)
		{
			switch (Lane)
			{
			case KingdomLifecycleLane.PlainGuest: return Book.PlainGuestNextSequence;
			case KingdomLifecycleLane.NotableGuest: return Book.NotableGuestNextSequence;
			case KingdomLifecycleLane.Raid: return Book.RaidNextSequence;
			case KingdomLifecycleLane.Petition: return Book.PetitionNextSequence;
			default: return 0L;
			}
		}

		private static void SetNextSequence(KingdomLifecycleBook Book,
			KingdomLifecycleLane Lane, long Value)
		{
			switch (Lane)
			{
			case KingdomLifecycleLane.PlainGuest: Book.PlainGuestNextSequence = Value; break;
			case KingdomLifecycleLane.NotableGuest: Book.NotableGuestNextSequence = Value; break;
			case KingdomLifecycleLane.Raid: Book.RaidNextSequence = Value; break;
			case KingdomLifecycleLane.Petition: Book.PetitionNextSequence = Value; break;
			}
		}

		private static long GetRetiredThrough(KingdomLifecycleBook Book,
			KingdomLifecycleLane Lane)
		{
			switch (Lane)
			{
			case KingdomLifecycleLane.PlainGuest: return Book.PlainGuestRetiredThrough;
			case KingdomLifecycleLane.NotableGuest: return Book.NotableGuestRetiredThrough;
			case KingdomLifecycleLane.Raid: return Book.RaidRetiredThrough;
			case KingdomLifecycleLane.Petition: return Book.PetitionRetiredThrough;
			default: return long.MaxValue;
			}
		}

		private static void SetRetiredThrough(KingdomLifecycleBook Book,
			KingdomLifecycleLane Lane, long Value)
		{
			switch (Lane)
			{
			case KingdomLifecycleLane.PlainGuest: Book.PlainGuestRetiredThrough = Value; break;
			case KingdomLifecycleLane.NotableGuest: Book.NotableGuestRetiredThrough = Value; break;
			case KingdomLifecycleLane.Raid: Book.RaidRetiredThrough = Value; break;
			case KingdomLifecycleLane.Petition: Book.PetitionRetiredThrough = Value; break;
			}
		}

		private static bool CanonicalOperationId(KingdomLifecycleOperation Operation)
		{
			return Operation != null && Operation.Sequence > 0L
				&& ValidRootId(Operation.SettlementId)
				&& string.Equals(Operation.Id,
					OperationId(Operation.SettlementId, Operation.Lane, Operation.Sequence),
					StringComparison.Ordinal);
		}

		private static bool CounterShape(long Next, long Retired)
		{
			return Next > 0L && Retired >= 0L && Retired < long.MaxValue && Next > Retired;
		}

		private static bool KnownAction(KingdomLifecycleAction Action)
		{
			return Enum.IsDefined(typeof(KingdomLifecycleAction), Action)
				&& Action != KingdomLifecycleAction.None;
		}

		private static bool KnownPhase(KingdomLifecyclePhase Phase)
		{
			return Enum.IsDefined(typeof(KingdomLifecyclePhase), Phase)
				&& Phase != KingdomLifecyclePhase.Invalid;
		}

		private static bool KnownPhysical(KingdomLifecyclePhysicalState State)
		{
			return Enum.IsDefined(typeof(KingdomLifecyclePhysicalState), State);
		}

		private static bool KnownSink(KingdomLifecycleSinkState State)
		{
			return Enum.IsDefined(typeof(KingdomLifecycleSinkState), State);
		}

		private static bool KnownDisposition(KingdomLifecycleSinkDisposition Disposition)
		{
			return Disposition == KingdomLifecycleSinkDisposition.Deliver
				|| Disposition == KingdomLifecycleSinkDisposition.Skip;
		}

		private static bool KnownOption(KingdomLifecycleOptionState State)
		{
			return Enum.IsDefined(typeof(KingdomLifecycleOptionState), State);
		}

		private static bool KnownResourceKind(KingdomLifecycleResourceKind Kind)
		{
			return Enum.IsDefined(typeof(KingdomLifecycleResourceKind), Kind)
				&& Kind != KingdomLifecycleResourceKind.None;
		}

		private static bool KnownOuterResourceKind(KingdomLifecycleResourceKind Kind)
		{
			return KnownResourceKind(Kind) &&
				(byte)Kind <= (byte)KingdomLifecycleResourceKind.Raid;
		}

		private static bool IsDomainResourceKind(KingdomLifecycleResourceKind Kind)
		{
			return KnownOuterResourceKind(Kind)
				&& Kind != KingdomLifecycleResourceKind.Schedule
				&& Kind != KingdomLifecycleResourceKind.WaterVessel
				&& Kind != KingdomLifecycleResourceKind.Object
				&& Kind != KingdomLifecycleResourceKind.Projection;
		}

	}
}
