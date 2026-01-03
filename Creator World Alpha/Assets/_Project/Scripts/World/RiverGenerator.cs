using UnityEngine;
using System.Collections.Generic;

namespace CreatorWorld.World
{
    /// <summary>
    /// Generates river paths from mountain peaks to ocean.
    /// Rivers follow terrain gradient, carving channels as they flow.
    /// </summary>
    public class RiverGenerator : MonoBehaviour
    {
        [Header("River Generation")]
        [SerializeField] private int worldSeed = 12345;
        [SerializeField] private float searchRadius = 1000f;
        [SerializeField] private int maxRivers = 8;
        [SerializeField] private float minPeakHeight = 15f;  // Lowered to match actual terrain heights (~22m max)
        [SerializeField] private float stepSize = 5f;
        [SerializeField] private int maxSteps = 500;

        [Header("River Properties")]
        [SerializeField] private float startWidth = 2f;
        [SerializeField] private float maxWidth = 20f;
        [SerializeField] private float widthGrowthRate = 0.02f;
        [SerializeField] private float carveDepth = 3f;
        [SerializeField] private float carveFalloff = 10f;

        [Header("Tributaries")]
        [SerializeField] private bool generateTributaries = true;
        [SerializeField] private int maxTributariesPerRiver = 3;
        [SerializeField] private float tributaryMinHeight = 10f;  // Lowered to match actual terrain
        [SerializeField] private float tributaryWidthScale = 0.5f;
        [SerializeField] private float tributarySpawnChance = 0.3f;

        [Header("Debug")]
        [SerializeField] private bool showDebugGizmos = true;
        [SerializeField] private bool generateOnStart = true;

        // Generated river data
        private List<RiverPath> rivers = new List<RiverPath>();
        private static RiverGenerator instance;

        public static RiverGenerator Instance => instance;
        public List<RiverPath> Rivers => rivers;

        private void Awake()
        {
            instance = this;
        }

        private void Start()
        {
            if (generateOnStart)
            {
                GenerateRivers();
            }
        }

        /// <summary>
        /// Generate all rivers in the world
        /// </summary>
        [ContextMenu("Generate Rivers")]
        public void GenerateRivers()
        {
            rivers.Clear();
            System.Random rng = new System.Random(worldSeed);

            // Find mountain peaks
            List<Vector2> peaks = FindMountainPeaks(rng);
            Debug.Log($"[RiverGenerator] Found {peaks.Count} potential river sources");

            // Generate main rivers from each peak
            List<RiverPath> mainRivers = new List<RiverPath>();
            foreach (var peak in peaks)
            {
                RiverPath river = TraceRiverPath(peak, rng, startWidth, maxWidth);
                if (river != null && river.Points.Count > 10)
                {
                    mainRivers.Add(river);
                    rivers.Add(river);
                    Debug.Log($"[RiverGenerator] Created main river with {river.Points.Count} points, length {river.TotalLength:F0}m");
                }
            }

            // Generate tributaries for each main river
            if (generateTributaries)
            {
                int totalTributaries = 0;
                foreach (var mainRiver in mainRivers)
                {
                    List<RiverPath> tributaries = GenerateTributaries(mainRiver, rng);
                    rivers.AddRange(tributaries);
                    totalTributaries += tributaries.Count;
                }
                Debug.Log($"[RiverGenerator] Generated {totalTributaries} tributaries");
            }

            Debug.Log($"[RiverGenerator] Generated {rivers.Count} total rivers (main + tributaries)");
        }

        /// <summary>
        /// Generate tributaries that branch into a main river
        /// </summary>
        private List<RiverPath> GenerateTributaries(RiverPath mainRiver, System.Random rng)
        {
            List<RiverPath> tributaries = new List<RiverPath>();
            int tributaryCount = 0;

            // Find potential branch points along the river (at higher elevations)
            for (int i = 0; i < mainRiver.Points.Count && tributaryCount < maxTributariesPerRiver; i += 10)
            {
                var point = mainRiver.Points[i];
                float height = TerrainGenerator.GetHeightAt(point.Position.x, point.Position.z, worldSeed);

                // Only spawn tributaries at higher elevations
                if (height < tributaryMinHeight) continue;

                // Random chance to spawn
                if (rng.NextDouble() > tributarySpawnChance) continue;

                // Find a nearby higher point to start the tributary
                Vector2 tributaryStart = FindTributarySource(point.Position.x, point.Position.z, rng);
                if (tributaryStart == Vector2.zero) continue;

                // Trace tributary to join the main river
                float tribWidth = startWidth * tributaryWidthScale;
                float tribMaxWidth = point.Width * 0.7f; // Smaller than main river at junction
                RiverPath tributary = TraceRiverPath(tributaryStart, rng, tribWidth, tribMaxWidth, mainRiver);

                if (tributary != null && tributary.Points.Count > 5)
                {
                    tributaries.Add(tributary);
                    tributaryCount++;
                }
            }

            return tributaries;
        }

