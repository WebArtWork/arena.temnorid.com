using ITKamianets.Engine.AI;
using ITKamianets.Engine.Character;
using ITKamianets.Engine.CharacterView;
using ITKamianets.Engine.Combat;
using ITKamianets.Engine.Feedback;
using ITKamianets.Engine.Net;
using UnityEngine;
using UnityEngine.AI;

namespace Temnorid.Arena
{
	/// <summary>Who drives a fighter on this machine.</summary>
	public enum FighterControl
	{
		/// <summary>The AI (offline, and on the host for every fighter nobody plays).</summary>
		Bot,
		/// <summary>The person at this machine.</summary>
		Player,
		/// <summary>Only shown here from what another machine publishes (online).</summary>
		Remote,
	}

	/// <summary>
	/// One fighter of the arena. The same object is a bot (a NavMesh agent driven by <see cref="AiAgent"/>) or the
	/// player (a character driven by the Character View controls); the game decides which when the match starts.
	/// Online it can also be a remote fighter, shown from the poses and states other machines publish.
	/// Whatever drives it, an attack goes through <see cref="TryAttack"/>, and what it does depends on the class.
	/// </summary>
	[RequireComponent(typeof(Health), typeof(CharacterController), typeof(NavMeshAgent))]
	[DisallowMultipleComponent]
	public sealed class Fighter : MonoBehaviour
	{
		public const ulong NoOwner = ulong.MaxValue;

		public HeroClass Class;
		public string DisplayName = "Fighter";

		[HideInInspector] public int Index;
		[HideInInspector] public float RespawnAt;

		public ClassDefinition Definition => Classes.Get(Class);
		public Health Health { get; private set; }
		public bool IsPlayer { get; private set; }
		public bool IsDown { get; private set; }
		public FighterControl Control { get; private set; } = FighterControl.Bot;

		/// <summary>Online: the person who plays this fighter, or <see cref="NoOwner"/> for a bot.</summary>
		public ulong OwnerId { get; private set; } = NoOwner;

		/// <summary>How many attacks this fighter has made (wraps at 8 on the wire), so other machines can show them.</summary>
		public int AttackCount { get; private set; }

		/// <summary>The name a bot has when nobody plays this fighter.</summary>
		public string BotName { get; private set; }

		/// <summary>
		/// Online, on a client: this person's attacks are requests. They look the same but do no damage here; a hit is
		/// raised as <see cref="HitRequested"/> for the host to check.
		/// </summary>
		public bool HitsAreRequests;

		/// <summary>This fighter's attack touched the second fighter on this machine.</summary>
		public event System.Action<Fighter, Fighter> HitRequested;

		public Vector3 Velocity => _animation != null ? _animation.Velocity : Vector3.zero;

		/// <summary>Where projectiles leave and where a bot aims for: about chest height.</summary>
		public Vector3 Chest => transform.position + Vector3.up * 1.3f;

		public RemoteBody Remote => _remote;

		CharacterController _controller;
		NavMeshAgent _nav;
		AiAgent _ai;
		AiTarget _marker;
		CharacterMotor _motor;
		CharacterViewControls _controls;
		PlayerControl _playerControl;
		FighterAnimation _animation;
		HeroBadge _badge;
		RemoteBody _remote;
		CapsuleCollider _proxy;
		float _nextAttack;
		bool _held;

		void Awake()
		{
			BotName = DisplayName;
			Health = GetComponent<Health>();
			_controller = GetComponent<CharacterController>();
			_nav = GetComponent<NavMeshAgent>();
			_ai = GetComponent<AiAgent>();
			_marker = GetComponent<AiTarget>();
			_motor = GetComponent<CharacterMotor>();
			_controls = GetComponent<CharacterViewControls>();
			_playerControl = GetComponent<PlayerControl>();
			_animation = GetComponent<FighterAnimation>();
			_badge = GetComponent<HeroBadge>();
			if (_ai != null) _ai.AttackRequested += OnBotAttack;
			Health.Damaged += (h, info) => _animation?.PlayHit();
		}

