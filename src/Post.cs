// Sleipnir's Post: a hitching post. Use it with a tamed animal following you to tie that animal to it; the animal then
// stays within a rope's length of the post instead of wandering, and a rope (the harpoon's rope material) runs from the
// post's ring to it. Use it with the hammer's alt key (Shift) to untie everything tied to it, which then follows you.
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
        private readonly Dictionary<Character, Rope> ropes = new Dictionary<Character, Rope>();
        private float nextScan;
        private static bool ropeLooked;

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
        // Each tied animal gets a rope from the post's nearer ring to a loop around its neck, both drawn with the
        // harpoon's own rope (its material, width and tiling).
        private sealed class Rope { public LineRenderer Line, Collar; }

        private void Update()
        {
            if (GUIManagerHeadless()) return;
            if (Time.time >= nextScan)
            {
                nextScan = Time.time + 0.5f;
                var tied = Tied();
                foreach (var gone in ropes.Keys.Where(c => !c || !tied.Contains(c)).ToList())
                {
                    Remove(ropes[gone]);
                    ropes.Remove(gone);
                }
                foreach (var animal in tied)
                    if (!ropes.ContainsKey(animal)) ropes[animal] = new Rope { Line = MakeLine("PotatoPostRope", 10, false), Collar = MakeLine("PotatoPostCollar", 12, true) };
            }
        }

        private static void Remove(Rope rope)
        {
            if (rope?.Line) Destroy(rope.Line.gameObject);
            if (rope?.Collar) Destroy(rope.Collar.gameObject);
        }

        private static readonly AccessTools.FieldRef<Character, Transform> HeadBone = AccessTools.FieldRefAccess<Character, Transform>("m_head");

        // The neck: part way from the head back towards the body's centre. The loop lies across the neck.
        private static void Neck(Character animal, out Vector3 neck, out Vector3 along, out float radius)
        {
            Vector3 center = animal.GetCenterPoint();
            var headBone = HeadBone(animal);
            Vector3 head = headBone ? headBone.position : center + animal.transform.forward * animal.GetRadius() + Vector3.up * animal.GetRadius() * 0.5f;
            neck = Vector3.Lerp(head, center, 0.35f);
            along = head - center;
            if (along.sqrMagnitude < 0.0001f) along = animal.transform.forward;
            along.Normalize();
            radius = Mathf.Clamp(animal.GetRadius() * 0.42f, 0.12f, 0.5f);
        }

        private void LateUpdate()
        {
            foreach (var pair in ropes)
            {
                var animal = pair.Key;
                var rope = pair.Value;
                if (!animal || rope?.Line == null || rope.Collar == null) continue;
                Neck(animal, out var neck, out var along, out float radius);
                // The collar: a ring across the neck.
                Vector3 a = Vector3.Cross(along, Vector3.up);
                if (a.sqrMagnitude < 0.0001f) a = Vector3.Cross(along, Vector3.right);
                a.Normalize();
                Vector3 c = Vector3.Cross(along, a).normalized;
                int n = rope.Collar.positionCount;
                for (int i = 0; i < n; i++)
                {
                    float angle = i * Mathf.PI * 2f / n;
                    rope.Collar.SetPosition(i, neck + (a * Mathf.Cos(angle) + c * Mathf.Sin(angle)) * radius);
                }
                // The lead: from the nearer ring to the point of the collar facing the post.
                Vector3 start = Rings.Select(r => transform.TransformPoint(r * Size)).OrderBy(r => (r - neck).sqrMagnitude).First();
                Vector3 toPost = Vector3.ProjectOnPlane(start - neck, along);
                Vector3 end = neck + (toPost.sqrMagnitude > 0.0001f ? toPost.normalized : a) * radius;
                var line = rope.Line;
                float length = Vector3.Distance(start, end);
                float sag = Mathf.Clamp(Plugin.PostRope.Value - length, 0f, 1.2f) * 0.5f + 0.05f;
                for (int i = 0; i < line.positionCount; i++)
                {
                    float t = i / (float)(line.positionCount - 1);
                    line.SetPosition(i, Vector3.Lerp(start, end, t) + Vector3.down * (sag * 4f * t * (1f - t)));
                }
            }
        }

        private void OnDestroy()
        {
            foreach (var rope in ropes.Values) Remove(rope);
            ropes.Clear();
        }

        private LineRenderer MakeLine(string name, int points, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = points;
            line.loop = loop;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var look = HarpoonRope();
            if (look)
            {
                line.sharedMaterial = look.sharedMaterial;
                line.textureMode = look.textureMode;
                line.widthCurve = look.widthCurve;
                line.colorGradient = look.colorGradient;
                line.alignment = look.alignment;
                line.numCapVertices = look.numCapVertices;
                // The harpoon's rope is at its thickest up close; a tie rope always is.
                line.widthMultiplier = harpoonThickness * (loop ? 0.7f : 1f);
            }
            else
            {
                line.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
                line.startColor = line.endColor = new Color(0.36f, 0.27f, 0.16f);
                line.widthMultiplier = loop ? 0.03f : 0.045f;
            }
            return line;
        }

        private static LineRenderer harpoonLine;
        private static float harpoonThickness = 0.8f;

        // The rope the game draws for the Abyssal Harpoon: the "Harpooned" status effect spawns it when the harpoon hits.
        private static LineRenderer HarpoonRope()
        {
            if (ropeLooked) return harpoonLine;
            ropeLooked = true;
            var db = ObjectDB.instance;
            if (db)
            {
                foreach (var effect in db.m_StatusEffects.OfType<SE_Harpooned>())
                    foreach (var entry in effect.m_startEffects?.m_effectPrefabs ?? new EffectList.EffectData[0])
                    {
                        var connect = entry?.m_prefab ? entry.m_prefab.GetComponentInChildren<LineConnect>(true) : null;
                        var line = connect ? connect.GetComponent<LineRenderer>() : null;
                        if (!line || !line.sharedMaterial) continue;
                        harpoonLine = line;
                        harpoonThickness = connect.m_dynamicThickness ? connect.m_maxThickness : line.widthMultiplier;
                        Plugin.Log.LogInfo($"Hitching post rope: {line.sharedMaterial.name} from {entry.m_prefab.name} ({effect.name}), thickness {harpoonThickness}.");
                        return harpoonLine;
                    }
            }
            Plugin.Log.LogWarning("The harpoon's rope wasn't found; the hitching post draws a plain rope.");
            return null;
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
