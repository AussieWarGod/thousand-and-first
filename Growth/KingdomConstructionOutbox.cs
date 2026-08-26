using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>Frozen construction telling, independent of later option changes.</summary>
	public sealed class KingdomConstructionOutbox
	{
		public string EventId;
		public int Mode;
		public string Chronicle;
		public KingdomConstructionSinkDisposition ChronicleState;
		public string Ledger;
		public KingdomConstructionSinkDisposition LedgerState;
		public int LedgerBeforeCount = -1;
		public string LedgerBeforeHash;
		public int LedgerAfterCount = -1;
		public string LedgerAfterHash;
		public string Message;
		public KingdomConstructionSinkDisposition MessageState;
		public string Deed;
		public KingdomConstructionSinkDisposition DeedState;

		public KingdomConstructionOutbox Copy()
		{
			return new KingdomConstructionOutbox
			{
				EventId = EventId,
				Mode = Mode,
				Chronicle = Chronicle,
				ChronicleState = ChronicleState,
				Ledger = Ledger,
				LedgerState = LedgerState,
				LedgerBeforeCount = LedgerBeforeCount,
				LedgerBeforeHash = LedgerBeforeHash,
				LedgerAfterCount = LedgerAfterCount,
				LedgerAfterHash = LedgerAfterHash,
				Message = Message,
				MessageState = MessageState,
				Deed = Deed,
				DeedState = DeedState
			};
		}
	}

}
