Sweet World Generator
Author: Sweet Briana

RimWorld 1.7
------------
Reusable controls on the vanilla Create World page, with a larger readable
window, searchable faction-count popup, biome search/provider filters and
separate draft, applied and saved settings. Harmony is required. Other biome
providers and official DLC are optional; only loaded definitions are offered.

Selected biomes only
--------------------
Open Sweet world controls, then Biomes, then Selected biomes only.
Enable strict mode and check eligible surface land biomes. Oceans and lakes
keep native generation. Original special/pocket-map biomes are not eligible.
Every selected biome needs a positive frequency weight. Optional adaptation
changes land temperature and rainfall only when no selected worker accepts
the original climate. It does not flatten mountains or replace water.
An impossible selection fails with an explanation rather than generating
unselected land. Extreme settings and third-party biome combinations can
still be incompatible; selection is not a guarantee of equal coverage or
that every checked biome will appear.

World recipes
-------------
World recipes / installed offers Temperate World and Desert World, plus
Alpha Biomes World when Alpha Biomes is loaded. With Anomaly it also offers
Labyrinth Planet, Metal Desolation and Mixed Anomaly Surface. Recipes have
fixed seeds, strict selections, climate adaptation and Hydrophobia enabled.
They are generation recipes, not prebuilt planet templates. Reproduction
requires the same game version, active mods, definitions and settings.
Copy a recipe into a personal slot to rename and save your own version.

Cavern colonies (1.7, Anomaly required)
-------------------------------------
Choose Cavern Pioneers or Reclamation Expedition when starting a new colony.
They select Cavern World automatically. The standalone Cavern World recipe
is also available under world controls. Fertile lake oases sit inside a dark
underground network, with one generated passage into each oasis and normal
mining afterward. Fungi and crystal lights grow only in the oasis habitat.
Dark-growing cavern oaks and healroot provide wood and herbal medicine.
Use 250 by 250 local colony maps for the intended cavern experience.
Fine dark-violet Hair glows faintly over exposed Flesh and slowly colonizes
mined ground once debris is cleared. Its strands are seventy percent
narrower than ordinary grass. Hair leaves fertility unchanged and respects
floors, growing zones and buildings; it is not livestock forage.
Hares and mixed-sex muffalo provide wildlife and livestock opportunities;
peaceful new player starts receive a domestic pair. Natural walls contain
mineable resources, a dry geyser supports geothermal power and lava pockets
illuminate recessed side chambers with broad dry margins, at least seven
tiles from the original travel network. Odyssey enables real lake fishing
and native lava.
Faction-held oases have compact furnished structures or insect-held ruins,
and can be explored and claimed after clearing their defenders. Custom Base
Framework is optional; its bounded stamping API uses original room layouts.
Ordinary and deep-drill infestation incidents are disabled in this custom
cavern biome while deliberately placed insect factions remain.
Ground trading caravans use the cave network. Orbital deliveries are blocked
because there is no surface access through the deep ceiling. Special objective
maps, including the escape ship, retain native generation rather than losing
their objective. This first release uses one primary oasis per local map.
See Cavern-Guide.txt for scenario supplies, travel, compatibility and limits.

Anomaly-inspired surface variants
--------------------------------
Labyrinth expanse and Metal desolation are custom settleable surface biomes,
not the original Labyrinth/Metal hell special maps. Both default off unless
selected or given an explicit positive frequency. Native surface generation
is retained, with open ruined plots, buildable floors and removable walls.
Ruins avoid water, roofs, occupied/reserved plots and existing road corridors.
They do not add pocket-map collapse, teleport rules, mandatory bosses or
Undercave flesh terrain. Original Anomaly content and normal story events
remain unchanged. This is not a guarantee against storyteller threats.

Other controls
--------------
- Three persistent personal slots and optional installed preset plug-ins
- Seed, coverage, subdivision detail 1-11, pollution and climate controls
- Population, landmarks, river and mountain density
- Searchable Factions popup with adjacent +/- counts
- Conditional insect infestation frequency when the matching hive is present
- Biome frequencies, including actual loaded Alpha Biomes definitions
- Hydrophobia: no water starting tile and dry initial colonists/pets/pods
- Pending initial arrival protection persists through save/load

Usage and safety
----------------
Apply to world changes the current new-world setup; it does not save a slot.
Save preset persists a personal recipe; it does not apply it. Generate uses
RimWorld's normal generation flow. Closing cannot undo an explicit Apply or
Save. High detail/coverage costs more time and memory. No live planet preview,
background generation, existing-world editing or world-template importer.
The new terrain/building definitions and arrival component are saved data:
do not remove or downgrade the mod in an ongoing colony without a backup.

Compatibility and development
-----------------------------
The 1.7 source rebuilds against the installed RimWorld 1.7.4982 runtime.
Build.ps1 is in 1.7/Source. Native gate receipts are retained in the sibling
Development/WorldGenerator-1.7 lab, including failures and bounded limitations.
The legacy 1.6 implementation and its UI strings remain unchanged. Visible
1.7 branding is Sweet; package ID unaidedmistake.worldgenerator, assembly,
namespace, settings class and existing serialized keys intentionally remain
stable. These technical identifiers are not display branding.