		/// <summary>Drive this fighter with the AI.</summary>
		public void ConfigureAsBot()
		{
			var wasRemote = Control == FighterControl.Remote;
			Control = FighterControl.Bot;
			IsPlayer = false;
			OwnerId = NoOwner;
			HitsAreRequests = false;
			if (_remote != null) _remote.enabled = false;
			if (_proxy != null) _proxy.enabled = false;
			if (_playerControl != null) _playerControl.enabled = false;
			if (_controls != null) _controls.enabled = false;
			if (_motor != null) _motor.enabled = false;
			if (wasRemote && !IsDown) _controller.enabled = true;
			_nav.enabled = true;
			_ai.enabled = !_held;
		}

		/// <summary>Drive this fighter with the keyboard and mouse, seen through <paramref name="camera"/>.</summary>
		public void ConfigureAsPlayer(CharacterViewCamera camera)
		{
			Control = FighterControl.Player;
			IsPlayer = true;
			if (_remote != null) _remote.enabled = false;
			if (_proxy != null) _proxy.enabled = false;
			_controller.enabled = true;
			_ai.enabled = false;
			_nav.enabled = false;
			_motor.enabled = true;
			_controls.enabled = true;
			_controls.FaceCamera = true; // a shooter: you face where you look and strafe
			_playerControl.enabled = true;
			if (camera != null) camera.Target = _motor;
		}

		/// <summary>Online: show this fighter from what another machine says. <paramref name="owner"/> is the person, or <see cref="NoOwner"/> for a host bot.</summary>
		public void ConfigureAsRemote(ulong owner)
		{
			Control = FighterControl.Remote;
			IsPlayer = false;
			OwnerId = owner;
			HitsAreRequests = false;
			if (_playerControl != null) _playerControl.enabled = false;
			if (_controls != null) _controls.enabled = false;
			if (_motor != null) _motor.enabled = false;
			_ai.enabled = false;
			_nav.enabled = false;
			_controller.enabled = false;
			if (_remote == null) _remote = gameObject.AddComponent<RemoteBody>();
			_remote.enabled = true;
			if (_proxy == null)
			{
				// Something solid for attacks to hit, now that the character controller is off.
				_proxy = gameObject.AddComponent<CapsuleCollider>();
				_proxy.radius = 0.4f;
				_proxy.height = 1.8f;
				_proxy.center = new Vector3(0f, 0.9f, 0f);
			}
			_proxy.enabled = !IsDown;
		}

		public void SetOwner(ulong owner) => OwnerId = owner;

		/// <summary>Online, host: a bot waits (before the match, between periods). It stays where it is.</summary>
		public bool Held
		{
			get => _held;
			set
			{
				_held = value;
				if (Control != FighterControl.Bot || _ai == null) return;
				_ai.enabled = !value && !IsDown;
				if (_nav.enabled && _nav.isOnNavMesh) _nav.isStopped = value;
				if (!value && !IsDown) _ai.ResetMind();
			}
		}

		public void SetHero(bool hero)
		{
			if (_badge != null) _badge.SetHero(hero);
		}

		// ---- attacking ----

		/// <summary>Attacks along <paramref name="direction"/> if the weapon is ready. Returns whether it attacked.</summary>
		public bool TryAttack(Vector3 direction)
		{
			if (IsDown || Time.time < _nextAttack) return false;
			var definition = Definition;
			_nextAttack = Time.time + definition.Cooldown;
			AttackCount++;
			var team = Health.Team;
			var amount = HitsAreRequests ? 0f : definition.Damage;
			var info = new DamageInfo(amount, gameObject, Chest, definition.Kind.ToString());

			if (definition.Kind == AttackKind.Melee)
			{
				direction.y = 0f;
				if (direction.sqrMagnitude < 0.001f) direction = transform.forward;
				direction.Normalize();
				if (HitsAreRequests)
				{
					foreach (var health in CombatQueries.FindInCone(transform.position, direction, definition.Range, 70f, team).ToArray())
						RequestHit(health);
				}
				else
				{
					CombatQueries.DamageInCone(transform.position, direction, definition.Range, 70f, team, info);
				}
				MeleeVisual(direction);
			}
			else
			{
				direction.Normalize();
				var projectile = LaunchVisual(direction, info);
				if (HitsAreRequests) projectile.Hit += (p, health) => RequestHit(health);
			}

			_animation?.PlayAttack(definition.Kind);
			return true;
		}

