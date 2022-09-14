using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Facepunch.Pool
{
    public partial class PlayRound : BaseRound
	{
		public override string RoundName => "PLAY";
		public override int RoundDuration => 0;
		public override bool CanPlayerSuicide => false;
		public override bool ShowTimeLeft => true;

		public List<Player> Spectators = new();
		
		public RealTimeUntil PlayerTurnEndTime;
		public TimeSince TimeSinceTurnTaken;

		public bool DidClaimThisTurn { get; private set; }
		public Sound? ClockTickingSound { get; private set; }

		public bool HasPlayedFastForwardSound { get; private set; }

		[Net] public PoolBall BallLikelyToPot { get; set; }
		[Net] public bool IsGameOver { get; set; }

		public override void OnPlayerLeave( Player player )
		{
			base.OnPlayerLeave( player );

			var playerOne = Game.Instance.PlayerOne;
			var playerTwo = Game.Instance.PlayerTwo;

			if ( player == playerOne || player == playerTwo )
			{
				if ( Game.Rules.IsRanked )
					GameServices.AbandonGame( true );

				Game.Instance.ChangeRound( new StatsRound() );
			}
		}

		public override void UpdatePlayerPosition( Player player )
		{
			if ( BallLikelyToPot.IsValid() )
				player.Position = BallLikelyToPot.Position.WithZ( 200f );
			else
				player.Position = new Vector3( 0f, 0f, 350f );
			
			player.Rotation = Rotation.LookAt( Vector3.Down );
		}

		public override void OnPlayerJoin( Player player )
		{
			Spectators.Add( player );

			base.OnPlayerJoin( player );
		}

		public override void OnBallEnterPocket( PoolBall ball, TriggerBallPocket pocket )
		{
			if ( Host.IsServer )
			{
				ball.PlaySound( $"ball-pocket-{Rand.Int( 1, 2 )}" );

				if ( BallLikelyToPot == ball )
				{
					// We don't wanna follow this ball anymore.
					BallLikelyToPot = null;
				}

				if ( ball.LastStriker == null || !ball.LastStriker.IsValid() )
				{
					if ( ball.Type == PoolBallType.White )
					{
						_ = Game.Instance.RespawnBallAsync( ball, true );
					}
					else if ( ball.Type == PoolBallType.Black )
					{
						_ = Game.Instance.RespawnBallAsync( ball, true );
					}
					else
					{
						var player = Game.Instance.GetBallPlayer( ball );

						if ( player != null && player.IsValid() )
						{
							var currentPlayer = Game.Instance.CurrentPlayer;

							if ( currentPlayer == player )
								player.HasSecondShot = true;

							DoPlayerPotBall( currentPlayer, ball, BallPotType.Silent );
						}

						_ = Game.Instance.RemoveBallAsync( ball, true );
					}

					return;
				}

				if ( ball.Type == PoolBallType.White )
				{
					ball.LastStriker.Foul( FoulReason.PotWhiteBall );
					_ = Game.Instance.RespawnBallAsync( ball, true );
				}
				else if ( ball.Type == ball.LastStriker.BallType )
				{
					if ( Game.Instance.CurrentPlayer == ball.LastStriker )
					{
						ball.LastStriker.HasSecondShot = true;
						ball.LastStriker.DidHitOwnBall = true;
					}

					DoPlayerPotBall( ball.LastStriker, ball, BallPotType.Normal );

					_ = Game.Instance.RemoveBallAsync( ball, true );
				}
				else if ( ball.Type == PoolBallType.Black )
				{
					DoPlayerPotBall( ball.LastStriker, ball, BallPotType.Normal );

					_ = Game.Instance.RemoveBallAsync( ball, true );
				}
				else
				{
					if ( ball.LastStriker.BallType == PoolBallType.White )
					{
						// We only get a second shot if we didn't foul.
						if ( ball.LastStriker.FoulReason == FoulReason.None )
							ball.LastStriker.HasSecondShot = true;

						// This is our ball type now, we've claimed it.
						ball.LastStriker.DidHitOwnBall = true;
						ball.LastStriker.BallType = ball.Type;

						var otherPlayer = Game.Instance.GetOtherPlayer( ball.LastStriker );
						otherPlayer.BallType = (ball.Type == PoolBallType.Spots ? PoolBallType.Stripes : PoolBallType.Spots);

						DoPlayerPotBall( ball.LastStriker, ball, BallPotType.Claim );

						DidClaimThisTurn = true;
					}
					else
					{
						if ( !DidClaimThisTurn )
							ball.LastStriker.Foul( FoulReason.PotOtherBall );

						DoPlayerPotBall( ball.LastStriker, ball, BallPotType.Normal );
					}

					_ = Game.Instance.RemoveBallAsync( ball, true );
				}
			}
		}

		public override void OnBallHitOtherBall( PoolBall ball, PoolBall other )
		{
			// Is this the first ball this striker has hit?
			if ( Host.IsServer && ball.Type == PoolBallType.White )
			{
				if ( ball.LastStriker.BallType == PoolBallType.White )
				{
					if ( other.Type == PoolBallType.Black )
					{
						// The player has somehow hit the black as their first strike.
						ball.LastStriker.Foul( FoulReason.HitOtherBall );
					}
				}
				else if ( other.Type == PoolBallType.Black )
				{
					if ( ball.LastStriker.BallsLeft > 0 )
					{
						if ( !ball.LastStriker.DidHitOwnBall )
							ball.LastStriker.Foul( FoulReason.HitOtherBall );
					}
					else
					{
						ball.LastStriker.DidHitOwnBall = true;
					}
				}
				else if ( other.Type != ball.LastStriker.BallType )
				{
					if ( !ball.LastStriker.DidHitOwnBall )
						ball.LastStriker.Foul( FoulReason.HitOtherBall );
				}
				else if ( ball.LastStriker.FoulReason == FoulReason.None )
				{
					ball.LastStriker.DidHitOwnBall = true;
				}
			}
		}

		public override void OnSecond()
		{
			if ( Host.IsClient ) return;

			var timeLeft = MathF.Max( PlayerTurnEndTime, 0f );
			var currentPlayer = Game.Instance.CurrentPlayer;

			if ( !currentPlayer.IsValid() )
				return;

			if ( currentPlayer.HasStruckWhiteBall )
				return;

			TimeLeftSeconds = timeLeft.CeilToInt();

			if ( timeLeft <= 4f && ClockTickingSound == null )
			{
				ClockTickingSound = currentPlayer.PlaySound( "clock-ticking" );
				ClockTickingSound.Value.SetVolume( 0.5f );
			}

			if ( timeLeft <= 0f )
			{
				EndTurn();
			}
		}

		public override void OnTick()
		{
			if ( Host.IsServer && Game.Instance != null && !IsGameOver )
			{
				var currentPlayer = Game.Instance.CurrentPlayer;

				if ( currentPlayer != null && currentPlayer.IsValid() && currentPlayer.HasStruckWhiteBall )
					CheckForStoppedBalls();
			}

			base.OnTick();
		}

		protected override void OnStart()
		{
			if ( Host.IsServer )
			{
				Game.Instance.RespawnAllBalls();

				var potentials = new List<Player>();
				var players = Client.All.Select( ( client ) => client.Pawn as Player );

				foreach ( var player in players )
					potentials.Add( player );

				var previousWinner = Game.Instance.PreviousWinner;
				var previousLoser = Game.Instance.PreviousLoser;

				if ( previousLoser.IsValid() )
				{
					if ( potentials.Count > 2 )
					{
						// Winner stays on, don't let losers play twice.
						potentials.Remove( previousLoser );
					}
				}

				var playerOne = previousWinner;

				if ( !playerOne.IsValid()) 
					playerOne = potentials[Rand.Int( 0, potentials.Count - 1 )];

				potentials.Remove( playerOne );

				var playerTwo = playerOne;
				
				if ( potentials.Count > 0 )
					playerTwo = potentials[Rand.Int( 0, potentials.Count - 1 )];

				potentials.Remove( playerTwo );

				AddPlayer( playerOne );
				AddPlayer( playerTwo );

				playerOne.StartPlaying();
				playerTwo.StartPlaying();

				Game.Instance.PlayerOne = playerOne;
				Game.Instance.PlayerTwo = playerTwo;

				// Everyone else is a spectator.
				potentials.ForEach( ( player ) =>
				{
					player.MakeSpectator( true );
					Spectators.Add( player );
				} );

				StartGame();
			}
		}

		protected override void OnFinish()
		{
			if ( Host.IsServer )
			{
				Game.Instance.PotHistory.Clear();

				var playerOne = Game.Instance.PlayerOne;
				var playerTwo = Game.Instance.PlayerTwo;

				playerOne?.MakeSpectator( true );
				playerTwo?.MakeSpectator( true );

				Spectators.Clear();
			}
		}

		private void StartGame()
		{
			var playerOne = Game.Instance.PlayerOne;
			var playerTwo = Game.Instance.PlayerTwo;

			if ( Rand.Float( 1f ) >= 0.5f )
				playerOne.StartTurn();
			else
				playerTwo.StartTurn();

			// We always start by letting the player choose the white ball location.
			Game.Instance.CurrentPlayer.StartPlacingWhiteBall();

			PlayerTurnEndTime = Game.TurnTime;

			if ( Game.Rules.IsRanked )
				GameServices.StartGame();
		}

		private void DoPlayerPotBall( Player player, PoolBall ball, BallPotType type )
		{
			player.DidPotBall = true;

			Game.Instance.PotHistory.Add( new PotHistoryItem
			{
				Type = ball.Type,
				Number = ball.Number
			} );

			if ( Game.Rules.IsRanked )
				GameServices.RecordEvent( player.Client, $"Potted {ball.Number} ({ball.Type})", 1 );

			if ( type == BallPotType.Normal )
				Game.Instance.AddToast( To.Everyone, player, $"{ player.Client.Name } has potted a ball", ball.GetIconClass() );
			else if ( type == BallPotType.Claim )
				Game.Instance.AddToast( To.Everyone, player, $"{ player.Client.Name } has claimed { ball.Type }", ball.GetIconClass() );

			var owner = Game.Instance.GetBallPlayer( ball );

			if ( owner != null && owner.IsValid() )
				owner.Score++;
		}

		private async void DoPlayerWin( Player winner )
		{
			if ( IsGameOver ) return;

			IsGameOver = true;

			var client = winner.Client;

			Game.Instance.AddToast( To.Everyone, winner, $"{ client.Name } has won the game", "wins" );

			var loser = Game.Instance.GetOtherPlayer( winner );

			if ( Game.Rules.IsRanked )
			{
				winner.Client.SetGameResult( GameplayResult.Win, winner.Score );
				loser.Client.SetGameResult( GameplayResult.Lose, loser.Score );
			}

			foreach ( var c in Entity.All.OfType<Player>() )
			{
				c.SendSound( To.Single( c ), c == loser ? "lose-game" : "win-game" );
			}

			Game.Instance.PreviousWinner = winner;
			Game.Instance.PreviousLoser = loser;

			if ( Game.Rules.IsRanked )
			{
				await GameServices.EndGameAsync();
				await winner.Elo.Update( winner.Client );
				await loser.Elo.Update( loser.Client );
			}

			Game.Instance.ShowWinSummary( To.Single( winner ), EloOutcome.Win, loser, winner.Elo.Rating, winner.Elo.Delta );
			Game.Instance.ShowWinSummary( To.Single( loser ), EloOutcome.Loss, winner, loser.Elo.Rating, loser.Elo.Delta );

			Game.Instance.ChangeRound( new StatsRound() );
		}

		private PoolBall FindBallLikelyToPot()
		{
			var potentials = Game.Instance.AllBalls;
			var pockets = Entity.All.OfType<TriggerBallPocket>();

			foreach ( var ball in potentials )
			{
				if ( ball.PhysicsBody.Velocity.Length < 2f || ball.IsAnimating )
					continue;

				var fromTransform = ball.PhysicsBody.Transform;
				var toTransform = ball.PhysicsBody.Transform;
				toTransform.Position = ball.Position + ball.PhysicsBody.Velocity * 3f;

				var sweep = Trace.Sweep( ball.PhysicsBody, fromTransform, toTransform )
					.Ignore( ball )
					.Run();

				if ( sweep.Entity is PoolBall )
					continue;

				foreach ( var pocket in pockets )
				{
					if ( pocket.Position.Distance( sweep.EndPosition ) <= 5f )
						return ball;

					if ( ball.Position.Distance( pocket.Position ) <= 5f )
						return ball;
				}
			}

			return null;
		}

		private bool ShouldIncreaseTimeScale()
		{
			var currentPlayer = Game.Instance.CurrentPlayer;

			if ( currentPlayer.TimeSinceWhiteStruck >= 7f )
				return true;

			if ( currentPlayer.TimeSinceWhiteStruck >= 4f && !BallLikelyToPot.IsValid() )
				return true;

			return false;
		}

		private void EndTurn()
		{
			var currentPlayer = Game.Instance.CurrentPlayer;

			Game.Instance.AllBalls.ForEach( ( ball ) =>
			{
				ball.PhysicsBody.AngularVelocity = Vector3.Zero;
				ball.PhysicsBody.Velocity = Vector3.Zero;
				ball.PhysicsBody.ClearForces();
			} );

			Game.Instance.Cue.Reset();

			var didHitAnyBall = currentPlayer.DidPotBall;

			if ( !didHitAnyBall )
			{
				foreach ( var ball in Game.Instance.AllBalls )
				{
					if ( ball.Type != PoolBallType.White && ball.LastStriker == currentPlayer )
					{
						didHitAnyBall = true;
						break;
					}
				}
			}

			foreach ( var ball in Game.Instance.AllBalls )
				ball.ResetLastStriker();

			if ( !didHitAnyBall )
				currentPlayer.Foul( FoulReason.HitNothing );

			if ( currentPlayer.IsPlacingWhiteBall )
				currentPlayer.StopPlacingWhiteBall();

			var otherPlayer = Game.Instance.GetOtherPlayer( currentPlayer );
			var blackBall = Game.Instance.BlackBall;

			if ( blackBall == null || !blackBall.IsValid() )
			{
				if ( currentPlayer.FoulReason == FoulReason.None )
				{
					if ( currentPlayer.BallsLeft == 0 )
						DoPlayerWin( currentPlayer );
					else
						DoPlayerWin( otherPlayer );
				}
				else
				{
					DoPlayerWin( otherPlayer );
				}
			}
			else
			{
				if ( !currentPlayer.HasSecondShot )
				{
					currentPlayer.FinishTurn();
					otherPlayer.StartTurn( currentPlayer.FoulReason != FoulReason.None );
				}
				else
				{
					currentPlayer.StartTurn( false, false );
				}
			}

			if ( ClockTickingSound != null )
			{
				ClockTickingSound.Value.Stop();
				ClockTickingSound = null;
			}

			Game.Instance.IsFastForwarding = false;

			PlayerTurnEndTime = Game.TurnTime;
			DidClaimThisTurn = false;
			BallLikelyToPot = null;
		}

		private void CheckForStoppedBalls()
		{
			var currentPlayer = Game.Instance.CurrentPlayer;

			if ( currentPlayer.TimeSinceWhiteStruck >= 2f && !BallLikelyToPot.IsValid() )
			{
				BallLikelyToPot = FindBallLikelyToPot();

				if ( BallLikelyToPot.IsValid() )
					currentPlayer.PlaySound( $"gasp-{Rand.Int( 1, 2 )}" );
			}

			if ( ShouldIncreaseTimeScale() && !Game.Instance.IsFastForwarding )
			{
				if ( !HasPlayedFastForwardSound )
				{
					// Only play this sound once per game because it's annoying.
					HasPlayedFastForwardSound = true;
					currentPlayer.PlaySound( "fast-forward" ).SetVolume( 0.05f );
				}

				Game.Instance.IsFastForwarding = true;
			}

			// Now check if all balls are essentially still.
			foreach ( var ball in Game.Instance.AllBalls )
			{
				if ( !ball.PhysicsBody.Velocity.IsNearlyZero( 0.2f ) )
					return;

				if ( ball.IsAnimating )
					return;
			}

			EndTurn();
		}
	}
}
