using System;
using Sandbox;
using System.Threading.Tasks;
using Sandbox.Services;

namespace Facepunch.Pool
{
	public enum EloOutcome
	{
		Loss = 0,
		Win = 1
	}

	public partial class EloScore : BaseNetworkable
	{
		public static void Update( EloScore playerOne, EloScore playerTwo, EloOutcome outcome )
		{
			const int eloK = 32;
			var delta = (int)(eloK * ((int)outcome - GetExpectationToWin( playerOne, playerTwo )));

			playerOne.Delta = delta;
			playerOne.Rating += delta;

			playerTwo.Delta = -delta;
			playerTwo.Rating -= delta;
		}
		
		public static double GetExpectationToWin( EloScore playerOne, EloScore playerTwo )
		{
			return 1f / (1f + MathF.Pow( 10f, (playerTwo.Rating - playerOne.Rating) / 400f ));
		}
		
		[Net] public int Rating { get; set; }
		[Net] public int Delta { get; set; }

		public PlayerRank GetRank()
		{
			return Elo.GetRank( Rating );
		}

		public PlayerRank GetNextRank()
		{
			return Elo.GetNextRank( Rating );
		}

		public int GetLevel()
		{
			return Elo.GetLevel( Rating );
		}

		public void Initialize( IClient client )
		{
			
		}
	}
}
