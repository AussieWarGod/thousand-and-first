namespace ThousandAndFirst
{
	/// <summary>The physical target an explicitly adoptable design can honestly designate.</summary>
	public enum KingdomAdoptionTargetKind : byte
	{
		None = 0,
		Room = 1,
		Larder = 2,
		/// <summary>An exact catalogue-sized open rectangle centred on a civic marker.
		/// Its cells are plot/yard authority; furniture still supplies every benefit.</summary>
		OpenPlot = 3
	}

	/// <summary>Pure, fail-closed proof boundary for player-designated buildings.</summary>
	public static class KingdomAdoptabilityRules
	{
		public const string LarderKey = "larder";

		public static bool TryClassify(string Key, string Category,
			KingdomPlotRules.PlotSize Size, bool Open,
			out KingdomAdoptionTargetKind Kind, out string Failure)
		{
			Kind = KingdomAdoptionTargetKind.None; Failure = null;
			string key = Fold(Key);
			if (!KingdomDesignationRules.SafeToken(key, 128))
				return Fail("adoptable design key is malformed", out Failure);
			if (Size == KingdomPlotRules.PlotSize.None)
				return Fail("single-cell and network works need their authored object", out Failure);
			KingdomAdoptRules.RoleKind role = KingdomAdoptRules.ClassifyRole(Category);
			if (role == KingdomAdoptRules.RoleKind.Storage)
			{
				if (key == LarderKey)
				{
					Kind = KingdomAdoptionTargetKind.Larder; return true;
				}
				if (Open && !NeedsAuthoredRoot(key))
				{
					Kind = KingdomAdoptionTargetKind.OpenPlot; return true;
				}
				return Fail("this storage role needs typed production or container proof",
					out Failure);
			}
			if (NeedsAuthoredRoot(key))
				return Fail("this design's operation belongs to authored machinery or topology",
					out Failure);
			Kind = Open ? KingdomAdoptionTargetKind.OpenPlot
				: KingdomAdoptionTargetKind.Room;
			return true;
		}

		public static bool CandidateMatches(KingdomAdoptionTargetKind Kind,
			bool HasLiquidVolume, bool HasInventory)
		{
			return Kind == KingdomAdoptionTargetKind.Room
				|| Kind == KingdomAdoptionTargetKind.OpenPlot
				|| Kind == KingdomAdoptionTargetKind.Larder
					&& !HasLiquidVolume && HasInventory;
		}

		/// <summary>Shipped designs whose meaningful operation still lives on a root part,
		/// fixed installation, specialized ground shape, remote endpoint, or unique programme.</summary>
		public static bool NeedsAuthoredRoot(string Key)
		{
			switch (Fold(Key))
			{
			case "saltpan": case "saltterrace":
			case "plot": case "plotrows": case "field": case "fieldrows":
			case "granary": case "grange": case "homefarm": case "sporecellar":
			case "fungalvault": case "vaultgalleries": case "joppaseedhouse":
			case "snapjawtrailden":
			case "kyakukyaspicehearth": case "svardymbrinenursery":
			case "mopangorefugekitchen": case "farmersseedcommons":
			case "ydvinebower": case "realmgranary": case "arcologyterrace":
			case "mill": case "waterwheel": case "sailvane": case "saltstore":
			case "robotchargebay": case "robotservicebay":
			case "grindmill": case "delve":
			case "butcherslab": case "vathouse": case "graftinghall":
			case "chimerictheatre": case "becomingannexe": case "deepbore":
			case "greatfoundry": case "stasisvault": case "mirrorgate":
			case "heartbasin": case "heartwaterstone": case "heartmoot":
			case "heartcourt": case "assentingmoot": case "crownhall":
			case "arcology": case "arcologyward": case "hallsurgery":
			case "registryoffice": case "reshephhospice":
			case "ezrawheelshade":
			case "gravegrove": case "sacramentcourt": case "nichetomb":
			case "reliquary": case "cragmenschstonegarden":
			case "baetylofferingframe": case "dromadcaravanshade":
			case "entropyblind": case "goatfolkhornmoot":
			case "naphtaaliscrapaltar": case "trollbridgecourt":
			case "issacharirifleporch": case "hindrenmooncourt":
			case "templarpurityarsenal": case "gyrewightashcourt":
			case "mamontithecistern": case "seekersquietcell":
			case "wardenswatchlodge": case "waterbaronsgaugehouse":
			case "merchantweighinghouse": case "daughtersrepairlodge":
			case "chavvahboughschool": case "girshrotchapel":
				return true;
			default:
				return false;
			}
		}

		private static string Fold(string Value)
		{
			return (Value ?? "").Trim().ToLowerInvariant();
		}

		private static bool Fail(string Message, out string Failure)
		{
			Failure = Message; return false;
		}
	}
}
