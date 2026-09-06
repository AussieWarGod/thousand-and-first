using System;
using XRL;
using XRL.Messages;
using XRL.World;

namespace XRL.World.Parts
{
	public partial class r_KingdomWear
	{
		[NonSerialized] private bool WearReadFailed;
		[NonSerialized] private bool WearReadNoticeAttempted;

		internal bool LoadFailed => WearReadFailed;

		public override void Read(GameObject Basis, SerializationReader Reader)
		{
			try
			{
				if (WearReadFailed)
					throw new InvalidOperationException("A failed wear load cannot be retried on the same part.");
				object first = Reader.ReadObject();
				if (first is int && (int)first == SerializationMagic)
				{
					object version = Reader.ReadObject();
					if (!(version is int) || (int)version != CurrentSerializationVersion)
						throw new InvalidOperationException("Unsupported ThousandAndFirst wear save version.");
					Reader.ReadNamedFields(this, typeof(r_KingdomWear));
				}
				else
				{
					Wear = Convert.ToInt32(first);
					LastCause = Convert.ToInt32(Reader.ReadObject());
					Held = Convert.ToBoolean(Reader.ReadObject());
					RepairEffortLeft = Convert.ToInt32(Reader.ReadObject());
					LastLeakTick = Convert.ToInt64(Reader.ReadObject());
					LeakAnnounced = Convert.ToBoolean(Reader.ReadObject());
					AnnouncedBlock = Convert.ToInt32(Reader.ReadObject());
				}
				NormalizeSerializedFields();
			}
			catch (Exception)
			{
				LatchWearReadFailure();
				throw;
			}
		}

		// IPart.Load can fail in StatShifter.Load before it reaches this part's custom Read.
		public override bool ReadError(Exception Exception, SerializationReader Reader, long Start, int Length)
		{
			LatchWearReadFailure();
			return false;
		}

		private void LatchWearReadFailure()
		{
			bool first = !WearReadFailed;
			WearReadFailed = true;
			LifecycleQuarantined = true;
			if (string.IsNullOrEmpty(QuarantineReason))
				QuarantineReason = "Its saved wear record could not be read; surviving evidence is retained.";
			if (first) LogWearReadFailure("ThousandAndFirst: a failed wear read was quarantined; partial fields were retained.");
		}

		public override bool HandleEvent(AfterGameLoadedEvent E)
		{
			if (WearReadFailed)
			{
				LifecycleQuarantined = true;
				if (!WearReadNoticeAttempted)
				{
					WearReadNoticeAttempted = true;
					try
					{
						MessageQueue.AddPlayerMessage("{{r|A settlement work's wear record could not be loaded. "
							+ "Its surviving record is quarantined; damage, leaks, and repairs will not be resumed. "
							+ "Keep this save and report the loading error.}}");
					}
					catch (Exception)
					{ LogWearReadFailure("ThousandAndFirst: the one-shot wear load warning could not be delivered."); }
				}
			}
			try { return base.HandleEvent(E); }
			catch (Exception)
			{
				LogWearReadFailure("ThousandAndFirst: the wear after-load event could not finish.");
				return true;
			}
		}

		private static void LogWearReadFailure(string message)
		{
			try { MetricsManager.LogError(message); }
			catch (Exception) { }
		}
	}
}
