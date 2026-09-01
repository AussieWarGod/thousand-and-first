using System;
using System.Collections.Generic;
using System.Globalization;

using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// The F1 architecture slice: resolve one exact catalogue case, drive the production gallery
	/// staging path for it, then prove the staged result is that exact case field by field.
	/// </summary>
	internal static class KingdomScenarioGallerySlice
	{
		// Durable object-property contract owned by the architecture review harness. Used only to
		// find and identify the staged owner, never as differential evidence: these properties do
		// not exist on an ordinary commission.
		internal const string ReceiptProperty = "r_TAF_ArchitectureGalleryReceipt";
		internal const string NumberProperty = "r_TAF_ArchitectureGalleryNumber";

		/// <summary>
		/// Any trace of debug-gallery authority on this object.
		/// <para>
		/// The gallery wish can be run in an ordinary game. Without this an operator could stage a
		/// case with the debug wish and then curate that very object as the ordinary-play anchor the
		/// scenario is judged against - the harness anchoring the gallery to itself, which is the
		/// self-signing the governing ruling exists to forbid. Presence is asked by KEY, so a stored
		/// empty receipt is authority too.
		/// </para>
		/// </summary>
		internal static bool CarriesGalleryAuthority(GameObject Item)
		{
			return GameObject.Validate(Item)
				&& (Item.HasStringProperty(ReceiptProperty) || Item.HasIntProperty(ReceiptProperty)
					|| Item.HasIntProperty(NumberProperty)
					|| Item.HasStringProperty(NumberProperty));
		}

		private static readonly string[] Facings = { "north", "east", "south", "west" };

		/// <summary>One resolved catalogue case: its gallery ordinal and its exact identity.</summary>
		internal sealed class Case
		{
			internal int Number;
			internal string BuildKey;
			internal string TypeKey;
			internal string VariantKey;
			internal ArchitectureLotSize LotSize;
			internal ArchitectureFacing Facing;
		}

		internal static bool TryParseFacing(string Token, out ArchitectureFacing Facing,
			out string Failure)
		{
			Facing = ArchitectureFacing.North;
			Failure = null;
			for (int i = 0; i < Facings.Length; i++)
				if (string.Equals(Facings[i], Token, StringComparison.Ordinal))
				{
					Facing = (ArchitectureFacing)i;
					return true;
				}
			return Refuse("'" + KingdomScenarioRules.Bounded(Token)
				+ "' is not one of the four poses", out Failure);
		}

		/// <summary>Parses the closed lot-size vocabulary used by authored scenario rows.</summary>
		private static bool TryParseLotSize(string Token, out ArchitectureLotSize LotSize,
			out string Failure)
		{
			LotSize = ArchitectureLotSize.Small;
			Failure = null;
			switch (Token)
			{
				case "s":
				case "small": LotSize = ArchitectureLotSize.Small; return true;
				case "m":
				case "medium": LotSize = ArchitectureLotSize.Medium; return true;
				case "l":
				case "large": LotSize = ArchitectureLotSize.Large; return true;
				case "xl":
				case "huge": LotSize = ArchitectureLotSize.Huge; return true;
				default:
					return Refuse("'" + KingdomScenarioRules.Bounded(Token)
						+ "' is not a canonical lot size", out Failure);
			}
		}

		/// <summary>
		/// Finds one exact (build, type, size, variant, pose) gallery ordinal by walking the
		/// catalogue in the same order the gallery enumerates it: mapping, then variant, then the
		/// four poses. The pose therefore selects a known case rather than a positional guess.
		/// </summary>
		internal static bool TryResolveCase(string Build, string Type, string SizeToken,
			string Variant, string FacingToken, out Case Resolved, out string Failure)
		{
			Resolved = null;
			ArchitectureFacing facing;
			if (!TryParseFacing(FacingToken, out facing, out Failure)) return false;
			ArchitectureLotSize lotSize;
			if (!TryParseLotSize(SizeToken, out lotSize, out Failure)) return false;
			if (!CatalogueHealthyAfterLoad())
				return Refuse("the authored architecture catalogue is not healthy", out Failure);
			IList<KingdomArchitectureMapping> mappings = KingdomArchitecture.InspectMappings();
			int number = 0;
			for (int m = 0; m < mappings.Count; m++)
			{
				KingdomArchitectureMapping mapping = mappings[m];
				IList<string> variants = mapping.VariantKeys;
				for (int v = 0; v < variants.Count; v++)
					for (int f = 0; f < 4; f++)
					{
						number++;
						if (!string.Equals(mapping.BuildKey, Build, StringComparison.Ordinal)
							|| !string.Equals(mapping.TypeKey, Type, StringComparison.Ordinal)
							|| mapping.LotSize != lotSize
							|| !string.Equals(variants[v], Variant, StringComparison.Ordinal)
							|| f != (int)facing) continue;
						Resolved = new Case
						{
							Number = number,
							BuildKey = mapping.BuildKey,
							TypeKey = mapping.TypeKey,
							VariantKey = variants[v],
							LotSize = mapping.LotSize,
							Facing = facing
						};
						return true;
					}
			}
			return Refuse("the catalogue holds no case for build '"
				+ KingdomScenarioRules.Bounded(Build) + "', type '"
				+ KingdomScenarioRules.Bounded(Type) + "', size '"
				+ KingdomScenarioRules.Bounded(SizeToken) + "', variant '"
				+ KingdomScenarioRules.Bounded(Variant) + "', pose '"
				+ KingdomScenarioRules.Bounded(FacingToken) + "'", out Failure);
		}

		/// <summary>Read-only preconditions for the single production transaction.</summary>
		internal static bool TryProvePreconditions(Zone Zone, Case Expected, out string Failure)
		{
			Failure = null;
			if (Zone == null || Expected == null)
				return Refuse("no loaded zone or exact case to stage", out Failure);
			if (!CatalogueHealthyAfterLoad())
				return Refuse("the authored architecture catalogue is not healthy", out Failure);
			if (Existing(Zone) != null)
				return Refuse("this zone already holds a staged gallery case; clear it first",
					out Failure);
			ArchitectureLayoutSnapshot snapshot;
			if (!KingdomArchitecture.TryResolveVariant(Expected.BuildKey, Expected.TypeKey,
				Expected.LotSize, Expected.VariantKey, Expected.Facing, out snapshot, out Failure))
				return Refuse("the exact requested case cannot resolve before mutation: "
					+ KingdomScenarioRules.Bounded(Failure), out Failure);
			int width;
			int height;
			if (!KingdomArchitectureRules.TryWorldDimensions(snapshot.Width, snapshot.Height,
				snapshot.Facing, out width, out height))
				return Refuse("the exact requested pose has impossible dimensions", out Failure);
			KingdomPlotRules.PlotRect probe;
			string canvasFailure;
			if (!KingdomArchitectureGalleryWishes.TryFindCanvas(Zone, width, height, out probe,
					out canvasFailure))
				return Refuse("no exact-case staging canvas fits before any attempt was recorded: "
					+ canvasFailure + " Use {{W|kingdom:scenario ground}} or "
					+ "{{W|kingdom:scenario flatten}}, step out of the flattened rectangle, and "
					+ "run realize again - the profile is NOT spent", out Failure);
			return true;
		}

		/// <summary>
		/// The single production transaction: the shipped gallery staging path, unchanged.
		/// <para>
		/// Atomicity is the shipped path's own: its staging runs under a try/finally that destroys
		/// every object it created under the staging lot id when no owner results, and it reports
		/// the case as refused without replacing live ground. That covers created objects, which is
		/// what makes one trailing transaction a lawful phase-1 shape. It is not a claim that
		/// ground-layer cell state is journalled; a scenario therefore runs in a throwaway profile
		/// and is re-run as a new dev game rather than repaired in place.
		/// </para>
		/// </summary>
		internal static bool TryStage(Zone Zone, Case Expected, out GameObject Owner,
			out string Failure)
		{
			Owner = null;
			Failure = null;
			// The inner staging API is called directly rather than through the wish wrapper:
			// the wrapper reports every refusal through Popup.Show, and under a sealed script
			// popups are suppressed, which swallowed the reason (proven live 2026-08-30). The
			// wrapper's own prechecks are replicated here and every failure string reaches the
			// journal verbatim.
			GameObject staged = null;
			try
			{
				KingdomData.EnsureBuildings();
				List<KingdomArchitectureGalleryWishes.GalleryCase> cases =
					KingdomArchitectureGalleryWishes.Cases();
				if (Expected.Number < 1 || Expected.Number > cases.Count)
					return Refuse("the frozen case number " + Expected.Number
						+ " is outside the catalogue's " + cases.Count + " cases", out Failure);
				string stageFailure;
				if (!KingdomArchitectureGalleryWishes.TryStage(Zone,
						cases[Expected.Number - 1], cases.Count, out staged, out _,
						out stageFailure))
					return Refuse("the production staging refused: "
						+ KingdomScenarioRules.Bounded(stageFailure ?? "unnamed"), out Failure);
			}
			catch (Exception exception)
			{
				return Refuse("the production gallery path threw: "
					+ KingdomScenarioRules.Bounded(exception.Message), out Failure);
			}
			if (staged == null)
				return Refuse("the production gallery path staged nothing; the case was refused "
					+ "without replacing live ground", out Failure);
			if (staged.GetIntProperty(NumberProperty) != Expected.Number)
				return Refuse("the staged gallery ordinal is not the case that was requested",
					out Failure);
			if (!TryProveExactCase(staged, Expected, out Failure)) return false;
			Owner = staged;
			return true;
		}

		/// <summary>
		/// Proves the staged owner is the exact frozen case, field by field, from the production
		/// intent. A substring match on a label would accept a neighbouring pose or variant.
		/// </summary>
		internal static bool TryProveExactCase(GameObject Owner, Case Expected, out string Failure)
		{
			Failure = null;
			KingdomArchitectureIntent intent;
			ArchitectureLayoutSnapshot snapshot;
			if (!KingdomArchitectureStamper.TryReadOwner(Owner, out intent, out snapshot, out _,
				out Failure)) return false;
			if (intent == null)
				return Refuse("the staged owner carries no architecture intent", out Failure);
			if (!string.Equals(intent.BuildKey, Expected.BuildKey, StringComparison.Ordinal))
				return Mismatch("build key", intent.BuildKey, Expected.BuildKey, out Failure);
			if (!string.Equals(intent.VariantKey, Expected.VariantKey, StringComparison.Ordinal))
				return Mismatch("variant key", intent.VariantKey, Expected.VariantKey, out Failure);
			if (!string.Equals(intent.LotType, Expected.TypeKey, StringComparison.Ordinal))
				return Mismatch("lot type", intent.LotType, Expected.TypeKey, out Failure);
			if (intent.LotSize != Expected.LotSize)
				return Mismatch("lot size", intent.LotSize.ToString(),
					Expected.LotSize.ToString(), out Failure);
			if (intent.Facing != Expected.Facing)
				return Mismatch("facing", intent.Facing.ToString(),
					Expected.Facing.ToString(), out Failure);
			return true;
		}

		internal static string Receipt(GameObject Owner)
		{
			return GameObject.Validate(Owner) ? Owner.GetStringProperty(ReceiptProperty) : null;
		}

		private static GameObject Existing(Zone Zone)
		{
			List<GameObject> objects = Zone?.GetObjects() ?? new List<GameObject>();
			for (int i = 0; i < objects.Count; i++)
			{
				GameObject candidate = objects[i];
				if (GameObject.Validate(candidate)
					&& !string.IsNullOrEmpty(candidate.GetStringProperty(ReceiptProperty)))
					return candidate;
			}
			return null;
		}

		private static bool Mismatch(string Field, string Actual, string Expected, out string Failure)
		{
			return Refuse("the staged case has " + Field + " '"
				+ KingdomScenarioRules.Bounded(Actual) + "' but the scenario froze '"
				+ KingdomScenarioRules.Bounded(Expected) + "'", out Failure);
		}

		private static bool Refuse(string Message, out string Failure)
		{
			Failure = Message;
			return false;
		}
		/// <summary>
		/// The catalogue loads lazily on the first KingdomData ask. A fresh dev-scenario game may
		/// reach this observation before any production system has asked, in which case Healthy
		/// reads false for "never loaded" rather than for a data fault. Trigger the exact lazy
		/// path production callers use, then judge health; a triggered load that still reports
		/// unhealthy is a genuine catalogue fault and refuses.
		/// </summary>
		private static bool CatalogueHealthyAfterLoad()
		{
			if (!KingdomArchitecture.Loaded && KingdomData.Buildings != null) { }
			return KingdomArchitecture.Healthy;
		}

	}
}
