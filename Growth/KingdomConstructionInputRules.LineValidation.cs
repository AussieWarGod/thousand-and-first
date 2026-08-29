using System;

namespace ThousandAndFirst
{
	public static partial class KingdomConstructionInputRules
	{
		internal static bool ValidateSource(KingdomConstructionInputSourceLine Line,
			KingdomConstructionInputReceipt Receipt, out KingdomConstructionInputFault Fault)
		{
			if (Line == null) return Refuse(KingdomConstructionInputFault.Null, out Fault);
			if (!Defined(Line.Kind) || !Defined(Line.Phase) || !Defined(Line.Topology)
				|| Line.Topology == KingdomConstructionInputTopology.Invalid)
				return Refuse(KingdomConstructionInputFault.Phase, out Fault);
			if (!ValidText(Line.LineId, MaxIdentityChars, false)
				|| !ValidText(Line.Classification, MaxClaimChars, false)
				|| !ValidText(Line.SourceSettlementId, MaxIdentityChars, false)
				|| !ValidText(Line.SourceZoneId, MaxIdentityChars, false)
				|| !ValidText(Line.HolderId, MaxIdentityChars, false)
				|| !ValidText(Line.SourceObjectId, MaxIdentityChars, false)
				|| !ValidText(Line.Blueprint, MaxBlueprintChars, false)
				|| !ValidText(Line.RemainderMarker, MaxIdentityChars, true)
				|| !ValidText(Line.RemainderObjectId, MaxIdentityChars, true))
				return Refuse(KingdomConstructionInputFault.Identity, out Fault);
			if (Line.X < 0 || Line.X > MaxCoordinate || Line.Y < 0 || Line.Y > MaxCoordinate
				|| Line.Before <= 0 || Line.Take <= 0 || Line.Take > Line.Before
				|| Line.ResidualAfter != Line.Before - Line.Take
				|| Line.HolderStockBefore < Line.Before || Line.PriorReserved < 0
				|| Line.ReserveFloor < 0 || Line.RouteCost < 0 || Line.DedicationOrdinal < 0
				|| Line.CargoOrdinal < 0 || Line.CargoOrdinal >= Receipt.CargoCount
				|| Line.ProvedLost < 0 || Line.ProvedLost > Line.Take)
				return Refuse(KingdomConstructionInputFault.Amount, out Fault);
			if (!OptionalDigest(Line.BeforeWitnessHash) || !OptionalDigest(Line.AfterWitnessHash)
				|| (!string.IsNullOrEmpty(Line.AfterWitnessHash)
					&& string.IsNullOrEmpty(Line.BeforeWitnessHash)))
				return Refuse(KingdomConstructionInputFault.Witness, out Fault);
			if (Line.ProvedLost > 0
				&& Line.Phase != KingdomConstructionInputSourcePhase.Debited
				&& Line.Phase != KingdomConstructionInputSourcePhase.Spent
				&& Line.Phase != KingdomConstructionInputSourcePhase.CompensationIntent
				&& Line.Phase != KingdomConstructionInputSourcePhase.Compensated
				&& Line.Phase != KingdomConstructionInputSourcePhase.Quarantined)
				return Refuse(KingdomConstructionInputFault.Conservation, out Fault);
			if (Line.Kind == KingdomConstructionInputKind.Water)
			{
				if (Line.Classification != WaterClassification
					|| Line.Topology != KingdomConstructionInputTopology.LiquidVessel
					|| !string.IsNullOrEmpty(Line.RemainderMarker)
					|| !string.IsNullOrEmpty(Line.RemainderObjectId)
					|| Line.Phase == KingdomConstructionInputSourcePhase.SplitIntent
					|| Line.Phase == KingdomConstructionInputSourcePhase.SplitProved)
					return Refuse(KingdomConstructionInputFault.Claim, out Fault);
			}
			else
			{
				bool partial = Line.ResidualAfter > 0;
				if (partial != !string.IsNullOrEmpty(Line.RemainderMarker))
					return Refuse(KingdomConstructionInputFault.Identity, out Fault);
				if (!partial && !string.IsNullOrEmpty(Line.RemainderObjectId))
					return Refuse(KingdomConstructionInputFault.Identity, out Fault);
				if (partial && RequiresRemainder(Line.Phase)
					&& !ValidText(Line.RemainderObjectId, MaxIdentityChars, false))
					return Refuse(KingdomConstructionInputFault.Witness, out Fault);
				if (!partial && (Line.Phase == KingdomConstructionInputSourcePhase.SplitIntent
					|| Line.Phase == KingdomConstructionInputSourcePhase.SplitProved))
					return Refuse(KingdomConstructionInputFault.Phase, out Fault);
				if (Line.RemainderObjectId == Line.SourceObjectId)
					return Refuse(KingdomConstructionInputFault.Overlap, out Fault);
			}
			Fault = KingdomConstructionInputFault.None;
			return true;
		}

