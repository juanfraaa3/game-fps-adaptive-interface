using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using System.Globalization;
using Unity.FPS.Gameplay;

/// <summary>
/// One-file system for gate obstacle metrics:
/// - GateZone: a trigger volume that defines "being inside the obstacle section"
/// - ContactSurface: trigger colliders that represent the hazardous geometry; they forward contact to the GateZone
///
/// Attach:
/// 1) Gate root: BoxCollider (isTrigger), Rigidbody (kinematic), this script with Role=GateZone
/// 2) Contact colliders (children): Collider (isTrigger), this script with Role=ContactSurface
///
/// Outputs a CSV per session in Application.persistentDataPath/ObstacleGateLogs/
/// </summary>
public class ObstacleGateMetricsRework : MonoBehaviour
{
    public enum Role { GateZone, ContactSurface }

    [Header("Role")]
    public Role role = Role.GateZone;

    [Header("IDs / Context")]
    public int attemptId = 0;           // optional; set from your run manager if you have it
    public int segmentId = 0;
    public int gateId = 0;

    [Header("Player Identification")]
    public string playerTag = "Player";

    [Header("Logging")]
    public bool enableLogging = true;
    public string folderName = "ObstacleGateLogs";
    public string filePrefix = "ObstacleGateMetrics";
    public bool writeHeaderIfNew = true;

    [Header("ClearType thresholds")]
    [Tooltip("If jetpack active time inside gate >= this, ClearType becomes Jetpack (unless touched rules you add later).")]
    public float jetpackTimeThreshold_s = 0.05f;

    [Tooltip("If crouch time inside gate >= this, ClearType becomes Duck (highest priority).")]
    public float crouchTimeThreshold_s = 0.15f;

    [Header("Ground Detection (fallback if no CharacterController)")]
    public float groundedRayDistance = 0.25f;
    public LayerMask groundMask = ~0;

    [Header("Reflection names (optional, only used if you don't provide adapters)")]
    [Tooltip("If your player has a component with any of these bool fields/properties, it will be used for jetpack active detection.")]
    public string[] jetpackBoolNames = new[] { "IsJetpacking", "IsJetpackActive", "JetpackActive", "jetpackActive" };

    [Tooltip("If your player has a component with any of these bool fields/properties, it will be used for crouch detection.")]
    public string[] crouchBoolNames = new[] { "IsCrouching", "isCrouching", "Crouching", "isCrouched", "IsCrouched" };

    // -----------------------------
    // Runtime state (GateZone only)
    // -----------------------------
    private bool _insideGate = false;
    private bool _hasLoggedThisPass = false;

    private float _entryTime;
    private float _exitTime;

    // outcome
    private bool _cleared = false;          // exited gate after entry and not failed

    // contact
    private bool _touched = false;
    private int _contactCount = 0;
    private float _contactDuration = 0f;
    private float _firstContactTime = -1f;
    private int _contactActiveOverlaps = 0;
    private float _contactCurrentStartTime = 0f;

    // strategy/time
    private float _jetpackActiveTime = 0f;
    private float _crouchTime = 0f;
    private float _airborneTime = 0f;

    // player ref
    private GameObject _player;
    private CharacterController _cc;
    private Rigidbody _rb;

    // file
    private string _filePath;

    // -----------------------------
    // ContactSurface state
    // -----------------------------
    private ObstacleGateMetricsRework _parentGate;
    public static string ActiveGateCsvPath = null;
    private Jetpack _jetpack;
    private bool _usedJetpack = false;
    // projected contact position (height crossing)
    private bool _hasProjectedContactPos = false;
    private Vector3 _projectedContactPos = Vector3.zero;
    private Collider[] _contactColliders;

    private void Awake()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;

