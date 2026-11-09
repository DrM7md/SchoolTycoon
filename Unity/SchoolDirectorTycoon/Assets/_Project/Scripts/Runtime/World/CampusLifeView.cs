#nullable enable
using System;
using System.Collections.Generic;
using SchoolDirectorTycoon.Bridge;
using UnityEngine;

namespace SchoolDirectorTycoon.World
{
    /// <summary>Ambient life, driven by session numbers but never writing to them: students walking between
    /// buildings along the lot edges, a school bus calling at the campus, and a flag. Its randomness is
    /// separate from Core, so it cannot affect simulation results.</summary>
    public sealed class CampusLifeView : MonoBehaviour
    {
        private const int MinWalkers = 10, MaxWalkers = 70;
        private const float WalkSpeed = 1.5f, BusSpeed = 9f, BusStopSeconds = 7f, BusGapSeconds = 10f;

        private sealed class Walker
        {
            public Transform Body = null!;
            public readonly Queue<Vector3> Route = new Queue<Vector3>();
            public float Wait, Phase, Speed;
            public int Column, Row = -1;
        }

        private readonly List<Walker> walkers = new List<Walker>();
        private readonly System.Random random = new System.Random(4242);
        private GameSession? session;
        private CampusPalette? palette;
        private IDisposable? subscription;
        private Transform? bus, flag;
        private float walkZ, busX0, busStop, busX1, busTimer = 2f;
        private int busState;

        public int WalkerCount => walkers.Count;

        public void Bind(GameSession newSession, CampusPalette newPalette)
        {
            subscription?.Dispose();
            session = newSession;
            palette = newPalette;
            walkZ = GridMetrics.CellCenter(0, CampusGridView.PathRow()).z;

            Vector3 c = GridMetrics.Center;
            busX0 = c.x - GridMetrics.Width * 0.5f - 22f;
            busStop = c.x;
            busX1 = c.x + GridMetrics.Width * 0.5f + 22f;
            GameObject busModel = FacilityModels.Create(FacilityKind.Bus, palette, transform);
            busModel.name = "ArrivingBus";
            bus = busModel.transform;
            bus.localRotation = Quaternion.Euler(0f, -90f, 0f); // bonnet (-Z) points along +X
            bus.localPosition = new Vector3(busX0, 0f, walkZ - 0.9f);
            bus.gameObject.SetActive(false);

            BuildFlag(c);
            subscription = CampusModel.OnChanged(session, Sync);
            Sync();
        }

        private void OnDestroy() => subscription?.Dispose();

        private void BuildFlag(Vector3 c)
        {
            Vector3 foot = new Vector3(c.x - GridMetrics.Width * 0.5f - 3.5f, 0f, walkZ + 7f);
            var kit = new FacilityModels.Kit();
            kit.Cylinder(palette!.trim!, foot + Vector3.up * 4f, 0.06f, 8f);
            kit.Box(palette.curb!, foot + Vector3.up * 0.1f, new Vector3(1.1f, 0.2f, 1.1f));
            kit.Build("FlagMast", transform);

            var flagKit = new FacilityModels.Kit();
            flagKit.Box(palette.trim!, new Vector3(0.3f, 0f, 0f), new Vector3(0.6f, 1.2f, 0.03f));
            flagKit.Box(palette.accent!, new Vector3(1.35f, 0f, 0f), new Vector3(1.5f, 1.2f, 0.03f));
            flag = flagKit.Build("Flag", transform).transform;
            flag.localPosition = foot + Vector3.up * 7.3f;
        }

        private void Sync()
        {
            if (session == null) return;
            int wanted = Mathf.Clamp(HudSnapshot.From(session).Students / 2, MinWalkers, MaxWalkers);
            while (walkers.Count < wanted) walkers.Add(Spawn());
            while (walkers.Count > wanted)
            {
                Walker last = walkers[walkers.Count - 1];
                walkers.RemoveAt(walkers.Count - 1);
                if (Application.isPlaying) Destroy(last.Body.gameObject);
                else DestroyImmediate(last.Body.gameObject);
            }
        }

        private Walker Spawn()
        {
            // Uniform colours, plus white thobes and black abayas seen at Gulf schools.
            Material[] clothes = { palette!.accent!, palette.accent!, palette.trim!, palette.StyleFor(FacilityKind.Bus).roof };
            Material cloth = clothes[random.Next(clothes.Length)];
            var kit = new FacilityModels.Kit();
            kit.Cylinder(cloth, new Vector3(0f, 0.36f, 0f), 0.16f, 0.72f);
            kit.Sphere(palette.palmTrunk!, new Vector3(0f, 0.86f, 0f), new Vector3(0.26f, 0.26f, 0.26f));
            kit.Box(cloth == palette.trim ? palette.trim! : palette.curb!, new Vector3(0f, 0.55f, 0.15f), new Vector3(0.14f, 0.2f, 0.08f));
            GameObject go = kit.Build("Student", transform);
            Vector3 c = GridMetrics.Center;
            go.transform.localPosition = new Vector3(c.x + (float)(random.NextDouble() - 0.5) * GridMetrics.Width, 0f, WalkwayZ());
            return new Walker
            {
                Body = go.transform,
                Phase = (float)random.NextDouble() * 6.28f,
                Speed = WalkSpeed * (0.8f + (float)random.NextDouble() * 0.5f),
                Wait = (float)random.NextDouble() * 2f,
                Column = Mathf.FloorToInt(go.transform.localPosition.x / GridMetrics.TileSize),
            };
        }