		internal static bool ValidateCargo(KingdomConstructionInputCargoLine Line,
			KingdomConstructionInputReceipt Receipt, KingdomConstructionInputSourceLine Source,
			out KingdomConstructionInputFault Fault)
		{
			if (Line == null || Source == null)
				return Refuse(KingdomConstructionInputFault.Null, out Fault);
			if (!Defined(Line.Kind) || !Defined(Line.Phase) || !Defined(Line.CustodyTopology))
				return Refuse(KingdomConstructionInputFault.Phase, out Fault);
			if (!ValidText(Line.CargoKey, MaxIdentityChars, false)
				|| !ValidText(Line.CreationMarker, MaxIdentityChars, false)
				|| !ValidText(Line.Classification, MaxClaimChars, false)
				|| !ValidText(Line.Blueprint, MaxBlueprintChars, false)
				|| !ValidText(Line.ExpectedObjectId, MaxIdentityChars, true)
				|| !ValidText(Line.ObjectId, MaxIdentityChars, true)
				|| !ValidText(Line.CustodyOwnerId, MaxIdentityChars, true)
				|| !ValidText(Line.CustodyZoneId, MaxIdentityChars, true))
				return Refuse(KingdomConstructionInputFault.Identity, out Fault);
			if (Line.Amount <= 0 || Line.Capacity < Line.Amount || Line.ChildJobId <= 0
				|| Line.ChildTripId <= 0 || Line.Spent < 0 || Line.Lost < 0
				|| (long)Line.Spent + Line.Lost > Line.Amount)
				return Refuse(KingdomConstructionInputFault.Amount, out Fault);
			if (Line.SourceLineOrdinal != Source.Ordinal || Source.CargoOrdinal != Line.Ordinal
				|| Line.Kind != Source.Kind || Line.Amount != Source.Take
				|| Line.Classification != Source.Classification
				|| (Line.Kind != KingdomConstructionInputKind.Water
					&& Line.Blueprint != Source.Blueprint))
				return Refuse(KingdomConstructionInputFault.CrossBinding, out Fault);
			if (Line.Kind == KingdomConstructionInputKind.Water)
			{
				if (!string.IsNullOrEmpty(Line.ExpectedObjectId)
					|| Line.Blueprint != WaterCargoBlueprint || Line.Capacity != WaterCargoCapacity)
					return Refuse(KingdomConstructionInputFault.Identity, out Fault);
			}
			else if (Line.ExpectedObjectId != Source.SourceObjectId)
				return Refuse(KingdomConstructionInputFault.Identity, out Fault);
			if (!OptionalDigest(Line.BeforeWitnessHash) || !OptionalDigest(Line.AfterWitnessHash)
				|| (!string.IsNullOrEmpty(Line.AfterWitnessHash)
					&& string.IsNullOrEmpty(Line.BeforeWitnessHash)))
				return Refuse(KingdomConstructionInputFault.Witness, out Fault);
			if (Line.Spent > 0 && Line.Phase != KingdomConstructionInputCargoPhase.DebitIntent
				&& Line.Phase != KingdomConstructionInputCargoPhase.Spent
				&& Line.Phase != KingdomConstructionInputCargoPhase.CompensationIntent
				&& Line.Phase != KingdomConstructionInputCargoPhase.Compensated
				&& Line.Phase != KingdomConstructionInputCargoPhase.Quarantined)
				return Refuse(KingdomConstructionInputFault.Conservation, out Fault);
			if (!CustodyShape(Line, Receipt, Source))
				return Refuse(KingdomConstructionInputFault.CrossBinding, out Fault);
			if (Line.Phase == KingdomConstructionInputCargoPhase.Spent
				&& (Line.Spent + Line.Lost != Line.Amount
					|| Line.CustodyTopology != KingdomConstructionInputTopology.Consumed))
				return Refuse(KingdomConstructionInputFault.Conservation, out Fault);
			Fault = KingdomConstructionInputFault.None;
			return true;
		}

