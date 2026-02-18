using System.Collections.Generic;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.FPS.UI
{
    public class Compass : MonoBehaviour
    {
        [Header("Compass Settings")]
        public RectTransform CompasRect;
        public float VisibilityAngle = 80f;
        public float HeightDifferenceMultiplier = 2f;
        public float MinScale = 0.5f;
        public float DistanceMinScale = 50f;
        public float CompasMarginRatio = 0.8f;
        public float MaxDetectionDistance = 20f;

        [Header("Back Arrows (Global)")]
        public Image ArrowBehindLeft;
        public Image ArrowBehindRight;

        [Header("Pulse Settings (Option B)")]
        public float PulseSpeed = 6f;      // antes 3
        public float PulseScale = 0.9f;    // antes 0.5

        [Header("Adaptive")]
        public JitterAdaptiveEvaluator Evaluator;

        Transform m_PlayerTransform;
        Dictionary<Transform, CompassMarker> m_ElementsDictionnary = new Dictionary<Transform, CompassMarker>();

        float m_WidthMultiplier;
        float m_HeightOffset;

        void Awake()
        {
            PlayerCharacterController playerCharacterController = FindObjectOfType<PlayerCharacterController>();
            DebugUtility.HandleErrorIfNullFindObject<PlayerCharacterController, Compass>(playerCharacterController, this);
            m_PlayerTransform = playerCharacterController.transform;

            m_WidthMultiplier = CompasRect.rect.width / VisibilityAngle;
            m_HeightOffset = -CompasRect.rect.height / 2;

            ArrowBehindLeft.enabled = false;
            ArrowBehindRight.enabled = false;
        }

        void Update()
        {
            Debug.Log("Aim axis = " + Input.GetAxis("Aim"));

            // =======================================
            // ADAPTIVE CONTROL
            // =======================================
            if (Evaluator != null)
            {
                float weight = Evaluator.JitterAssistWeight01;

                if (weight <= 0f)
                {
                    PulseScale = 0.2f;
                }
                else
                {
                    PulseScale = Mathf.Lerp(0.2f, 0.8f, weight);
                }
            }

            bool enemyBehindLeft = false;
            bool enemyBehindRight = false;

            float halfVisibility = VisibilityAngle / 2f;

            foreach (var element in m_ElementsDictionnary)
            {
                float distanceRatio = 1;
                float heightDiff = 0;
                float angle;

                if (element.Value.IsDirection)
                {
                    angle = Vector3.SignedAngle(
                        m_PlayerTransform.forward,
                        element.Key.transform.localPosition.normalized,
                        Vector3.up);
                }
                else
                {
                    Vector3 directionVector = element.Key.transform.position - m_PlayerTransform.position;

                    if (directionVector.magnitude > MaxDetectionDistance)
                    {
                        element.Value.CanvasGroup.alpha = 0;
                        continue;
                    }

                    Vector3 targetDir = Vector3.ProjectOnPlane(directionVector.normalized, Vector3.up);
                    Vector3 forward = Vector3.ProjectOnPlane(m_PlayerTransform.forward, Vector3.up);

                    angle = Vector3.SignedAngle(forward, targetDir, Vector3.up);

                    if (angle < -halfVisibility)
                        enemyBehindLeft = true;

                    if (angle > halfVisibility)
                        enemyBehindRight = true;

                    heightDiff = directionVector.y * HeightDifferenceMultiplier;
                    heightDiff = Mathf.Clamp(
                        heightDiff,
                        -CompasRect.rect.height / 2 * CompasMarginRatio,
                        CompasRect.rect.height / 2 * CompasMarginRatio
                    );

                    distanceRatio = Mathf.Clamp01(directionVector.magnitude / DistanceMinScale);
                }

                if (Mathf.Abs(angle) <= halfVisibility)
                {
                    element.Value.CanvasGroup.alpha = 1;
                    element.Value.CanvasGroup.transform.localPosition =
                        new Vector2(m_WidthMultiplier * angle, heightDiff + m_HeightOffset);

                    element.Value.CanvasGroup.transform.localScale =
                        Vector3.one * Mathf.Lerp(1, MinScale, distanceRatio);
                }
                else
                {
                    element.Value.CanvasGroup.alpha = 0;
                }
            }

            // ---------------------------------------------------
            // ⭐ EFECTO B: LATIDO GRANDE (1.0 → 1.5 → 1.0)
            // ---------------------------------------------------
            // Escala: suave, pulsante, visible pero no agresivo
            float pulseRaw = 1f + (PulseScale * Mathf.Sin(Time.time * PulseSpeed));
            float pulse = 1f + Mathf.Abs(Mathf.Sin(Time.time * PulseSpeed)) * PulseScale;


            // 1 + 0.5*sin → 0.5–1.5 (suave)

            // LEFT
            if (enemyBehindLeft)
            {
                ArrowBehindLeft.enabled = true;

                // Escala pulsante
                ArrowBehindLeft.rectTransform.localScale = new Vector3(pulse, pulse, 1);

                // Alpha siempre visible en este modo (opción B no usa blink)
                Color c = ArrowBehindLeft.color;
                ArrowBehindLeft.color = new Color(c.r, c.g, c.b, 1f);
            }
            else
            {
                ArrowBehindLeft.enabled = false;
            }

            // RIGHT
            if (enemyBehindRight)
            {
                ArrowBehindRight.enabled = true;
                ArrowBehindRight.rectTransform.localScale = new Vector3(pulse, pulse, 1);

                Color c = ArrowBehindRight.color;
                ArrowBehindRight.color = new Color(c.r, c.g, c.b, 1f);
            }
            else
            {
                ArrowBehindRight.enabled = false;
            }
        }

        public void RegisterCompassElement(Transform element, CompassMarker marker)
        {
            marker.transform.SetParent(CompasRect);
            m_ElementsDictionnary.Add(element, marker);
        }

        public void UnregisterCompassElement(Transform element)
        {
            if (m_ElementsDictionnary.TryGetValue(element, out CompassMarker marker)
                && marker.CanvasGroup != null)
                Destroy(marker.CanvasGroup.gameObject);

            m_ElementsDictionnary.Remove(element);
        }
    }
}
