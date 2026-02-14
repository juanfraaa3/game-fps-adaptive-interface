using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Unity.FPS.Game
{
    /// Ajusta sensibilidad (ganancia) de la cámara por escalones entre StartCheckpointId (incl) y EndCheckpointId (excl).
    /// Requiere: Player con PlayerDeathRelay + Health, y que tus checkpoints actualicen el Relay (CheckpointBridge).
    public class CameraSensitivityAssist : MonoBehaviour
    {
        [Header("Sección por checkpoints")]
        public int StartCheckpointId = 4;
        public int EndCheckpointId = 5;

        [Header("Comportamiento")]
        public bool ResetWhenEnteringSection = true;
        public bool RevertWhenSectionCompleted = true;
        public bool LogDebug = true;

        [Header("Objetivo (dónde está la sensibilidad)")]
        [Tooltip("Componente que tiene el float de sensibilidad (PlayerInputHandler, FirstPersonController, etc.)")]
        public MonoBehaviour SensitivityComponent;

        [Tooltip("Nombre del campo/propiedad de sensibilidad (LookSensitivity, MouseSensitivity, RotationSpeed, etc.)")]
        public string SensitivityMemberName = "LookSensitivity";

        [Tooltip("Límites del multiplicador sobre la base para no romper el control")]
        public float MinMultiplier = 0.6f;
        public float MaxMultiplier = 1.4f;

        [Serializable]
        public class SensRule
        {
            public int DeathsThreshold = 3;
            [Tooltip("Multiplicador sobre la base: 0.9 = -10%, 1.1 = +10%")]
            public float Multiplier = 0.90f;
            [HideInInspector] public bool Applied;
        }

        [Header("Escalones (edítalos a gusto)")]
        public List<SensRule> Rules = new()
        {
            new SensRule{DeathsThreshold=3, Multiplier=0.90f}, // -10%
            new SensRule{DeathsThreshold=6, Multiplier=0.80f}, // -20%
            new SensRule{DeathsThreshold=9, Multiplier=0.70f}, // -30%
        };

        // cache
        float _baseSensitivity = 1f;
        FieldInfo _field; PropertyInfo _prop; bool _cached;

        // estado sección
        PlayerDeathRelay _relay; Health _playerHealth;
        int _lastCheckpoint = -1;
        int _deathsInSection = 0;

        // nombres candidatos por si dejas SensitivityMemberName vacío
        static readonly string[] Candidates = {
            "LookSensitivity","MouseSensitivity","Sensitivity","RotationSpeed",
            "XSensitivity","YSensitivity"
        };

        void Awake()
        {
            _relay = FindObjectOfType<PlayerDeathRelay>();
            if (_relay != null) _lastCheckpoint = _relay.CurrentCheckpointId;

            _playerHealth = _relay ? _relay.GetComponent<Health>() : FindObjectOfType<Health>();
            if (_playerHealth) _playerHealth.OnDie += OnPlayerDie;

            CacheMember();
        }

        void OnDestroy()
        {
            if (_playerHealth) _playerHealth.OnDie -= OnPlayerDie;
        }

        void CacheMember()
        {
            if (_cached) return;

            if (!SensitivityComponent)
            {
                //Debug.LogWarning("[CamSensAssist] Asigna SensitivityComponent en el Inspector.");
                return;
            }

            var t = SensitivityComponent.GetType();

            // Resolver nombre si no lo definiste
            string name = SensitivityMemberName;
            if (string.IsNullOrEmpty(name))
            {
                foreach (var c in Candidates)
                {
                    var fTry = t.GetField(c, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
                    if (fTry != null && fTry.FieldType == typeof(float)) { name = fTry.Name; break; }

                    var pTry = t.GetProperty(c, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
                    if (pTry != null && pTry.PropertyType == typeof(float) && pTry.CanRead && pTry.CanWrite) { name = pTry.Name; break; }
                }
            }

            // Campo
            _field = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
            if (!(_field != null && _field.FieldType == typeof(float))) _field = null;

            // Propiedad
            if (_field == null)
            {
                _prop = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
                if (!(_prop != null && _prop.PropertyType == typeof(float) && _prop.CanRead && _prop.CanWrite))
                    _prop = null;
            }

            if (_field != null) _baseSensitivity = (float)_field.GetValue(SensitivityComponent);
            else if (_prop != null) _baseSensitivity = (float)_prop.GetValue(SensitivityComponent);
            else
            {
                //Debug.LogWarning($"[CamSensAssist] No encontré float '{(string.IsNullOrEmpty(SensitivityMemberName) ? "(auto)" : SensitivityMemberName)}' en {t.Name}.", SensitivityComponent);
                _baseSensitivity = 1f;
            }

            _cached = true;
            //if (LogDebug) Debug.Log($"[CamSensAssist] Base={_baseSensitivity} en {t.Name}.{(string.IsNullOrEmpty(name) ? "?" : name)}");
        }

        void Update()
        {
            if (_relay == null) return;

            if (_relay.CurrentCheckpointId != _lastCheckpoint)
            {
                _lastCheckpoint = _relay.CurrentCheckpointId;

                // Entró a la sección
                if (_lastCheckpoint == StartCheckpointId && ResetWhenEnteringSection)
                {
                    ResetSection();
                    //if (LogDebug) Debug.Log("[CamSensAssist] Entraste a la sección. Reset.");
                }

                // Completó la sección
                if (_lastCheckpoint >= EndCheckpointId)
                {
                    if (RevertWhenSectionCompleted)
                    {
                        Revert();
                        //if (LogDebug) Debug.Log("[CamSensAssist] Sección completada. Revert.");
                    }
                }
            }
        }

        bool InsideSection() =>
            _relay != null &&
            _relay.CurrentCheckpointId == StartCheckpointId &&
            _lastCheckpoint < EndCheckpointId;

        void OnPlayerDie()
        {
            if (!InsideSection()) return;

            _deathsInSection++;
            //if (LogDebug) Debug.Log($"[CamSensAssist] Muerte #{_deathsInSection}.");

            SensRule toApply = null;
            foreach (var r in Rules)
                if (_deathsInSection >= r.DeathsThreshold && !r.Applied)
                    toApply = r;

            if (toApply == null) return;

            float m = Mathf.Clamp(toApply.Multiplier, MinMultiplier, MaxMultiplier);
            SetSensitivity(_baseSensitivity * m);

            toApply.Applied = true;
            //if (LogDebug) Debug.Log($"[CamSensAssist] Sens = base({_baseSensitivity}) x {m}.");
        }

        void SetSensitivity(float value)
        {
            if (!_cached) CacheMember();
            if (_field != null) _field.SetValue(SensitivityComponent, value);
            else if (_prop != null) _prop.SetValue(SensitivityComponent, value);
        }

        public void ResetSection()
        {
            _deathsInSection = 0;
            foreach (var r in Rules) r.Applied = false;
            SetSensitivity(_baseSensitivity);
        }

        public void Revert() => SetSensitivity(_baseSensitivity);
    }
}
