// How an item hangs from one of Læraðr's Rack's hooks (looks only). Worked out and checked offline against renders of
// every meat, honey and jerky on the rack before building (tools/hang/: hangplan2.py is the same rules in Python).
//
// The game's cooking spits hang meat by putting the item's "attach" model at the spit's slot, turned by the slot's
// rotation (CookingStation.SetSlotVisual; slot rotation (-0.5, 0.5, 0.5, 0.5) on both the wooden and the iron spit).
// So each meat first tries the game's own way, also turned a quarter turn about the vertical. The other way tried is
// the longest side hanging straight down, the end with more of the model lowest. The biggest fit wins, but the game's
// way is kept when it fits within 10 %; on a near tie, the way showing the broad side along the bar (to the front).
// An item is shrunk only when it can't fit its hook's share: along the bar it may overlap its neighbours by a third
// (as the iron spit allows), across it must stay within its own row, and it must clear the ground.
using System.Collections.Generic;
using UnityEngine;

namespace PotatoFarmer
{
    internal static class Hanging
    {
        internal struct Fit
        {
            public Quaternion Rotation;
            public float Scale;
            public Vector3 Position, Low, High;
            public bool Game;   // hung the game's spit way (false: longest side down)
        }

        private static readonly Quaternion SpitSlot = new Quaternion(-0.5f, 0.5f, 0.5f, 0.5f);

        // Which end of the longest side hangs lowest for known items (from their vertices, measured offline: the game's
        // meshes can't be read while it runs). Anything else hangs with its -1 end down.
        private static readonly Dictionary<string, int> LongSign = new Dictionary<string, int>
        {
            { "SerpentMeat", 1 }, { "HareMeat", 1 }, { "BoneMawSerpentMeat", 1 }, { "SharkMeat_TW", 1 }, { "Honey", 1 }, { "WolfJerky", 1 },
        };

        internal static int SignFor(string prefab) => LongSign.TryGetValue(prefab, out int sign) ? sign : -1;

        // bounds: the model's box in its own space, already multiplied by its own scale.
        internal static Fit Place(Bounds bounds, Vector3 hook, float roomAlong, float roomAcross, float roomDown, bool gameWay, int longSign)
        {
            var size0 = bounds.size;
            int longest = size0.x >= size0.y && size0.x >= size0.z ? 0 : size0.y >= size0.z ? 1 : 2;
            var bases = new List<KeyValuePair<bool, Quaternion>>();
            if (gameWay) bases.Add(new KeyValuePair<bool, Quaternion>(true, SpitSlot));
            bases.Add(new KeyValuePair<bool, Quaternion>(false, AxisDown(longest, longSign)));

            var options = new List<(float scale, bool game, float broad, Quaternion rotation, Vector3 low, Vector3 high)>();
            foreach (var basis in bases)
                foreach (float turn in new[] { 0f, 90f })
                {
                    var rotation = Quaternion.Euler(0f, turn, 0f) * basis.Value;
                    Box(bounds, rotation, out var low, out var high);
                    var size = high - low;
                    float scale = Mathf.Min(1f, Mathf.Min(roomAlong / Mathf.Max(size.x, 0.001f), Mathf.Min(roomDown / Mathf.Max(size.y, 0.001f), roomAcross / Mathf.Max(size.z, 0.001f))));
                    options.Add((scale, basis.Key, size.x / Mathf.Max(size.z, 0.001f), rotation, low * scale, high * scale));
                }
            float best = 0f;
            foreach (var option in options) best = Mathf.Max(best, option.scale);
            bool anyGame = false;
            foreach (var option in options) if (option.game && option.scale >= best * 0.9f) anyGame = true;
            int pick = -1;
            for (int i = 0; i < options.Count; i++)
            {
                var option = options[i];
                if (anyGame ? !(option.game && option.scale >= best * 0.9f) : option.scale < best - 0.03f) continue;
                if (pick < 0 || Better(option.scale, option.broad, options[pick].scale, options[pick].broad)) pick = i;
            }
            var chosen = options[pick];
            // Top of the item at the hook tip, centred under it.
            var offset = new Vector3(hook.x - (chosen.low.x + chosen.high.x) / 2f, hook.y - chosen.high.y, hook.z - (chosen.low.z + chosen.high.z) / 2f);
            return new Fit { Rotation = chosen.rotation, Scale = chosen.scale, Position = offset, Low = chosen.low + offset, High = chosen.high + offset, Game = chosen.game };
        }

        private static bool Better(float scale, float broad, float otherScale, float otherBroad)
        {
            float a = Mathf.Round(scale * 100f), b = Mathf.Round(otherScale * 100f);
            return a != b ? a > b : broad > otherBroad;
        }

        // The rotation that hangs the model's axis (0 x, 1 y, 2 z) straight down, sign picking which end; its other two
        // axes go along the bar (x) and across it (z).
        private static Quaternion AxisDown(int axis, int sign)
        {
            var images = new Vector3[3];
            int first = axis == 0 ? 1 : 0, second = axis == 2 ? 1 : 2;
            images[axis] = new Vector3(0f, -sign, 0f);
            images[first] = Vector3.right;
            images[second] = Vector3.forward;
            // Keep it a proper rotation (no mirroring).
            if (Vector3.Dot(Vector3.Cross(images[0], images[1]), images[2]) < 0f) images[second] = -images[second];
            return Quaternion.LookRotation(images[2], images[1]);
        }

        private static void Box(Bounds bounds, Quaternion rotation, out Vector3 low, out Vector3 high)
        {
            low = Vector3.positiveInfinity;
            high = Vector3.negativeInfinity;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? bounds.min.x : bounds.max.x, (i & 2) == 0 ? bounds.min.y : bounds.max.y, (i & 4) == 0 ? bounds.min.z : bounds.max.z);
                var turned = rotation * corner;
                low = Vector3.Min(low, turned);
                high = Vector3.Max(high, turned);
            }
        }
    }
}
