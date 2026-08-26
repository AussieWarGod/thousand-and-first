using System;
using System.Reflection;
using XRL.World;

namespace XRL.World.Parts
{
	/// <summary>One in-run founder shrine-marker. This is owned by the live city's succession
	/// register and is deliberately unrelated to the profile inheritance cairn.</summary>
	[Serializable]
	public sealed class r_KingdomFounderShrine : IPart
	{
		private const int SerializationMagic = 1397248562;
		private const int CurrentSerializationVersion = 1;
		private const int MaxText = 4096;

		private int SerializationVersion = CurrentSerializationVersion;
		private string DeathToken;
		private string FounderName;
		private long DeathTick;
		private string Cause;
		private string History;
		private string CityName;
		private string FixtureObjectId;

		public r_KingdomFounderShrine()
		{
		}

		internal bool Matches(string token)
		{
			return !string.IsNullOrEmpty(token)
				&& string.Equals(DeathToken, token, StringComparison.Ordinal);
		}

		internal void Stamp(string token, string founderName, long deathTick, string cause,
			string history, string cityName, string fixtureObjectId)
		{
			DeathToken = token ?? "";
			FounderName = founderName ?? "";
			DeathTick = deathTick < 0L ? 0L : deathTick;
			Cause = cause ?? "";
			History = history ?? "";
			CityName = cityName ?? "";
			FixtureObjectId = fixtureObjectId ?? "";
			Validate();
			ApplyPresentation();
		}

		public override void Write(GameObject Basis, SerializationWriter Writer)
		{
			SerializationVersion = CurrentSerializationVersion;
			Writer.Write(SerializationMagic);
			Writer.Write(CurrentSerializationVersion);
			Writer.WriteNamedFields(this, typeof(r_KingdomFounderShrine),
				BindingFlags.Instance | BindingFlags.NonPublic);
		}

		public override void Read(GameObject Basis, SerializationReader Reader)
		{
			int magic = Reader.ReadInt32();
			int version = Reader.ReadInt32();
			if (magic != SerializationMagic || version != CurrentSerializationVersion)
			{
				throw new InvalidOperationException("Unsupported founder shrine save block.");
			}
			Reader.ReadNamedFields(this, typeof(r_KingdomFounderShrine),
				BindingFlags.Instance | BindingFlags.NonPublic);
			if (SerializationVersion != CurrentSerializationVersion)
			{
				throw new InvalidOperationException("Unsupported founder shrine named-field version.");
			}
			Validate();
			ApplyPresentation(Basis);
		}

		private void Validate()
		{
			DeathToken = DeathToken ?? "";
			FounderName = FounderName ?? "";
			Cause = Cause ?? "";
			History = History ?? "";
			CityName = CityName ?? "";
			FixtureObjectId = FixtureObjectId ?? "";
			int ordinal;
			long tokenTick;
			if (!ThousandAndFirst.KingdomSuccessionRules.TryReadDeathToken(
				DeathToken, out ordinal, out tokenTick) || tokenTick != DeathTick
				|| string.IsNullOrEmpty(FounderName) || string.IsNullOrEmpty(Cause)
				|| string.IsNullOrEmpty(History) || string.IsNullOrEmpty(CityName)
				|| FounderName.Length > MaxText || Cause.Length > MaxText
				|| History.Length > MaxText || CityName.Length > MaxText
				|| FixtureObjectId.Length > MaxText)
			{
				throw new InvalidOperationException("The founder shrine history is malformed.");
			}
		}

		private void ApplyPresentation(GameObject Basis = null)
		{
			GameObject owner = Basis ?? ParentObject;
			if (owner?.Render != null)
			{
				owner.Render.DisplayName = "shrine-marker of "
					+ ThousandAndFirst.KingdomPresentation.Rich(FounderName);
			}
			Description description = owner?.GetPart<Description>();
			if (description != null)
			{
				description.Short = History + "\n\nDeath-token: " + DeathToken
					+ ". The stone stands in "
					+ ThousandAndFirst.KingdomPresentation.Rich(CityName) + ".";
			}
		}
	}
}
