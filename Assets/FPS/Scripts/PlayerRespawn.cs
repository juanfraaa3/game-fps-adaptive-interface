using UnityEngine;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using System.Collections;
using System.IO;


public class PlayerRespawn : MonoBehaviour
{
    public JitterMetricsLogger jitterLogger;

    Health m_Health;
    CharacterController m_Controller;

    void Start()
    {
        m_Health = GetComponent<Health>();
        m_Controller = GetComponent<CharacterController>();

        // Guardar posición inicial como primer checkpoint
        CheckpointManager.Instance.SetCheckpoint(transform.position, transform.rotation);


        if (m_Health != null)
            m_Health.OnDie += RespawnAtCheckpoint;
    }

    void Update()
    {
        // Matar al jugador si cae fuera del mapa
        if (transform.position.y < -100f && m_Health.CurrentHealth > 0)
        {
            m_Health.Kill();
        }
    }

    void RespawnAtCheckpoint()
    {
        // === Registrar DEATH en ObstacleGateMetrics CSV ===
        if (!string.IsNullOrEmpty(ObstacleGateMetricsRework.ActiveGateCsvPath))
        {
            File.AppendAllText(
                ObstacleGateMetricsRework.ActiveGateCsvPath,
                $"DEATH;;;;;;;;;;;;;\n"
            );
        }

        LogMovementDeath();
        // 🔁 RESET PLATAFORMAS MÓVILES (JUANES)
        var movingPlatforms = FindObjectsOfType<MovingPlatformMultiple>();
        foreach (var platform in movingPlatforms)
        {
            platform.ResetPlatform();
        }

        // 🔁 RESET PLATAFORMAS SIMPLES (2 puntos)
        var simplePlatforms = FindObjectsOfType<MovingPlatform>();
        foreach (var platform in simplePlatforms)
        {
            platform.ResetPlatform();
        }

        var syncedPlatforms = FindObjectsOfType<PerfectSyncedTwoPoints_WithMargin>();
        foreach (var synced in syncedPlatforms)
        {
            synced.ResetPlatforms();
        }

        // === Registrar DEATH en JetpackTrajectoryLogger ===
        var traj = FindObjectOfType<JetpackTrajectoryLogger>();
        if (traj != null)
            traj.MarkDeath();

        // === Registrar DEATH en JetpackOrientationMetrics ===
        var orient = FindObjectOfType<JetpackOrientationMetrics>();
        if (orient != null)
            orient.MarkDeath();

        var landing = FindObjectOfType<LandingMetricsLogger>();

        Jetpack jp = GetComponent<Jetpack>();
        JetpackOrientationMetrics metrics = GetComponent<JetpackOrientationMetrics>();
        if (jp != null && metrics != null)
        {

            metrics.StopTrackingAndLog();
            Debug.Log("JETPACK SEGMENT CLOSED (death @ RespawnAtCheckpoint)");
        }
        if (jitterLogger != null)
        {
            jitterLogger.loggingActive = false; //detener logging al morir
            jitterLogger.Death = 1;       // 1 = muerte
            jitterLogger.RetryNumber++;   // suma 1 retry
            jitterLogger.AttemptID++;     // nuevo intento empieza al respawn

            var wm = FindObjectOfType<WaveManager>();
            if (wm != null)
            {
                float failTime = wm.GetWaveTimeWithOffset();
                File.AppendAllText(
                    jitterLogger.FilePath,
                    $"WaveFailTime;{wm.CurrentWaveIndex};{failTime.ToString("F4")}\n"
                );
            }


        }
        // === Reiniciar AttemptID en los 3 pilares ===
        if (traj != null)
            traj.AttemptID++;

        if (orient != null)
            orient.AttemptID++;

        if (landing != null)
            landing.AttemptID++;
        // === Registrar FIN DE INTENTO en Multitask ===
        var multitask = GetComponent<MultitaskMetricsLogger>();
        if (multitask != null)
        {
            multitask.EndAttempt(true);
        }

        Transform firstAnchor = GameObject.Find("TargetAnchor1")?.transform;

        if (jp != null)
        {
            if (firstAnchor != null)
            {
                jp.OrientationTargetPlatform = firstAnchor;

                if (metrics != null)
                    metrics.ForceSetTargetAnchor(firstAnchor);

                //Debug.Log("TARGET RESET → TargetAnchor1 (ALL PILLARS)");
            }
            else
            {
                Debug.LogError("❌ [Respawn] No se encontró 'TargetAnchor1' en la escena.");
            }
        }
        // 🔹 Limpiar solo los pickups sueltos por enemigos (prefab Loot_Health)
        foreach (var pickup in FindObjectsOfType<HealthPickup>())
        {
            if (pickup.name.Contains("Loot_Health"))
            {
                Destroy(pickup.gameObject);
            }
        }

        if (m_Controller != null)
            m_Controller.enabled = false;

        // 🔹 Reposicionar y restaurar orientación del jugador
        transform.position = CheckpointManager.Instance.GetCheckpoint() + Vector3.up * 1f;
        transform.rotation = CheckpointManager.Instance.GetCheckpointRotation();

        // 🔹 Forzar orientación de la cámara y del controlador del jugador
        var controller = GetComponent<Unity.FPS.Gameplay.PlayerCharacterController>();
        if (controller != null)
        {
            controller.SetLookRotation(CheckpointManager.Instance.GetCheckpointRotation());
        }

        if (m_Controller != null)
            m_Controller.enabled = true;

        // 🔹 Reactivar el arma
        StartCoroutine(DelayedWeaponEquip());


        // 🔹 Reactivar HUD si está desactivado
        GameObject hud = GameObject.Find("PlayerHUD");
        if (hud != null)
        {
            hud.SetActive(true);
        }

        // 🔹 Resetear el estado de muerte
        typeof(Health)
            .GetField("m_IsDead", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .SetValue(m_Health, false);

        // 🔹 Reactivar el arma si está desactivada
        Transform weaponParent = transform.Find("Main Camera/FirstPersonSocket/WeaponParentSocket");
        if (weaponParent != null && weaponParent.childCount > 0)
        {
            Transform weapon = weaponParent.GetChild(0);
            if (weapon != null && !weapon.gameObject.activeSelf)
            {
                weapon.gameObject.SetActive(true);
            }
        }

        // 🔹 Volver a suscribirse a OnDie (por seguridad)
        m_Health.OnDie -= RespawnAtCheckpoint;
        m_Health.OnDie += RespawnAtCheckpoint;

        // 🔹 Restaurar salud
        m_Health.Heal(m_Health.MaxHealth);

        // 🔹 Resetear las animaciones de la cámara y el arma (si está en ADS)
        ResetWeaponAndCamera();

        // 🔥 Reiniciar las waves al reaparecer
        var waveManager = FindObjectOfType<WaveManager>();
        if (waveManager != null)
        {
            waveManager.ResetWaves();
        }
        JetpackTrajectoryLogger.LastRespawnTime = Time.time;
        var landingLogger = FindObjectOfType<LandingMetricsLogger>();
        if (landingLogger != null)
        {
            landingLogger.ForceClearSuppressFlag();
            Debug.Log("[RESPAWN] _suppressNextSegment = FALSE (reset)");
        }
    }

    void ResetWeaponAndCamera()
    {
        // Si existe el 'PlayerWeaponsManager' y el 'WeaponController', reseteamos el FOV
        var weaponsManager = GetComponent<PlayerWeaponsManager>();
        if (weaponsManager != null)
        {
            weaponsManager.SetAiming(false); // Desactivamos el estado de apuntado
        }

        // Aquí puedes añadir código para resetear cualquier animación, FOV o estado visual del arma
        var playerCamera = GetComponentInChildren<Camera>();
        if (playerCamera != null)
        {
            playerCamera.fieldOfView = 60f;  // Restaurar el FOV al valor por defecto
        }
    }
    IEnumerator DelayedWeaponEquip()
    {
        yield return new WaitForSeconds(0.1f); // ⏳ Espera a que el PlayerWeaponsManager se inicialice

        var weaponsManager = GetComponent<PlayerWeaponsManager>();
        if (weaponsManager != null)
        {
            weaponsManager.SwitchToWeaponIndex(0, true);
            weaponsManager.SetAiming(false);

            // Asegurarse de que el arma esté realmente activa
            Transform weaponParent = transform.Find("Main Camera/FirstPersonSocket/WeaponParentSocket");
            if (weaponParent != null && weaponParent.childCount > 0)
            {
                var weapon = weaponParent.GetChild(0);
                if (weapon != null)
                    weapon.gameObject.SetActive(true);
            }

            Debug.Log("✅ Arma equipada correctamente tras respawn (DelayedWeaponEquip)");
        }
    }
    void LogMovementDeath()
    {
        string header =
            "EventType;SegmentID;ElementID;TimeOnPlatform;RelativePositionStd;RelativePositionMean;RelativePositionMax;RelativePositionMin;" +
            "SampleCount;EdgeRiskTime;CorrectionPeaks;ObstacleType;ClearType;MinClearance;MicroCorrections;ObstacleDuration";

        string[] fields = new string[16];

        fields[0] = "DEATH";
        fields[1] = "";


        for (int i = 2; i < fields.Length; i++)
            fields[i] = "";

        CSVMetricWriter.WriteLine(
            "MovementMetrics",
            header,
            string.Join(";", fields)
        );
    }

}
