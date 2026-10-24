// Where a rope ring sits on a creature, worked out from its real body (Valheim, PotatoFarmer, hitching post).
//
// The animal's body is snapshotted in its current pose, then cut by planes square to the line between two bones (the
// neck and head, or two spine bones for a waist belt). Each cut gives the body's outline there; a rope pulled tight
// round an outline lies on its convex hull, so that is the ring. The ring goes where the outline is smallest, which is
// where a real rope would slide to. The centre line is corrected by the outlines' middles, since some rigs' bones run
// along the back of the neck rather than through it (the hen's). Works for any creature, modded ones included; falls
// back to a plain circle if the body can't be read.
//
// Reading the posed body: SkinnedMeshRenderer.BakeMesh(mesh, true) returns vertices in the renderer's own space
// without its scale, so they go through the renderer's full localToWorldMatrix. (BakeMesh(mesh, false) with the
// rotation and position only is the same.) Many creatures carry a big scale on the renderer (the wolf 195, the lox 75,
// the hen 60), so the other combinations put the body far too big or small.
using System.Collections.Generic;
using UnityEngine;

namespace PotatoFarmer
{
    internal static class NeckRing
    {
        internal sealed class Fit
        {
            public Vector3 Center, A, C;   // world space: the ring's middle and the two directions across it
            public float[] Radii;           // distance from the middle at each of the angles round it
            public float At;                // where it sits between the two bones, 0 to 1
            public float Round;             // the outline's length in metres
        }

        private const float Weld = 0.0005f;
        private const float Back = 0.015f;  // metres further towards the body than the narrowest place

        // The body in world space: every visible skinned mesh (only the most detailed level of a LOD group), with
        // vertices that share a place merged, so the outlines join up across UV seams.
        internal static bool Body(Character animal, List<Vector3> points, List<int> triangles)
        {
            points.Clear();
            triangles.Clear();
            var index = new Dictionary<Vector3Int, int>();
            var baked = new Mesh();
            try
            {
                foreach (var skin in animal.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if (!skin.enabled || !skin.gameObject.activeInHierarchy || !skin.sharedMesh || !FinestLevel(skin)) continue;
                    try { skin.BakeMesh(baked, true); }
                    catch { continue; }
                    var place = skin.transform.localToWorldMatrix;
                    var vertices = baked.vertices;
                    var map = new int[vertices.Length];
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        var p = place.MultiplyPoint3x4(vertices[i]);
                        var key = new Vector3Int(Mathf.RoundToInt(p.x / Weld), Mathf.RoundToInt(p.y / Weld), Mathf.RoundToInt(p.z / Weld));
                        if (!index.TryGetValue(key, out int at))
                        {
                            at = points.Count;
                            points.Add(p);
                            index[key] = at;
                        }
                        map[i] = at;
                    }
                    var tris = baked.triangles;
                    for (int i = 0; i + 2 < tris.Length; i += 3)
                    {
                        int x = map[tris[i]], y = map[tris[i + 1]], z = map[tris[i + 2]];
                        if (x == y || y == z || x == z) continue;
                        triangles.Add(x);
                        triangles.Add(y);
                        triangles.Add(z);
                    }
                }
            }
            finally { Object.Destroy(baked); }
            return triangles.Count > 0;
        }

        private static bool FinestLevel(Renderer renderer)
        {
            var group = renderer.GetComponentInParent<LODGroup>();
            if (!group) return true;
            var levels = group.GetLODs();
            if (levels.Length == 0) return true;
            foreach (var r in levels[0].renderers)
                if (r == renderer) return true;
            foreach (var level in levels)
                foreach (var r in level.renderers)
                    if (r == renderer) return false;
            return true;
        }

        private static void Frame(Vector3 along, Vector3 up, out Vector3 a, out Vector3 u, out Vector3 v)
        {
            a = along.normalized;
            u = Vector3.Cross(up, a);
            if (u.sqrMagnitude < 0.000001f) u = Vector3.Cross(Vector3.right, a);
            u.Normalize();
            v = Vector3.Cross(a, u);
        }

