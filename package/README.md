# 🌾 Potato Farmer

**Farming and animal pieces for Valheim**, each with its own model, textured to blend right in with the game's own pieces. More pieces will join over time, and the server can switch any of them off.

## 🐗 Gullinbursti's Trough

**A feeding trough your animals eat from**, named for Gullinbursti, the god Freyr's golden-bristled boar. Build it, fill it with food, and walk away: hungry boars, wolves, lox and any other tameable animal in range come over and eat from it. Every bite is a normal meal to the game, so **taming, breeding and keeping tamed animals fed** all count, and mods that change those keep working.

Dark oak, hammered bronze bands and a carved boar head on each end.

![Tamed boars eating carrots from Gullinbursti's Trough](https://raw.githubusercontent.com/TheRealRel/PotatoFarmer/main/docs/images/trough-boars.jpg)

- **Build it** with the hammer under **Furniture**: 10 Wood and 2 Bronze, near a workbench.
- **Fill it** from your hotbar: look at it and press the food's **hotbar key (1-8)** to add that stack. It holds 40 pieces of food (the server can change this).
- **Take it back** with **Shift + E** (the alternate place key + Use). Whatever doesn't fit in your inventory lands at your feet.
- **Look at it** to see what's inside. The food also shows in the bowl.

Hungry animals within 25 m (the server can change this) walk over and eat one bite at a time, from the nearest trough holding something they eat. Which food counts is the game's own list for each animal, so modded animals and their foods work too. If the trough is broken or taken down, its food spills out.

- **It's not a chest.** Crafting from containers, quick-stack and similar mods leave its food alone.
- A bite waiting in the bowl isn't grabbed by auto-pickup when you walk past.
- With **LetMeTameYou**, animals that can't be tamed yet don't eat from it, the same as with food on the ground.

## 🐓 Gullinkambi's Roost

**A nesting box that gathers your hens' eggs**, named for Gullinkambi, the golden-combed rooster that wakes the warriors of Valhalla. Fine wood, black metal and a carved rooster head on each end.

![Gullinkambi's Roost in a hen pen with chicks](https://raw.githubusercontent.com/TheRealRel/PotatoFarmer/main/docs/images/roost.jpg)

Hens stop laying once there are 10 hens and eggs within 10 m, so eggs left lying around shut a coop down. The roost gathers the eggs laid within 10 m (the server can change this), so your hens keep laying.

- **Build it** with the hammer under **Furniture**: 10 Fine Wood and 2 Black Metal, near a workbench.
- **Eggs keep their stars:** a star hen's eggs stay star eggs.
- **Hatching:** with a **fire near the roost**, its lid counts as the roof: it warms its eggs and hatches the first chick after 30 minutes, then one every 5 minutes, until there are 6 hens and chicks within its reach (the server can change this). Kill a few hens for meat and the flock refills itself. Eggs already warming by a fire on the ground are left alone, and **E** switches gathering off when you want eggs to lie.
- **Take the eggs** with **Shift + E**. Whatever doesn't fit in your inventory lands at your feet.
- **Look at it** to see how many eggs it holds; they also show in its three cubbies. If it's broken, the eggs spill out.
- It gathers any egg that hatches, so eggs from other animals (for example with LetMeTameYou) work too.

## Good to know

- **Everyone needs it, the server included**, because the pieces are part of your world. Dedicated servers load it too.

Part of the **PotatoServer** pack for the Potato Server. 🛠️ Source and bug reports: [github.com/TheRealRel/PotatoFarmer](https://github.com/TheRealRel/PotatoFarmer)

## Settings

In `BepInEx/config/potato.farmer.cfg`, or in game with Configuration Manager.

| Setting | Default | What it does |
|---|---|---|
| Trough: Enabled | On | Show the trough on the hammer. Troughs already built stay and keep working. Set by the server. |
| Trough: Capacity | 40 | How much food one trough holds. Set by the server. |
| Trough: Reach | 25 m | How far away hungry animals notice the trough and walk over (5-60 m). Set by the server. |
| Trough: Show food in the bowl | On | Show the trough's contents as food in the bowl (only on your screen). |
| Roost: Enabled | On | Show the roost on the hammer. Roosts already built stay and keep working. Set by the server. |
| Roost: Capacity | 60 | How many eggs one roost holds. Set by the server. |
| Roost: Reach | 10 m | How far away the roost gathers eggs (2-30 m). Set by the server. |
| Roost: Flock size | 6 | With a fire nearby, the roost hatches eggs while there are fewer hens and chicks than this within its reach. 0 = never hatch. Set by the server. |

## Requirements

- [BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/)
- [Jötunn](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/)
