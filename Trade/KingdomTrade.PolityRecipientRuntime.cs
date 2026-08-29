using System;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomTrade
	{
		/// <summary>Rebinds only the active loaded body; never requests or loads another zone.</summary>
		private static bool TryBindPolityConsignmentRecipient(KingdomSystem System,
			KingdomTradeOperation Operation, Zone Ground, out string Failure)
		{
			Failure = null;
			if (Operation?.Kind != KingdomTradeOperationKind.PolityConsignmentDelivery)
				return true;
			KingdomTradePolityRecipientWitness expected = Operation.PolityRecipient;
			if (!KingdomTradeRules.TryValidatePolityRecipientWitnessShape(expected,
				out Failure) || System?.PolityLedger == null || Ground == null)
			{
				Failure = Failure ?? "Polity recipient authority is unavailable"; return false;
			}
			KingdomPolityConsignmentRequest request;
			KingdomPolityCorrespondenceReplyKind reply;
			if (!KingdomPolityCorrespondenceRules.TryDescribeConsignment(System.PolityLedger,
				Operation.CharterId, out request, out reply, out Failure) ||
				reply != KingdomPolityCorrespondenceReplyKind.None)
			{
				Failure = Failure ?? "Polity consignment request is no longer open"; return false;
			}
			GameObject body;
			LoadedTopologyWitness topology;
			LoadedObjectResolution resolution = ResolveLoadedObject(expected.BodyId, Ground,
				out body, out topology);
			int matches = resolution == LoadedObjectResolution.ExactUnique ? 1 :
				(resolution == LoadedObjectResolution.Ambiguous ? 2 : 0);
			if (matches != 1)
				return KingdomTradeRules.TryValidatePolityConsignmentCheckpoint(Operation,
					request, null, matches, System.SeatName, out Failure);
			if (!ExactLoadedTopology(topology) || !KingdomPolityVisitInteraction.
				TryCaptureConsignmentRecipientWitness(System, body, request,
					out KingdomTradePolityRecipientWitness live, out Failure))
			{
				Failure = Failure ?? "Loaded polity recipient topology changed"; return false;
			}
			return KingdomTradeRules.TryValidatePolityConsignmentCheckpoint(Operation,
				request, live, 1, System.SeatName, out Failure);
		}

		private static bool RequirePolityConsignmentRecipient(KingdomSystem System,
			KingdomTradeOperation Operation, Zone Ground, string Checkpoint)
		{
			if (Operation?.Kind != KingdomTradeOperationKind.PolityConsignmentDelivery)
				return true;
			if (TryBindPolityConsignmentRecipient(System, Operation, Ground,
				out string failure)) return true;
			KingdomTradeRules.SealUnstartedPolityConsignmentLegs(Operation);
			Quarantine(Operation, "Exact loaded polity recipient was lost before " +
				(Checkpoint ?? "continuation") + ": " + (failure ?? "unknown witness fault"));
			return false;
		}

		private static bool ContinueOrQuarantinePolityRecipient(KingdomSystem System,
			KingdomTradeBook Book, KingdomTradeOperation Operation, TradeLiveFrame Frame,
			long Tick, string Checkpoint)
		{
			if (RequirePolityConsignmentRecipient(System, Operation, Frame?.Zone,
				Checkpoint)) return true;
			FinalizeQuarantine(System, Book, Operation, Tick, Frame);
			return false;
		}
	}
}
