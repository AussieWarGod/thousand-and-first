namespace ThousandAndFirst
{
	public static partial class KingdomPurposePortfolioRules
	{
		public static bool TryCreatePair(string PairId, string RealmId, long Epoch,
			KingdomPurposeKind FirstKind, KingdomPurposeKind SecondKind,
			string FirstSettlementId, string SecondSettlementId, string FirstWorkId,
			string SecondWorkId, string FirstZoneId, string SecondZoneId, string FirstInputStoreId,
			string FirstOutputStoreId, string SecondInputStoreId, string SecondOutputStoreId,
			string FirstGateKey, string SecondGateKey, string RouteDigest,
			out KingdomPurposePairReceipt Pair, out KingdomPurposePairFault Fault)
		{
			Pair = new KingdomPurposePairReceipt
			{
				PairId = PairId, RealmId = RealmId, Epoch = Epoch, FirstKind = FirstKind,
				SecondKind = SecondKind, FirstSettlementId = FirstSettlementId,
				SecondSettlementId = SecondSettlementId, FirstWorkId = FirstWorkId,
				SecondWorkId = SecondWorkId,
				FirstZoneId = FirstZoneId, SecondZoneId = SecondZoneId,
				FirstInputStoreId = FirstInputStoreId, FirstOutputStoreId = FirstOutputStoreId,
				SecondInputStoreId = SecondInputStoreId, SecondOutputStoreId = SecondOutputStoreId,
				FirstGateKey = FirstGateKey, SecondGateKey = SecondGateKey,
				RouteDigest = RouteDigest, Phase = KingdomPurposePairPhase.Frozen,
				NextOperationOrdinal = 1, Revision = 0
			};
			if (ValidPair(Pair, out Fault)) return true;
			Pair = null;
			return false;
		}

		public static bool TryCreateOperation(KingdomPurposePairReceipt Pair,
			string OperationId, int Ordinal, KingdomPurposeKind Source,
			bool Bootstrap, bool Returned, string InputCargoId, string InputCargoReceipt,
			string ProcedureKey, string ProcedureReceipt, string NewSecondWorkId,
			out KingdomPurposeOperationReceipt Operation, out KingdomPurposePairFault Fault)
		{
			return TryCreateOperationCore(Pair, OperationId, Ordinal, Source, Bootstrap,
				Returned, InputCargoId, InputCargoReceipt, ProcedureKey, ProcedureReceipt,
				NewSecondWorkId, null, null, null, out Operation, out Fault);
		}

		/// <summary>Creates the sole return operation that may adopt a newly completed second
		/// root's authored stores. The authenticated endpoint becomes durable only through the
		/// matching pair transition; this factory mutates no receipt.</summary>
		public static bool TryCreateOperationWithSecondEndpoint(KingdomPurposePairReceipt Pair,
			string OperationId, int Ordinal, KingdomPurposeKind Source,
			string ProcedureKey, string ProcedureReceipt, string NewSecondWorkId,
			string NewSecondInputStoreId, string NewSecondOutputStoreId, string NewRouteDigest,
			out KingdomPurposeOperationReceipt Operation, out KingdomPurposePairFault Fault)
		{
			return TryCreateOperationCore(Pair, OperationId, Ordinal, Source, false, true,
				null, null, ProcedureKey, ProcedureReceipt, NewSecondWorkId,
				NewSecondInputStoreId, NewSecondOutputStoreId, NewRouteDigest,
				out Operation, out Fault);
		}

		private static bool TryCreateOperationCore(KingdomPurposePairReceipt Pair,
			string OperationId, int Ordinal, KingdomPurposeKind Source,
			bool Bootstrap, bool Returned, string InputCargoId, string InputCargoReceipt,
			string ProcedureKey, string ProcedureReceipt, string NewSecondWorkId,
			string NewSecondInputStoreId, string NewSecondOutputStoreId, string NewRouteDigest,
			out KingdomPurposeOperationReceipt Operation, out KingdomPurposePairFault Fault)
		{
			Operation = null;
			Fault = KingdomPurposePairFault.Malformed;
			if (!ValidPair(Pair, out _) || Ordinal != Pair.NextOperationOrdinal
				|| !CanStartOperationAtRevision(Pair.Revision, Pair.Phase)
				|| !OperationRequestMatchesPair(Pair, Source, Bootstrap, Returned,
					InputCargoId, InputCargoReceipt, NewSecondWorkId)
				|| !TryRecipe(Source, Other(Pair, Source), out var recipe))
				return false;
			bool adoptsEndpoint = Returned && string.IsNullOrEmpty(Pair.SecondWorkId)
				&& !string.IsNullOrEmpty(NewSecondInputStoreId)
				&& !string.IsNullOrEmpty(NewSecondOutputStoreId)
				&& !string.IsNullOrEmpty(NewRouteDigest);
			if (adoptsEndpoint)
			{
				KingdomPurposePairReceipt endpoint = Pair.Copy();
				endpoint.SecondWorkId = NewSecondWorkId;
				endpoint.SecondInputStoreId = NewSecondInputStoreId;
				endpoint.SecondOutputStoreId = NewSecondOutputStoreId;
				endpoint.RouteDigest = NewRouteDigest;
				if (!RouteDigestMatches(endpoint)) return false;
			}
			else if (!string.IsNullOrEmpty(NewSecondInputStoreId)
				|| !string.IsNullOrEmpty(NewSecondOutputStoreId)
				|| !string.IsNullOrEmpty(NewRouteDigest)) return false;
			bool first = Source == Pair.FirstKind;
			string secondWork = string.IsNullOrEmpty(Pair.SecondWorkId)
				? NewSecondWorkId : Pair.SecondWorkId;
			string secondInput = adoptsEndpoint
				? NewSecondInputStoreId : Pair.SecondInputStoreId;
			string secondOutput = adoptsEndpoint
				? NewSecondOutputStoreId : Pair.SecondOutputStoreId;
			string routeDigest = adoptsEndpoint ? NewRouteDigest : Pair.RouteDigest;
			Operation = new KingdomPurposeOperationReceipt
			{
				PairId = Pair.PairId, PairEpoch = Pair.Epoch,
				OperationId = OperationId, Ordinal = Ordinal, SourceKind = Source,
				DestinationKind = recipe.Destination, Phase = KingdomPurposeOperationPhase.Prepared,
				SourceSettlementId = first ? Pair.FirstSettlementId : Pair.SecondSettlementId,
				DestinationSettlementId = first ? Pair.SecondSettlementId : Pair.FirstSettlementId,
				SourceWorkId = first ? Pair.FirstWorkId : secondWork,
				DestinationWorkId = first ? secondWork : Pair.FirstWorkId,
				BootstrapExemption = Bootstrap, ReturnExemption = Returned,
				InputCargoId = InputCargoId, InputCargoReceipt = InputCargoReceipt,
				SourceZoneId = first ? Pair.FirstZoneId : Pair.SecondZoneId,
				DestinationZoneId = first ? Pair.SecondZoneId : Pair.FirstZoneId,
				SourceInputStoreId = first ? Pair.FirstInputStoreId : secondInput,
				SourceOutputStoreId = first ? Pair.FirstOutputStoreId : secondOutput,
				DestinationInputStoreId = first ? secondInput : Pair.FirstInputStoreId,
				SourceGateKey = first ? Pair.FirstGateKey : Pair.SecondGateKey,
				DestinationGateKey = first ? Pair.SecondGateKey : Pair.FirstGateKey,
				RouteDigest = routeDigest, WaterRequested = recipe.WaterDrams,
				FoodRequested = recipe.FoodServings, MaterialRequested = recipe.MaterialClaim,
				MaterialSpent = EmptyClaim(), MaterialLost = EmptyClaim(),
				ProcedureKey = ProcedureKey, ProcedureReceipt = ProcedureReceipt,
				EffectStep = PurposeEffectNone
			};
			if (ValidOperation(Operation, out Fault)) return true;
			Operation = null;
			return false;
		}

		public static bool TryCreateCargo(KingdomPurposePairReceipt Pair,
			KingdomPurposeOperationReceipt Operation, string ObjectId, string TransportJobId,
			out KingdomPurposeCargoReceipt Cargo, out KingdomPurposePairFault Fault)
		{
			Cargo = null;
			Fault = KingdomPurposePairFault.Malformed;
			if (!ValidPair(Pair, out _) || !ValidOperation(Operation, out _)
				|| Operation.PairId != Pair.PairId || Operation.PairEpoch != Pair.Epoch
				|| !OperationEndpointMatches(Pair, Operation)
				|| !TryRecipe(Operation.SourceKind, Operation.DestinationKind, out var recipe))
				return false;
			bool sourceFirst = Operation.SourceKind == Pair.FirstKind;
			Cargo = new KingdomPurposeCargoReceipt
			{
				PairId = Pair.PairId, PairEpoch = Pair.Epoch, OperationId = Operation.OperationId,
				SourceKind = Operation.SourceKind, DestinationKind = Operation.DestinationKind,
				BootstrapExemption = Operation.BootstrapExemption,
				ReturnExemption = Operation.ReturnExemption,
				SourceSettlementId = sourceFirst ? Pair.FirstSettlementId : Pair.SecondSettlementId,
				DestinationSettlementId = sourceFirst ? Pair.SecondSettlementId : Pair.FirstSettlementId,
				SourceWorkId = sourceFirst ? Pair.FirstWorkId : Pair.SecondWorkId,
				DestinationWorkId = sourceFirst ? Pair.SecondWorkId : Pair.FirstWorkId,
				CargoKey = recipe.CargoKey, EmbodiedMaterial = recipe.EmbodiedMaterial,
				EmbodiedUnits = recipe.EmbodiedUnits, CarriedFood = recipe.CarriedFood,
				ObjectId = ObjectId, TransportJobId = TransportJobId, RouteDigest = Pair.RouteDigest
			};
			if (ValidCargo(Cargo, out Fault)) return true;
			Cargo = null;
			return false;
		}

		private static KingdomPurposeKind Other(KingdomPurposePairReceipt Pair,
			KingdomPurposeKind Kind)
		{
			if (Pair == null) return KingdomPurposeKind.None;
			if (Pair.FirstKind == Kind) return Pair.SecondKind;
			return Pair.SecondKind == Kind ? Pair.FirstKind : KingdomPurposeKind.None;
		}

		private static bool OperationRequestMatchesPair(KingdomPurposePairReceipt Pair,
			KingdomPurposeKind Source, bool Bootstrap, bool Returned, string InputCargoId,
			string InputCargoReceipt, string NewSecondWorkId)
		{
			if (Bootstrap && !Returned)
				return Pair.Phase == KingdomPurposePairPhase.Frozen && !Pair.BootstrapUsed
					&& Source == Pair.FirstKind && string.IsNullOrEmpty(NewSecondWorkId)
					&& string.IsNullOrEmpty(InputCargoId)
					&& string.IsNullOrEmpty(InputCargoReceipt);
			if (Returned && !Bootstrap)
				return Pair.Phase == KingdomPurposePairPhase.SecondPending
					&& Pair.BootstrapUsed && !Pair.ReturnUsed
					&& Source == Pair.SecondKind
					&& (string.IsNullOrEmpty(Pair.SecondWorkId)
						? Id(NewSecondWorkId) : string.IsNullOrEmpty(NewSecondWorkId))
					&& string.IsNullOrEmpty(InputCargoId)
					&& string.IsNullOrEmpty(InputCargoReceipt);
			return (!Bootstrap && !Returned && Pair.Phase == KingdomPurposePairPhase.Active
					&& Source == Pair.NextKind && string.IsNullOrEmpty(NewSecondWorkId)
					&& InputCargoId == Pair.CreditCargoId
					&& InputCargoReceipt == Pair.CreditCargoReceipt)
				|| (!Bootstrap && !Returned
					&& Pair.Phase == KingdomPurposePairPhase.CargoAwaitingActivation
					&& Pair.Operation?.Phase == KingdomPurposeOperationPhase.Delivered
					&& Source == Pair.FirstKind && string.IsNullOrEmpty(NewSecondWorkId)
					&& InputCargoId == Pair.Operation.OutputCargoId
					&& InputCargoReceipt == Pair.Operation.OutputCargoReceipt);
		}
	}
}
