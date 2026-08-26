using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>An OS-held, process-scoped proof that one exact target is still creating a world
	/// for a reserved legacy. The empty lock file is not proof; only this open exclusive handle is.</summary>
	internal sealed class KingdomSealReservationLease : IDisposable
	{
		private readonly object _sync = new object();

		private FileStream _gate;

		internal readonly string LineageId;

		internal readonly string LegacyId;

		internal readonly string TargetGameId;

		internal KingdomSealReservationLease(KingdomSealReceipt Receipt, FileStream Gate)
		{
			LineageId = Receipt.LineageId;
			LegacyId = Receipt.LegacyId;
			TargetGameId = Receipt.TargetGameId;
			_gate = Gate;
		}

		internal bool IsHeld
		{
			get
			{
				lock (_sync)
				{
					return _gate != null;
				}
			}
		}

		internal bool Matches(KingdomSealReceipt Receipt)
		{
			return Receipt != null && Receipt.LineageId == LineageId
				&& Receipt.LegacyId == LegacyId && Receipt.TargetGameId == TargetGameId;
		}

		public void Dispose()
		{
			FileStream gate;
			lock (_sync)
			{
				gate = _gate;
				_gate = null;
			}
			gate?.Dispose();
		}
	}
}