        /// <summary>
        /// Find a higher terrain point near a river point to start a tributary
        /// </summary>
        private Vector2 FindTributarySource(float riverX, float riverZ, System.Random rng)
        {
            float searchDist = 80f;
            float riverHeight = TerrainGenerator.GetHeightAt(riverX, riverZ, worldSeed);

            // Try random directions to find higher ground
            for (int attempt = 0; attempt < 8; attempt++)
            {
                float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                float dist = searchDist + (float)rng.NextDouble() * searchDist;

                float sx = riverX + Mathf.Cos(angle) * dist;
                float sz = riverZ + Mathf.Sin(angle) * dist;

                float sourceHeight = TerrainGenerator.GetHeightAt(sx, sz, worldSeed);

                // Need significantly higher terrain
                if (sourceHeight > riverHeight + 15f)
                {
                    return new Vector2(sx, sz);
                }
            }

            return Vector2.zero;
        }

        /// <summary>
        /// Find mountain peaks that could be river sources
        /// </summary>
        private List<Vector2> FindMountainPeaks(System.Random rng)
        {
            List<Vector2> peaks = new List<Vector2>();
            List<(Vector2 pos, float height)> candidates = new List<(Vector2, float)>();

            // Grid-based search for more reliable peak detection
            float gridStep = 50f; // Sample every 50m
            int gridSize = Mathf.CeilToInt(searchRadius * 2 / gridStep);

            float highestFound = float.MinValue;
            float lowestFound = float.MaxValue;

            for (int gz = 0; gz < gridSize; gz++)
            {
                for (int gx = 0; gx < gridSize; gx++)
                {
                    float x = -searchRadius + gx * gridStep;
                    float z = -searchRadius + gz * gridStep;

                    float height = TerrainGenerator.GetHeightAt(x, z, worldSeed);

                    // Track height range for debugging
                    if (height > highestFound) highestFound = height;
                    if (height < lowestFound) lowestFound = height;

                    // Check if this is a potential peak
                    if (height >= minPeakHeight && IsLocalPeak(x, z, height))
                    {
                        candidates.Add((new Vector2(x, z), height));
                    }
                }
            }

            Debug.Log($"[RiverGenerator] Terrain height range: {lowestFound:F1} to {highestFound:F1}, found {candidates.Count} peak candidates");

            // Sort by height (highest first) and pick best peaks
            candidates.Sort((a, b) => b.height.CompareTo(a.height));

            foreach (var candidate in candidates)
            {
                if (peaks.Count >= maxRivers) break;

                // Make sure it's not too close to existing peaks
                bool tooClose = false;
                foreach (var existing in peaks)
                {
                    if (Vector2.Distance(candidate.pos, existing) < 150f)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (!tooClose)
                {
                    peaks.Add(candidate.pos);
                    Debug.Log($"[RiverGenerator] Found peak at ({candidate.pos.x:F0}, {candidate.pos.y:F0}) height: {candidate.height:F1}");
                }
            }

            return peaks;
        }

        /// <summary>
        /// Check if position is a local height maximum
        /// </summary>
        private bool IsLocalPeak(float x, float z, float height)
        {
            float sampleDist = 20f;

            // Check surrounding points
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (dx == 0 && dz == 0) continue;

                    float neighborHeight = TerrainGenerator.GetHeightAt(
                        x + dx * sampleDist,
                        z + dz * sampleDist,
                        worldSeed
                    );

                    if (neighborHeight > height)
                        return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Trace a river path from peak to ocean (or to join another river) following terrain gradient
        /// </summary>
        private RiverPath TraceRiverPath(Vector2 start, System.Random rng, float initialWidth, float maxRiverWidth, RiverPath targetRiver = null)
        {
            RiverPath river = new RiverPath();
            Vector2 current = start;
            float currentWidth = initialWidth;

            for (int step = 0; step < maxSteps; step++)
            {
                float height = TerrainGenerator.GetHeightAt(current.x, current.y, worldSeed);

                // Add point to river
                RiverPoint point = new RiverPoint
                {
                    Position = new Vector3(current.x, height - carveDepth * 0.5f, current.y),
                    Width = currentWidth,
                    FlowDirection = Vector2.zero // Will be calculated after
                };
                river.Points.Add(point);

                // Stop if we reached water level
                if (height <= TerrainGenerator.WaterLevel + 1f)
                {
                    break;
                }

                // For tributaries: stop if we reached the target river
                if (targetRiver != null)
                {
                    float distToTarget = GetDistanceToRiver(current.x, current.y, targetRiver);
                    if (distToTarget < 5f)
                    {
                        break; // Joined the main river
                    }
                }

                // Find steepest downhill direction
                Vector2 gradient = GetTerrainGradient(current.x, current.y);

                // For tributaries, also pull towards target river
                if (targetRiver != null)
                {
                    Vector2 toTarget = GetDirectionToRiver(current.x, current.y, targetRiver);
                    gradient = gradient.normalized * 0.7f + toTarget.normalized * 0.3f;
                }

                // Add some randomness to prevent perfectly straight rivers
                float noise = (float)(rng.NextDouble() * 2 - 1) * 0.2f;
                gradient = RotateVector2(gradient, noise);

                // Move in gradient direction
                if (gradient.magnitude < 0.001f)
                {
                    // Flat area - add random direction
                    float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                    gradient = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                }

                current += gradient.normalized * stepSize;

                // Grow river width downstream
                currentWidth = Mathf.Min(currentWidth + widthGrowthRate * stepSize, maxRiverWidth);
            }

            // Calculate flow directions (structs require get-modify-set pattern)
            for (int i = 0; i < river.Points.Count - 1; i++)
            {
                Vector3 dir = river.Points[i + 1].Position - river.Points[i].Position;
                var rp = river.Points[i];
                rp.FlowDirection = new Vector2(dir.x, dir.z).normalized;
                river.Points[i] = rp;
            }
            if (river.Points.Count > 1)
            {
                var lastPoint = river.Points[river.Points.Count - 1];
                lastPoint.FlowDirection = river.Points[river.Points.Count - 2].FlowDirection;
                river.Points[river.Points.Count - 1] = lastPoint;
            }

            // Calculate total length
            river.CalculateTotalLength();

            return river;
        }

        /// <summary>
        /// Get distance from a point to the nearest point on a river
        /// </summary>
        private float GetDistanceToRiver(float x, float z, RiverPath river)
        {
            float minDist = float.MaxValue;
            Vector2 pos = new Vector2(x, z);

            foreach (var point in river.Points)
            {
                float dist = Vector2.Distance(pos, new Vector2(point.Position.x, point.Position.z));
                if (dist < minDist) minDist = dist;
            }

            return minDist;
        }

        /// <summary>
        /// Get direction from a point towards the nearest point on a river
        /// </summary>
        private Vector2 GetDirectionToRiver(float x, float z, RiverPath river)
        {
            float minDist = float.MaxValue;
            Vector2 pos = new Vector2(x, z);
            Vector2 closest = pos;

            foreach (var point in river.Points)
            {
                Vector2 riverPos = new Vector2(point.Position.x, point.Position.z);
                float dist = Vector2.Distance(pos, riverPos);
                if (dist < minDist)
                {
                    minDist = dist;
                    closest = riverPos;
                }
            }

            return (closest - pos).normalized;
        }

        /// <summary>
        /// Get terrain gradient (direction of steepest descent)
        /// </summary>
        private Vector2 GetTerrainGradient(float x, float z)
        {
            float sampleDist = stepSize * 0.5f;

            float hCenter = TerrainGenerator.GetHeightAt(x, z, worldSeed);
            float hLeft = TerrainGenerator.GetHeightAt(x - sampleDist, z, worldSeed);
            float hRight = TerrainGenerator.GetHeightAt(x + sampleDist, z, worldSeed);
            float hUp = TerrainGenerator.GetHeightAt(x, z + sampleDist, worldSeed);
            float hDown = TerrainGenerator.GetHeightAt(x, z - sampleDist, worldSeed);

            // Gradient points downhill (negative height change)
            float gradX = (hLeft - hRight) / (2f * sampleDist);
            float gradZ = (hDown - hUp) / (2f * sampleDist);

            return new Vector2(gradX, gradZ);
        }

        /// <summary>
        /// Rotate a Vector2 by an angle in radians
        /// </summary>
        private Vector2 RotateVector2(Vector2 v, float angle)
        {
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);
            return new Vector2(
                v.x * cos - v.y * sin,
                v.x * sin + v.y * cos
            );
        }

        /// <summary>
        /// Get river carve depth at a world position.
        /// Returns how much to lower the terrain.
        /// </summary>
        public static float GetRiverCarveDepth(float worldX, float worldZ)
        {
            if (instance == null || instance.rivers == null || instance.rivers.Count == 0)
                return 0f;

            float totalCarve = 0f;

            foreach (var river in instance.rivers)
            {
                float carve = river.GetCarveDepthAt(worldX, worldZ, instance.carveDepth, instance.carveFalloff);
                totalCarve = Mathf.Max(totalCarve, carve);
            }

            return totalCarve;
        }

        /// <summary>
        /// Check if a position is within a river
        /// </summary>
        public static bool IsInRiver(float worldX, float worldZ, out float depth)
        {
            depth = GetRiverCarveDepth(worldX, worldZ);
            return depth > 0.1f;
        }

        private void OnDrawGizmos()
        {
            if (!showDebugGizmos || rivers == null) return;

            foreach (var river in rivers)
            {
                if (river.Points == null || river.Points.Count < 2) continue;

                // Draw river path
                Gizmos.color = Color.blue;
                for (int i = 0; i < river.Points.Count - 1; i++)
                {
                    Gizmos.DrawLine(river.Points[i].Position, river.Points[i + 1].Position);
                }

                // Draw width indicators
                Gizmos.color = Color.cyan;
                for (int i = 0; i < river.Points.Count; i += 5)
                {
                    var point = river.Points[i];
                    Vector3 right = new Vector3(-point.FlowDirection.y, 0, point.FlowDirection.x);
                    Gizmos.DrawLine(
                        point.Position - right * point.Width * 0.5f,
                        point.Position + right * point.Width * 0.5f
                    );
                }

                // Draw start point (peak)
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(river.Points[0].Position, 5f);

                // Draw end point (ocean)
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(river.Points[river.Points.Count - 1].Position, 3f);
            }
        }
    }

