using Sandbox;
using System;

namespace Facepunch.Pool
{
	public static class Elo
	{
		public static int GetNextLevelRating( int rating )
		{
			var roundedUp = Math.Max( ((int)Math.Ceiling( rating / 100f ) * 100), 0 );
			return rating == roundedUp ? rating + 100 : roundedUp;
		}

		public static PlayerRank GetRank( int rating )
		{
			return rating switch
			{
				< 1149 => PlayerRank.Bronze,
				< 1499 => PlayerRank.Silver,
				< 1849 => PlayerRank.Gold,
				< 2199 => PlayerRank.Platinum,
				_ => PlayerRank.Diamond
			};
		}

		public static PlayerRank GetNextRank( int rating )
		{
			var rank = GetRank( rating );

			return rank switch
			{
				PlayerRank.Bronze => PlayerRank.Silver,
				PlayerRank.Silver => PlayerRank.Gold,
				PlayerRank.Gold => PlayerRank.Platinum,
				_ => PlayerRank.Diamond
			};
		}

		public static int GetLevel( int rating )
		{
			return (rating / 100) - 10;
		}
	}
}