        if (role == Role.ContactSurface)
        {
            // Buscar en padres (incluyendo este GO) el componente con Role = GateZone
            var candidates = GetComponentsInParent<ObstacleGateMetricsRework>(true);
            _parentGate = null;

            foreach (var c in candidates)
            {
                if (c != null && c != this && c.role == Role.GateZone)
                {
                    _parentGate = c;
                    break;
                }
            }

            // Debug opcional (quita después)
            // Debug.Log($"[ContactSurface] parentGate = {(_parentGate ? _parentGate.name : "NULL")} on {name}");
        }
        else // Role.GateZone
        {
            PrepareLogFilePath();
        }
    }



    private void Update()
    {
        if (!enableLogging) return;
        if (role != Role.GateZone) return;
        if (!_insideGate) return;
        if (_player == null) return;

        // Accumulate strategy / state times
        float dt = Time.deltaTime;

        bool jetpackActive =
            _jetpack != null &&
            _jetpack.IsJetpackInUse;
        bool crouching = TryReadBoolFromPlayer(_player, crouchBoolNames);
        bool airborne = IsAirborne(_player);

        if (jetpackActive)
        {
            _jetpackActiveTime += dt;
            _usedJetpack = true;
        }


        if (crouching) _crouchTime += dt;
        if (airborne) _airborneTime += dt;
        if (!_hasProjectedContactPos && _contactColliders != null)
        {
            Vector3 p = _player.transform.position;

            foreach (var col in _contactColliders)
            {
                if (col == null || !col.isTrigger)
                    continue;

                Bounds b = col.bounds;

                // ¿Está alineado en XZ con el contact?
                bool insideXZ =
                    p.x >= b.min.x && p.x <= b.max.x &&
                    p.z >= b.min.z && p.z <= b.max.z;

                if (!insideXZ)
                    continue;

                float topY = b.max.y;
                const float yMargin = 0.02f;

                // ¿Cruza la altura del collider (lo toque o no)?
                if (p.y >= topY - yMargin)
                {
                    _projectedContactPos = p;

                    _hasProjectedContactPos = true;
                    break;
                }
            }
        }
    }

    // ---------------------------------------
    // Public hook for your death/respawn logic
    // ---------------------------------------
    public void MarkDeath()
    {
        if (role != Role.GateZone) return;
        if (!enableLogging) return;

        // 1) Si estaba en un gate y aún no se escribió la fila, escribir métricas parciales
        if (_insideGate && !_hasLoggedThisPass)
        {
            _cleared = false;
            _exitTime = Time.time;
            LogAndFinalize();
        }

        // 2) Escribir SIEMPRE una fila DEATH independiente
        WriteDeathRow();
    }
    private void WriteDeathRow()
    {
        if (role != Role.GateZone) return;
        if (string.IsNullOrEmpty(_filePath)) return;

        string line =
            $"DEATH;{attemptId};{segmentId};{gateId};;;;;;;;;;;;";

        File.AppendAllText(_filePath, line + Environment.NewLine);
    }


    // ---------------------------------------
    // GateZone: zone trigger (entry/exit)
    // ---------------------------------------
    private void OnTriggerEnter(Collider other)
    {
        if (!enableLogging) return;

        // ContactSurface forwards to parent
        if (role == Role.ContactSurface)
        {
            if (_parentGate != null && other.CompareTag(_parentGate.playerTag))
                _parentGate.NotifyContactEnter();
            return;
        }

        // GateZone logic
        if (!other.CompareTag(playerTag)) return;

        // Start pass if not already inside
        if (!_insideGate)
        {
            _player = other.gameObject;
            _cc = _player.GetComponent<CharacterController>();
            _rb = _player.GetComponent<Rigidbody>();
            _jetpack = _player.GetComponent<Jetpack>();
            _contactColliders = GetComponentsInChildren<Collider>();

            _insideGate = true;
            _hasLoggedThisPass = false;

            _entryTime = Time.time;
            _exitTime = 0f;

            ResetPerPassAccumulators(keepPlayerRef: true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!enableLogging) return;

        // ContactSurface forwards to parent
        if (role == Role.ContactSurface)
        {
            if (_parentGate != null && other.CompareTag(_parentGate.playerTag))
                _parentGate.NotifyContactExit();
            return;
        }

        // GateZone logic
        if (!other.CompareTag(playerTag)) return;
        if (!_insideGate) return;

        // End pass
        _insideGate = false;
        _exitTime = Time.time;

        // If we already logged due to death, do nothing
        if (_hasLoggedThisPass) return;


        _cleared = true;

        LogAndFinalize();
    }

    // ---------------------------------------
    // GateZone: contact notifications
    // ---------------------------------------
    private void NotifyContactEnter()
    {
        if (role != Role.GateZone) return;
        if (!_insideGate) return;
        if (_hasLoggedThisPass) return;

        _touched = true;

        _contactCount++;

        if (_firstContactTime < 0f)
            _firstContactTime = Time.time - _entryTime;

        // Overlap-safe: only start timing when first overlap begins
        _contactActiveOverlaps++;
        if (_contactActiveOverlaps == 1)
        {
            _contactCurrentStartTime = Time.time;
        }
    }

    private void NotifyContactExit()
    {
        if (!_insideGate) return;
        if (_contactActiveOverlaps == 0) return;

        _contactDuration += Mathf.Max(0f, Time.time - _contactCurrentStartTime);
        _contactActiveOverlaps = 0;
        _contactCurrentStartTime = 0f;
    }


    // ---------------------------------------
    // Logging
    // ---------------------------------------
    private void PrepareLogFilePath()
    {
        if (role != Role.GateZone)
            return;

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;

        string dir = Path.Combine(
            projectRoot,
            "Assets",
            "FPS",
            "Scripts",
            "MovingSystem",
            "Registers"
        );

        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        _filePath = Path.Combine(dir, $"{filePrefix}_{stamp}.csv");

        if (writeHeaderIfNew && !File.Exists(_filePath))
        {
            string header =
                "EventType;AttemptID;SegmentID;GateID;" +
                "EntryTime;ExitTime;TimeToClear_s;" +
                "Cleared;ObstacleTouched;ContactDuration_s;ContactCount;FirstContactTime_s;" +
                "JetpackActiveTime_s;CrouchTime_s;AirborneTime_s;" +
                "ContactX;ContactY;ContactZ";

            File.AppendAllText(_filePath, header + Environment.NewLine);
        }

        ActiveGateCsvPath = _filePath;
    }



    private void LogAndFinalize()
    {
        if (role != Role.GateZone) return;
        if (!enableLogging) return;
        // Close any ongoing contact interval if still overlapping at end
        if (_contactActiveOverlaps > 0)
        {
            _contactDuration += Mathf.Max(0f, Time.time - _contactCurrentStartTime);
            _contactActiveOverlaps = 0;
            _contactCurrentStartTime = 0f;
        }

        float timeToClear = Mathf.Max(0f, _exitTime - _entryTime);
        string cx = _hasProjectedContactPos ? _projectedContactPos.x.ToString("F4") : "0";
        string cy = _hasProjectedContactPos ? _projectedContactPos.y.ToString("F4") : "0";
        string cz = _hasProjectedContactPos ? _projectedContactPos.z.ToString("F4") : "0";


        string line =
            $"GATE;{attemptId};{segmentId};{gateId};" +
            $"{_entryTime:F4};{_exitTime:F4};{timeToClear:F4};" +
            $"{(_cleared ? 1 : 0)};" +
            $"{(_touched ? 1 : 0)};{_contactDuration:F4};{_contactCount};{_firstContactTime:F4};" +
            $"{_jetpackActiveTime:F4};{_crouchTime:F4};{_airborneTime:F4};" +
            $"{cx};{cy};{cz}";

        File.AppendAllText(_filePath, line + Environment.NewLine);

        _hasLoggedThisPass = true;

        // If you want: reset player ref after pass
        ResetPerPassAccumulators(keepPlayerRef: true);
    }

    private string DecideClearType()
    {
        // 1) Jetpack domina por presencia
        if (_usedJetpack)
            return "Jetpack";

        // 2) Duck se decide por su propio umbral
        if (_crouchTime >= crouchTimeThreshold_s)
            return "Duck";

        // 3) Si no hubo jetpack ni duck, airborne = Jump
        if (_airborneTime > 0.05f)
            return "Jump";

        return "Walk";
    }


    private void ResetPerPassAccumulators(bool keepPlayerRef)
    {
        _cleared = false;

        _touched = false;
        _contactCount = 0;
        _contactDuration = 0f;
        _firstContactTime = -1f;
        _contactActiveOverlaps = 0;
        _contactCurrentStartTime = 0f;

        _jetpackActiveTime = 0f;
        _crouchTime = 0f;
        _airborneTime = 0f;
        _usedJetpack = false;
        _hasProjectedContactPos = false;
        _projectedContactPos = Vector3.zero;
        if (!keepPlayerRef)
        {
            _player = null;
            _cc = null;
            _rb = null;
        }
    }

    // ---------------------------------------
    // Player state helpers
    // ---------------------------------------
    private bool IsAirborne(GameObject player)
    {
        Vector3 origin = player.transform.position + Vector3.up * 0.05f;
        float rayLength = 0.25f;

        bool grounded = Physics.Raycast(
            origin,
            Vector3.down,
            rayLength,
            ~0, // TODO: todo, sin layers
            QueryTriggerInteraction.Ignore
        );

        return !grounded;
    }


    private static bool TryReadBoolFromPlayer(GameObject player, string[] possibleNames)
    {
        if (player == null || possibleNames == null || possibleNames.Length == 0) return false;

        // Look through all components (cheap enough for this scope; if you want, cache later)
        var comps = player.GetComponents<MonoBehaviour>();
        foreach (var c in comps)
        {
            if (c == null) continue;

            Type t = c.GetType();

            for (int i = 0; i < possibleNames.Length; i++)
            {
                string name = possibleNames[i];

                // property
                PropertyInfo p = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (p != null && p.PropertyType == typeof(bool) && p.CanRead)
                {
                    try { return (bool)p.GetValue(c); } catch { }
                }

                // field
                FieldInfo f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f != null && f.FieldType == typeof(bool))
                {
                    try { return (bool)f.GetValue(c); } catch { }
                }
            }
        }

        return false;
    }
    private void OnTriggerStay(Collider other)
    {
        if (!enableLogging) return;
        if (role != Role.ContactSurface) return;
        if (_parentGate == null) return;

        if (!other.CompareTag(_parentGate.playerTag)) return;

        _parentGate.NotifyContactStay();
    }
    private void NotifyContactStay()
    {
        if (!_insideGate) return;
        if (_hasLoggedThisPass) return;

        _touched = true;

        if (_firstContactTime < 0f)
            _firstContactTime = Time.time - _entryTime;

        // si aún no estaba contando contacto, iniciar
        if (_contactActiveOverlaps == 0)
        {
            _contactActiveOverlaps = 1;
            _contactCurrentStartTime = Time.time;
            _contactCount++;
        }
    }


}
