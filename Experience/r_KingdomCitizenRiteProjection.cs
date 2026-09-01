using System;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>Exact provenance for native host parts added by TAF. False ownership preserves
	/// pre-existing or legacy-ambiguous native state through accession.</summary>
	[Serializable]
	public sealed class r_KingdomCitizenRiteProjection : IPart
	{
		public const int CurrentVersion = 1;
		public int Version = CurrentVersion;
		public string RealmId = "";
		public string BodyObjectId = "";
		public bool AddedGivesRep;
		public string GivesRepDigest = "";
		public bool AddedConversation;
		public string ConversationDigest = "";
		public int GreetingBand;
		public string Fault = "";

		public override bool CanGenerateStacked() => false;

		public override bool SameAs(IPart Part) => ReferenceEquals(this, Part);

		public override void Write(GameObject Basis, SerializationWriter Writer)
		{
			Writer.WriteNamedFields(this, typeof(r_KingdomCitizenRiteProjection));
		}

		public override void Read(GameObject Basis, SerializationReader Reader)
		{
			Reader.ReadNamedFields(this, typeof(r_KingdomCitizenRiteProjection));
			RealmId = RealmId ?? ""; BodyObjectId = BodyObjectId ?? "";
			GivesRepDigest = GivesRepDigest ?? "";
			ConversationDigest = ConversationDigest ?? ""; Fault = Fault ?? "";
		}
	}
}