        // The cut through the body at c, square to a: each separate outline's points, in (u, v) round c.
        private static List<List<Vector2>> Outlines(List<Vector3> points, List<int> triangles, Vector3 c, Vector3 a, Vector3 u, Vector3 v)
        {
            var side = new float[points.Count];
            for (int i = 0; i < points.Count; i++) side[i] = Vector3.Dot(points[i] - c, a);
            var crossing = new List<int>();
            for (int t = 0; t < triangles.Count; t += 3)
            {
                float s0 = side[triangles[t]], s1 = side[triangles[t + 1]], s2 = side[triangles[t + 2]];
                if (Mathf.Min(s0, Mathf.Min(s1, s2)) < 0f && Mathf.Max(s0, Mathf.Max(s1, s2)) >= 0f) crossing.Add(t);
            }
            var parent = new int[crossing.Count];
            for (int i = 0; i < parent.Length; i++) parent[i] = i;
            int Find(int x)
            {
                while (parent[x] != x) x = parent[x] = parent[parent[x]];
                return x;
            }
            var edges = new Dictionary<long, int>();
            var hits = new List<KeyValuePair<int, Vector2>>();
            for (int k = 0; k < crossing.Count; k++)
            {
                int t = crossing[k];
                for (int j = 0; j < 3; j++)
                {
                    int p = triangles[t + j], q = triangles[t + (j + 1) % 3];
                    if ((side[p] < 0f) == (side[q] < 0f)) continue;
                    long key = p < q ? ((long)p << 32) | (uint)q : ((long)q << 32) | (uint)p;
                    if (edges.TryGetValue(key, out int other)) parent[Find(k)] = Find(other);
                    else edges[key] = k;
                    float f = side[p] / (side[p] - side[q]);
                    var x = points[p] + (points[q] - points[p]) * f - c;
                    hits.Add(new KeyValuePair<int, Vector2>(k, new Vector2(Vector3.Dot(x, u), Vector3.Dot(x, v))));
                }
            }
            var groups = new Dictionary<int, List<Vector2>>();
            foreach (var hit in hits)
            {
                int root = Find(hit.Key);
                if (!groups.TryGetValue(root, out var list)) groups[root] = list = new List<Vector2>();
                list.Add(hit.Value);
            }
            var result = new List<List<Vector2>>();
            foreach (var g in groups.Values)
                if (g.Count >= 6) result.Add(g);
            return result;
        }

        // Convex hull, counter-clockwise (monotone chain).
        private static List<Vector2> Hull(List<Vector2> pts)
        {
            pts.Sort((p, q) => p.x != q.x ? p.x.CompareTo(q.x) : p.y.CompareTo(q.y));
            var hull = new List<Vector2>();
            float Cross(Vector2 o, Vector2 p, Vector2 q) => (p.x - o.x) * (q.y - o.y) - (p.y - o.y) * (q.x - o.x);
            for (int pass = 0; pass < 2; pass++)
            {
                int start = hull.Count;
                for (int i = 0; i < pts.Count; i++)
                {
                    var p = pts[pass == 0 ? i : pts.Count - 1 - i];
                    while (hull.Count >= start + 2 && Cross(hull[hull.Count - 2], hull[hull.Count - 1], p) <= 0f) hull.RemoveAt(hull.Count - 1);
                    hull.Add(p);
                }
                hull.RemoveAt(hull.Count - 1);
            }
            return hull;
        }

        private static bool Contains(List<Vector2> hull, Vector2 p)
        {
            for (int i = 0; i < hull.Count; i++)
            {
                var o = hull[i];
                var q = hull[(i + 1) % hull.Count];
                if ((q.x - o.x) * (p.y - o.y) - (q.y - o.y) * (p.x - o.x) < 0f) return false;
            }
            return hull.Count >= 3;
        }

        private static float Length(List<Vector2> hull)
        {
            float total = 0f;
            for (int i = 0; i < hull.Count; i++) total += Vector2.Distance(hull[i], hull[(i + 1) % hull.Count]);
            return total;
        }

        private static float Area(List<Vector2> hull)
        {
            float total = 0f;
            for (int i = 0; i < hull.Count; i++)
            {
                var p = hull[i];
                var q = hull[(i + 1) % hull.Count];
                total += p.x * q.y - q.x * p.y;
            }
            return Mathf.Abs(total) * 0.5f;
        }

        // The outline the rope goes round at c: the one around the centre line (else the nearest), the biggest of
        // those (fur over skin), and only one that fits within reach.
        private static List<Vector2> Ring(List<Vector3> points, List<int> triangles, Vector3 c, Vector3 a, Vector3 u, Vector3 v, float reach)
        {
            List<Vector2> best = null;
            bool bestInside = false;
            float bestDistance = 0f, bestArea = 0f;
            foreach (var outline in Outlines(points, triangles, c, a, u, v))
            {
                float nearest = float.MaxValue;
                foreach (var p in outline) nearest = Mathf.Min(nearest, p.magnitude);
                var hull = Hull(outline);
                if (hull.Count < 3) continue;
                bool fits = true;
                foreach (var p in hull) if (p.magnitude > reach) { fits = false; break; }
                if (!fits) continue;
                bool inside = Contains(hull, Vector2.zero);
                float distance = inside ? 0f : nearest, area = Area(hull);
                bool better = best == null || (inside && !bestInside) ||
                    (inside == bestInside && (distance < bestDistance - 0.0001f || (Mathf.Abs(distance - bestDistance) <= 0.0001f && area > bestArea)));
                if (!better) continue;
                best = hull;
                bestInside = inside;
                bestDistance = distance;
                bestArea = area;
            }
            return best != null && bestDistance <= reach * 0.25f ? best : null;
        }

