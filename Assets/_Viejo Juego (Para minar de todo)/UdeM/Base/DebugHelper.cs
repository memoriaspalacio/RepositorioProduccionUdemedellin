using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UdeM.Base {
    public static class DebugHelper
    {
        public static void DrawDebugCircle(Vector3 position, float radius, Color color, int segments = 32)
        {
            float angle = 0f;
            float angleStep = 360f / segments;

            for (int i = 0; i < segments; i++) {
                Vector3 startPoint = position + new Vector3(Mathf.Cos(Mathf.Deg2Rad * angle) * radius, Mathf.Sin(Mathf.Deg2Rad * angle) * radius, 0);
                angle += angleStep;
                Vector3 endPoint = position + new Vector3(Mathf.Cos(Mathf.Deg2Rad * angle) * radius, Mathf.Sin(Mathf.Deg2Rad * angle) * radius, 0);

                Debug.DrawLine(startPoint, endPoint, color);
            }
        }

        public static void DrawLine(Vector3 start, Vector3 end, Color color)
        {
            Debug.DrawLine(start, end, color);
        }

        public static void DrawRay(Vector3 start, Vector3 direction, Color color)
        {
            Debug.DrawRay(start, direction, color);
        }
    }
}