Farming and animal pieces for Valheim, each with its own model, textured to sit right next to the game's own pieces. Three so far: a feeding trough, a nesting box and a mead cask. The server can switch any of them off, and more will join over time.

![](https://raw.githubusercontent.com/TheRealRel/PotatoFarmer/main/docs/images/icon-trough.png) **Gullinbursti's Trough** is a feeding trough your animals eat from, named for the god Freyr's golden-bristled boar. Dark oak, hammered bronze bands and a carved boar head on each end. Fill it with food and walk away: hungry boars, wolves, lox and any other tameable animal within 25 m come over and eat from it, one bite at a time. The game treats every bite as a normal meal, so taming, breeding and keeping tamed animals fed all count, and mods that change those keep working.

![Gullinbursti's Trough on a grassy hill](https://raw.githubusercontent.com/TheRealRel/PotatoFarmer/main/docs/images/trough.jpg)

- Build it with the hammer under Furniture: 10 Wood and 2 Bronze, at a forge.
- Fill it from your hotbar: look at it and press the food's hotbar key (1-8) to add that stack. It holds 100 pieces of food, and what's inside shows in the bowl.
- Shift + E takes the food back. Whatever doesn't fit in your inventory lands at your feet, and if the trough is broken its food spills out.

Which food counts is the game's own list for each animal, so modded animals and their foods work too. It isn't a chest, so crafting-from-containers and quick-stack mods leave its food alone, and auto-pickup won't grab a bite waiting in the bowl. With LetMeTameYou, animals that can't be tamed yet don't eat from it, the same as with food on the ground.

![](https://raw.githubusercontent.com/TheRealRel/PotatoFarmer/main/docs/images/icon-roost.png) **Gullinkambi's Roost** gathers your hens' eggs, named for the golden-combed rooster that wakes the warriors of Valhalla. Fine wood, black metal and a carved rooster head on each end. Hens stop laying once there are 10 hens and eggs within 10 m, so eggs left lying around shut a coop down; the roost gathers the eggs laid within 10 m so your hens keep laying, and a star hen's eggs stay star eggs.

![Gullinkambi's Roost in a hen pen with chicks](https://raw.githubusercontent.com/TheRealRel/PotatoFarmer/main/docs/images/roost.jpg)

- Build it with the hammer under Furniture: 10 Fine Wood and 2 Black Metal, at a forge.
- With a fire near the roost, its lid counts as the roof. It warms its eggs and hatches the first chick after 30 minutes, then one every 5 minutes, until there are 6 hens and chicks within its reach. Kill a few hens for meat and the flock refills itself.
- E switches gathering off when you want eggs to lie, and Shift + E takes the eggs. Its eggs show in its three cubbies.

Eggs already warming by a fire on the ground are left alone. It gathers any egg that hatches, so eggs from other animals (for example with LetMeTameYou) work too.

![](https://raw.githubusercontent.com/TheRealRel/PotatoFarmer/main/docs/images/icon-cask.png) **Heiðrún's Cask** is a great mead cask, blessed by Heiðrún, the goat whose udder runs with mead in Valhalla. A great barrel of fine wood with black metal bands, a goat carved into each end and a cork to tap it from, on a carved stand. It brews several mead bases at once, 4 by default, each exactly like a fermenter of its own.

![Heiðrún's Cask on a grassy hill](https://raw.githubusercontent.com/TheRealRel/PotatoFarmer/main/docs/images/cask.jpg)

- Build it with the hammer under Crafting: 30 Fine Wood, 6 Black Metal, 4 Tar and 20 Iron Nails, at a forge.
- Add mead bases with E, one at a time, or with a mead base's hotbar key (1-8) to pour in as many as there's room for. It needs a roof, like the fermenter.
- When batches are ready, E taps them all, and the mead pours out in front of the cork.

Brewing takes as long as the game's fermenter and gives the same amount. With PotatoTweaks, the cask follows its fermenter speed and output settings too. One cask brews one kind of mead base at a time until it's emptied, unless the server turns on Mix mead bases.

Everyone needs the mod, the server included, because the pieces are part of your world. Dedicated servers load it too. It's part of the PotatoServer pack for the Potato Server.

## Settings

In `BepInEx/config/potato.farmer.cfg`, or in game with Configuration Manager. Everything marked "server" is set by the server and synced to players.

| Setting | Default | What it does |
|---|---|---|
| Trough: Enabled | On | Show the trough on the hammer. Troughs already built stay and keep working. Server. |
| Trough: Capacity | 100 | How much food one trough holds. Server. |
| Trough: Reach | 25 m | How far away hungry animals notice the trough and walk over (5-60 m). Server. |
| Trough: Show food in the bowl | On | Show the trough's contents as food in the bowl, only on your screen. |
| Roost: Enabled | On | Show the roost on the hammer. Roosts already built stay and keep working. Server. |
| Roost: Capacity | 60 | How many eggs one roost holds. Server. |
| Roost: Reach | 10 m | How far away the roost gathers eggs (2-30 m). Server. |
| Roost: Flock size | 6 | With a fire nearby, the roost hatches eggs while there are fewer hens and chicks than this within its reach. 0 never hatches. Server. |
| Cask: Enabled | On | Show the cask on the hammer. Casks already built stay and keep working. Server. |
| Cask: Batches | 4 | How many mead bases one cask brews at once (1-20). Server. |
| Cask: Mix mead bases | Off | Let one cask brew different mead bases at once. Server. |

Bug reports go to [GitHub Issues](https://github.com/TheRealRel/PotatoFarmer/issues), where the source lives too. Questions are welcome on Discord: [relanebeis](https://discord.com/users/373574370267889674).

Needs [BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/) and [Jötunn](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/).

The code is under the MIT License. The models, textures, icon and screenshots are © TheRealRel under [CC BY-NC 4.0](https://creativecommons.org/licenses/by-nc/4.0/): free to share and adapt with credit, but not for commercial use. See LICENSE for the details.