		void RequestHit(Health health)
		{
			var other = health != null ? health.GetComponentInParent<Fighter>() : null;
			if (other != null && other != this && !other.IsDown) HitRequested?.Invoke(this, other);
		}

		void MeleeVisual(Vector3 direction)
		{
			var definition = Definition;
			Fx.Pulse(PrimitiveType.Cube, Chest + direction * 1.4f, Quaternion.LookRotation(direction) * Quaternion.Euler(0f, 0f, 30f),
				new Vector3(0.3f, 0.06f, 0.5f), new Vector3(2.8f, 0.06f, 0.5f), definition.Tint, 0.14f);
		}

		Projectile LaunchVisual(Vector3 direction, in DamageInfo info)
		{
			var definition = Definition;
			var arrow = definition.Kind == AttackKind.Arrow;
			var origin = Chest + direction * 0.7f;
			var projectile = Projectile.Launch(origin, direction, definition.ProjectileSpeed, definition.Range, arrow ? 0.15f : 0.35f, Health.Team, info,
				arrow ? new Color(0.95f, 0.95f, 0.8f) : new Color(1f, 0.5f, 0.15f));
			Fx.Pulse(PrimitiveType.Sphere, origin, Quaternion.identity, Vector3.one * 0.15f, Vector3.one * 0.7f, definition.Tint, 0.12f);
			return projectile;
		}

		/// <summary>Online: another machine's fighter attacked. Looks like the real thing and hurts nobody here.</summary>
		public void ShowRemoteAttack()
		{
			if (IsDown) return;
			var definition = Definition;
			var direction = transform.forward;
			if (definition.Kind == AttackKind.Melee) MeleeVisual(direction);
			else LaunchVisual(direction, new DamageInfo(0f, gameObject, Chest, definition.Kind.ToString()));
			_animation?.PlayAttack(definition.Kind);
		}

		void OnBotAttack(AiAgent agent, Transform target)
		{
			var other = target.GetComponent<Fighter>();
			var aim = target.position + Vector3.up * 1.2f;

			// Ranged bots lead a moving target a little, and are never quite perfect.
			if (Definition.Kind != AttackKind.Melee && other != null)
			{
				var distance = Vector3.Distance(Chest, aim);
				aim += other.Velocity * (distance / Mathf.Max(1f, Definition.ProjectileSpeed)) * 0.8f;
			}
			var direction = aim - Chest;
			direction = Quaternion.Euler(0f, Random.Range(-3.5f, 3.5f), 0f) * direction;
			TryAttack(direction);
		}

		// ---- falling and getting up ----

		/// <summary>Defeated: lie down and wait to be put back by the game.</summary>
		public void GoDown()
		{
			IsDown = true;
			if (_marker != null) _marker.Alive = false;
			if (_ai != null) _ai.Stop();
			_nav.enabled = false;
			_controller.enabled = false;
			if (_proxy != null) _proxy.enabled = false;
			if (_motor != null) _motor.enabled = false;
			if (_controls != null) _controls.enabled = false;
			if (_playerControl != null) _playerControl.enabled = false;
			_animation?.SetDead(true);
		}

		/// <summary>Back on their feet at full health at <paramref name="position"/>, facing <paramref name="rotation"/>.</summary>
		public void GetUp(Vector3 position, Quaternion rotation)
		{
			IsDown = false;
			Health.Restore();
			_nextAttack = Time.time + 0.5f;

			_controller.enabled = false;
			transform.SetPositionAndRotation(position, rotation);
			_controller.enabled = Control != FighterControl.Remote;
			_animation?.Snap();
			_animation?.SetDead(false);
			if (_marker != null) _marker.Alive = true;

			if (Control == FighterControl.Remote)
			{
				if (_proxy != null) _proxy.enabled = true;
			}
			else if (IsPlayer)
			{
				_motor.enabled = true;
				_controls.enabled = true;
				_playerControl.enabled = true;
				_motor.Teleport(position, rotation.eulerAngles.y);
			}
			else
			{
				_nav.enabled = true;
				_nav.Warp(position);
				_ai.enabled = !_held;
				_ai.ResetMind();
				if (_held && _nav.isOnNavMesh) _nav.isStopped = true;
			}
		}
	}
}
