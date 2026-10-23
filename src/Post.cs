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
        // The two iron rings on the post's sides, in the model's own space.
        private static readonly Vector3[] Rings = { new Vector3(0.32f, 1.45f, 0f), new Vector3(-0.33f, 1.62f, 0f) };

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
        private const float RopeRadius = 0.022f;
        private const int Sides = 6, Segments = 12;
        private sealed class Rope { public GameObject Lead, Collar; public Mesh Mesh; public Transform Neck; public float CollarRadius; }
        private static Material ropeLook;
        private static readonly HashSet<string> loggedBones = new HashSet<string>();

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
        }

        private static readonly AccessTools.FieldRef<Character, Transform> HeadBone = AccessTools.FieldRefAccess<Character, Transform>("m_head");

        // The neck bone: a bone named like a neck, else the bone the head hangs from.
        private static Transform FindNeck(Character animal)
        {
            var head = HeadBone(animal);
            var bones = animal.GetComponentsInChildren<Transform>(true);
            string key = animal.name.Replace("(Clone)", "");
            if (loggedBones.Add(key))
                Plugin.Log.LogInfo($"Hitching post: {key} head bone {(head ? head.name : "none")}, parent {(head && head.parent ? head.parent.name : "none")}; " +
                    $"neck-like bones: {string.Join(", ", bones.Where(b => b.name.IndexOf("neck", System.StringComparison.OrdinalIgnoreCase) >= 0).Select(b => b.name).ToArray())}");
            var necks = bones.Where(b => b.name.IndexOf("neck", System.StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            if (necks.Count > 0) return head ? necks.OrderBy(b => (b.position - head.position).sqrMagnitude).First() : necks[0];
            if (head && head.parent && head.parent != animal.transform) return head.parent;
            return head;
        }

        private Rope MakeRope(Character animal)
        {
            var rope = new Rope { Mesh = new Mesh { name = "potato_post_rope" } };
            rope.Mesh.MarkDynamic();
            rope.Lead = new GameObject("PotatoPostRope");
            rope.Lead.transform.SetParent(transform, false);
            rope.Lead.AddComponent<MeshFilter>().sharedMesh = rope.Mesh;
            rope.Lead.AddComponent<MeshRenderer>().sharedMaterial = RopeLook();
            rope.Neck = FindNeck(animal);
            var head = HeadBone(animal);
            if (rope.Neck)
            {
                // The ring lies across the neck: square to the line from the neck to the head (or the body's forward).
                Vector3 along = head && head != rope.Neck ? head.position - rope.Neck.position : animal.transform.forward;
                if (along.sqrMagnitude < 0.0001f) along = animal.transform.forward;
                rope.CollarRadius = Mathf.Clamp(animal.GetRadius() * 0.33f, 0.1f, 0.45f);
                rope.Collar = new GameObject("PotatoPostCollar");
                rope.Collar.transform.SetParent(rope.Neck, false);
                rope.Collar.transform.rotation = Quaternion.LookRotation(along.normalized, animal.transform.up);
                rope.Collar.transform.localScale = new Vector3(1f / Mathf.Max(0.0001f, rope.Neck.lossyScale.x), 1f / Mathf.Max(0.0001f, rope.Neck.lossyScale.y), 1f / Mathf.Max(0.0001f, rope.Neck.lossyScale.z));
                rope.Collar.AddComponent<MeshFilter>().sharedMesh = Ring(rope.CollarRadius);
                rope.Collar.AddComponent<MeshRenderer>().sharedMaterial = RopeLook();
            }
            return rope;
        }

        private void LateUpdate()
        {
            foreach (var pair in ropes)
            {
                var animal = pair.Key;
                var rope = pair.Value;
                if (!animal || rope?.Mesh == null) continue;
                Vector3 neck = rope.Collar ? rope.Collar.transform.position : animal.GetCenterPoint();
                Vector3 start = Rings.Select(r => transform.TransformPoint(r * Size)).OrderBy(r => (r - neck).sqrMagnitude).First();
                Vector3 end = neck;
                if (rope.Collar)
                {
                    // Where the lead meets the ring: the ring's point facing the post.
                    Vector3 axis = rope.Collar.transform.forward;
                    Vector3 toPost = Vector3.ProjectOnPlane(start - neck, axis);
                    if (toPost.sqrMagnitude > 0.0001f) end = neck + toPost.normalized * rope.CollarRadius;
                }
                float length = Vector3.Distance(start, end);
                float sag = Mathf.Clamp(Plugin.PostRope.Value - length, 0f, 1.2f) * 0.5f + 0.05f;
                var points = new Vector3[Segments + 1];
                for (int i = 0; i <= Segments; i++)
                {
                    float t = i / (float)Segments;
                    points[i] = Vector3.Lerp(start, end, t) + Vector3.down * (sag * 4f * t * (1f - t));
                }
                Tube(rope.Mesh, points, rope.Lead.transform);
            }
        }

        private void OnDestroy()
        {
            foreach (var rope in ropes.Values) Remove(rope);
            ropes.Clear();
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
                    vertices[v] = holder.InverseTransformPoint(points[i] + n * RopeRadius);
                    normals[v] = holder.InverseTransformDirection(n);
                    uvs[v] = new Vector2(k / (float)Sides, along * 6f);
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

        // The neck ring: a small torus of rope in the XY plane, around the Z axis.
        private static Mesh Ring(float radius)
        {
            const int around = 16;
            var points = new Vector3[around + 1];
            for (int i = 0; i <= around; i++)
            {
                float angle = i * Mathf.PI * 2f / around;
                points[i] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
            }
            var mesh = new Mesh { name = "potato_post_collar" };
            var holder = new GameObject("PotatoRingTemp").transform;
            Tube(mesh, points, holder);
            Destroy(holder.gameObject);
            return mesh;
        }

        // Rope look: the game's piece shader (lit like the post) with a twisted-rope texture drawn in pixels.
        private static Material RopeLook()
        {
            if (ropeLook) return ropeLook;
            var template = Jotunn.Managers.PrefabManager.Cache.GetPrefab<Material>("ironchest");
            ropeLook = template ? new Material(template) : new Material(Shader.Find("Standard"));
            ropeLook.name = "potato_post_rope";
            var texture = new Texture2D(8, 16, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat, name = "potato_rope_d" };
            var light = new Color(0.62f, 0.5f, 0.33f);
            var dark = new Color(0.36f, 0.27f, 0.17f);
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 8; x++)
                {
                    // Three strands twisting round: diagonal bands, each with a lit middle and a dark edge.
                    int band = ((x * 2 + y) % 16 + 16) % 16;
                    float shade = band < 2 ? 0f : band < 4 ? 0.4f : band < 9 ? 1f : 0.65f;
                    texture.SetPixel(x, y, Color.Lerp(dark, light, shade));
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

        // While tied, the animal wanders only a rope's length from the post (the game allows up to twice its range
        // before it walks back).
        [HarmonyPatch(typeof(BaseAI), "RandomMovement")]
        private static class ShortRope
        {
            private static readonly AccessTools.FieldRef<BaseAI, ZNetView> View = AccessTools.FieldRefAccess<BaseAI, ZNetView>("m_nview");

            private static void Prefix(BaseAI __instance, out float __state)
            {
                __state = -1f;
                var zdo = View(__instance)?.GetZDO();
                if (!IsTied(zdo)) return;
                __state = __instance.m_randomMoveRange;
                __instance.m_randomMoveRange = Mathf.Min(__instance.m_randomMoveRange, Plugin.PostRope.Value * 0.5f);
            }

            private static void Finalizer(BaseAI __instance, float __state)
            {
                if (__state >= 0f) __instance.m_randomMoveRange = __state;
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
