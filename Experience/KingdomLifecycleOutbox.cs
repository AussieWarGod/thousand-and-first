using System;

namespace ThousandAndFirst
{

	[Serializable]
	public sealed class KingdomLifecycleOutbox
	{
		public string OperationId;
		public string EventId;
		public string ChronicleReceiptId;
		public string Chronicle;
		public bool ChronicleAccomplishment;
		public KingdomLifecycleSinkDisposition ChronicleDisposition;
		public KingdomLifecycleSinkState ChronicleState;
		public string Ledger;
		public KingdomLifecycleSinkDisposition LedgerDisposition;
		public KingdomLifecycleSinkState LedgerState;
		public string Message;
		public KingdomLifecycleSinkDisposition MessageDisposition;
		public KingdomLifecycleSinkState MessageState;
		public string Deed;
		public KingdomLifecycleSinkDisposition DeedDisposition;
		public KingdomLifecycleSinkState DeedState;
		public string GuestbookLine;
		public KingdomLifecycleSinkDisposition GuestbookDisposition;
		public KingdomLifecycleSinkState GuestbookState;
	}
}