    /// <summary>
    /// A single river path from source to ocean
    /// </summary>
    [System.Serializable]
    public class RiverPath
    {
        public List<RiverPoint> Points = new List<RiverPoint>();
        public float TotalLength;

        public void CalculateTotalLength()
        {
            TotalLength = 0f;
            for (int i = 0; i < Points.Count - 1; i++)
            {
                TotalLength += Vector3.Distance(Points[i].Position, Points[i + 1].Position);
            }
        }

        /// <summary>
        /// Get carve depth at a world position based on distance to river
        /// </summary>
        public float GetCarveDepthAt(float worldX, float worldZ, float maxDepth, float falloff)
        {
            float minDist = float.MaxValue;
            float widthAtClosest = 0f;

            Vector2 pos = new Vector2(worldX, worldZ);

            // Find closest point on river
            for (int i = 0; i < Points.Count; i++)
            {
                Vector2 riverPos = new Vector2(Points[i].Position.x, Points[i].Position.z);
                float dist = Vector2.Distance(pos, riverPos);

                if (dist < minDist)
                {
                    minDist = dist;
                    widthAtClosest = Points[i].Width;
                }
            }

            // Calculate carve based on distance
            float halfWidth = widthAtClosest * 0.5f;

            if (minDist <= halfWidth)
            {
                // Inside river - full carve
                return maxDepth;
            }
            else if (minDist <= halfWidth + falloff)
            {
                // Riverbank falloff
                float t = (minDist - halfWidth) / falloff;
                return maxDepth * (1f - t * t); // Quadratic falloff
            }

            return 0f;
        }
    }

    /// <summary>
    /// A single point along a river path
    /// </summary>
    [System.Serializable]
    public struct RiverPoint
    {
        public Vector3 Position;
        public float Width;
        public Vector2 FlowDirection;
    }
}