		private static bool CustodyShape(KingdomConstructionInputCargoLine Line,
			KingdomConstructionInputReceipt Receipt, KingdomConstructionInputSourceLine Source)
		{
			bool empty = Line.CustodyTopology == KingdomConstructionInputTopology.Invalid;
			if (empty && (!string.IsNullOrEmpty(Line.CustodyOwnerId)
				|| !string.IsNullOrEmpty(Line.CustodyZoneId)
				|| Line.CustodyX != -1 || Line.CustodyY != -1)) return false;
			if (!empty && (!ValidText(Line.CustodyOwnerId, MaxIdentityChars, false)
				|| !ValidText(Line.CustodyZoneId, MaxIdentityChars, false)
				|| Line.CustodyX < 0 || Line.CustodyX > MaxCoordinate
				|| Line.CustodyY < 0 || Line.CustodyY > MaxCoordinate)) return false;
			if (Line.Phase == KingdomConstructionInputCargoPhase.Planned)
				return empty && Line.Spent == 0 && Line.Lost == 0
					&& (string.IsNullOrEmpty(Line.ObjectId)
						|| (Line.Kind != KingdomConstructionInputKind.Water
							&& Line.ObjectId == Line.ExpectedObjectId));
			if (Line.Phase == KingdomConstructionInputCargoPhase.CreateIntent)
				return Line.Kind == KingdomConstructionInputKind.Water
					&& (empty || AtSource(Line, Source));
			if (Line.Phase == KingdomConstructionInputCargoPhase.AtSource)
				return AtSource(Line, Source) && ValidText(Line.ObjectId, MaxIdentityChars, false);
			if (Line.Phase == KingdomConstructionInputCargoPhase.PickupIntent)
				return ValidText(Line.ObjectId, MaxIdentityChars, false)
					&& (AtSource(Line, Source)
						|| Line.CustodyTopology == KingdomConstructionInputTopology.CarrierInventory);
			if (Line.Phase == KingdomConstructionInputCargoPhase.InFlight)
				return ValidText(Line.ObjectId, MaxIdentityChars, false)
					&& Line.CustodyTopology == KingdomConstructionInputTopology.CarrierInventory;
			if (Line.Phase == KingdomConstructionInputCargoPhase.Landed)
				return AtTarget(Line, Receipt);
			if (Line.Phase == KingdomConstructionInputCargoPhase.DebitIntent)
				return AtTarget(Line, Receipt)
					|| Line.CustodyTopology == KingdomConstructionInputTopology.Consumed;
			if (Line.Phase == KingdomConstructionInputCargoPhase.Spent)
				return Line.CustodyTopology == KingdomConstructionInputTopology.Consumed;
			if (Line.Phase == KingdomConstructionInputCargoPhase.ReleaseIntent)
				return empty || AtSource(Line, Source)
					|| Line.CustodyTopology == KingdomConstructionInputTopology.Released;
			if (Line.Phase == KingdomConstructionInputCargoPhase.Released)
				return Line.CustodyTopology == KingdomConstructionInputTopology.Released;
			if (Line.Phase == KingdomConstructionInputCargoPhase.CompensationIntent)
				return !empty || Line.Lost == Line.Amount;
			if (Line.Phase == KingdomConstructionInputCargoPhase.Compensated)
				return Line.CustodyTopology == KingdomConstructionInputTopology.Returned
					|| Line.CustodyTopology == KingdomConstructionInputTopology.Consumed
					|| Line.Lost == Line.Amount;
			return Line.Phase == KingdomConstructionInputCargoPhase.Quarantined;
		}

		private static bool AtSource(KingdomConstructionInputCargoLine Line,
			KingdomConstructionInputSourceLine Source)
		{
			if (Line.Kind == KingdomConstructionInputKind.Water)
				return Line.CustodyTopology == KingdomConstructionInputTopology.CarrierInventory
					&& ValidText(Line.CustodyOwnerId, MaxIdentityChars, false)
					&& Line.CustodyZoneId == Source.SourceZoneId
					&& Line.CustodyX == Source.X && Line.CustodyY == Source.Y;
			return (Line.CustodyTopology == KingdomConstructionInputTopology.ContainerInventory
				|| Line.CustodyTopology == KingdomConstructionInputTopology.LooseCell
				|| Line.CustodyTopology == KingdomConstructionInputTopology.LiquidVessel)
				&& Line.CustodyOwnerId == Source.HolderId
				&& Line.CustodyZoneId == Source.SourceZoneId
				&& Line.CustodyX == Source.X && Line.CustodyY == Source.Y;
		}

		private static bool AtTarget(KingdomConstructionInputCargoLine Line,
			KingdomConstructionInputReceipt Receipt)
		{
			return Line.CustodyTopology == KingdomConstructionInputTopology.LandingEscrow
				&& ValidText(Line.ObjectId, MaxIdentityChars, false)
				&& Line.CustodyZoneId == Receipt.TargetZoneId
				&& Line.CustodyX == Receipt.TargetX && Line.CustodyY == Receipt.TargetY;
		}

		private static bool RequiresRemainder(KingdomConstructionInputSourcePhase Phase)
		{
			return Phase == KingdomConstructionInputSourcePhase.SplitProved
				|| Phase == KingdomConstructionInputSourcePhase.TransferIntent
				|| Phase == KingdomConstructionInputSourcePhase.Debited
				|| Phase == KingdomConstructionInputSourcePhase.Spent
				|| Phase == KingdomConstructionInputSourcePhase.CompensationIntent
				|| Phase == KingdomConstructionInputSourcePhase.Compensated;
		}

		private static bool OptionalDigest(string Value)
		{
			return string.IsNullOrEmpty(Value) || ValidDigest(Value);
		}
	}
}
