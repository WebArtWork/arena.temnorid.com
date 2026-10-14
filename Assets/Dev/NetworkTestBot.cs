using System;
using ITKamianets.Engine.Character;
using ITKamianets.Engine.Network;
using UnityEngine;

/// <summary>
/// Development only: when the game is started with <c>-bot</c>, drives the local networked
/// player in a circle and jumps every two seconds, and logs its position and prediction
/// corrections once a second. Also logs every session message it receives and, once in the
/// session, asks the server for a "temnorid-arena.test-ping" game event. In the arena it also
/// casts fireballs at the nearest enemy through the normal client path (request, server checks). Used to test client prediction with a built client next to an
/// Editor host. Runs after the controls (-40), so its command replaces theirs.
/// </summary>
[DefaultExecutionOrder(-30)]
public class NetworkTestBot : MonoBehaviour
{
    private float _nextLog;
    private bool _subscribed;
    private bool _pinged;
    private float _nextCast;
    private int _casts;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateIfRequested()
    {
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-bot") >= 0)
        {
            DontDestroyOnLoad(new GameObject("NetworkTestBot").AddComponent<NetworkTestBot>());
        }
    }

    private static string ScoreNames()
    {
        var match = Temnorid.Arena.Core.ArenaMatch.Current;
        if (match == null)
        {
            return "-";
        }

        var names = new System.Collections.Generic.List<string>();
        foreach (var score in match.Scores)
        {
            names.Add($"{score.Id}:{score.Name}");
        }

        return string.Join(" ", names);
    }

    private void Update()
    {
        NetworkCharacter own = null;
        foreach (var character in FindObjectsByType<NetworkCharacter>())
        {
            if (character.IsOwner)
            {
                own = character;
            }
        }

        var session = NetworkSession.Current;
        if (session != null && !_subscribed)
        {
            _subscribed = true;
            session.Messages.MessageReceived += message =>
                Debug.Log($"[bot-msg] seq={message.Seq} type={message.Type} from={message.From} payload={message.Payload.ToString(Newtonsoft.Json.Formatting.None)}");
        }

        if (session?.Messages.Session != null && !_pinged)
        {
            _pinged = true;
            session.Messages.Request("temnorid-arena.test-ping", new { note = "hello from the bot" });
        }

        if (own == null)
        {
            return;
        }

        var motor = own.GetComponent<CharacterMotor>();
        var angle = Time.time * 0.8f;
        motor.SetCommand(new CharacterCommand
        {
            Move = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)),
            Yaw = angle * Mathf.Rad2Deg,
            Jump = Time.time % 2f < Time.deltaTime,
        });

        if (Time.time >= _nextLog)
        {
            _nextLog = Time.time + 1f;
            // Arena: cast at the nearest living enemy, the way a player's click does.
            var me = own.GetComponent<Temnorid.Arena.Core.Combatant>();
            var caster = own.GetComponent<Temnorid.Arena.Spells.SpellCaster>();
            if (me != null && caster != null && me.Alive.Value && Time.time >= _nextCast)
            {
                _nextCast = Time.time + 0.8f;
                Temnorid.Arena.Core.Combatant nearest = null;
                foreach (var other in Temnorid.Arena.Core.Combatant.All)
                {
                    if (other != me && other.Alive.Value && (nearest == null || Vector3.Distance(other.transform.position, motor.transform.position) < Vector3.Distance(nearest.transform.position, motor.transform.position)))
                    {
                        nearest = other;
                    }
                }

                if (nearest != null && caster.TryCast(0, nearest.Motor.EyePosition - Vector3.up * 0.5f))
                {
                    _casts++;
                }
            }

            var known = session?.Messages.Session;
            var players = known == null ? "-" : string.Join(",", known.Players.ConvertAll(p => $"{p.DisplayName}({p.PlayerId},{(p.Connected ? "on" : "off")})"));
            Debug.Log($"[bot] t={Time.time:F1} pos={motor.transform.position:F2} grounded={motor.IsGrounded} corrections={own.Corrections} role={session?.Role} me={session?.Messages.LocalPlayerId} state={known?.State} scene={known?.SceneId} players=[{players}] combatants={Temnorid.Arena.Core.Combatant.All.Count} hp={(me != null ? me.Health.Value : -1):F0} casts={_casts} scores=[{ScoreNames()}]");
        }
    }
}
