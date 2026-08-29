using System;
#if !TAF_TESTS
using XRL.World;
#endif

namespace ThousandAndFirst
{
	/// <summary>
	/// City authority for one exact resident's native <c>TeachesDish</c> projection. Learned
	/// recipes belong to vanilla; this receipt owns only the live teaching appointment.
	/// </summary>
	[Serializable]
	public sealed class KingdomNamedCookReceipt
#if !TAF_TESTS
		: IComposite
#endif
	{
		public int Version = KingdomNamedCookRules.CurrentVersion;
		public KingdomNamedCookPhase Phase;
		public int Generation;
		public string RealmId = "";
		public string SettlementId = "";
		public string SettlementName = "";
		public int ResidentId;
		public string ResidentName = "";
		public string BodyObjectId = "";
		public string RecipeId = "";
		public string RecipeDisplayName = "";
		public string EffectId = "";
		public string GraphFingerprint = "";
		public long DesignatedTick;
		public long ReleasedTick;
		public string Fault = "";

#if !TAF_TESTS
		public bool WantFieldReflection => false;

		public void Write(SerializationWriter Writer)
		{
			Writer.WriteNamedFields(this, typeof(KingdomNamedCookReceipt));
		}

		public void Read(SerializationReader Reader)
		{
			Reader.ReadNamedFields(this, typeof(KingdomNamedCookReceipt));
			Normalize();
		}
#endif

		public void Normalize()
		{
			RealmId = RealmId ?? "";
			SettlementId = SettlementId ?? "";
			SettlementName = SettlementName ?? "";
			ResidentName = ResidentName ?? "";
			BodyObjectId = BodyObjectId ?? "";
			RecipeId = RecipeId ?? "";
			RecipeDisplayName = RecipeDisplayName ?? "";
			EffectId = EffectId ?? "";
			GraphFingerprint = GraphFingerprint ?? "";
			Fault = Fault ?? "";
			if (Phase == KingdomNamedCookPhase.None)
			{
				Generation = ResidentId = 0;
				DesignatedTick = ReleasedTick = 0L;
				RealmId = SettlementId = SettlementName = ResidentName = BodyObjectId = "";
				RecipeId = RecipeDisplayName = EffectId = GraphFingerprint = Fault = "";
			}
		}

		public KingdomNamedCookReceipt Copy()
		{
			return (KingdomNamedCookReceipt)MemberwiseClone();
		}
	}
}
