using Sandbox;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Facepunch.Pool
{
	public struct PotHistoryItem
	{
		public PoolBallNumber Number;
		public PoolBallType Type;
	}

	partial class Game : GameManager
	{
		private PoolCamera Camera { get; set; }

		public TriggerWhiteArea WhiteArea { get; set; }
		public Player PreviousWinner { get; set; }
		public Player PreviousLoser { get; set; }
		public List<PoolBall> AllBalls { get; private set; }

		public static Game Instance => Current as Game;
		public static BaseGameRules Rules => Instance?.InternalGameRules;

		[Net] public PoolCue Cue { get; private set; }
		[Net, Change( nameof( OnRoundChanged ) )] public BaseRound Round { get; private set; }
		[Net] public PoolBall WhiteBall { get; set; }
		[Net] public PoolBall BlackBall { get; set; }
		[Net] public Player CurrentPlayer { get; set; }
		[Net] public Player PlayerOne { get; set; }
		[Net] public Player PlayerTwo { get; set; }
		[Net] public IList<PotHistoryItem> PotHistory { get; set; }
		[Net, Change] public bool IsFastForwarding { get; set; }

		[Net] private BaseGameRules InternalGameRules { get; set; }

		[ConVar.Replicated( "pool_turn_time" )]
		public static int TurnTime { get; set; } = 30;

		private RealTimeUntil NextSecondTime;
		private FastForward FastForwardHud;
		private WinSummary WinSummaryHud;

		public override void Spawn()
		{
			LoadGameRules( "rules_regular" );
			ChangeRound( new LobbyRound() );

			base.Spawn();
		}

		public override void ClientSpawn()
		{
			Local.Hud?.Delete( true );
			Local.Hud = new Hud();

			Camera = new();

			base.ClientSpawn();
		}

		public async Task RespawnBallAsync( PoolBall ball, bool shouldAnimate = false )
		{
			if ( shouldAnimate )
			{
				await ball.AnimateIntoPocket();
			}

			var entities = All.Where( ( e ) => e is PoolBallSpawn );

			foreach ( var entity in entities )
			{
				if ( entity is PoolBallSpawn spawner )
				{
					if ( spawner.Type == ball.Type && spawner.Number == ball.Number )
					{
						ball.Scale = 1f;
						ball.Position = spawner.Position;
						ball.RenderColor = ball.RenderColor.WithAlpha(1.0f);
						ball.PhysicsBody.AngularVelocity = Vector3.Zero;
						ball.PhysicsBody.Velocity = Vector3.Zero;
						ball.PhysicsBody.ClearForces();
						ball.ResetInterpolation();

						return;
					}
				}
			}
		}

		public async Task RemoveBallAsync( PoolBall ball, bool shouldAnimate = false )
		{
			if ( shouldAnimate )
				await ball.AnimateIntoPocket();

			AllBalls.Remove( ball );
			ball.Delete();
		}

		[ClientRpc]
		public void ShowWinSummary( EloOutcome outcome, Player opponent, int rating, int delta )
		{
			HideWinSummary();

			WinSummaryHud = Local.Hud.AddChild<WinSummary>();
			WinSummaryHud.Outcome = outcome;
			WinSummaryHud.Opponent = opponent;
			WinSummaryHud.Rating = rating;
			WinSummaryHud.Delta = delta;
		}

		[ClientRpc]
		public void HideWinSummary()
		{
			if ( WinSummaryHud != null )
			{
				WinSummaryHud.Delete();
				WinSummaryHud = null;
			}
		}

		[ClientRpc]
		public void AddToast( Player player, string text, string iconClass = "" )
		{
			if ( player == null )
			{
				Log.Warning( "Player was NULL in Game.AddToast!" );
				return;
			}

			ToastList.Current.AddItem( player, text, iconClass );
		}

		public void RemoveAllBalls()
		{
			if ( AllBalls != null )
			{
				foreach ( var entity in AllBalls )
					entity.Delete();

				AllBalls.Clear();
			}
			else
				AllBalls = new();
		}

		public Player GetBallPlayer( PoolBall ball )
		{
			if ( PlayerOne.BallType == ball.Type )
				return PlayerOne;
			else if ( PlayerTwo.BallType == ball.Type )
				return PlayerTwo;
			else
				return null;
		}

		public Player GetOtherPlayer( Player player )
		{
			if ( player == PlayerOne )
				return PlayerTwo;
			else
				return PlayerOne;
		}

		public void UpdatePotHistory()
		{
			if ( BallHistory.Current != null )
			{
				BallHistory.Current.Clear();

				foreach ( var item in PotHistory )
					BallHistory.Current.AddByType( item.Type, item.Number );
			}
		}

		public void RespawnAllBalls()
		{
			RemoveAllBalls();

			var entities = All.Where( ( e ) => e is PoolBallSpawn );
			var spawners = new List<Entity>();
			spawners.AddRange( entities );

			foreach ( var entity in spawners )
			{
				if ( entity is PoolBallSpawn spawner )
				{
					var ball = Rules.CreatePoolBall();

					ball.Position = spawner.Position;
					ball.Rotation = Rotation.LookAt( Vector3.Random );

					ball.SetType( spawner.Type, spawner.Number );

					if ( ball.Type == PoolBallType.White )
						WhiteBall = ball;
					else if ( ball.Type == PoolBallType.Black )
						BlackBall = ball;

					AllBalls.Add( ball );
				}
			}
		}

		public void ChangeRound(BaseRound round)
		{
			Assert.NotNull( round );

			Round?.Finish();
			Round = round;
			Round?.Start();
		}

		public override void DoPlayerNoclip( Client client )
		{
			// Do nothing. The player can't noclip in this mode.
		}

		public override void PostLevelLoaded()
		{
			if ( IsServer )
			{
				Cue = Rules.CreatePoolCue();
			}

			base.PostLevelLoaded();
		}

		public override void OnKilled( Entity entity )
		{
			if ( entity is Player player )
				Round?.OnPlayerKilled( player );

			base.OnKilled( entity );
		}

		public override void ClientDisconnect( Client client, NetworkDisconnectionReason reason )
		{
			Log.Info( client.Name + " left, checking minimum player count..." );

			Round?.OnPlayerLeave( client.Pawn as Player );

			base.ClientDisconnect( client, reason );
		}

		public override void ClientJoined( Client client )
		{
			var player = new Player();

			client.Pawn = player;

			Round?.OnPlayerJoin( player );

			base.ClientJoined( client );
		}

		public override void Simulate( Client client )
		{
			if ( Cue != null && Cue.IsValid() && Cue.Client == client )
				Cue.Simulate( client );

			base.Simulate( client );
		}

		private void OnIsFastForwardingChanged( bool oldValue, bool newValue )
		{
			if ( FastForwardHud != null )
			{
				FastForwardHud.Delete();
				FastForwardHud = null;
			}

			if ( newValue )
			{
				FastForwardHud = Local.Hud.AddChild<FastForward>();
			}
		}

		private void LoadGameRules( string rules )
		{
			InternalGameRules = TypeLibrary.Create<BaseGameRules>( rules, false ) ?? new RegularRules();
		}

		private void OnSecond()
		{
			CheckMinimumPlayers();
			Round?.OnSecond();
		}

		[Event.Client.Frame]
		private void OnFrame()
		{
			Camera?.Update();
		}

		[Event.Tick]
		private void Tick()
		{
			Round?.OnTick();

			Map.Physics.TimeScale = IsFastForwarding ? 5f : 1f;

			if ( NextSecondTime )
			{
				NextSecondTime = 1f;
				OnSecond();
			}

			if ( IsClient )
			{
				if ( WhiteArea == null )
				{
					WhiteArea = All.OfType<TriggerWhiteArea>().FirstOrDefault();

					if ( WhiteArea != null )
						WhiteArea.MakeAreaQuad();
				}

				if ( Time.Tick % 30 == 0 )
				{
					UpdatePotHistory();
				}
			}
		}

		private void OnRoundChanged( BaseRound oldRound, BaseRound newRound )
		{
			oldRound?.Finish();
			newRound?.Start();
		}

		private void CheckMinimumPlayers()
		{
			if ( Client.All.Count >= 2)
			{
				if ( Round is LobbyRound || Round == null )
				{
					ChangeRound( new PlayRound() );
				}
			}
			else if ( Round is not LobbyRound )
			{
				ChangeRound( new LobbyRound() );
			}
		}
	}
}
