using UnityEngine;

namespace Temnorid.Arena
{
	/// <summary>How a class attacks.</summary>
	public enum AttackKind
	{
		/// <summary>A sword swing that hurts everything in front.</summary>
		Melee,
		/// <summary>A fast, straight arrow.</summary>
		Arrow,
		/// <summary>A slower, harder fireball.</summary>
		Fireball,
	}

	/// <summary>The numbers that make a class what it is. Everything else about a fighter is the same.</summary>
	public sealed class ClassDefinition
	{
		public HeroClass Class;
		public string Name;
		public string Description;
		public AttackKind Kind;
		public float Health;
		public float Damage;
		public float Cooldown;
		/// <summary>How far the attack reaches, in metres.</summary>
		public float Range;
		public float ProjectileSpeed;
		/// <summary>A bot of this class backs away when its target is closer than this (0 for melee).</summary>
		public float MinDistance;
		public float BotSpeed;
		public Color Tint;
	}

	/// <summary>The three classes. Easy to change: this is the whole balance of the game.</summary>
	public static class Classes
	{
		public static readonly ClassDefinition Warrior = new ClassDefinition
		{
			Class = HeroClass.Warrior,
			Name = "Warrior",
			Description = "Strong and tough. A sword that hits everything in front of you.",
			Kind = AttackKind.Melee,
			Health = 225f,
			Damage = 22f,
			Cooldown = 0.65f,
			Range = 3.0f,
			BotSpeed = 5.2f,
			Tint = new Color(0.85f, 0.25f, 0.2f),
		};

		public static readonly ClassDefinition Archer = new ClassDefinition
		{
			Class = HeroClass.Archer,
			Name = "Archer",
			Description = "Quick and far-reaching. Fast arrows, but not much health.",
			Kind = AttackKind.Arrow,
			Health = 170f,
			Damage = 17f,
			Cooldown = 0.7f,
			Range = 30f,
			ProjectileSpeed = 32f,
			MinDistance = 7f,
			BotSpeed = 5.6f,
			Tint = new Color(0.25f, 0.8f, 0.35f),
		};

		public static readonly ClassDefinition Mage = new ClassDefinition
		{
			Class = HeroClass.Mage,
			Name = "Mage",
			Description = "Slow, heavy fireballs. Hard to aim, hurts a lot.",
			Kind = AttackKind.Fireball,
			Health = 185f,
			Damage = 28f,
			Cooldown = 1.0f,
			Range = 26f,
			ProjectileSpeed = 18f,
			MinDistance = 8f,
			BotSpeed = 5.0f,
			Tint = new Color(0.4f, 0.55f, 1f),
		};

		public static ClassDefinition Get(HeroClass fighterClass)
		{
			switch (fighterClass)
			{
				case HeroClass.Warrior: return Warrior;
				case HeroClass.Archer: return Archer;
				default: return Mage;
			}
		}
	}
}
