using System.Collections;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Temnorid.Arena.Tests
{
	/// <summary>
	/// Measures the classes against each other: six bots (two of every class) fight whole periods at high speed, and the
	/// kills, deaths and Heroes of every class are written to <c>Builds/balance.txt</c>. Slow, so it only runs when asked for by name:
	/// <c>-testFilter Temnorid.Arena.Tests.BalanceTests.Bots_fight_whole_periods</c>.
	/// </summary>
	public class BalanceTests
	{
		const int Periods = 12;

		[UnityTest]
		[Explicit("Balance measurement: run by name")]
		[Timeout(1800000)]
		public IEnumerator Bots_fight_whole_periods()
		{
			ArenaGame.SaveRootOverride = Path.Combine(Path.GetTempPath(), "arena-balance-" + System.Guid.NewGuid());
			var report = new StringBuilder();
			var kills = new int[3];
			var deaths = new int[3];
			var heroes = new int[3];
			var total = 0;

			for (var run = 0; run < Periods; run++)
			{
				yield return SceneManager.LoadSceneAsync("Main");
				yield return null;
				var game = ArenaGame.Instance;
				game.SaveEnabled = false;
				game.StartMatch(HeroClass.Warrior, autopilot: true);
				Time.timeScale = 6f;
				var deadline = Time.realtimeSinceStartup + 90f;
				while (game.Phase == ArenaPhase.Playing && Time.realtimeSinceStartup < deadline) yield return null;
				Time.timeScale = 1f;

				var result = game.LastResult;
				if (result == null) { report.AppendLine($"period {run}: did not finish"); continue; }
				var line = new StringBuilder($"period {run}: ");
				var periodKills = 0;
				for (var i = 0; i < game.Fighters.Length; i++)
				{
					var c = (int)game.Fighters[i].Class;
					kills[c] += result.Kills[i];
					deaths[c] += result.Deaths[i];
					periodKills += result.Kills[i];
					line.Append($"{game.Fighters[i].DisplayName} {result.Kills[i]}/{result.Deaths[i]}  ");
				}
				for (var c = 0; c < 3; c++) if (result.HeroIndex[c] >= 0) heroes[c]++;
				total += periodKills;
				line.Append($"| total kills {periodKills}");
				report.AppendLine(line.ToString());
			}

			report.AppendLine($"== average kills per period {total / (float)Periods:0.0}");
			foreach (HeroClass c in System.Enum.GetValues(typeof(HeroClass)))
				report.AppendLine($"== {c,-7} kills {kills[(int)c],3} ({(total > 0 ? 100f * kills[(int)c] / total : 0f):0}% of all), deaths {deaths[(int)c],3}, Hero in {heroes[(int)c]} of {Periods} periods");

			Directory.CreateDirectory("Builds");
			File.WriteAllText("Builds/balance.txt", report.ToString());
			ArenaGame.SaveRootOverride = null;
			Assert.Pass(report.ToString());
		}
	}
}
