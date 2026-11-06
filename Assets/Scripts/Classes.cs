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

	/// <summary>The six classes of a lord in Lords. Each belongs to one of the three <see cref="HeroClass"/> families that compete for the Hero title.</summary>
	public enum ClassId
	{
		Warrior,
		Knight,
		Archer,
		Mage,
		Rogue,
		Healer,
	}

	/// <summary>What a skill does when it is cast.</summary>
	public enum SkillEffect
	{
		/// <summary>A wide, hard swing in front: damage = the class's damage x <see cref="SkillDefinition.Power"/>.</summary>
		Strike,
		/// <summary>One projectile, harder than the basic attack.</summary>
		Bolt,
		/// <summary>Several projectiles in a fan.</summary>
		Volley,
		/// <summary>A blast around the caster.</summary>
		Nova,
		/// <summary>Heals the caster by <see cref="SkillDefinition.Power"/> of their maximum health.</summary>
		Heal,
		/// <summary>Takes <see cref="SkillDefinition.Power"/> off every hit taken, for the duration.</summary>
		Guard,
		/// <summary>Moves <see cref="SkillDefinition.Power"/> times faster, for the duration.</summary>
		Haste,
		/// <summary>Attacks faster: the basic attack's wait is multiplied by <see cref="SkillDefinition.Power"/> (0.5 = twice as fast), for the duration.</summary>
		Frenzy,
	}

	/// <summary>One skill of a class, on keys 1 to 4. Costs mana, then waits its cooldown.</summary>
	public sealed class SkillDefinition
	{
		public string Name;
		public string Description;
		public SkillEffect Effect;
		public float ManaCost;
		public float Cooldown;
		/// <summary>Damage multiplier, healed share, damage taken off, speed multiplier or attack-wait multiplier, by <see cref="Effect"/>.</summary>
		public float Power;
		/// <summary>Reach of a strike or a projectile, or the radius of a nova, in metres.</summary>
		public float Range;
		/// <summary>Half angle of a strike's cone in degrees, or the spread between a volley's projectiles.</summary>
		public float Angle;
		/// <summary>Seconds a guard, haste or frenzy lasts.</summary>
		public float Duration;
		/// <summary>Projectile speed in metres a second.</summary>
		public float Speed;
		/// <summary>How many projectiles a volley fires.</summary>
		public int Count = 1;
	}

	/// <summary>The numbers that make a class what it is. Everything else about a fighter is the same.</summary>
	public sealed class ClassDefinition
	{
		public ClassId Id;
		/// <summary>The family this class competes in for the Hero title.</summary>
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
		/// <summary>The mana pool. Basic attacks are free; skills spend it.</summary>
		public float Mana;
		/// <summary>Mana won back every second.</summary>
		public float ManaRegen;
		public SkillDefinition[] Skills;
	}

	/// <summary>
	/// The six classes, in the pattern of Lineage 2 and World of Warcraft: warriors and knights are tough with a small pool and slow
	/// recovery, their skills cheap; rogues and archers sit in the middle; healers and mages have the deepest pools and win mana back
	/// fastest, but their skills cost the most. Easy to change: this is the whole balance of the game.
	/// </summary>
	public static class Classes
	{
		public const int SkillSlots = 4;

		public static readonly ClassDefinition Warrior = new ClassDefinition
		{
			Id = ClassId.Warrior,
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
			Mana = 80f,
			ManaRegen = 3f,
			Skills = new[]
			{
				new SkillDefinition { Name = "Power Strike", Description = "A heavy blow in front.", Effect = SkillEffect.Strike, ManaCost = 12f, Cooldown = 3f, Power = 2.2f, Range = 3.4f, Angle = 55f },
				new SkillDefinition { Name = "Whirlwind", Description = "Hits everyone around you.", Effect = SkillEffect.Nova, ManaCost = 25f, Cooldown = 8f, Power = 1.5f, Range = 4.5f },
				new SkillDefinition { Name = "Battle Cry", Description = "Run faster for a while.", Effect = SkillEffect.Haste, ManaCost = 20f, Cooldown = 20f, Power = 1.35f, Duration = 8f },
				new SkillDefinition { Name = "Second Wind", Description = "Heals a third of your health.", Effect = SkillEffect.Heal, ManaCost = 30f, Cooldown = 30f, Power = 0.3f },
			},
		};

		public static readonly ClassDefinition Knight = new ClassDefinition
		{
			Id = ClassId.Knight,
			Class = HeroClass.Warrior,
			Name = "Knight",
			Description = "The toughest of all, with a shield and a prayer.",
			Kind = AttackKind.Melee,
			Health = 260f,
			Damage = 19f,
			Cooldown = 0.7f,
			Range = 2.8f,
			BotSpeed = 4.8f,
			Tint = new Color(0.8f, 0.78f, 0.9f),
			Mana = 100f,
			ManaRegen = 4f,
			Skills = new[]
			{
				new SkillDefinition { Name = "Shield Bash", Description = "A short, hard blow.", Effect = SkillEffect.Strike, ManaCost = 15f, Cooldown = 5f, Power = 1.6f, Range = 3.0f, Angle = 45f },
				new SkillDefinition { Name = "Holy Smite", Description = "A bolt of light.", Effect = SkillEffect.Bolt, ManaCost = 25f, Cooldown = 6f, Power = 2.0f, Range = 18f, Speed = 24f },
				new SkillDefinition { Name = "Shield Wall", Description = "Takes 60% off every hit.", Effect = SkillEffect.Guard, ManaCost = 30f, Cooldown = 25f, Power = 0.6f, Duration = 6f },
				new SkillDefinition { Name = "Lay on Hands", Description = "Heals half of your health.", Effect = SkillEffect.Heal, ManaCost = 50f, Cooldown = 40f, Power = 0.5f },
			},
		};

		public static readonly ClassDefinition Archer = new ClassDefinition
		{
			Id = ClassId.Archer,
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
			Mana = 120f,
			ManaRegen = 5f,
			Skills = new[]
			{
				new SkillDefinition { Name = "Power Shot", Description = "One arrow that hits very hard.", Effect = SkillEffect.Bolt, ManaCost = 18f, Cooldown = 4f, Power = 2.4f, Range = 40f, Speed = 45f },
				new SkillDefinition { Name = "Multishot", Description = "Three arrows in a fan.", Effect = SkillEffect.Volley, ManaCost = 28f, Cooldown = 7f, Power = 0.9f, Range = 30f, Speed = 36f, Angle = 12f, Count = 3 },
				new SkillDefinition { Name = "Rapid Fire", Description = "Shoot twice as fast.", Effect = SkillEffect.Frenzy, ManaCost = 30f, Cooldown = 25f, Power = 0.5f, Duration = 6f },
				new SkillDefinition { Name = "Disengage", Description = "A short burst of speed.", Effect = SkillEffect.Haste, ManaCost = 15f, Cooldown = 12f, Power = 1.5f, Duration = 3f },
			},
		};

		public static readonly ClassDefinition Mage = new ClassDefinition
		{
			Id = ClassId.Mage,
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
			Mana = 260f,
			ManaRegen = 10f,
			Skills = new[]
			{
				new SkillDefinition { Name = "Fire Blast", Description = "A fast, hard fireball.", Effect = SkillEffect.Bolt, ManaCost = 40f, Cooldown = 3f, Power = 2.2f, Range = 30f, Speed = 26f },
				new SkillDefinition { Name = "Frost Nova", Description = "A freezing blast around you.", Effect = SkillEffect.Nova, ManaCost = 70f, Cooldown = 10f, Power = 1.3f, Range = 6f },
				new SkillDefinition { Name = "Mana Shield", Description = "Takes half off every hit.", Effect = SkillEffect.Guard, ManaCost = 60f, Cooldown = 30f, Power = 0.5f, Duration = 8f },
				new SkillDefinition { Name = "Arcane Burst", Description = "A huge explosion around you.", Effect = SkillEffect.Nova, ManaCost = 120f, Cooldown = 20f, Power = 2.5f, Range = 8f },
			},
		};

		public static readonly ClassDefinition Rogue = new ClassDefinition
		{
			Id = ClassId.Rogue,
			Class = HeroClass.Archer,
			Name = "Scout",
			Description = "Fast and sneaky. Quick daggers and tricks to get away.",
			Kind = AttackKind.Melee,
			Health = 160f,
			Damage = 15f,
			Cooldown = 0.45f,
			Range = 2.4f,
			BotSpeed = 6.0f,
			Tint = new Color(0.55f, 0.35f, 0.7f),
			Mana = 110f,
			ManaRegen = 6f,
			Skills = new[]
			{
				new SkillDefinition { Name = "Backstab", Description = "A deadly stab in front.", Effect = SkillEffect.Strike, ManaCost = 20f, Cooldown = 5f, Power = 2.8f, Range = 2.6f, Angle = 40f },
				new SkillDefinition { Name = "Fan of Knives", Description = "Throws knives all around.", Effect = SkillEffect.Nova, ManaCost = 35f, Cooldown = 9f, Power = 1.2f, Range = 5f },
				new SkillDefinition { Name = "Sprint", Description = "Run much faster.", Effect = SkillEffect.Haste, ManaCost = 25f, Cooldown = 20f, Power = 1.7f, Duration = 5f },
				new SkillDefinition { Name = "Evasion", Description = "Takes 75% off every hit.", Effect = SkillEffect.Guard, ManaCost = 30f, Cooldown = 30f, Power = 0.75f, Duration = 3f },
			},
		};

		public static readonly ClassDefinition Healer = new ClassDefinition
		{
			Id = ClassId.Healer,
			Class = HeroClass.Mage,
			Name = "Healer",
			Description = "Heals and protects. A staff of light.",
			Kind = AttackKind.Fireball,
			Health = 175f,
			Damage = 14f,
			Cooldown = 0.8f,
			Range = 24f,
			ProjectileSpeed = 22f,
			MinDistance = 8f,
			BotSpeed = 5.0f,
			Tint = new Color(0.95f, 0.9f, 0.5f),
			Mana = 220f,
			ManaRegen = 9f,
			Skills = new[]
			{
				new SkillDefinition { Name = "Smite", Description = "A bolt of holy light.", Effect = SkillEffect.Bolt, ManaCost = 22f, Cooldown = 3f, Power = 1.8f, Range = 24f, Speed = 28f },
				new SkillDefinition { Name = "Heal", Description = "Heals 40% of your health.", Effect = SkillEffect.Heal, ManaCost = 55f, Cooldown = 5f, Power = 0.4f },
				new SkillDefinition { Name = "Holy Nova", Description = "Light bursts around you.", Effect = SkillEffect.Nova, ManaCost = 70f, Cooldown = 10f, Power = 1.2f, Range = 6f },
				new SkillDefinition { Name = "Blessing", Description = "Takes 40% off every hit.", Effect = SkillEffect.Guard, ManaCost = 50f, Cooldown = 30f, Power = 0.4f, Duration = 10f },
			},
		};

		/// <summary>All six, in the order the player cycles through them.</summary>
		public static readonly ClassDefinition[] All = { Warrior, Knight, Archer, Mage, Rogue, Healer };

		/// <summary>The family's own class (the warrior for the warriors, and so on).</summary>
		public static ClassDefinition Get(HeroClass fighterClass)
		{
			switch (fighterClass)
			{
				case HeroClass.Warrior: return Warrior;
				case HeroClass.Archer: return Archer;
				default: return Mage;
			}
		}

		public static ClassDefinition Get(ClassId id)
		{
			switch (id)
			{
				case ClassId.Knight: return Knight;
				case ClassId.Archer: return Archer;
				case ClassId.Mage: return Mage;
				case ClassId.Rogue: return Rogue;
				case ClassId.Healer: return Healer;
				default: return Warrior;
			}
		}

		/// <summary>The class for the id Lords saves (<c>warrior</c>, <c>knight</c>, <c>archer</c>, <c>mage</c>, <c>rogue</c>, <c>healer</c>), or false for an unknown one.</summary>
		public static bool TryFromLords(string id, out ClassId result)
		{
			switch ((id ?? "").Trim().ToLowerInvariant())
			{
				case "warrior": result = ClassId.Warrior; return true;
				case "knight": result = ClassId.Knight; return true;
				case "archer": result = ClassId.Archer; return true;
				case "mage": result = ClassId.Mage; return true;
				case "rogue": case "scout": result = ClassId.Rogue; return true;
				case "healer": result = ClassId.Healer; return true;
				default: result = ClassId.Warrior; return false;
			}
		}
	}
}
