using ITKamianets.Engine.AI;
using ITKamianets.Engine.Character;
using ITKamianets.Engine.CharacterView;
using ITKamianets.Engine.Combat;
using ITKamianets.Engine.Feedback;
using UnityEngine;
using UnityEngine.AI;

namespace Temnorid.Arena
{
	/// <summary>
	/// One fighter of the arena. The same object is a bot (a NavMesh agent driven by <see cref="AiAgent"/>) or the
	/// player (a character driven by the Character View controls); the game decides which when the match starts.
	/// Whatever drives it, an attack goes through <see cref="TryAttack"/>, and what it does depends on the class.
	/// </summary>
	[RequireComponent(typeof(Health), typeof(CharacterController), typeof(NavMeshAgent))]
	[DisallowMultipleComponent]
	public sealed class Fighter : MonoBehaviour
	{
		public HeroClass Class;
		public string DisplayName = "Fighter";

		[HideInInspector] public int Index;
		[HideInInspector] public float RespawnAt;

		public ClassDefinition Definition => Classes.Get(Class);
		public Health Health { get; private set; }
		public bool IsPlayer { get; private set; }
		public bool IsDown { get; private set; }
		public Vector3 Velocity => _animation != null ? _animation.Velocity : Vector3.zero;

		/// <summary>Where projectiles leave and where a bot aims for: about chest height.</summary>
		public Vector3 Chest => transform.position + Vector3.up * 1.3f;

		CharacterController _controller;
		NavMeshAgent _nav;
		AiAgent _ai;
		AiTarget _marker;
		CharacterMotor _motor;
		CharacterViewControls _controls;
		PlayerControl _playerControl;
		FighterAnimation _animation;
		HeroBadge _badge;
		float _nextAttack;

		void Awake()
		{
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
			IsPlayer = false;
			if (_playerControl != null) _playerControl.enabled = false;
			if (_controls != null) _controls.enabled = false;
			if (_motor != null) _motor.enabled = false;
			_nav.enabled = true;
			_ai.enabled = true;
		}

		/// <summary>Drive this fighter with the keyboard and mouse, seen through <paramref name="camera"/>.</summary>
		public void ConfigureAsPlayer(CharacterViewCamera camera)
		{
			IsPlayer = true;
			_ai.enabled = false;
			_nav.enabled = false;
			_motor.enabled = true;
			_controls.enabled = true;
			_controls.FaceCamera = true; // a shooter: you face where you look and strafe
			_playerControl.enabled = true;
			if (camera != null) camera.Target = _motor;
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
			var team = Health.Team;
			var info = new DamageInfo(definition.Damage, gameObject, Chest, definition.Kind.ToString());

			if (definition.Kind == AttackKind.Melee)
			{
				direction.y = 0f;
				if (direction.sqrMagnitude < 0.001f) direction = transform.forward;
				direction.Normalize();
				CombatQueries.DamageInCone(transform.position, direction, definition.Range, 70f, team, info);
				Fx.Pulse(PrimitiveType.Cube, Chest + direction * 1.4f, Quaternion.LookRotation(direction) * Quaternion.Euler(0f, 0f, 30f),
					new Vector3(0.3f, 0.06f, 0.5f), new Vector3(2.8f, 0.06f, 0.5f), definition.Tint, 0.14f);
			}
			else
			{
				direction.Normalize();
				var arrow = definition.Kind == AttackKind.Arrow;
				var origin = Chest + direction * 0.7f;
				Projectile.Launch(origin, direction, definition.ProjectileSpeed, definition.Range, arrow ? 0.15f : 0.35f, team, info,
					arrow ? new Color(0.95f, 0.95f, 0.8f) : new Color(1f, 0.5f, 0.15f));
				Fx.Pulse(PrimitiveType.Sphere, origin, Quaternion.identity, Vector3.one * 0.15f, Vector3.one * 0.7f, definition.Tint, 0.12f);
			}

			_animation?.PlayAttack(definition.Kind);
			return true;
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
			_controller.enabled = true;
			_animation?.Snap();
			_animation?.SetDead(false);
			if (_marker != null) _marker.Alive = true;

			if (IsPlayer)
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
				_ai.enabled = true;
				_ai.ResetMind();
			}
		}
	}
}
