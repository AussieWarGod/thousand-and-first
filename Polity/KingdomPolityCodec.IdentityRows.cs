using System.IO;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityCodec
	{
		private static void WritePolity(BinaryWriter W, KingdomPolityRecord V)
		{
			WriteString(W, V.PolityId); WriteString(W, V.DisplayName); W.Write(V.NameRevision);
			W.Write((byte)V.Source); W.Write((byte)V.Lifecycle); WriteString(W, V.ProfileId);
			W.Write(V.ProfileRevision); WriteString(W, V.ProjectedFactionId);
			WriteString(W, V.ExternalCounterpartyKey); W.Write(V.EndedTick);
		}

		private static KingdomPolityRecord ReadPolity(BinaryReader R)
		{
			return new KingdomPolityRecord
			{
				PolityId = ReadString(R), DisplayName = ReadString(R), NameRevision = R.ReadInt32(),
				Source = (KingdomPolitySource)R.ReadByte(),
				Lifecycle = (KingdomPolityLifecycle)R.ReadByte(), ProfileId = ReadString(R),
				ProfileRevision = R.ReadInt32(), ProjectedFactionId = ReadString(R),
				ExternalCounterpartyKey = ReadString(R), EndedTick = R.ReadInt64()
			};
		}

		private static void WriteRelation(BinaryWriter W, KingdomPolityRelation V)
		{
			WriteString(W, V.RelationId); WriteString(W, V.FromPolityId);
			WriteString(W, V.ToPolityId); W.Write((byte)V.Band);
			WriteStrings(W, V.SourceRefs, KingdomPolityRules.MaxRefs); W.Write(V.ChangedTick);
		}

		private static KingdomPolityRelation ReadRelation(BinaryReader R)
		{
			return new KingdomPolityRelation
			{
				RelationId = ReadString(R), FromPolityId = ReadString(R), ToPolityId = ReadString(R),
				Band = (KingdomPolityRelationBand)R.ReadByte(),
				SourceRefs = ReadStrings(R, KingdomPolityRules.MaxRefs), ChangedTick = R.ReadInt64()
			};
		}

		private static void WriteProfile(BinaryWriter W, KingdomPolityProfileRevision V)
		{
			WriteString(W, V.ProfileId); W.Write(V.Revision); WriteString(W, V.PolityId);
			W.Write(V.EffectiveTick); W.Write(V.RulesVersion);
			WriteStrings(W, V.DerivedFromFactIds, KingdomPolityRules.MaxRefs);
			WriteString(W, V.FactsDigest); W.Write(V.TechnologyBand);
			WriteStrings(W, V.PracticeTags, 8); WriteStrings(W, V.BodyKeys, KingdomPolityRules.MaxRefs);
			WriteStrings(W, V.RoleKeys, KingdomPolityRules.MaxRefs);
			WriteStrings(W, V.GearKeys, KingdomPolityRules.MaxRefs); WriteLoadout(W, V.Loadout);
		}

		private static KingdomPolityProfileRevision ReadProfile(BinaryReader R)
		{
			return new KingdomPolityProfileRevision
			{
				ProfileId = ReadString(R), Revision = R.ReadInt32(), PolityId = ReadString(R),
				EffectiveTick = R.ReadInt64(), RulesVersion = R.ReadInt32(),
				DerivedFromFactIds = ReadStrings(R, KingdomPolityRules.MaxRefs),
				FactsDigest = ReadString(R), TechnologyBand = R.ReadInt32(),
				PracticeTags = ReadStrings(R, 8), BodyKeys = ReadStrings(R, KingdomPolityRules.MaxRefs),
				RoleKeys = ReadStrings(R, KingdomPolityRules.MaxRefs),
				GearKeys = ReadStrings(R, KingdomPolityRules.MaxRefs), Loadout = ReadLoadout(R)
			};
		}

		private static void WriteProfileV7(BinaryWriter W, KingdomPolityProfileRevision V)
		{
			WriteProfile(W, V); WriteList(W, V.ExpressionCues,
				KingdomPolityProfileExpressionCatalogue.MaxCues, WriteExpressionCue);
		}

		private static KingdomPolityProfileRevision ReadProfileV7(BinaryReader R)
		{
			KingdomPolityProfileRevision result = ReadProfile(R);
			result.ExpressionCues = ReadList(R,
				KingdomPolityProfileExpressionCatalogue.MaxCues, ReadExpressionCue);
			return result;
		}

		private static void WriteExpressionCue(BinaryWriter W, KingdomPolityExpressionCue V)
		{
			W.Write((byte)V.Kind); WriteString(W, V.ExpressionKey); W.Write(V.Weight);
			W.Write((byte)V.SourceKind); WriteString(W, V.SourceValueKey);
			WriteString(W, V.SourceRef); WriteString(W, V.ReasonFactId);
		}

		private static KingdomPolityExpressionCue ReadExpressionCue(BinaryReader R)
		{
			return new KingdomPolityExpressionCue { Kind = (KingdomPolityExpressionKind)R.ReadByte(),
				ExpressionKey = ReadString(R), Weight = R.ReadInt32(),
				SourceKind = (KingdomPolityProfileFactKind)R.ReadByte(),
				SourceValueKey = ReadString(R), SourceRef = ReadString(R),
				ReasonFactId = ReadString(R) };
		}

		private static void WriteLoadout(BinaryWriter W, KingdomPolityLoadoutPolicy V)
		{
			if (V == null) throw new InvalidDataException("Polity loadout is null.");
			W.Write((byte)V.Kind); W.Write(V.ExpectedValueBudget);
			WriteStrings(W, V.ExcludedKeys, KingdomPolityRules.MaxRefs);
			WriteStrings(W, V.SelectedKeys, KingdomPolityRules.MaxRefs);
		}

		private static KingdomPolityLoadoutPolicy ReadLoadout(BinaryReader R)
		{
			return new KingdomPolityLoadoutPolicy
			{
				Kind = (KingdomPolityLoadoutPolicyKind)R.ReadByte(),
				ExpectedValueBudget = R.ReadInt32(),
				ExcludedKeys = ReadStrings(R, KingdomPolityRules.MaxRefs),
				SelectedKeys = ReadStrings(R, KingdomPolityRules.MaxRefs)
			};
		}

		private static void WriteProfileRef(BinaryWriter W, KingdomPolityProfileRef V)
		{
			WriteString(W, V.ProfileId); W.Write(V.Revision);
		}

		private static KingdomPolityProfileRef ReadProfileRef(BinaryReader R)
		{
			return new KingdomPolityProfileRef { ProfileId = ReadString(R), Revision = R.ReadInt32() };
		}
	}
}
