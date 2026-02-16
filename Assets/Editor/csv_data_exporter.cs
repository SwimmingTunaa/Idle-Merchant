#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;

/// <summary>
/// Exports game data to CSV files that can be imported into Excel.
/// Menu: Tools > Excel Data > Export Template CSVs
/// </summary>
public class GameDataCSVExporter : EditorWindow
{
    private string exportPath = "Assets/Data/CSV_Export";
    
    [MenuItem("Tools/Excel Data/Export Template CSVs")]
    public static void ShowWindow()
    {
        var window = GetWindow<GameDataCSVExporter>("CSV Exporter");
        window.minSize = new Vector2(400, 300);
    }
    
    private void OnGUI()
    {
        GUILayout.Label("Game Data CSV Exporter", EditorStyles.boldLabel);
        EditorGUILayout.Space(10);
        
        EditorGUILayout.HelpBox(
            "Exports your game data to CSV files.\n\n" +
            "You can then:\n" +
            "1. Open Excel\n" +
            "2. Import each CSV as a separate sheet\n" +
            "3. Set up data validation\n" +
            "4. Use the Excel importer to bring it back",
            MessageType.Info
        );
        
        EditorGUILayout.Space(10);
        
        exportPath = EditorGUILayout.TextField("Export Folder:", exportPath);
        
        EditorGUILayout.Space(20);
        
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("EXPORT ALL CSV FILES", GUILayout.Height(50)))
        {
            ExportAllCSVs();
        }
        GUI.backgroundColor = Color.white;
    }
    
    private void ExportAllCSVs()
    {
        if (!Directory.Exists(exportPath))
        {
            Directory.CreateDirectory(exportPath);
        }
        
        ExportItemsCSV();
        ExportMobsCSV();
        ExportRecipesCSV();
        ExportLootTablesCSV();
        
        AssetDatabase.Refresh();
        
        EditorUtility.DisplayDialog("Export Complete", 
            $"Exported 4 CSV files to:\n{exportPath}\n\n" +
            "Open these files in Excel and set up your sheets!", 
            "OK");
    }
    
    private void ExportItemsCSV()
    {
        StringBuilder csv = new StringBuilder();
        csv.AppendLine("Name,Category,Layer,SellPrice,Description");
        
        // Layer 1: Forest Materials
        csv.AppendLine($"Wood,Common,1,2,\"{EscapeCSV("Sturdy branches that sprites tend with care. The foundation of any proper workshop.")}\"");
        csv.AppendLine($"Slime Gel,Common,1,2,\"{EscapeCSV("Viscous goo that's surprisingly useful. Sticks things together and makes great waterproofing!")}\"");
        csv.AppendLine($"Sprite Dust,Common,1,5,\"{EscapeCSV("Shimmering powder that sprites shed naturally. Enhances any craft with a touch of forest magic.")}\"");
        
        // Layer 2: Stone & Minerals
        csv.AppendLine($"Stone,Common,2,1,\"{EscapeCSV("Basic quarry stone. Heavy, reliable, and found everywhere in the caverns.")}\"");
        csv.AppendLine($"Iron Ore,Common,2,5,\"{EscapeCSV("Raw metal veins extracted from golem bodies. The backbone of any smithy's inventory.")}\"");
        csv.AppendLine($"Crystal Shard,Common,2,10,\"{EscapeCSV("Fragments of living crystal. They hum faintly with residual magic when held to light.")}\"");
        
        // Layer 3: Beast Materials
        csv.AppendLine($"Wolf Pelt,Crafted,3,12,\"{EscapeCSV("Thick fur from wild predators. Warm, durable, and smells faintly of pine needles.")}\"");
        csv.AppendLine($"Bear Hide,Crafted,3,20,\"{EscapeCSV("Legendary toughness. Hunters claim bear leather can stop a sword stroke—they're not exaggerating.")}\"");
        csv.AppendLine($"Spider Silk,Crafted,3,15,\"{EscapeCSV("Impossibly strong thread found in monster dens. Lighter than cotton, stronger than steel.")}\"");
        
        // Layer 4: Undead Materials
        csv.AppendLine($"Bone Fragment,Crafted,4,8,\"{EscapeCSV("Ancient bones that refuse to crumble. Necromancers used them for... something. You'd rather not know.")}\"");
        csv.AppendLine($"Ectoplasm,Crafted,4,12,\"{EscapeCSV("Spectral essence that ghosts leave behind. Cold to touch and faintly glowing. Perfect for spirit enchantments.")}\"");
        csv.AppendLine($"Soul Shard,Crafted,4,30,\"{EscapeCSV("Crystallized willpower of the departed. Handle with respect—these were once people.")}\"");
        
        // Layer 5: Infernal Materials
        csv.AppendLine($"Demon Horn,Luxury,5,25,\"{EscapeCSV("Twisted horns that radiate malevolent energy. Prized by dark enchanters and nobles alike.")}\"");
        csv.AppendLine($"Orc Hide,Luxury,5,30,\"{EscapeCSV("Battle-tested leather from warriors who never retreated. The best armor money can buy.")}\"");
        csv.AppendLine($"Infernal Core,Luxury,5,80,\"{EscapeCSV("Hearts of flame from demon lords. They burn eternally without fuel—priceless for master crafters.")}\"");
        
        File.WriteAllText($"{exportPath}/Items.csv", csv.ToString());
        Debug.Log("[CSV Export] Items.csv created");
    }
    
    private void ExportMobsCSV()
    {
        StringBuilder csv = new StringBuilder();
        csv.AppendLine("Name,Layer,HP,CanAttack,BehaviorType,MoveSpeed,AttackDamage,AttackInterval,AttackRange,ScanRange,SpawnWeight,TerritorialRadius,Description");
        
        // Layer 1: Forest Glade
        csv.AppendLine($"Forest Sprite,1,10,FALSE,Passive,1.5,5,1.5,1.5,10,2.0,0,\"{EscapeCSV("Gentle forest guardians who shed magical dust as they tend to the trees. Too shy to fight back.")}\"");
        csv.AppendLine($"Blue Slime,1,12,TRUE,Territorial,1.5,5,1.5,1.5,10,2.0,2,\"{EscapeCSV("Gelatinous blobs that wobble indignantly when you get too close. Surprisingly territorial about their puddles.")}\"");
        csv.AppendLine($"Deer,1,8,FALSE,Passive,1.5,5,1.5,1.5,10,1.0,0,\"{EscapeCSV("Peaceful woodland dwellers. They drop antlers naturally—no harm done! (They're just clumsy.)")}\"");
        
        // Layer 2: Stone Caverns
        csv.AppendLine($"Stone Golem,2,35,TRUE,Aggressive,1.5,5,1.5,1.5,10,2.0,0,\"{EscapeCSV("Ancient constructs awakened by your presence. They crumble reluctantly, leaving valuable minerals behind.")}\"");
        csv.AppendLine($"Red Slime,2,25,TRUE,Aggressive,1.5,5,1.5,1.5,10,1.8,0,\"{EscapeCSV("Molten cousins of the blue variety. Angrier, hotter, and somehow managed to absorb rocks.")}\"");
        csv.AppendLine($"Crystal Golem,2,50,TRUE,Aggressive,1.5,5,1.5,1.5,10,1.0,0,\"{EscapeCSV("Rare guardians protecting crystalline veins. Their bodies refract light beautifully before shattering into treasure.")}\"");
        
        // Layer 3: Wild Thicket
        csv.AppendLine($"Gray Wolf,3,60,TRUE,Aggressive,1.5,5,1.5,1.5,12,2.2,0,\"{EscapeCSV("Pack hunters with keen senses. They spot intruders from afar and coordinate their attacks with eerie precision.")}\"");
        csv.AppendLine($"Brown Bear,3,120,TRUE,Aggressive,1.5,15,1.5,1.5,10,1.2,0,\"{EscapeCSV("Territorial titans who don't take kindly to dungeon delvers. Somehow their dens are always full of spider silk.")}\"");
        
        // Layer 4: Cursed Crypts
        csv.AppendLine($"Skeleton,4,100,TRUE,Aggressive,1.5,5,1.2,1.5,10,2.0,0,\"{EscapeCSV("Restless bones animated by lingering magic. They're not evil—just very committed to their guard duty.")}\"");
        csv.AppendLine($"Ghost,4,80,TRUE,Aggressive,2.5,5,1.5,1.5,10,1.5,0,\"{EscapeCSV("Wispy spirits who phase through walls. Their ectoplasm is surprisingly useful for enchanting.")}\"");
        csv.AppendLine($"Necromancer,4,200,TRUE,Aggressive,1.5,5,1.5,1.5,15,0.8,0,\"{EscapeCSV("Former scholars who took 'eternal study' too literally. They're protective of their soul gem collections.")}\"");
        
        // Layer 5: Infernal Depths
        csv.AppendLine($"Imp,5,150,TRUE,Aggressive,2.0,5,1.5,1.5,10,2.0,0,\"{EscapeCSV("Mischievous fiends who dart around cackling. Their horns are prized for their dark enchantment properties.")}\"");
        csv.AppendLine($"Orc Grunt,5,250,TRUE,Aggressive,1.5,20,1.5,1.5,10,1.8,0,\"{EscapeCSV("Brutish warriors wearing surprisingly well-crafted leather. They hit hard and ask questions never.")}\"");
        csv.AppendLine($"Demon Lord,5,500,TRUE,Aggressive,1.5,30,1.5,1.5,10,0.5,0,\"{EscapeCSV("Ancient powers from beyond the veil. Their infernal cores burn with otherworldly flame—legendary crafting material.")}\"");
        
        File.WriteAllText($"{exportPath}/Mobs.csv", csv.ToString());
        Debug.Log("[CSV Export] Mobs.csv created");
    }
    
    private void ExportRecipesCSV()
    {
        StringBuilder csv = new StringBuilder();
        csv.AppendLine("Name,OutputItem,OutputCategory,SellPrice,CraftTime,Ing1,Qty1,Ing2,Qty2,Ing3,Qty3,Description");
        
        // Tier 1
        csv.AppendLine($"Wooden Club,Wooden Club,Common,10,5,Wood,2,Slime Gel,1,,,\"{EscapeCSV("A solid stick with slime-wrapped grip. Simple, effective, and surprisingly popular with farmers.")}\"");
        csv.AppendLine($"Leather Armor,Leather Armor,Common,20,8,Wood,3,Sprite Dust,1,,,\"{EscapeCSV("Wooden plates laced with sprite magic for flexibility. It's not much, but it keeps the rain off.")}\"");
        
        // Tier 2
        csv.AppendLine($"Stone Sword,Stone Sword,Common,30,10,Wooden Club,1,Stone,2,Slime Gel,1,\"{EscapeCSV("Sharpened stone blade mounted on a club handle. Crude but deadly—and way better than just a stick.")}\"");
        csv.AppendLine($"Iron Sword,Iron Sword,Crafted,60,15,Stone Sword,1,Iron Ore,2,Wood,1,\"{EscapeCSV("A proper blade at last! Forged from golem ore, it holds an edge that stone never could.")}\"");
        csv.AppendLine($"Crystal Focus,Crystal Focus,Crafted,80,18,Crystal Shard,1,Iron Ore,1,Sprite Dust,1,\"{EscapeCSV("A gemstone set in an iron frame, blessed with sprite magic. Mages clutch these like their lives depend on it.")}\"");
        
        // Tier 3
        csv.AppendLine($"Wolf Armor,Wolf Armor,Crafted,120,25,Leather Armor,1,Wolf Pelt,2,Iron Ore,1,\"{EscapeCSV("Reinforced leather with wolf fur trim and iron buckles. Rangers swear it lets them move like their namesake.")}\"");
        csv.AppendLine($"Bear Cloak,Bear Cloak,Crafted,200,30,Wolf Armor,1,Bear Hide,1,Spider Silk,1,\"{EscapeCSV("Heavy bear leather lined with spider silk for impossible flexibility. Warriors call it 'the unkillable coat.'")}\"");
        csv.AppendLine($"Enchanted Ring,Enchanted Ring,Crafted,150,28,Crystal Focus,1,Spider Silk,1,Wolf Pelt,1,\"{EscapeCSV("Crystal focus reshaped into a ring, wrapped in enchanted silk. The gem pulses faintly with stored mana.")}\"");
        
        // Tier 4
        csv.AppendLine($"Bone Blade,Bone Blade,Luxury,300,35,Iron Sword,1,Bone Fragment,1,Soul Shard,1,\"{EscapeCSV("Iron blade grafted with spectral bones. It cuts flesh and spirit alike—unnerving but undeniably effective.")}\"");
        csv.AppendLine($"Spectral Robe,Spectral Robe,Luxury,350,40,Bear Cloak,1,Ectoplasm,2,Soul Shard,1,\"{EscapeCSV("Bear hide infused with ghost essence. Wearers report feeling lighter, colder... and occasionally invisible.")}\"");
        csv.AppendLine($"Soul Gem,Soul Gem,Luxury,400,38,Enchanted Ring,1,Soul Shard,2,Ectoplasm,1,\"{EscapeCSV("A crystal ring supercharged with captured souls. Necromancers pay fortunes for these. You try not to think about it.")}\"");
        
        // Tier 5
        csv.AppendLine($"Demon Warblade,Demon Warblade,Luxury,600,50,Bone Blade,1,Demon Horn,1,Infernal Core,1,\"{EscapeCSV("A bone blade reforged in demon flame. The edge burns with hellfire—no sheath can contain it.")}\"");
        csv.AppendLine($"Abyssal Armor,Abyssal Armor,Luxury,800,55,Spectral Robe,1,Orc Hide,2,Infernal Core,1,\"{EscapeCSV("Orc leather bound with spectral wards and demon fire. Nothing alive or dead can pierce it—or so the legends claim.")}\"");
        csv.AppendLine($"Infernal Amulet,Infernal Amulet,Luxury,700,48,Soul Gem,1,Demon Horn,1,Orc Hide,1,\"{EscapeCSV("A soul gem wrapped in demon horn and orc leather. It whispers promises of power—best not to listen too closely.")}\"");
        
        File.WriteAllText($"{exportPath}/Recipes.csv", csv.ToString());
        Debug.Log("[CSV Export] Recipes.csv created");
    }
    
    private void ExportLootTablesCSV()
    {
        StringBuilder csv = new StringBuilder();
        csv.AppendLine("MobName,ItemName,DropChance");
        
        // Layer 1
        csv.AppendLine("Forest Sprite,Wood,80");
        csv.AppendLine("Forest Sprite,Sprite Dust,20");
        csv.AppendLine("Blue Slime,Slime Gel,85");
        csv.AppendLine("Blue Slime,Sprite Dust,15");
        csv.AppendLine("Deer,Wood,70");
        csv.AppendLine("Deer,Slime Gel,30");
        
        // Layer 2
        csv.AppendLine("Stone Golem,Stone,75");
        csv.AppendLine("Stone Golem,Iron Ore,25");
        csv.AppendLine("Red Slime,Stone,80");
        csv.AppendLine("Red Slime,Slime Gel,20");
        csv.AppendLine("Crystal Golem,Iron Ore,50");
        csv.AppendLine("Crystal Golem,Crystal Shard,50");
        
        // Layer 3
        csv.AppendLine("Gray Wolf,Wolf Pelt,80");
        csv.AppendLine("Gray Wolf,Bear Hide,20");
        csv.AppendLine("Brown Bear,Bear Hide,70");
        csv.AppendLine("Brown Bear,Spider Silk,30");
        
        // Layer 4
        csv.AppendLine("Skeleton,Bone Fragment,80");
        csv.AppendLine("Skeleton,Soul Shard,20");
        csv.AppendLine("Ghost,Ectoplasm,85");
        csv.AppendLine("Ghost,Soul Shard,15");
        csv.AppendLine("Necromancer,Soul Shard,60");
        csv.AppendLine("Necromancer,Ectoplasm,40");
        
        // Layer 5
        csv.AppendLine("Imp,Demon Horn,80");
        csv.AppendLine("Imp,Orc Hide,20");
        csv.AppendLine("Orc Grunt,Orc Hide,75");
        csv.AppendLine("Orc Grunt,Demon Horn,25");
        csv.AppendLine("Demon Lord,Infernal Core,60");
        csv.AppendLine("Demon Lord,Demon Horn,30");
        csv.AppendLine("Demon Lord,Orc Hide,10");
        
        File.WriteAllText($"{exportPath}/LootTables.csv", csv.ToString());
        Debug.Log("[CSV Export] LootTables.csv created");
    }
    
    private string EscapeCSV(string text)
    {
        // Escape quotes by doubling them
        return text.Replace("\"", "\"\"");
    }
}
#endif