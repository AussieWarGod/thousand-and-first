using System;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>Color-independent map states, in gallery order. Sound owns no overlay.</summary>
	public enum KingdomVisualStateKind : byte
	{
		Sound = 0,
		Raising = 1,
		RaisingWaitingForHands = 2,
		RaisingQueued = 3,
		SalvageOrdered = 4,
		Repairing = 5,
		Ruined = 6,
		HalfRuined = 7,
		Battered = 8,
		WitheredAndFamished = 9,
		Withered = 10,
		Famished = 11,
		Dark = 12,
		Idle = 13,
		Shorthanded = 14
	}

	/// <summary>Exact runtime facts supplied to the visual resolver. No presentation latch is a
	/// fact: every field names simulation state that already changes play.</summary>
	public readonly struct KingdomVisualFacts
	{
		public readonly bool ConstructionActive;
		public readonly bool ConstructionSelected;
		public readonly int ConstructionHands;
		public readonly bool SalvageOrdered;
		public readonly bool Repairing;
		public readonly int Wear;
		public readonly bool Heart;
		public readonly bool Withered;
		public readonly bool Famished;
		public readonly bool Brownout;
		public readonly int StaffNeeded;
		public readonly int StaffEffectiveness;

		public KingdomVisualFacts(bool ConstructionActive, bool ConstructionSelected,
			int ConstructionHands, bool SalvageOrdered, bool Repairing, int Wear, bool Heart,
			bool Withered, bool Famished, bool Brownout, int StaffNeeded,
			int StaffEffectiveness)
		{
			this.ConstructionActive = ConstructionActive;
			this.ConstructionSelected = ConstructionSelected;
			this.ConstructionHands = ConstructionHands;
			this.SalvageOrdered = SalvageOrdered;
			this.Repairing = Repairing;
			this.Wear = Wear;
			this.Heart = Heart;
			this.Withered = Withered;
			this.Famished = Famished;
			this.Brownout = Brownout;
			this.StaffNeeded = StaffNeeded;
			this.StaffEffectiveness = StaffEffectiveness;
		}
	}

	/// <summary>One vanilla render-channel cue. Glyph and tile silhouette carry meaning; color is
	/// redundant, so text mode, tiles, and color-vision differences all retain the state.</summary>
	public readonly struct KingdomVisualCue
	{
		public readonly string Glyph;
		public readonly string Tile;
		public readonly string ColorString;
		public readonly string DetailColor;
		public readonly string Label;

		public KingdomVisualCue(string Glyph, string Tile, string ColorString,
			string DetailColor, string Label)
		{
			this.Glyph = Glyph;
			this.Tile = Tile;
			this.ColorString = ColorString;
			this.DetailColor = DetailColor;
			this.Label = Label;
		}
	}

	/// <summary>Pure state priority, legend, and deterministic gallery receipt.</summary>
	public static class KingdomVisualStateRules
	{
		public const string GalleryVersion = "taf:visual-state-gallery:v1";

		public static readonly KingdomVisualStateKind[] GalleryStates =
			(KingdomVisualStateKind[])Enum.GetValues(typeof(KingdomVisualStateKind));

		public static KingdomVisualStateKind Resolve(KingdomVisualFacts Facts)
		{
			if (Facts.ConstructionActive)
			{
				if (!Facts.ConstructionSelected) return KingdomVisualStateKind.RaisingQueued;
				return Facts.ConstructionHands > 0 ? KingdomVisualStateKind.Raising
					: KingdomVisualStateKind.RaisingWaitingForHands;
			}
			if (Facts.SalvageOrdered) return KingdomVisualStateKind.SalvageOrdered;
			if (Facts.Repairing) return KingdomVisualStateKind.Repairing;
			if (Facts.Wear >= KingdomMaterialRules.HalfWreckedWearPercent)
				return KingdomVisualStateKind.Ruined;
			if (Facts.Wear >= KingdomMaterialRules.BadlyUsedWearPercent)
				return KingdomVisualStateKind.HalfRuined;
			if (Facts.Wear > 0) return KingdomVisualStateKind.Battered;
			if (Facts.Heart && Facts.Withered && Facts.Famished)
				return KingdomVisualStateKind.WitheredAndFamished;
			if (Facts.Heart && Facts.Withered) return KingdomVisualStateKind.Withered;
			if (Facts.Heart && Facts.Famished) return KingdomVisualStateKind.Famished;
			if (Facts.Brownout) return KingdomVisualStateKind.Dark;
			if (Facts.StaffNeeded > 0 && Facts.StaffEffectiveness <= 0)
				return KingdomVisualStateKind.Idle;
			if (Facts.StaffNeeded > 0 && Facts.StaffEffectiveness < 100)
				return KingdomVisualStateKind.Shorthanded;
			return KingdomVisualStateKind.Sound;
		}

		public static KingdomVisualCue Cue(KingdomVisualStateKind State)
		{
			switch (State)
			{
			case KingdomVisualStateKind.Raising:
				return new KingdomVisualCue("/", "Items/wrench.bmp", "&y", "c",
					"raising; assigned builders are at the frame");
			case KingdomVisualStateKind.RaisingWaitingForHands:
				return new KingdomVisualCue("_", null, "&K", "k",
					"half-raised and waiting for free hands");
			case KingdomVisualStateKind.RaisingQueued:
				return new KingdomVisualCue("=", null, "&w", "K",
					"half-raised and queued behind an older raising");
			case KingdomVisualStateKind.SalvageOrdered:
				return new KingdomVisualCue("x", "Items/sw_broken_arrow.bmp", "&r", "K",
					"condemned; being taken down for salvage");
			case KingdomVisualStateKind.Repairing:
				return new KingdomVisualCue("+", "Items/sw_toolbox_large.bmp", "&C", "y",
					"damaged and actively being mended");
			case KingdomVisualStateKind.Ruined:
				return new KingdomVisualCue("#", "Tiles2/sw_rubble_4.bmp", "&K", "w",
					"ruined shell; still mendable or salvageable");
			case KingdomVisualStateKind.HalfRuined:
				return new KingdomVisualCue("%", "Tiles2/sw_rubble_2.bmp", "&w", "K",
					"half-ruined; working at reduced measure");
			case KingdomVisualStateKind.Battered:
				return new KingdomVisualCue("\\", "Tiles2/sw_rubble_1.bmp", "&y", "w",
					"battered; working at reduced measure");
			case KingdomVisualStateKind.WitheredAndFamished:
				return new KingdomVisualCue("!", null, "&R", "K",
					"city heart withered by thirst and famished by hunger");
			case KingdomVisualStateKind.Withered:
				return new KingdomVisualCue(";", null, "&y", "K",
					"city heart withered by sustained thirst");
			case KingdomVisualStateKind.Famished:
				return new KingdomVisualCue(":", null, "&r", "K",
					"city heart famished by sustained hunger");
			case KingdomVisualStateKind.Dark:
				return new KingdomVisualCue("o", "Items/sw_power_cut_small.png", "&W", "R",
					"dark in a real power brownout");
			case KingdomVisualStateKind.Idle:
				return new KingdomVisualCue("-", null, "&K", "w",
					"idle and unstaffed");
			case KingdomVisualStateKind.Shorthanded:
				return new KingdomVisualCue("?", null, "&y", "w",
					"working shorthanded");
			default:
				return new KingdomVisualCue(null, null, null, null, "sound");
			}
		}

		/// <summary>Stable, engine-free legend used by tests, debug audit, and screenshot receipts.</summary>
		public static string GalleryReceipt()
		{
			StringBuilder text = new StringBuilder();
			text.Append(GalleryVersion);
			for (int i = 0; i < GalleryStates.Length; i++)
			{
				KingdomVisualStateKind state = GalleryStates[i];
				KingdomVisualCue cue = Cue(state);
				text.Append('\n').Append((int)state).Append('|').Append(state).Append('|')
					.Append(cue.Glyph ?? "-").Append('|').Append(cue.Tile ?? "text")
					.Append('|').Append(cue.Label);
			}
			return text.ToString();
		}

		public static string GalleryHash()
		{
			using (SHA256 sha = SHA256.Create())
			{
				byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(GalleryReceipt()));
				StringBuilder text = new StringBuilder(64);
				for (int i = 0; i < digest.Length; i++) text.Append(digest[i].ToString("x2"));
				return text.ToString();
			}
		}
	}
}
