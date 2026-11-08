#nullable enable
using System;
using System.Collections.Generic;
using SchoolDirectorTycoon.Bridge;
using UnityEngine;

namespace SchoolDirectorTycoon.World
{
    /// <summary>Keeps one placeholder per placed building in sync with the session. Never writes state.</summary>
    public sealed class BuildingsView : MonoBehaviour
    {
        private const float GrowSeconds = 0.35f;

        private readonly Dictionary<(int, int), (FacilityKind kind, GameObject go)> views = new Dictionary<(int, int), (FacilityKind, GameObject)>();
        private readonly List<(Transform t, float age)> growing = new List<(Transform, float)>();
        private GameSession? session;
        private CampusPalette? palette;
        private IDisposable? subscription;

        public int Count => views.Count;

        public void Bind(GameSession newSession, CampusPalette newPalette)
        {
            subscription?.Dispose();
            session = newSession;
            palette = newPalette;
            subscription = CampusModel.OnChanged(session, () => Sync(animate: true));
            Sync(animate: false);
        }

        private void OnDestroy() => subscription?.Dispose();

        public void Sync(bool animate)
        {
            if (session == null || palette == null) return;
            var wanted = new HashSet<(int, int)>();
            foreach (PlacedBuilding building in CampusModel.Buildings(session))
            {
                var key = (building.Column, building.Row);
                wanted.Add(key);
                if (views.TryGetValue(key, out var existing))
                {
                    if (existing.kind == building.Kind) continue;
                    Release(existing.go);
                }

                GameObject go = Placeholders.Building(building.Kind, palette.StyleFor(building.Kind), transform);
                go.transform.localPosition = GridMetrics.CellCenter(building.Column, building.Row);
                views[key] = (building.Kind, go);
                if (animate)
                {
                    go.transform.localScale = new Vector3(1f, 0.01f, 1f);
                    growing.Add((go.transform, 0f));
                }
            }

            var stale = new List<(int, int)>();
            foreach (var key in views.Keys)
                if (!wanted.Contains(key)) stale.Add(key);
            foreach (var key in stale)
            {
                Release(views[key].go);
                views.Remove(key);
            }
        }

        private static void Release(GameObject go)
        {
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }

        private void Update()
        {
            for (int i = growing.Count - 1; i >= 0; i--)
            {
                var (t, age) = growing[i];
                if (t == null) { growing.RemoveAt(i); continue; }
                age += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(age / GrowSeconds);
                // Ease-out with a small overshoot so a new building visibly "pops" into place.
                float y = 1f + 0.15f * Mathf.Sin(k * Mathf.PI) - Mathf.Pow(1f - k, 3f);
                t.localScale = new Vector3(1f, Mathf.Max(0.01f, y), 1f);
                if (k >= 1f) { t.localScale = Vector3.one; growing.RemoveAt(i); }
                else growing[i] = (t, age);
            }
        }
    }
}
