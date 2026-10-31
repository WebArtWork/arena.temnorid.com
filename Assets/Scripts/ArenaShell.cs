using ITKamianets.Engine.Shell;
using UnityEngine;

namespace Temnorid.Arena
{
	/// <summary>What the shared shell (splash, landing, lobby, top and bottom bars) needs to know about this game.</summary>
	public static class ArenaShell
	{
		public const string Repo = "WebArtWork/arena.temnorid.com";

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		static void Configure()
		{
			ShellConfig.Current = new ShellConfig
			{
				Title = "Temnorid Arena",
				Tagline = "Step into the arena. Win your class's crown.",
				GameId = "arena",
				Repo = Repo,
				InstallerSuffix = "-Setup.exe",
				MaxPlayers = 4,
				Multiplayer = true,
				GameScene = "Main",
				Accent = new Color(1f, 0.82f, 0.3f),
				Places =
				{
					new ShellPlace("arena", "Arena", "A"),
					new ShellPlace("board", "Scoreboard", "S"),
				},
			};
		}
	}
}
