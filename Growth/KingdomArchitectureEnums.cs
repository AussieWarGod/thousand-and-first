using System;

namespace ThousandAndFirst
{
	/// <summary>The four fixed plot envelopes authored by the settlement catalogue.</summary>
	public enum ArchitectureLotSize : byte
	{
		Small = 1,
		Medium = 2,
		Large = 3,
		Huge = 4
	}

	/// <summary>The side of a lot its authored north/front edge faces in the world.</summary>
	public enum ArchitectureFacing : byte
	{
		North = 0,
		East = 1,
		South = 2,
		West = 3
	}

	/// <summary>How one semantic scenery blueprint responds to lot pose.</summary>
	public enum ArchitecturePoseMode : byte
	{
		Invariant = 0,
		Connected = 1,
		Cardinal = 2
	}

	/// <summary>Semantic frontage resolved by the runtime into a fixed world facing.</summary>
	public enum ArchitectureFrontage : byte
	{
		Heart = 0,
		Road = 1
	}

	/// <summary>Authored use of one canonical lot cell. LegacyClaimed exists only when decoding
	/// schema a1-a3, whose one-bit wire format cannot distinguish building from yard.</summary>
	public enum ArchitectureClaim : byte
	{
		Unclaimed = 0,
		Yard = 1,
		Building = 2,
		LegacyClaimed = 3
	}

	/// <summary>One of the three permanent object layers an authored map may place.</summary>
	public enum ArchitectureLayer : byte
	{
		Ground = 0,
		Structure = 1,
		Object = 2
	}

	/// <summary>Semantic movement truth used before an engine blueprint is available.</summary>
	public enum ArchitecturePassability : byte
	{
		Walkable = 0,
		Blocked = 1,
		Adjacent = 2
	}

	/// <summary>Whether one claimed cell is under sky, a roof, a wall roof, or natural rock.</summary>
	public enum ArchitectureCover : byte
	{
		Open = 0,
		Soft = 1,
		Walled = 2,
		Natural = 3
	}

	/// <summary>Where an actor stands to use an anchor.</summary>
	public enum ArchitectureAnchorAccess : byte
	{
		OnCell = 0,
		Adjacent = 1
	}

	/// <summary>Whether a target plan belongs to the standing lot or needs a true restake.</summary>
	public enum ArchitectureSetChange : byte
	{
		SameSet = 0,
		Restake = 1
	}

	/// <summary>
	/// Authored physical contract for one incoming building tier or explicit plan route.
	/// None is valid only for a fresh commission. Replacement is a named refusal: strike the
	/// standing work and commission the successor fresh rather than pretending to preserve it.
	/// </summary>
	public enum ArchitectureTransitionMode : byte
	{
		None = 0,
		Additive = 1,
		Renovate = 2,
		RenovateExpand = 3,
		Replacement = 4,
		AdditiveExpand = 5
	}

	/// <summary>One immutable point in canonical or world coordinates.</summary>
	public struct ArchitecturePoint : IEquatable<ArchitecturePoint>
	{
		public readonly int X;
		public readonly int Y;

		public ArchitecturePoint(int X, int Y)
		{
			this.X = X;
			this.Y = Y;
		}

		public bool Equals(ArchitecturePoint Other)
		{
			return X == Other.X && Y == Other.Y;
		}

		public override bool Equals(object Object)
		{
			return Object is ArchitecturePoint && Equals((ArchitecturePoint)Object);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return X * 397 ^ Y;
			}
		}
	}
}
