using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>
	/// How far apart two cities of one realm have grown. Ordered mildest-first, so a larger value
	/// is a worse temper and every comparison in this file reads that way.
	/// </summary>
	public enum CityTemper
	{
		Concord = 0,
		Muttering = 1,
		Quarrel = 2,
		Rupture = 3,
		Secession = 4
	}
}
