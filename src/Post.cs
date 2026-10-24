// Sleipnir's Post: a hitching post. Use it with a tamed animal following you to tie that animal to it; the animal then
// stays within a rope's length of the post instead of wandering, and a rope runs from the
// post's ring to a ring around its neck. Use it with the hammer's alt key (Shift) to untie everything tied to it, which then follows you.
// Built on the game's own "stay": tying sets the animal's patrol point to the post and shortens its wander range while
// tied. The tie lives in the animal's network data, so every player sees the same ropes and it survives restarts.
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace PotatoFarmer
{
    public class Post : MonoBehaviour, Hoverable, Interactable
    {
        public const float Size = 1f;
        private const string TiedUser = "PotatoPost.User", TiedId = "PotatoPost.Id";
        private const float FollowersWithin = 20f;
        private const float Beside = 1.5f;
        // The bottoms of the two iron rings hanging from the crossbar, in the model's own space.
        private static readonly Vector3[] Rings = { new Vector3(0.28f, 1.55f, 0f), new Vector3(-0.28f, 1.55f, 0f) };

        private ZNetView view;

        private float nextScan;
        private readonly Dictionary<Character, Rope> ropes = new Dictionary<Character, Rope>();

        private void Awake()
        {
            view = GetComponent<ZNetView>();
            var wear = GetComponent<WearNTear>();
            if (wear) wear.m_onDestroyed += UntieAll;
        }

        private ZDOID Id => view && view.GetZDO() != null ? view.GetZDO().m_uid : ZDOID.None;

        internal static bool IsTiedTo(ZDO animal, ZDOID post) =>
            animal != null && !post.IsNone() && animal.GetLong(TiedUser, 0L) == post.UserID && (uint)animal.GetInt(TiedId, 0) == post.ID;

        internal static bool IsTied(ZDO animal) => animal != null && animal.GetInt(TiedId, 0) != 0;

        private List<Character> Tied()
        {
            var id = Id;
            return Character.GetAllCharacters().Where(c => c && c.IsTamed() && IsTiedTo(c.GetComponent<ZNetView>()?.GetZDO(), id)).ToList();
        }

        private static List<Character> Following(Humanoid user) =>
            Character.GetAllCharacters().Where(c => c && c.IsTamed() && c.GetComponent<MonsterAI>() is MonsterAI ai && ai.GetFollowTarget() == user.gameObject &&
                Vector3.Distance(c.transform.position, user.transform.position) < FollowersWithin).ToList();

        public float GetHoverOffset() => 0f;

        public string GetHoverName() => Localization.instance.Localize(Plugin.PostName);

        public string GetHoverText()
        {
            if (!view || !view.IsValid()) return "";
            var tied = Tied();
            var text = Plugin.PostName;
            if (tied.Count > 0) text += "\n" + string.Join(", ", tied.Select(c => c.GetHoverName()).ToArray()) + " tied here";
            var player = Player.m_localPlayer;
            var follower = player ? Following(player).OrderBy(c => Vector3.Distance(c.transform.position, transform.position)).FirstOrDefault() : null;
            if (follower && tied.Count < Plugin.PostAnimals.Value) text += $"\n[<color=yellow><b>$KEY_Use</b></color>] Tie {follower.GetHoverName()}";
            else if (tied.Count >= Plugin.PostAnimals.Value) text += $"\nHolds {Plugin.PostAnimals.Value}";
            else if (tied.Count == 0) text += "\nBring a tamed animal that follows you";
            if (tied.Count > 0) text += "\n[<color=yellow><b>$KEY_AltPlace + $KEY_Use</b></color>] Untie and lead away";
            return Localization.instance.Localize(text);
        }

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (hold || !view || !view.IsValid() || !(user is Player player)) return false;
            if (alt)
            {
                var tied = Tied();
                foreach (var animal in tied) Send(animal, "PotatoUntie", Id, player.GetZDOID());
                return tied.Count > 0;
            }
            if (Tied().Count >= Plugin.PostAnimals.Value) return false;
            var follower = Following(player).OrderBy(c => Vector3.Distance(c.transform.position, transform.position)).FirstOrDefault();
            if (!follower) return false;
            Send(follower, "PotatoTie", Id, player.GetZDOID());
            return true;
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item) => false;

        private static void Send(Character animal, string rpc, ZDOID post, ZDOID player)
        {
            var nview = animal.GetComponent<ZNetView>();
            if (nview && nview.IsValid()) nview.InvokeRPC(rpc, post, player);
        }

        // The post is gone: whatever was tied to it stays where it is, untied.
        private void UntieAll()
        {
            foreach (var animal in Tied()) Send(animal, "PotatoUntie", Id, ZDOID.None);
        }

        // ------------------------------------------------------------------ ropes
        // Each tied animal gets a real rope: a round tube built along a sagging curve every frame, from the post's nearer
        // ring to a rope ring around the animal's neck. The ring hangs on the neck bone, so it moves with the animation.
        private const float RopeRadius = 0.028f;
        private const int Sides = 12, Segments = 12;
        // Three strands twisting round: one full turn of the lay every Twist metres, shaped into the tube and the texture.
        private const float Twist = 0.14f, Spacing = 0.02f;
        private sealed class Rope
        {
            public GameObject Lead, Collar;
            public Mesh Mesh, CollarMesh;
            public Transform Neck;
            // The neck ring, measured off the animal's body when tied: its centre and axes in the neck bone's space, and
            // its distance from the centre (in metres) at each of the Bins angles round the neck.
            public Vector3 LocalCenter, LocalA, LocalC;
            public float[] Radii;
        }
        private const int Bins = 48;
        private const float Pull = 0.045f;  // how far the lead draws the ring out towards the post
        private static Material ropeLook;

        private void Update()
        {
            if (GUIManagerHeadless()) return;
            if (Time.time < nextScan) return;
            nextScan = Time.time + 0.5f;
            var tied = Tied();
            foreach (var gone in ropes.Keys.Where(c => !c || !tied.Contains(c)).ToList())
            {
                Remove(ropes[gone]);
                ropes.Remove(gone);
            }
            foreach (var animal in tied)
                if (!ropes.ContainsKey(animal)) ropes[animal] = MakeRope(animal);
        }

        private static void Remove(Rope rope)
        {
            if (rope == null) return;
            if (rope.Lead) Destroy(rope.Lead);
            if (rope.Collar) Destroy(rope.Collar);
            if (rope.Mesh) Destroy(rope.Mesh);
            if (rope.CollarMesh) Destroy(rope.CollarMesh);
        }

        private static readonly AccessTools.FieldRef<Character, Transform> HeadBone = AccessTools.FieldRefAccess<Character, Transform>("m_head");

        // The neck bone: a bone named like a neck on the way from the body to the head (else the one nearest the head),
        // else the bone the head hangs from, else the head itself.
        private static Transform FindNeck(Character animal) => FindNeck(animal.transform, Head(animal), out _);

        // The head bone the game knows, else a bone named like a head (some rigs, like the hen's, have none set).
        private static Transform Head(Character animal) => HeadBone(animal) ?? NamedHead(animal.transform);

        private static Transform NamedHead(Transform root) => root.GetComponentsInChildren<Transform>(true)
            .Where(b => b != root && b.name.IndexOf("head", System.StringComparison.OrdinalIgnoreCase) >= 0 && b.GetComponent<Renderer>() == null)
            .OrderBy(b => b.name.Length).FirstOrDefault();

        internal static Transform FindNeck(Transform root, Transform head, out string how)
        {
            bool Neckish(Transform b) => b.name.IndexOf("neck", System.StringComparison.OrdinalIgnoreCase) >= 0 &&
                b.name.IndexOf("lace", System.StringComparison.OrdinalIgnoreCase) < 0;
            if (head)
                for (var b = head.parent; b && b != root; b = b.parent)
                    if (Neckish(b)) { how = "neck bone"; return b; }
            var necks = root.GetComponentsInChildren<Transform>(true).Where(Neckish).ToList();
            if (necks.Count > 0)
            {
                how = "neck bone (off the head's chain)";
                return head ? necks.OrderBy(b => (b.position - head.position).sqrMagnitude).First() : necks[0];
            }
            if (head && head.parent && head.parent != root)
            {
                how = "the bone the head hangs from";
                return head.parent;
            }
            how = head ? "the head (no neck)" : "no head bone: rope ties to the body";
            return head;
        }

        private Rope MakeRope(Character animal)
        {
            var rope = new Rope { Mesh = new Mesh { name = "potato_post_rope" } };
            rope.Mesh.MarkDynamic();
            rope.Lead = Holder("PotatoPostRope", rope.Mesh);
            var head = Head(animal);
            rope.Neck = FindNeck(animal.transform, head, out string how);
            NeckData.Save(animal, head, rope.Neck);
            if (!rope.Neck) return rope;
            // Where the ring goes: between the neck and head bones, or for creatures with no neck to speak of
            // (Greydwarf, Greyling) a belt between the two lowest spine bones.
            Transform from = rope.Neck, to = head && head != rope.Neck ? head : null;
            string where = "neck";
            if (head && Waist(animal) && head.parent && head.parent.parent && head.parent.parent.parent)
            {
                from = head.parent.parent.parent;
                to = head.parent.parent;
                where = "waist";
            }
            NeckRing.Fit fit = null;
            if (to)
                try { fit = NeckRing.Between(animal, from.position, to.position, Bins, RopeRadius * 0.8f, SetPlace(animal)); }
                catch (System.Exception e) { Plugin.Log.LogWarning($"Hitching post: couldn't measure {animal.name}: {e.GetBaseException().Message}"); }
            Vector3 center, a, c;
            if (fit != null)
            {
                center = fit.Center;
                a = fit.A;
                c = fit.C;
                rope.Radii = fit.Radii;
                // The ring rides on whichever bone it sits nearer, so it stays put when the head turns.
                rope.Neck = fit.At < 0.5f ? from : to;
                Plugin.Log.LogDebug($"Hitching post: {Utils.GetPrefabName(animal.gameObject)} {where} ring {fit.Round + 2f * Mathf.PI * RopeRadius * 0.8f:0.00} m round, " +
                    $"{fit.At:0.00} of the way from {from.name} to {to.name}.");
            }
            else
            {
                // Plain circle round the neck bone, square to the line to the head (or the body's forward).
                center = rope.Neck.position;
                Vector3 along = to ? to.position - from.position : animal.transform.forward;
                if (along.sqrMagnitude < 0.0001f) along = animal.transform.forward;
                along.Normalize();
                a = Vector3.Cross(along, animal.transform.up);
                if (a.sqrMagnitude < 0.0001f) a = Vector3.Cross(along, Vector3.right);
                a.Normalize();
                c = Vector3.Cross(along, a).normalized;
                rope.Radii = Enumerable.Repeat(Mathf.Clamp(animal.GetRadius() * 0.26f, 0.08f, 0.38f), Bins).ToArray();
                Plugin.Log.LogDebug($"Hitching post: {Utils.GetPrefabName(animal.gameObject)} no outline found round the {where}, plain ring.");
            }
            rope.LocalCenter = rope.Neck.InverseTransformPoint(center);
            rope.LocalA = rope.Neck.InverseTransformDirection(a);
            rope.LocalC = rope.Neck.InverseTransformDirection(c);
            rope.CollarMesh = new Mesh { name = "potato_post_collar" };
            rope.CollarMesh.MarkDynamic();
            rope.Collar = Holder("PotatoPostCollar", rope.CollarMesh);
            return rope;
        }

        private GameObject Holder(string name, Mesh mesh)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = RopeLook();
            return go;
        }

        // Creatures whose neck outline changes too much with their pose for the narrowest-place search, so the ring goes
        // at a set place between the neck and head bones instead (0 = neck bone, 1 = head bone).
        private static readonly Dictionary<string, float> SetPlaces = new Dictionary<string, float> { { "Bjorn", 0.88f } };
        private static float SetPlace(Character animal)
        {
            string name = Utils.GetPrefabName(animal.gameObject);
            foreach (var pair in SetPlaces) if (name.StartsWith(pair.Key, System.StringComparison.OrdinalIgnoreCase)) return pair.Value;
            return -1f;
        }

        // Creatures with no real neck, which wear the rope as a belt instead.
        private static readonly string[] Waisted = { "Greydwarf", "Greyling" };
        private static bool Waist(Character animal)
        {
            string name = Utils.GetPrefabName(animal.gameObject);
            foreach (var w in Waisted) if (name.StartsWith(w, System.StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private void LateUpdate()
        {
            foreach (var pair in ropes)
            {
                var animal = pair.Key;
                var rope = pair.Value;
                if (!animal || rope?.Mesh == null) continue;
                Vector3 neck = rope.Neck ? rope.Neck.TransformPoint(rope.LocalCenter) : animal.GetCenterPoint() + Vector3.up * (animal.GetRadius() * 0.4f);
                Vector3 start = Rings.Select(r => transform.TransformPoint(r * Size)).OrderBy(r => (r - neck).sqrMagnitude).First();
                Vector3 end = neck;
                if (rope.Neck && rope.CollarMesh != null)
                {
                    Vector3 a = rope.Neck.TransformDirection(rope.LocalA).normalized;
                    Vector3 c = rope.Neck.TransformDirection(rope.LocalC).normalized;
                    Vector3 axis = Vector3.Cross(a, c);
                    Vector3 toPost = Vector3.ProjectOnPlane(start - neck, axis);
                    Vector3 d = toPost.sqrMagnitude > 0.0001f ? toPost.normalized : a;
                    // The ring round the neck, drawn out into a point where the lead pulls it.
                    var loop = new Vector3[Bins + 1];
                    float best = -2f;
                    for (int i = 0; i <= Bins; i++)
                    {
                        float angle = (i % Bins + 0.5f) * Mathf.PI * 2f / Bins;
                        Vector3 dir = a * Mathf.Cos(angle) + c * Mathf.Sin(angle);
                        float facing = Vector3.Dot(dir, d);
                        float w = Mathf.Max(0f, facing);
                        loop[i] = neck + dir * (rope.Radii[i % Bins] + Pull * w * w * w * w * w * w);
                        if (facing > best) { best = facing; end = loop[i]; }
                    }
                    Tube(rope.CollarMesh, loop, rope.Collar.transform);
                }
                float length = Vector3.Distance(start, end);
                float sag = Mathf.Clamp(Plugin.PostRope.Value - length, 0f, 1.2f) * 0.5f + 0.05f;
                var points = new Vector3[Segments + 1];
                for (int i = 0; i <= Segments; i++)
                {
                    float t = i / (float)Segments;
                    points[i] = Vector3.Lerp(start, end, t) + Vector3.down * (sag * 4f * t * (1f - t));
                }
                Tube(rope.Mesh, Resample(points), rope.Lead.transform);
            }
        }

        private void OnDestroy()
        {
            foreach (var rope in ropes.Values) Remove(rope);
            ropes.Clear();
        }

        // Points every couple of centimetres along the curve, so the strands' twist shows.
        private static Vector3[] Resample(Vector3[] points)
        {
            float total = 0f;
            for (int i = 1; i < points.Length; i++) total += Vector3.Distance(points[i - 1], points[i]);
            int count = Mathf.Clamp(Mathf.CeilToInt(total / Spacing), 2, 600) + 1;
            var result = new Vector3[count];
            int seg = 1;
            float segStart = 0f, segLength = Vector3.Distance(points[0], points[1]);
            for (int i = 0; i < count; i++)
            {
                float d = total * i / (count - 1);
                while (seg < points.Length - 1 && d > segStart + segLength)
                {
                    segStart += segLength;
                    seg++;
                    segLength = Vector3.Distance(points[seg - 1], points[seg]);
                }
                result[i] = Vector3.Lerp(points[seg - 1], points[seg], segLength > 0f ? Mathf.Clamp01((d - segStart) / segLength) : 0f);
            }
            return result;
        }

        // A round tube through the points (in world space), written into the mesh in the holder's local space.
        private static void Tube(Mesh mesh, Vector3[] points, Transform holder)
        {
            int rings = points.Length;
            var vertices = new Vector3[rings * (Sides + 1)];
            var normals = new Vector3[vertices.Length];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[(rings - 1) * Sides * 6];
            float along = 0f;
            Vector3 side = Vector3.up;
            for (int i = 0; i < rings; i++)
            {
                Vector3 dir = (i < rings - 1 ? points[i + 1] - points[i] : points[i] - points[i - 1]).normalized;
                if (i > 0) along += Vector3.Distance(points[i], points[i - 1]);
                Vector3 a = Vector3.Cross(dir, Mathf.Abs(Vector3.Dot(dir, Vector3.up)) > 0.95f ? Vector3.right : Vector3.up).normalized;
                Vector3 b = Vector3.Cross(dir, a);
                for (int k = 0; k <= Sides; k++)
                {
                    float angle = k * Mathf.PI * 2f / Sides;
                    Vector3 n = a * Mathf.Cos(angle) + b * Mathf.Sin(angle);
                    int v = i * (Sides + 1) + k;
                    // Three strands: the surface swells over each strand and dips into the grooves between them.
                    float lay = 3f * k / Sides - along / Twist;
                    float swell = 0.84f + 0.16f * Mathf.Cos(lay * Mathf.PI * 2f);
                    vertices[v] = holder.InverseTransformPoint(points[i] + n * (RopeRadius * swell));
                    normals[v] = holder.InverseTransformDirection(n);
                    uvs[v] = new Vector2(k / (float)Sides, along / Twist);
                }
            }
            int tri = 0;
            for (int i = 0; i < rings - 1; i++)
                for (int k = 0; k < Sides; k++)
                {
                    int v0 = i * (Sides + 1) + k, v1 = v0 + 1, v2 = v0 + Sides + 1, v3 = v2 + 1;
                    triangles[tri++] = v0; triangles[tri++] = v2; triangles[tri++] = v1;
                    triangles[tri++] = v1; triangles[tri++] = v2; triangles[tri++] = v3;
                }
            mesh.Clear();
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
        }

        // Rope look: the game's piece shader (lit like the post) with a twisted-rope texture drawn in pixels.
        private static Material RopeLook()
        {
            if (ropeLook) return ropeLook;
            var template = Jotunn.Managers.PrefabManager.Cache.GetPrefab<Material>("ironchest");
            ropeLook = template ? new Material(template) : new Material(Shader.Find("Standard"));
            ropeLook.name = "potato_post_rope";
            // One turn of the lay: u goes round the rope, v along it. Each of the three strands is a light band with a dark
            // groove on either side, following the same twist as the tube's shape, with fibres drawn as lighter flecks.
            const int w = 12, h = 12;
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat, name = "potato_rope_d" };
            var light = new Color(0.82f, 0.7f, 0.48f);
            var mid = new Color(0.66f, 0.54f, 0.35f);
            var dark = new Color(0.36f, 0.27f, 0.17f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float lay = 3f * (x + 0.5f) / w - (y + 0.5f) / h;
                    float f = lay - Mathf.Floor(lay);            // 0..1 across one strand
                    var color = f < 0.12f || f > 0.9f ? dark : f < 0.3f || f > 0.75f ? mid : light;
                    if (color == light && (x * 7 + y * 3) % 5 == 0) color = Color.Lerp(light, Color.white, 0.25f);
                    texture.SetPixel(x, y, color);
                }
            texture.Apply();
            ropeLook.SetTexture("_MainTex", texture);
            if (ropeLook.HasProperty("_Color")) ropeLook.SetColor("_Color", Color.white);
            var black = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            black.SetPixel(0, 0, new Color(0f, 0f, 0f, 0f));
            black.Apply();
            if (ropeLook.HasProperty("_MetallicTex")) ropeLook.SetTexture("_MetallicTex", black);
            if (ropeLook.HasProperty("_MetallicGlossMap")) ropeLook.SetTexture("_MetallicGlossMap", black);
            if (ropeLook.HasProperty("_BumpMap")) ropeLook.SetTexture("_BumpMap", null);
            if (ropeLook.HasProperty("_Metallic")) ropeLook.SetFloat("_Metallic", 0f);
            return ropeLook;
        }

        private static bool GUIManagerHeadless() => Jotunn.Managers.GUIManager.IsHeadless();

        // ------------------------------------------------------------------ tying, on the animal's owner
        [HarmonyPatch(typeof(Tameable), "Awake")]
        private static class Rpcs
        {
            private static void Postfix(Tameable __instance)
            {
                var nview = __instance.GetComponent<ZNetView>();
                if (!nview || nview.GetZDO() == null) return;
                var tame = __instance;
                nview.Register<ZDOID, ZDOID>("PotatoTie", (sender, post, player) => Tie(tame, nview, post, player));
                nview.Register<ZDOID, ZDOID>("PotatoUntie", (sender, post, player) => Untie(tame, nview, post, player));
            }
        }

        private static void Tie(Tameable tame, ZNetView nview, ZDOID post, ZDOID player)
        {
            if (!nview.IsOwner() || post.IsNone()) return;
            var ai = tame.GetComponent<MonsterAI>();
            var postObject = ZNetScene.instance.FindInstance(post);
            if (!ai || !postObject) return;
            var zdo = nview.GetZDO();
            zdo.Set(TiedUser, post.UserID);
            zdo.Set(TiedId, (int)post.ID);
            ai.SetFollowTarget(null);
            zdo.Set(ZDOVars.s_follow, "");
            // The game's own "stay", centred beside the post on the animal's side: animals can't stand in the post itself
            // (a point at its centre lies inside it, and walking back to it would push into the pole).
            Vector3 side = tame.transform.position - postObject.transform.position;
            side.y = 0f;
            if (side.sqrMagnitude < 0.01f) side = postObject.transform.forward;
            zdo.Set(ZDOVars.s_patrolPoint, postObject.transform.position + side.normalized * Beside);
            zdo.Set(ZDOVars.s_patrol, true);
            var who = ZNetScene.instance.FindInstance(player)?.GetComponent<Player>();
            if (who == Player.m_localPlayer && who) who.Message(MessageHud.MessageType.Center, tame.GetHoverName() + " is tied");
        }

        private static void Untie(Tameable tame, ZNetView nview, ZDOID post, ZDOID player)
        {
            var zdo = nview.GetZDO();
            if (!nview.IsOwner() || !IsTiedTo(zdo, post)) return;
            zdo.Set(TiedUser, 0L);
            zdo.Set(TiedId, 0);
            var ai = tame.GetComponent<MonsterAI>();
            var leader = player.IsNone() ? null : ZNetScene.instance.FindInstance(player)?.GetComponent<Player>();
            if (ai && leader)
            {
                ai.ResetPatrolPoint();
                ai.SetFollowTarget(leader.gameObject);
                zdo.Set(ZDOVars.s_follow, leader.GetPlayerName());
            }
            else if (ai) ai.SetPatrolPoint(); // The post broke: stay put where it stands.
        }

        // A tied animal's patrol point (beside the post), or false if it isn't tied.
        private static readonly AccessTools.FieldRef<BaseAI, ZNetView> AiView = AccessTools.FieldRefAccess<BaseAI, ZNetView>("m_nview");
        private static bool TiedPoint(BaseAI ai, out Vector3 point)
        {
            point = Vector3.zero;
            var zdo = AiView(ai)?.GetZDO();
            if (!IsTied(zdo)) return false;
            point = zdo.GetVec3(ZDOVars.s_patrolPoint, ai.transform.position);
            return true;
        }

        // While tied, the animal wanders only within its rope (the game allows up to twice the range before it walks
        // back), and once it's past the rope's length it walks straight back instead of drifting back step by step.
        [HarmonyPatch(typeof(BaseAI), "RandomMovement")]
        private static class ShortRope
        {
            private static readonly System.Func<BaseAI, float, Vector3, float, bool, bool> MoveTo =
                AccessTools.MethodDelegate<System.Func<BaseAI, float, Vector3, float, bool, bool>>(AccessTools.Method(typeof(BaseAI), "MoveTo"));

            private static bool Prefix(BaseAI __instance, float dt, Vector3 centerPoint, out float __state)
            {
                __state = -1f;
                if (!TiedPoint(__instance, out var post)) return true;
                if (Utils.DistanceXZ(centerPoint, post) < 0.5f && Utils.DistanceXZ(__instance.transform.position, post) > Plugin.PostRope.Value)
                {
                    MoveTo(__instance, dt, post, Plugin.PostRope.Value * 0.25f, false);
                    return false;
                }
                __state = __instance.m_randomMoveRange;
                __instance.m_randomMoveRange = Mathf.Min(__instance.m_randomMoveRange, Plugin.PostRope.Value * 0.5f);
                return true;
            }

            private static void Finalizer(BaseAI __instance, float __state)
            {
                if (__state >= 0f) __instance.m_randomMoveRange = __state;
            }
        }

        // The rope holds in a fight too: a tied animal only goes after what comes within its rope's reach (plus a
        // swing), and drops anything further away, then walks back.
        [HarmonyPatch(typeof(MonsterAI), "UpdateTarget")]
        private static class RopeHoldsInFights
        {
            private const float Swing = 3f;
            private static readonly AccessTools.FieldRef<MonsterAI, Character> Target = AccessTools.FieldRefAccess<MonsterAI, Character>("m_targetCreature");
            private static readonly AccessTools.FieldRef<MonsterAI, StaticTarget> Static = AccessTools.FieldRefAccess<MonsterAI, StaticTarget>("m_targetStatic");

            private static void Postfix(MonsterAI __instance, ref bool canHearTarget, ref bool canSeeTarget)
            {
                if (!TiedPoint(__instance, out var post)) return;
                float reach = Plugin.PostRope.Value + Swing;
                var target = Target(__instance);
                if (target && Utils.DistanceXZ(target.transform.position, post) > reach)
                {
                    Target(__instance) = null;
                    canHearTarget = false;
                    canSeeTarget = false;
                }
                var still = Static(__instance);
                if (still && Utils.DistanceXZ(still.transform.position, post) > reach) Static(__instance) = null;
            }
        }

        // Telling a tied animal to follow (using it) unties it.
        [HarmonyPatch(typeof(Tameable), "RPC_Command")]
        private static class CommandUnties
        {
            private static readonly AccessTools.FieldRef<Tameable, ZNetView> View = AccessTools.FieldRefAccess<Tameable, ZNetView>("m_nview");

            private static void Prefix(Tameable __instance)
            {
                var zdo = View(__instance)?.GetZDO();
                if (zdo == null || !View(__instance).IsOwner() || !IsTied(zdo)) return;
                zdo.Set(TiedUser, 0L);
                zdo.Set(TiedId, 0);
            }
        }
    }
}