        private static Vector2 Middle(List<Vector2> hull)
        {
            var sum = Vector2.zero;
            foreach (var p in hull) sum += p;
            return sum / hull.Count;
        }

        // The ring between two bones, from the body's real outline. Null if the body can't be read.
        internal static Fit Between(Character animal, Vector3 from, Vector3 to, int bins, float ropeRadius)
        {
            var points = new List<Vector3>();
            var triangles = new List<int>();
            if (!Body(animal, points, triangles)) return null;
            float length = Vector3.Distance(from, to);
            if (length < 0.001f) return null;
            float reach = Mathf.Max(animal.GetRadius(), length) * 2f;
            Vector3 up = animal.transform.up;
            Frame(to - from, up, out var a, out var u, out var v);

            // The neck's own centre line, through the middles of its outlines.
            var middles = new List<Vector3>();
            for (int i = 0; i <= 8; i++)
            {
                var c = Vector3.Lerp(from, to, i / 8f);
                var ring = Ring(points, triangles, c, a, u, v, reach);
                if (ring == null) continue;
                var m = Middle(ring);
                middles.Add(c + u * m.x + v * m.y);
            }
            if (middles.Count >= 3)
            {
                var mean = Vector3.zero;
                foreach (var m in middles) mean += m;
                mean /= middles.Count;
                // The main direction of the middles (power iteration on their spread), starting from the bone line.
                var dir = a;
                for (int it = 0; it < 16; it++)
                {
                    var next = Vector3.zero;
                    foreach (var m in middles) next += (m - mean) * Vector3.Dot(m - mean, dir);
                    if (next.sqrMagnitude < 1e-12f) break;
                    dir = next.normalized;
                }
                if (Vector3.Dot(dir, a) < 0f) dir = -dir;
                if (Vector3.Dot(dir, a) > 0.7f)
                {
                    Frame(dir, up, out a, out u, out v);
                    from = mean + dir * Vector3.Dot(from - mean, dir);
                    to = mean + dir * Vector3.Dot(to - mean, dir);
                }
            }

            // The smallest outline between the bones; on near-ties, the one nearer the body.
            var found = new List<KeyValuePair<float, List<Vector2>>>();
            var at = new List<float>();
            float smallest = float.MaxValue;
            // Not right at the head bone: there the cut goes through the skull and jaw (the bear's ring bowed out).
            for (int i = 0; i <= 17; i++)
            {
                float t = i / 20f;
                var ring = Ring(points, triangles, Vector3.Lerp(from, to, t), a, u, v, reach);
                if (ring == null) continue;
                float round = Length(ring);
                found.Add(new KeyValuePair<float, List<Vector2>>(round, ring));
                at.Add(t);
                smallest = Mathf.Min(smallest, round);
            }
            if (found.Count == 0) return null;
            int pick = 0;
            for (int i = 0; i < found.Count; i++)
                if (found[i].Key <= smallest * 1.02f) { pick = i; break; }
            // Then a touch further back towards the body, so it sits on the neck rather than up against the head.
            float back = Mathf.Max(0f, at[pick] - Back / length);
            var hull = back < at[pick] ? Ring(points, triangles, Vector3.Lerp(from, to, back), a, u, v, reach) : null;
            if (hull != null) at[pick] = back;
            else hull = found[pick].Value;
            var middle = Middle(hull);
            var fit = new Fit
            {
                At = at[pick],
                Round = Length(hull),
                A = u,
                C = v,
                Center = Vector3.Lerp(from, to, at[pick]) + u * middle.x + v * middle.y,
                Radii = new float[bins]
            };
            // The hull as a distance from its middle at each angle, plus the rope's own thickness so it rests on top.
            for (int b = 0; b < bins; b++)
            {
                float angle = (b + 0.5f) * Mathf.PI * 2f / bins;
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float r = 0f;
                for (int i = 0; i < hull.Count; i++)
                {
                    var p = hull[i] - middle;
                    var q = hull[(i + 1) % hull.Count] - middle;
                    var e = q - p;
                    float den = dir.x * e.y - dir.y * e.x;
                    if (Mathf.Abs(den) < 1e-9f) continue;
                    float s = (p.x * e.y - p.y * e.x) / den;      // along the ray
                    float w = (p.x * dir.y - p.y * dir.x) / den;  // along the edge
                    if (s > 0f && w >= -0.0001f && w <= 1.0001f) r = Mathf.Max(r, s);
                }
                fit.Radii[b] = r + ropeRadius;
            }
            return fit;
        }
    }
}
