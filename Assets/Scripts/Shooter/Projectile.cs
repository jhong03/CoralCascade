using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CoralCascade
{
    /// <summary>
    /// The visual travel of a fired bubble along an ALREADY-RESOLVED path. This is purely
    /// cosmetic movement (transform lerp, no Rigidbody) — the attach cell was decided
    /// deterministically at fire time. When it reaches the contact point it invokes a
    /// callback so the board can commit the attach and run match/cascade logic.
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        public float Speed = 22f;

        public void Launch(List<Vector2> points, BubbleColor color, float diameter, Action onArrived)
        {
            BubbleArt.Apply(gameObject, color, false, 20);
            transform.localScale = new Vector3(diameter, diameter, 1f);
            StartCoroutine(Travel(points, onArrived));
        }

        private IEnumerator Travel(List<Vector2> points, Action onArrived)
        {
            if (points == null || points.Count == 0)
            {
                onArrived?.Invoke();
                Destroy(gameObject);
                yield break;
            }

            transform.position = points[0];
            for (int i = 1; i < points.Count; i++)
            {
                Vector2 a = points[i - 1];
                Vector2 b = points[i];
                float dist = Vector2.Distance(a, b);
                float t = 0f;
                float dur = Mathf.Max(0.0001f, dist / Speed);
                while (t < 1f)
                {
                    // Scaled time so the slow-mo beat also affects an in-flight bubble.
                    t += Time.deltaTime / dur;
                    transform.position = Vector2.Lerp(a, b, Mathf.Clamp01(t));
                    yield return null;
                }
            }

            onArrived?.Invoke();
            Destroy(gameObject);
        }
    }
}