        private float WalkwayZ() => walkZ + (float)(random.NextDouble() - 0.5) * 2.6f;

        private static float DoorZ(int row) =>
            GridMetrics.CellCenter(0, row).z + (row < CampusGridView.PathRow() ? 1f : -1f) * GridMetrics.TileSize * 0.5f;

        private void PlanRoute(Walker w)
        {
            var buildings = new List<PlacedBuilding>(CampusModel.Buildings(session!));
            float pz = WalkwayZ();
            float Lane(int column) => column * GridMetrics.TileSize;

            if (w.Row >= 0) // leave the current door by the lot edge
            {
                float dz = DoorZ(w.Row);
                w.Route.Enqueue(new Vector3(Lane(w.Column), 0f, dz));
                w.Route.Enqueue(new Vector3(Lane(w.Column), 0f, pz));
            }
            if (buildings.Count == 0 || random.NextDouble() < 0.2)
            {
                Vector3 c = GridMetrics.Center;
                w.Route.Enqueue(new Vector3(c.x + (float)(random.NextDouble() - 0.5) * GridMetrics.Width, 0f, pz));
                w.Row = -1;
                return;
            }
            PlacedBuilding target = buildings[random.Next(buildings.Count)];
            float doorZ = DoorZ(target.Row);
            w.Route.Enqueue(new Vector3(Lane(target.Column), 0f, pz));
            w.Route.Enqueue(new Vector3(Lane(target.Column), 0f, doorZ));
            w.Route.Enqueue(new Vector3(GridMetrics.CellCenter(target.Column, 0).x, 0f, doorZ));
            w.Column = target.Column;
            w.Row = target.Row;
        }

        private void Update()
        {
            if (session == null) return;
            float dt = Time.unscaledDeltaTime, time = Time.unscaledTime;

            foreach (Walker w in walkers)
            {
                if (w.Route.Count == 0)
                {
                    w.Wait -= dt;
                    if (w.Wait <= 0f) { PlanRoute(w); w.Wait = 1f + (float)random.NextDouble() * 5f; }
                    w.Body.localPosition = new Vector3(w.Body.localPosition.x, 0f, w.Body.localPosition.z);
                    continue;
                }
                Vector3 position = w.Body.localPosition;
                position.y = 0f;
                Vector3 target = w.Route.Peek();
                Vector3 next = Vector3.MoveTowards(position, target, w.Speed * dt);
                Vector3 step = next - position;
                if (step.sqrMagnitude > 1e-6f) w.Body.localRotation = Quaternion.Slerp(w.Body.localRotation, Quaternion.LookRotation(step), 12f * dt);
                if ((next - target).sqrMagnitude < 1e-4f) w.Route.Dequeue();
                next.y = Mathf.Abs(Mathf.Sin(time * 9f + w.Phase)) * 0.05f;
                w.Body.localPosition = next;
            }

            if (flag != null)
                flag.localRotation = Quaternion.Euler(0f, 18f + Mathf.Sin(time * 2.2f) * 16f + Mathf.Sin(time * 5.3f) * 5f, 0f);

            UpdateBus(dt);
        }

        private void UpdateBus(float dt)
        {
            if (bus == null) return;
            busTimer -= dt;
            Vector3 p = bus.localPosition;
            switch (busState)
            {
                case 0:
                    if (busTimer > 0f) return;
                    p.x = busX0;
                    bus.gameObject.SetActive(true);
                    busState = 1;
                    break;
                case 1:
                    // Ease into the stop.
                    p.x = Mathf.MoveTowards(p.x, busStop, Mathf.Max(1.2f, Mathf.Min(BusSpeed, (busStop - p.x) * 0.9f)) * dt);
                    if (Mathf.Approximately(p.x, busStop)) { busState = 2; busTimer = BusStopSeconds; }
                    break;
                case 2:
                    if (busTimer <= 0f) busState = 3;
                    break;
                case 3:
                    p.x = Mathf.MoveTowards(p.x, busX1, Mathf.Min(BusSpeed, 1.5f + (p.x - busStop) * 0.9f) * dt);
                    if (Mathf.Approximately(p.x, busX1)) { bus.gameObject.SetActive(false); busState = 0; busTimer = BusGapSeconds; }
                    break;
            }
            bus.localPosition = p;
        }
    }
}
