using System.Collections.Generic;

namespace ThousandAndFirst
{
	internal sealed partial class KingdomSealRecord
	{
		private static bool TryReadProfile(int Schema, KingdomSealBody Body,
			KingdomSealRecord Record, ref KingdomSealFault Fault, ref string Detail)
		{
			if (Schema < 6) return true;
			if (!ReadInt(Body, KeyProfileSchema,
				KingdomPolityProfileRules.UnresolvedLegacyProfileSchema,
				KingdomPolityProfileRules.CurrentLegacyProfileSchema,
				out Record.ProfileSchema, ref Fault, ref Detail) ||
				!ReadInt(Body, KeyTechnologyBand, 0, 10,
					out Record.TechnologyBand, ref Fault, ref Detail) ||
				!ReadTokens(Body, KeyCanonicalBody, 6,
					out Record.CanonicalBodyKeys, ref Fault, ref Detail) ||
				!ReadOptionalToken(Body, KeySourceProfileDigest,
					out Record.SourceProfileDigest, ref Fault, ref Detail) ||
				!ReadOptionalToken(Body, KeyProfileProvenanceDigest,
					out Record.ProfileProvenanceDigest, ref Fault, ref Detail)) return false;
			KingdomPolityLegacySnapshot profile = new KingdomPolityLegacySnapshot
			{
				ProfileSchema = Record.ProfileSchema,
				TechnologyBand = Record.TechnologyBand,
				CanonicalBodyKeys = new List<string>(Record.CanonicalBodyKeys),
				SourceProfileDigest = Record.SourceProfileDigest,
				ProfileProvenanceDigest = Record.ProfileProvenanceDigest
			};
			if (KingdomPolityProfileRules.ValidLegacyProfile(profile)) return true;
			Fault = KingdomSealFault.OutOfBounds;
			Detail = "the seal's polity profile provenance cannot be reproved";
			return false;
		}
	}
}
