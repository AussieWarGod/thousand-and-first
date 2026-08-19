using System;
using XRL;
using XRL.Serialization;
using XRL.World;

namespace ThousandAndFirst
{
	[Serializable]
	public class r_KingdomHandCrank : IPart
	{
		private const int SerializationMagic = 1413563971;

		private const int CurrentSerializationVersion = 1;

		public override void Write(GameObject Basis, SerializationWriter Writer)
		{
			Writer.Write(SerializationMagic);
			Writer.Write(CurrentSerializationVersion);
		}

		public override void Read(GameObject Basis, SerializationReader Reader)
		{
			int magic = Reader.ReadInt32();
			if (magic != SerializationMagic)
			{
				throw new InvalidOperationException("Invalid ThousandAndFirst hand-crank save marker.");
			}
			int version = Reader.ReadInt32();
			if (version != CurrentSerializationVersion)
			{
				throw new InvalidOperationException("Unsupported ThousandAndFirst hand-crank save version " + version + ".");
			}
		}

		public override bool WantTurnTick()
		{
			return true;
		}

		public override void TurnTick(long TimeTick, int Amount)
		{
			try
			{
				if (Amount > 0 && ParentObject != null)
				{
					int charge = KingdomChargingRules.Output(ParentObject.GetIntProperty("KingdomEffectiveness"));
					if (charge > 0)
					{
						ParentObject.ChargeAvailable(charge, 0L, Amount);
					}
				}
			}
			catch (Exception ex)
			{
				MetricsManager.LogError("ThousandAndFirst charging post: charge production failed and was skipped", ex);
			}
			base.TurnTick(TimeTick, Amount);
		}
	}
}
