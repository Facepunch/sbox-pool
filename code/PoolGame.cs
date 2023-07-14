using Sandbox;
using Sandbox.Diagnostics;
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

	public partial class PoolGame : GameManager
	{
		private PoolCamera Camera { get; set; }

		public TriggerWhiteArea WhiteArea { get; set; }
		public Player PreviousWinner { get; set; }
		public Player PreviousLoser { get; set; }
		public List<PoolBall> AllBalls { get; private set; }

		public static PoolGame Entity => Current as PoolGame;
		public static BaseGameRules Rules => Entity?.InternalGameRules;

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

		public PoolGame() : base()
		{
			
		}

		public override void ClientSpawn()
		{
			Game.RootPanel?.Delete( true );
			Game.RootPanel = new Hud();

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
				if ( entity is not PoolBallSpawn spawner )
				{
					continue;
				}

				if ( spawner.Type != ball.Type || spawner.Number != ball.Number )
				{
					continue;
				}

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

			WinSummaryHud = Game.RootPanel.AddChild<WinSummary>();
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
			if ( BallHistory.Current == null )
			{
				return;
			}

			BallHistory.Current.Clear();

			foreach ( var item in PotHistory )
				BallHistory.Current.AddByType( item.Type, item.Number );
		}

		public void RespawnAllBalls()
		{
			RemoveAllBalls();

			var entities = All.Where( ( e ) => e is PoolBallSpawn );
			var spawners = new List<Entity>();
			spawners.AddRange( entities );

			foreach ( var entity in spawners )
			{
				if ( entity is not PoolBallSpawn spawner )
				{
					continue;
				}

				var ball = Rules.CreatePoolBall();

				ball.Position = spawner.Position;
				ball.Rotation = Rotation.LookAt( Vector3.Random );

				ball.SetType( spawner.Type, spawner.Number );

				switch ( ball.Type )
				{
					case PoolBallType.White:
						WhiteBall = ball;
						break;
					case PoolBallType.Black:
						BlackBall = ball;
						break;
				}

				AllBalls.Add( ball );
			}
		}

		public void ChangeRound(BaseRound round)
		{
			Assert.NotNull( round );

			Round?.Finish();
			Round = round;
			Round?.Start();
		}

		public override void PostLevelLoaded()
		{
			LoadGameRules( "rules_regular" );
			ChangeRound( new LobbyRound() );

			Cue = Rules.CreatePoolCue();

			base.PostLevelLoaded();
		}

		public override void OnKilled( Entity entity )
		{
			if ( entity is Player player )
				Round?.OnPlayerKilled( player );

			base.OnKilled( entity );
		}

		public override void ClientDisconnect( IClient client, NetworkDisconnectionReason reason )
		{
			Log.Info( client.Name + " left, checking minimum player count..." );

			Round?.OnPlayerLeave( client.Pawn as Player );

			base.ClientDisconnect( client, reason );
		}

		public override void ClientJoined( IClient client )
		{
			var player = new Player();

			client.Pawn = player;

			Round?.OnPlayerJoin( player );

			base.ClientJoined( client );
		}

		public override void Simulate( IClient client )
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
				FastForwardHud = Game.RootPanel.AddChild<FastForward>();
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

		[GameEvent.Client.Frame]
		private void OnFrame()
		{
			Camera?.Update();
		}

		[GameEvent.Tick]
		private void Tick()
		{
			Round?.OnTick();

			Game.PhysicsWorld.TimeScale = IsFastForwarding ? 5f : 1f;

			if ( NextSecondTime )
			{
				NextSecondTime = 1f;
				OnSecond();
			}

			if ( !Game.IsClient )
			{
				return;
			}

			if ( WhiteArea == null )
			{
				WhiteArea = All.OfType<TriggerWhiteArea>().FirstOrDefault();

				if ( WhiteArea != null )
					WhiteArea.MakeAreaQuad();
			}

			if ( Time.Tick % 30 == 0 )
			{
				Log.Info( Round );
				UpdatePotHistory();
			}
		}

		private void OnRoundChanged( BaseRound oldRound, BaseRound newRound )
		{
			oldRound?.Finish();
			newRound?.Start();
		}

		private void CheckMinimumPlayers()
		{
			if ( Game.Clients.Count >= 2)
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
