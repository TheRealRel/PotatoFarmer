# 🌾 Potato Farmer

**Farming and animal pieces for Valheim**, each with its own model, textured to match the game's own pieces: the same pixel size and sharp pixels. More pieces will join over time; the server can switch any of them off.

## 🐗 Gullinbursti's Trough

**A feeding trough your animals eat from**, named for Gullinbursti, the god Freyr's golden-bristled boar. Build it, fill it with food, and walk away: hungry boars, wolves, lox and any other tameable animal in range come over and eat from it. Every bite is a normal meal to the game, so **taming, breeding and keeping tamed animals fed** all count, and mods that change those keep working.

Dark oak, hammered bronze bands and a carved boar head on each end.

![Tamed boars eating carrots from Gullinbursti's Trough](https://raw.githubusercontent.com/TheRealRel/PotatoFarmer/main/docs/images/trough-boars.jpg)

![Gullinbursti's Trough filled with carrots](https://raw.githubusercontent.com/TheRealRel/PotatoFarmer/main/docs/images/trough.jpg)

- **Build it** with the hammer under **Furniture**: 10 Wood and 2 Bronze, near a workbench.
- **Fill it** from your hotbar: look at it and press the food's **hotbar key (1-8)** to add that stack. It holds 40 pieces of food (the server can change this).
- **Take it back** with **Shift + E** (the alternate place key + Use). Whatever doesn't fit in your inventory lands at your feet.
- **Look at it** to see what's inside. The food also shows in the bowl.

Hungry animals within 25 m (the server can change this) walk over and eat one bite at a time, from the nearest trough holding something they eat. Which food counts is the game's own list for each animal, so modded animals and their foods work too. If the trough is broken or taken down, its food spills out.

- **It's not a chest.** Crafting from containers, quick-stack and similar mods leave its food alone.
- A bite waiting in the bowl isn't grabbed by auto-pickup when you walk past.
- With **LetMeTameYou**, animals that can't be tamed yet don't eat from it, the same as with food on the ground.

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

## Requirements

- [BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/)
- [Jötunn](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/)
