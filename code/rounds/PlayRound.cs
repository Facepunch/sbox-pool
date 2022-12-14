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

			var playerOne = PoolGame.Entity.PlayerOne;
			var playerTwo = PoolGame.Entity.PlayerTwo;

			if ( player == playerOne || player == playerTwo )
			{
				/*
				if ( Pool.Rules.IsRanked )
					GameServices.AbandonGame( true );
				*/

				PoolGame.Entity.ChangeRound( new StatsRound() );
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
			if ( Game.IsServer )
			{
				ball.PlaySound( $"ball-pocket-{Game.Random.Int( 1, 2 )}" );

				if ( BallLikelyToPot == ball )
				{
					// We don't wanna follow this ball anymore.
					BallLikelyToPot = null;
				}

				if ( ball.LastStriker == null || !ball.LastStriker.IsValid() )
				{
					if ( ball.Type == PoolBallType.White )
					{
						_ = PoolGame.Entity.RespawnBallAsync( ball, true );
					}
					else if ( ball.Type == PoolBallType.Black )
					{
						_ = PoolGame.Entity.RespawnBallAsync( ball, true );
					}
					else
					{
						var player = PoolGame.Entity.GetBallPlayer( ball );

						if ( player != null && player.IsValid() )
						{
							var currentPlayer = PoolGame.Entity.CurrentPlayer;

							if ( currentPlayer == player )
								player.HasSecondShot = true;

							DoPlayerPotBall( currentPlayer, ball, BallPotType.Silent );
						}

						_ = PoolGame.Entity.RemoveBallAsync( ball, true );
					}

					return;
				}

				if ( ball.Type == PoolBallType.White )
				{
					ball.LastStriker.Foul( FoulReason.PotWhiteBall );
					_ = PoolGame.Entity.RespawnBallAsync( ball, true );
				}
				else if ( ball.Type == ball.LastStriker.BallType )
				{
					if ( PoolGame.Entity.CurrentPlayer == ball.LastStriker )
					{
						ball.LastStriker.HasSecondShot = true;
						ball.LastStriker.DidHitOwnBall = true;
					}

					DoPlayerPotBall( ball.LastStriker, ball, BallPotType.Normal );

					_ = PoolGame.Entity.RemoveBallAsync( ball, true );
				}
				else if ( ball.Type == PoolBallType.Black )
				{
					DoPlayerPotBall( ball.LastStriker, ball, BallPotType.Normal );

					_ = PoolGame.Entity.RemoveBallAsync( ball, true );
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

						var otherPlayer = PoolGame.Entity.GetOtherPlayer( ball.LastStriker );
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

					_ = PoolGame.Entity.RemoveBallAsync( ball, true );
				}
			}
		}

		public override void OnBallHitOtherBall( PoolBall ball, PoolBall other )
		{
			// Is this the first ball this striker has hit?
			if ( Game.IsServer && ball.Type == PoolBallType.White )
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
			if ( Game.IsClient ) return;

			var timeLeft = MathF.Max( PlayerTurnEndTime, 0f );
			var currentPlayer = PoolGame.Entity.CurrentPlayer;

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
			if ( Game.IsServer && PoolGame.Entity != null && !IsGameOver )
			{
				var currentPlayer = PoolGame.Entity.CurrentPlayer;

				if ( currentPlayer != null && currentPlayer.IsValid() && currentPlayer.HasStruckWhiteBall )
					CheckForStoppedBalls();
			}

			base.OnTick();
		}

		protected override void OnStart()
		{
			if ( Game.IsServer )
			{
				PoolGame.Entity.RespawnAllBalls();

				var potentials = new List<Player>();
				var players = Game.Clients.Select( ( client ) => client.Pawn as Player );

				foreach ( var player in players )
					potentials.Add( player );

				var previousWinner = PoolGame.Entity.PreviousWinner;
				var previousLoser = PoolGame.Entity.PreviousLoser;

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
					playerOne = potentials[Game.Random.Int( 0, potentials.Count - 1 )];

				potentials.Remove( playerOne );

				var playerTwo = playerOne;
				
				if ( potentials.Count > 0 )
					playerTwo = potentials[Game.Random.Int( 0, potentials.Count - 1 )];

				potentials.Remove( playerTwo );

				AddPlayer( playerOne );
				AddPlayer( playerTwo );

				playerOne.StartPlaying();
				playerTwo.StartPlaying();

				PoolGame.Entity.PlayerOne = playerOne;
				PoolGame.Entity.PlayerTwo = playerTwo;

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
			if ( Game.IsServer )
			{
				PoolGame.Entity.PotHistory.Clear();

				var playerOne = PoolGame.Entity.PlayerOne;
				var playerTwo = PoolGame.Entity.PlayerTwo;

				playerOne?.MakeSpectator( true );
				playerTwo?.MakeSpectator( true );

				Spectators.Clear();
			}
		}

		private void StartGame()
		{
			var playerOne = PoolGame.Entity.PlayerOne;
			var playerTwo = PoolGame.Entity.PlayerTwo;

			if ( Game.Random.Float( 1f ) >= 0.5f )
				playerOne.StartTurn();
			else
				playerTwo.StartTurn();

			// We always start by letting the player choose the white ball location.
			PoolGame.Entity.CurrentPlayer.StartPlacingWhiteBall();

			PlayerTurnEndTime = PoolGame.TurnTime;

			//if ( Pool.Rules.IsRanked )
				//GameServices.StartGame();

			PoolGame.Rules.Start();
		}

		private void DoPlayerPotBall( Player player, PoolBall ball, BallPotType type )
		{
			player.DidPotBall = true;

			PoolGame.Entity.PotHistory.Add( new PotHistoryItem
			{
				Type = ball.Type,
				Number = ball.Number
			} );

			//if ( Pool.Rules.IsRanked )
				//GameServices.RecordEvent( player.Client, $"Potted {ball.Number} ({ball.Type})", 1 );

			if ( type == BallPotType.Normal )
				PoolGame.Entity.AddToast( To.Everyone, player, $"{ player.Client.Name } has potted a ball", ball.GetIconClass() );
			else if ( type == BallPotType.Claim )
				PoolGame.Entity.AddToast( To.Everyone, player, $"{ player.Client.Name } has claimed { ball.Type }", ball.GetIconClass() );

			var owner = PoolGame.Entity.GetBallPlayer( ball );

			if ( owner != null && owner.IsValid() )
				owner.Score++;
		}

		private async void DoPlayerWin( Player winner )
		{
			if ( IsGameOver ) return;

			IsGameOver = true;

			var client = winner.Client;

			PoolGame.Entity.AddToast( To.Everyone, winner, $"{ client.Name } has won the game", "wins" );

			var loser = PoolGame.Entity.GetOtherPlayer( winner );

			/*
			if ( Pool.Rules.IsRanked )
			{
				winner.Client.SetGameResult( GameplayResult.Win, winner.Score );
				loser.Client.SetGameResult( GameplayResult.Lose, loser.Score );
			}
			*/

			foreach ( var c in Entity.All.OfType<Player>() )
			{
				c.SendSound( To.Single( c ), c == loser ? "lose-game" : "win-game" );
			}

			PoolGame.Entity.PreviousWinner = winner;
			PoolGame.Entity.PreviousLoser = loser;

			/*
			if ( Pool.Rules.IsRanked )
			{
				await GameServices.EndGameAsync();
				await winner.Elo.Update( winner.Client );
				await loser.Elo.Update( loser.Client );
			}
			*/

			PoolGame.Entity.ShowWinSummary( To.Single( winner ), EloOutcome.Win, loser, winner.Elo.Rating, winner.Elo.Delta );
			PoolGame.Entity.ShowWinSummary( To.Single( loser ), EloOutcome.Loss, winner, loser.Elo.Rating, loser.Elo.Delta );

			PoolGame.Entity.ChangeRound( new StatsRound() );

			PoolGame.Rules.Finish();
		}

		private PoolBall FindBallLikelyToPot()
		{
			var potentials = PoolGame.Entity.AllBalls;
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
			var currentPlayer = PoolGame.Entity.CurrentPlayer;

			if ( currentPlayer.TimeSinceWhiteStruck >= 7f )
				return true;

			if ( currentPlayer.TimeSinceWhiteStruck >= 4f && !BallLikelyToPot.IsValid() )
				return true;

			return false;
		}

		private void EndTurn()
		{
			var currentPlayer = PoolGame.Entity.CurrentPlayer;

			PoolGame.Entity.AllBalls.ForEach( ( ball ) =>
			{
				ball.PhysicsBody.AngularVelocity = Vector3.Zero;
				ball.PhysicsBody.Velocity = Vector3.Zero;
				ball.PhysicsBody.ClearForces();
			} );

			PoolGame.Entity.Cue.Reset();

			var didHitAnyBall = currentPlayer.DidPotBall;

			if ( !didHitAnyBall )
			{
				foreach ( var ball in PoolGame.Entity.AllBalls )
				{
					if ( ball.Type != PoolBallType.White && ball.LastStriker == currentPlayer )
					{
						didHitAnyBall = true;
						break;
					}
				}
			}

			foreach ( var ball in PoolGame.Entity.AllBalls )
				ball.ResetLastStriker();

			if ( !didHitAnyBall )
				currentPlayer.Foul( FoulReason.HitNothing );

			if ( currentPlayer.IsPlacingWhiteBall )
				currentPlayer.StopPlacingWhiteBall();

			var otherPlayer = PoolGame.Entity.GetOtherPlayer( currentPlayer );
			var blackBall = PoolGame.Entity.BlackBall;

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

			PoolGame.Entity.IsFastForwarding = false;

			PlayerTurnEndTime = PoolGame.TurnTime;
			DidClaimThisTurn = false;
			BallLikelyToPot = null;
		}

		private void CheckForStoppedBalls()
		{
			var currentPlayer = PoolGame.Entity.CurrentPlayer;

			if ( currentPlayer.TimeSinceWhiteStruck >= 2f && !BallLikelyToPot.IsValid() )
			{
				BallLikelyToPot = FindBallLikelyToPot();

				if ( BallLikelyToPot.IsValid() )
					currentPlayer.PlaySound( $"gasp-{Game.Random.Int( 1, 2 )}" );
			}

			if ( ShouldIncreaseTimeScale() && !PoolGame.Entity.IsFastForwarding )
			{
				if ( !HasPlayedFastForwardSound )
				{
					// Only play this sound once per game because it's annoying.
					HasPlayedFastForwardSound = true;
					currentPlayer.PlaySound( "fast-forward" ).SetVolume( 0.05f );
				}

				PoolGame.Entity.IsFastForwarding = true;
			}

			// Now check if all balls are essentially still.
			foreach ( var ball in PoolGame.Entity.AllBalls )
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
