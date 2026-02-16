#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Data;
using ExcelDataReader;

public class ExcelDataImporter : EditorWindow
{
    private enum SheetType { Items, Mobs, Recipes, LootTables, AllSheets }
    private enum TabType { Items, Mobs, Recipes, LootTables }
    
    // Settings
    private string excelFilePath = "Assets/Data/GameData.xlsx";
    private string outputPath = "Assets/Data";
    private SheetType selectedSheet = SheetType.Items;
    private TabType activeTab = TabType.Items;
    
    // Prefab assignments
    private GameObject itemPrefab;
    private GameObject mobPrefab;
    
    // Preview data
    private List<ItemPreviewData> itemPreviews = new List<ItemPreviewData>();
    private List<MobPreviewData> mobPreviews = new List<MobPreviewData>();
    private List<RecipePreviewData> recipePreviews = new List<RecipePreviewData>();
    private List<LootTablePreviewData> lootTablePreviews = new List<LootTablePreviewData>();
    
    private bool isParsed = false;
    private Vector2 scrollPos;
    
    // Import results
    private int itemsCreated = 0;
    private int mobsCreated = 0;
    private int recipesCreated = 0;
    private int lootTablesAssigned = 0;
    private List<string> errors = new List<string>();
    private List<string> warnings = new List<string>();
    
    // Preview data classes
    private class ItemPreviewData
    {
        public bool selected = true;
        public bool overwrite = false;
        public string name;
        public ItemCategory category;
        public int layer;
        public int sellPrice;
        public string description;
    }
    
    private class MobPreviewData
    {
        public bool selected = true;
        public bool overwrite = false;
        public string name;
        public int layer;
        public float hp;
        public bool canAttack;
        public CombatBehaviorType behaviorType;
        public float moveSpeed;
        public float attackDamage;
        public float attackInterval;
        public float attackRange;
        public float scanRange;
        public float spawnWeight;
        public float territorialRadius;
        public string description;
    }
    
    private class RecipePreviewData
    {
        public bool selected = true;
        public bool overwrite = false;
        public string name;
        public string outputItem;
        public ItemCategory outputCategory;
        public int sellPrice;
        public float craftTime;
        public List<RecipeIngredientData> ingredients;
        public string description;
    }
    
    private class LootTablePreviewData
    {
        public bool selected = true;
        public string mobName;
        public string itemName;
        public float dropChance;
    }
    
    private struct RecipeIngredientData
    {
        public string itemName;
        public int quantity;
    }
    
    [MenuItem("Tools/Excel Data/Import Game Data")]
    public static void ShowWindow()
    {
        var window = GetWindow<ExcelDataImporter>("Excel Data Importer");
        window.minSize = new Vector2(700, 600);
    }
    
    private void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
        
        GUILayout.Label("Excel Data Importer - Preview System", EditorStyles.boldLabel);
        EditorGUILayout.Space(10);
        
        DrawFileSettings();
        EditorGUILayout.Space(10);
        DrawSheetSelector();
        EditorGUILayout.Space(10);
        DrawPrefabAssignments();
        EditorGUILayout.Space(10);
        DrawParseButton();
        
        if (isParsed)
        {
            EditorGUILayout.Space(20);
            
            if (selectedSheet == SheetType.AllSheets)
            {
                DrawTabBar();
                EditorGUILayout.Space(5);
            }
            
            DrawPreviewTable();
            EditorGUILayout.Space(10);
            DrawBulkControls();
            EditorGUILayout.Space(10);
            DrawImportButton();
        }
        
        if (itemsCreated > 0 || mobsCreated > 0 || recipesCreated > 0 || lootTablesAssigned > 0)
        {
            EditorGUILayout.Space(20);
            DrawResults();
        }
        
        EditorGUILayout.EndScrollView();
    }
    
    private void DrawFileSettings()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("File Settings", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        excelFilePath = EditorGUILayout.TextField("Excel File:", excelFilePath);
        if (GUILayout.Button("Browse", GUILayout.Width(70)))
        {
            string path = EditorUtility.OpenFilePanel("Select GameData.xlsx", "Assets/Data", "xlsx");
            if (!string.IsNullOrEmpty(path))
            {
                excelFilePath = GetRelativePath(path);
            }
        }
        EditorGUILayout.EndHorizontal();
        
        outputPath = EditorGUILayout.TextField("Output Path:", outputPath);
        EditorGUILayout.EndVertical();
    }
    
    private void DrawSheetSelector()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Sheet Selection", EditorStyles.boldLabel);
        selectedSheet = (SheetType)EditorGUILayout.EnumPopup("Select Sheet:", selectedSheet);
        EditorGUILayout.EndVertical();
    }
    
    private void DrawPrefabAssignments()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Prefab Assignments", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("These prefabs will be assigned to all imported assets of that type.", MessageType.Info);
        
        if (selectedSheet == SheetType.Items || selectedSheet == SheetType.AllSheets)
        {
            itemPrefab = (GameObject)EditorGUILayout.ObjectField("Item Prefab:", itemPrefab, typeof(GameObject), false);
        }
        
        if (selectedSheet == SheetType.Mobs || selectedSheet == SheetType.AllSheets)
        {
            mobPrefab = (GameObject)EditorGUILayout.ObjectField("Mob Prefab:", mobPrefab, typeof(GameObject), false);
        }
        
        EditorGUILayout.EndVertical();
    }
    
    private void DrawParseButton()
    {
        GUI.backgroundColor = Color.cyan;
        if (GUILayout.Button("PARSE SHEET", GUILayout.Height(40)))
        {
            ParseSheet();
        }
        GUI.backgroundColor = Color.white;
    }
    
    private void DrawTabBar()
    {
        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Toggle(activeTab == TabType.Items, $"Items ({itemPreviews.Count})", EditorStyles.toolbarButton))
            activeTab = TabType.Items;
        
        if (GUILayout.Toggle(activeTab == TabType.Mobs, $"Mobs ({mobPreviews.Count})", EditorStyles.toolbarButton))
            activeTab = TabType.Mobs;
        
        if (GUILayout.Toggle(activeTab == TabType.Recipes, $"Recipes ({recipePreviews.Count})", EditorStyles.toolbarButton))
            activeTab = TabType.Recipes;
        
        if (GUILayout.Toggle(activeTab == TabType.LootTables, $"Loot Tables ({lootTablePreviews.Count})", EditorStyles.toolbarButton))
            activeTab = TabType.LootTables;
        
        EditorGUILayout.EndHorizontal();
    }
    
    private void DrawPreviewTable()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
        
        if (selectedSheet == SheetType.AllSheets)
        {
            switch (activeTab)
            {
                case TabType.Items:
                    DrawItemsTable();
                    break;
                case TabType.Mobs:
                    DrawMobsTable();
                    break;
                case TabType.Recipes:
                    DrawRecipesTable();
                    break;
                case TabType.LootTables:
                    DrawLootTablesTable();
                    break;
            }
        }
        else
        {
            switch (selectedSheet)
            {
                case SheetType.Items:
                    DrawItemsTable();
                    break;
                case SheetType.Mobs:
                    DrawMobsTable();
                    break;
                case SheetType.Recipes:
                    DrawRecipesTable();
                    break;
                case SheetType.LootTables:
                    DrawLootTablesTable();
                    break;
            }
        }
        
        EditorGUILayout.EndVertical();
    }
    
    private void DrawItemsTable()
    {
        if (itemPreviews.Count == 0)
        {
            EditorGUILayout.HelpBox("No items found in sheet.", MessageType.Warning);
            return;
        }
        
        EditorGUILayout.LabelField($"Found {itemPreviews.Count} items:", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);
        
        foreach (var item in itemPreviews)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            item.selected = EditorGUILayout.Toggle(item.selected, GUILayout.Width(20));
            EditorGUILayout.LabelField(item.name, GUILayout.Width(150));
            EditorGUILayout.LabelField($"Layer {item.layer}", GUILayout.Width(60));
            EditorGUILayout.LabelField($"{item.category}", GUILayout.Width(80));
            EditorGUILayout.LabelField($"${item.sellPrice}", GUILayout.Width(60));
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("Override:", GUILayout.Width(60));
            item.overwrite = EditorGUILayout.Toggle(item.overwrite, GUILayout.Width(20));
            EditorGUILayout.EndHorizontal();
        }
    }
    
    private void DrawMobsTable()
    {
        if (mobPreviews.Count == 0)
        {
            EditorGUILayout.HelpBox("No mobs found in sheet.", MessageType.Warning);
            return;
        }
        
        EditorGUILayout.LabelField($"Found {mobPreviews.Count} mobs:", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);
        
        foreach (var mob in mobPreviews)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            mob.selected = EditorGUILayout.Toggle(mob.selected, GUILayout.Width(20));
            EditorGUILayout.LabelField(mob.name, GUILayout.Width(150));
            EditorGUILayout.LabelField($"Layer {mob.layer}", GUILayout.Width(60));
            EditorGUILayout.LabelField($"HP: {mob.hp}", GUILayout.Width(70));
            EditorGUILayout.LabelField($"{mob.behaviorType}", GUILayout.Width(80));
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("Override:", GUILayout.Width(60));
            mob.overwrite = EditorGUILayout.Toggle(mob.overwrite, GUILayout.Width(20));
            EditorGUILayout.EndHorizontal();
        }
    }
    
    private void DrawRecipesTable()
    {
        if (recipePreviews.Count == 0)
        {
            EditorGUILayout.HelpBox("No recipes found in sheet.", MessageType.Warning);
            return;
        }
        
        EditorGUILayout.LabelField($"Found {recipePreviews.Count} recipes:", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);
        
        foreach (var recipe in recipePreviews)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            recipe.selected = EditorGUILayout.Toggle(recipe.selected, GUILayout.Width(20));
            EditorGUILayout.LabelField(recipe.name, GUILayout.Width(150));
            EditorGUILayout.LabelField($"→ {recipe.outputItem}", GUILayout.Width(150));
            EditorGUILayout.LabelField($"{recipe.craftTime}s", GUILayout.Width(60));
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("Override:", GUILayout.Width(60));
            recipe.overwrite = EditorGUILayout.Toggle(recipe.overwrite, GUILayout.Width(20));
            EditorGUILayout.EndHorizontal();
        }
    }
    
    private void DrawLootTablesTable()
    {
        if (lootTablePreviews.Count == 0)
        {
            EditorGUILayout.HelpBox("No loot table entries found in sheet.", MessageType.Warning);
            return;
        }
        
        EditorGUILayout.LabelField($"Found {lootTablePreviews.Count} loot entries:", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);
        
        foreach (var entry in lootTablePreviews)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            entry.selected = EditorGUILayout.Toggle(entry.selected, GUILayout.Width(20));
            EditorGUILayout.LabelField(entry.mobName, GUILayout.Width(150));
            EditorGUILayout.LabelField("→", GUILayout.Width(20));
            EditorGUILayout.LabelField(entry.itemName, GUILayout.Width(150));
            EditorGUILayout.LabelField($"{entry.dropChance}%", GUILayout.Width(60));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }
    }
    
    private void DrawBulkControls()
    {
        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button("Select All"))
        {
            SelectAll(true);
        }
        
        if (GUILayout.Button("Deselect All"))
        {
            SelectAll(false);
        }
        
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button("Override All"))
        {
            OverrideAll(true);
        }
        
        if (GUILayout.Button("Clear Override"))
        {
            OverrideAll(false);
        }
        
        EditorGUILayout.EndHorizontal();
    }
    
    private void DrawImportButton()
    {
        int selectedCount = GetTotalSelectedCount();
        
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button($"IMPORT SELECTED ({selectedCount})", GUILayout.Height(50)))
        {
            ImportSelected();
        }
        GUI.backgroundColor = Color.white;
    }
    
    private void DrawResults()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Import Results:", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            $"✓ Items Created: {itemsCreated}\n" +
            $"✓ Mobs Created: {mobsCreated}\n" +
            $"✓ Recipes Created: {recipesCreated}\n" +
            $"✓ Loot Tables Assigned: {lootTablesAssigned}\n" +
            $"⚠ Warnings: {warnings.Count}\n" +
            $"✗ Errors: {errors.Count}",
            errors.Count > 0 ? MessageType.Error : MessageType.Info
        );
        
        if (warnings.Count > 0)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Warnings:", EditorStyles.boldLabel);
            foreach (var warning in warnings)
            {
                EditorGUILayout.HelpBox(warning, MessageType.Warning);
            }
        }
        
        if (errors.Count > 0)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Errors:", EditorStyles.boldLabel);
            foreach (var error in errors)
            {
                EditorGUILayout.HelpBox(error, MessageType.Error);
            }
        }
        
        EditorGUILayout.EndVertical();
    }
    
    // ===== PARSE SHEET =====
    
    private void ParseSheet()
    {
        errors.Clear();
        warnings.Clear();
        isParsed = false;
        
        itemPreviews.Clear();
        mobPreviews.Clear();
        recipePreviews.Clear();
        lootTablePreviews.Clear();
        
        if (!File.Exists(excelFilePath))
        {
            errors.Add($"Excel file not found: {excelFilePath}");
            return;
        }
        
        try
        {
            using (var stream = File.Open(excelFilePath, FileMode.Open, FileAccess.Read))
            {
                using (var reader = ExcelReaderFactory.CreateReader(stream))
                {
                    var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration()
                    {
                        ConfigureDataTable = (_) => new ExcelDataTableConfiguration()
                        {
                            UseHeaderRow = true
                        }
                    });
                    
                    if (selectedSheet == SheetType.AllSheets)
                    {
                        ParseItems(dataSet);
                        ParseMobs(dataSet);
                        ParseRecipes(dataSet);
                        ParseLootTables(dataSet);
                    }
                    else
                    {
                        switch (selectedSheet)
                        {
                            case SheetType.Items:
                                ParseItems(dataSet);
                                break;
                            case SheetType.Mobs:
                                ParseMobs(dataSet);
                                break;
                            case SheetType.Recipes:
                                ParseRecipes(dataSet);
                                break;
                            case SheetType.LootTables:
                                ParseLootTables(dataSet);
                                break;
                        }
                    }
                    
                    isParsed = true;
                }
            }
        }
        catch (System.Exception e)
        {
            errors.Add($"Parse failed: {e.Message}");
        }
    }
    
    private void ParseItems(DataSet dataSet)
    {
        if (!dataSet.Tables.Contains("Items"))
        {
            errors.Add("Sheet 'Items' not found in Excel file");
            return;
        }
        
        DataTable table = dataSet.Tables["Items"];
        
        for (int i = 0; i < table.Rows.Count; i++)
        {
            DataRow row = table.Rows[i];
            
            try
            {
                string name = GetString(row, "Name");
                if (string.IsNullOrEmpty(name)) continue;
                
                itemPreviews.Add(new ItemPreviewData
                {
                    name = name,
                    category = ParseCategory(GetString(row, "Category")),
                    layer = GetInt(row, "Layer"),
                    sellPrice = GetInt(row, "SellPrice"),
                    description = GetString(row, "Description")
                });
            }
            catch (System.Exception e)
            {
                errors.Add($"Items row {i + 2}: {e.Message}");
            }
        }
        
        Debug.Log($"[ExcelImporter] Parsed {itemPreviews.Count} items");
    }
    
    private void ParseMobs(DataSet dataSet)
    {
        if (!dataSet.Tables.Contains("Mobs"))
        {
            errors.Add("Sheet 'Mobs' not found in Excel file");
            return;
        }
        
        DataTable table = dataSet.Tables["Mobs"];
        
        for (int i = 0; i < table.Rows.Count; i++)
        {
            DataRow row = table.Rows[i];
            
            try
            {
                string name = GetString(row, "Name");
                if (string.IsNullOrEmpty(name)) continue;
                
                mobPreviews.Add(new MobPreviewData
                {
                    name = name,
                    layer = GetInt(row, "Layer"),
                    hp = GetFloat(row, "HP"),
                    canAttack = GetBool(row, "CanAttack"),
                    behaviorType = ParseBehaviorType(GetString(row, "BehaviorType")),
                    moveSpeed = GetFloat(row, "MoveSpeed", 1.5f),
                    attackDamage = GetFloat(row, "AttackDamage", 5f),
                    attackInterval = GetFloat(row, "AttackInterval", 1.5f),
                    attackRange = GetFloat(row, "AttackRange", 1.5f),
                    scanRange = GetFloat(row, "ScanRange", 10f),
                    spawnWeight = GetFloat(row, "SpawnWeight", 2f),
                    territorialRadius = GetFloat(row, "TerritorialRadius", 0f),
                    description = GetString(row, "Description")
                });
            }
            catch (System.Exception e)
            {
                errors.Add($"Mobs row {i + 2}: {e.Message}");
            }
        }
        
        Debug.Log($"[ExcelImporter] Parsed {mobPreviews.Count} mobs");
    }
    
    private void ParseRecipes(DataSet dataSet)
    {
        if (!dataSet.Tables.Contains("Recipes"))
        {
            errors.Add("Sheet 'Recipes' not found in Excel file");
            return;
        }
        
        DataTable table = dataSet.Tables["Recipes"];
        
        for (int i = 0; i < table.Rows.Count; i++)
        {
            DataRow row = table.Rows[i];
            
            try
            {
                string name = GetString(row, "Name");
                if (string.IsNullOrEmpty(name)) continue;
                
                List<RecipeIngredientData> ingredients = new List<RecipeIngredientData>();
                
                for (int j = 1; j <= 3; j++)
                {
                    string ingName = GetString(row, $"Ing{j}");
                    if (!string.IsNullOrEmpty(ingName))
                    {
                        int qty = GetInt(row, $"Qty{j}", 1);
                        ingredients.Add(new RecipeIngredientData { itemName = ingName, quantity = qty });
                    }
                }
                
                recipePreviews.Add(new RecipePreviewData
                {
                    name = name,
                    outputItem = GetString(row, "OutputItem"),
                    outputCategory = ParseCategory(GetString(row, "OutputCategory")),
                    sellPrice = GetInt(row, "SellPrice"),
                    craftTime = GetFloat(row, "CraftTime"),
                    ingredients = ingredients,
                    description = GetString(row, "Description")
                });
            }
            catch (System.Exception e)
            {
                errors.Add($"Recipes row {i + 2}: {e.Message}");
            }
        }
        
        Debug.Log($"[ExcelImporter] Parsed {recipePreviews.Count} recipes");
    }
    
    private void ParseLootTables(DataSet dataSet)
    {
        if (!dataSet.Tables.Contains("LootTables"))
        {
            errors.Add("Sheet 'LootTables' not found in Excel file");
            return;
        }
        
        DataTable table = dataSet.Tables["LootTables"];
        
        for (int i = 0; i < table.Rows.Count; i++)
        {
            DataRow row = table.Rows[i];
            
            try
            {
                string mobName = GetString(row, "MobName");
                string itemName = GetString(row, "ItemName");
                
                if (string.IsNullOrEmpty(mobName) || string.IsNullOrEmpty(itemName))
                    continue;
                
                lootTablePreviews.Add(new LootTablePreviewData
                {
                    mobName = mobName,
                    itemName = itemName,
                    dropChance = GetFloat(row, "DropChance")
                });
            }
            catch (System.Exception e)
            {
                errors.Add($"LootTables row {i + 2}: {e.Message}");
            }
        }
        
        Debug.Log($"[ExcelImporter] Parsed {lootTablePreviews.Count} loot table entries");
    }
    
    // ===== IMPORT SELECTED =====
    
    private void ImportSelected()
    {
        errors.Clear();
        warnings.Clear();
        itemsCreated = 0;
        mobsCreated = 0;
        recipesCreated = 0;
        lootTablesAssigned = 0;
        
        if (selectedSheet == SheetType.AllSheets)
        {
            ImportSelectedItems();
            ImportSelectedMobs();
            ImportSelectedRecipes();
            ImportSelectedLootTables();
        }
        else
        {
            switch (selectedSheet)
            {
                case SheetType.Items:
                    ImportSelectedItems();
                    break;
                case SheetType.Mobs:
                    ImportSelectedMobs();
                    break;
                case SheetType.Recipes:
                    ImportSelectedRecipes();
                    break;
                case SheetType.LootTables:
                    ImportSelectedLootTables();
                    break;
            }
        }
        
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        if (errors.Count == 0)
        {
            EditorUtility.DisplayDialog("Import Complete",
                $"Successfully imported:\n" +
                $"• {itemsCreated} Items\n" +
                $"• {mobsCreated} Mobs\n" +
                $"• {recipesCreated} Recipes\n" +
                $"• {lootTablesAssigned} Loot Table Entries\n\n" +
                $"Warnings: {warnings.Count}",
                "OK");
        }
    }
    
    private void ImportSelectedItems()
    {
        foreach (var item in itemPreviews)
        {
            if (!item.selected) continue;
            
            try
            {
                string itemPath = $"{outputPath}/Items/RawMaterials"; // Always RawMaterials for Items.csv
                EnsureFolderExists(itemPath);
                CreateItem(itemPath, item, item.overwrite);
                itemsCreated++;
            }
            catch (System.Exception e)
            {
                errors.Add($"Failed to create item '{item.name}': {e.Message}");
            }
        }
    }
    
    private void ImportSelectedMobs()
    {
        foreach (var mob in mobPreviews)
        {
            if (!mob.selected) continue;
            
            try
            {
                string mobPath = GetMobPath(mob.layer);
                EnsureFolderExists(mobPath);
                CreateMob(mobPath, mob, mob.overwrite);
                mobsCreated++;
            }
            catch (System.Exception e)
            {
                errors.Add($"Failed to create mob '{mob.name}': {e.Message}");
            }
        }
    }
    
    private void ImportSelectedRecipes()
    {
        string recipePath = $"{outputPath}/Recipes";
        EnsureFolderExists(recipePath);
        
        foreach (var recipe in recipePreviews)
        {
            if (!recipe.selected) continue;
            
            try
            {
                CreateRecipe(recipePath, recipe, recipe.overwrite);
                recipesCreated++;
            }
            catch (System.Exception e)
            {
                errors.Add($"Failed to create recipe '{recipe.name}': {e.Message}");
            }
        }
    }
    
    private void ImportSelectedLootTables()
    {
        Dictionary<string, List<LootTablePreviewData>> lootByMob = new Dictionary<string, List<LootTablePreviewData>>();
        
        foreach (var entry in lootTablePreviews)
        {
            if (!entry.selected) continue;
            
            if (!lootByMob.ContainsKey(entry.mobName))
                lootByMob[entry.mobName] = new List<LootTablePreviewData>();
            
            lootByMob[entry.mobName].Add(entry);
        }
        
        foreach (var kvp in lootByMob)
        {
            MobDef mob = FindMob(kvp.Key);
            if (mob == null)
            {
                warnings.Add($"LootTable references unknown mob: {kvp.Key}");
                continue;
            }
            
            mob.loot.Clear();
            foreach (var entry in kvp.Value)
            {
                ItemDef item = FindItem(entry.itemName);
                if (item == null)
                {
                    warnings.Add($"LootTable for '{kvp.Key}' references unknown item: {entry.itemName}");
                    continue;
                }
                
                item.chance = entry.dropChance / 100f;
                mob.loot.Add(item);
                EditorUtility.SetDirty(item);
                lootTablesAssigned++;
            }
            
            EditorUtility.SetDirty(mob);
        }
    }
    
    // ===== CREATE ASSETS =====
    
    private void CreateItem(string path, ItemPreviewData data, bool overwrite)
    {
        string fileName = $"Item_{data.name.Replace(" ", "")}";
        string fullPath = $"{path}/{fileName}.asset";
        
        if (!overwrite && File.Exists(fullPath))
        {
            warnings.Add($"Skipping existing item: {data.name}");
            return;
        }
        
        ItemDef item = ScriptableObject.CreateInstance<ItemDef>();
        item.id = fileName;
        item.displayName = data.name;
        item.itemCategory = data.category;
        item.sellPrice = data.sellPrice;
        item.baseValue = data.sellPrice;
        item.description = data.description;
        item.chance = 1f;
        item.prefab = itemPrefab;
        
        AssetDatabase.CreateAsset(item, fullPath);
        EditorUtility.SetDirty(item);
    }
    
    private void CreateMob(string path, MobPreviewData data, bool overwrite)
    {
        string fileName = $"Mob_{data.name.Replace(" ", "")}";
        string fullPath = $"{path}/{fileName}.asset";
        
        if (!overwrite && File.Exists(fullPath))
        {
            warnings.Add($"Skipping existing mob: {data.name}");
            return;
        }
        
        MobDef mob = ScriptableObject.CreateInstance<MobDef>();
        mob.id = fileName;
        mob.displayName = data.name;
        mob.assignedLayer = data.layer;
        mob.baseHealth = data.hp;
        mob.moveSpeed = data.moveSpeed;
        mob.attackDamage = data.attackDamage;
        mob.attackInterval = data.attackInterval;
        mob.attackRange = data.attackRange;
        mob.scanRange = data.scanRange;
        mob.spawnWeight = data.spawnWeight;
        mob.description = data.description;
        mob.lootDropAmount = new Vector2Int(1, 2);
        mob.prefab = mobPrefab;
        
        mob.combatConfig = new CombatConfig
        {
            canAttack = data.canAttack,
            behaviorType = data.behaviorType,
            hostileTo = data.canAttack ? HostilityTargets.Adventurers : HostilityTargets.None,
            territorialRadius = data.territorialRadius
        };
        
        mob.loot = new List<ItemDef>();
        
        AssetDatabase.CreateAsset(mob, fullPath);
        EditorUtility.SetDirty(mob);
    }
    
    private void CreateRecipe(string path, RecipePreviewData data, bool overwrite)
    {
        string fileName = $"Recipe_{data.name.Replace(" ", "")}";
        string fullPath = $"{path}/{fileName}.asset";
        
        if (!overwrite && File.Exists(fullPath))
        {
            warnings.Add($"Skipping existing recipe: {data.name}");
            return;
        }
        
        ItemDef outputItem = FindOrCreateCraftedItem(data.outputItem, data.outputCategory, data.sellPrice, data.description);
        if (outputItem == null)
        {
            errors.Add($"Recipe '{data.name}': Failed to create output item '{data.outputItem}'");
            return;
        }
        
        RecipeDef recipe = ScriptableObject.CreateInstance<RecipeDef>();
        AssetDatabase.CreateAsset(recipe, fullPath);
        
        SerializedObject so = new SerializedObject(recipe);
        
        SerializedProperty outputProp = so.FindProperty("output");
        outputProp.objectReferenceValue = outputItem;
        
        SerializedProperty outputQtyProp = so.FindProperty("outputQty");
        outputQtyProp.intValue = 1;
        
        SerializedProperty craftSecondsProp = so.FindProperty("craftSeconds");
        craftSecondsProp.floatValue = data.craftTime;
        
        SerializedProperty ingredientsProp = so.FindProperty("ingredients");
        ingredientsProp.ClearArray();
        
        foreach (var ing in data.ingredients)
        {
            ItemDef item = FindItem(ing.itemName);
            if (item == null)
            {
                errors.Add($"Recipe '{data.name}': Ingredient '{ing.itemName}' not found");
                continue;
            }
            
            ingredientsProp.InsertArrayElementAtIndex(ingredientsProp.arraySize);
            SerializedProperty element = ingredientsProp.GetArrayElementAtIndex(ingredientsProp.arraySize - 1);
            
            SerializedProperty itemProp = element.FindPropertyRelative("item");
            SerializedProperty qtyProp = element.FindPropertyRelative("qty");
            
            itemProp.objectReferenceValue = item;
            qtyProp.intValue = ing.quantity;
        }
        
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(recipe);
    }
    
    // ===== HELPER METHODS =====
    
    private void SelectAll(bool selected)
    {
        if (selectedSheet == SheetType.AllSheets)
        {
            switch (activeTab)
            {
                case TabType.Items:
                    itemPreviews.ForEach(i => i.selected = selected);
                    break;
                case TabType.Mobs:
                    mobPreviews.ForEach(m => m.selected = selected);
                    break;
                case TabType.Recipes:
                    recipePreviews.ForEach(r => r.selected = selected);
                    break;
                case TabType.LootTables:
                    lootTablePreviews.ForEach(l => l.selected = selected);
                    break;
            }
        }
        else
        {
            switch (selectedSheet)
            {
                case SheetType.Items:
                    itemPreviews.ForEach(i => i.selected = selected);
                    break;
                case SheetType.Mobs:
                    mobPreviews.ForEach(m => m.selected = selected);
                    break;
                case SheetType.Recipes:
                    recipePreviews.ForEach(r => r.selected = selected);
                    break;
                case SheetType.LootTables:
                    lootTablePreviews.ForEach(l => l.selected = selected);
                    break;
            }
        }
    }
    
    private void OverrideAll(bool overwrite)
    {
        if (selectedSheet == SheetType.AllSheets)
        {
            switch (activeTab)
            {
                case TabType.Items:
                    itemPreviews.ForEach(i => i.overwrite = overwrite);
                    break;
                case TabType.Mobs:
                    mobPreviews.ForEach(m => m.overwrite = overwrite);
                    break;
                case TabType.Recipes:
                    recipePreviews.ForEach(r => r.overwrite = overwrite);
                    break;
            }
        }
        else
        {
            switch (selectedSheet)
            {
                case SheetType.Items:
                    itemPreviews.ForEach(i => i.overwrite = overwrite);
                    break;
                case SheetType.Mobs:
                    mobPreviews.ForEach(m => m.overwrite = overwrite);
                    break;
                case SheetType.Recipes:
                    recipePreviews.ForEach(r => r.overwrite = overwrite);
                    break;
            }
        }
    }
    
    private int GetTotalSelectedCount()
    {
        if (selectedSheet == SheetType.AllSheets)
        {
            return itemPreviews.FindAll(i => i.selected).Count +
                   mobPreviews.FindAll(m => m.selected).Count +
                   recipePreviews.FindAll(r => r.selected).Count +
                   lootTablePreviews.FindAll(l => l.selected).Count;
        }
        else
        {
            switch (selectedSheet)
            {
                case SheetType.Items:
                    return itemPreviews.FindAll(i => i.selected).Count;
                case SheetType.Mobs:
                    return mobPreviews.FindAll(m => m.selected).Count;
                case SheetType.Recipes:
                    return recipePreviews.FindAll(r => r.selected).Count;
                case SheetType.LootTables:
                    return lootTablePreviews.FindAll(l => l.selected).Count;
                default:
                    return 0;
            }
        }
    }
    
    private ItemDef FindItem(string itemName)
    {
        string searchName = itemName.Replace(" ", "");
        string[] guids = AssetDatabase.FindAssets($"Item_{searchName} t:ItemDef");
        
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<ItemDef>(path);
        }
        
        return null;
    }
    
    private MobDef FindMob(string mobName)
    {
        string searchName = mobName.Replace(" ", "");
        string[] guids = AssetDatabase.FindAssets($"Mob_{searchName} t:MobDef");
        
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<MobDef>(path);
        }
        
        return null;
    }
    
    private ItemDef FindOrCreateCraftedItem(string itemName, ItemCategory category, int sellPrice, string description)
    {
        ItemDef existing = FindItem(itemName);
        if (existing != null)
            return existing;
        
        string itemPath = $"{outputPath}/Items/Crafted"; // Always Crafted for recipe outputs
        EnsureFolderExists(itemPath);
        string fileName = $"Item_{itemName.Replace(" ", "")}";
        string fullPath = $"{itemPath}/{fileName}.asset";
        
        ItemDef item = ScriptableObject.CreateInstance<ItemDef>();
        item.id = fileName;
        item.displayName = itemName;
        item.itemCategory = category;
        item.sellPrice = sellPrice;
        item.baseValue = sellPrice;
        item.description = description;
        item.chance = 1f;
        item.prefab = itemPrefab;
        
        AssetDatabase.CreateAsset(item, fullPath);
        EditorUtility.SetDirty(item);
        
        Debug.Log($"[ExcelImporter] Auto-created crafted item: {itemName}");
        return item;
    }
    
    private string GetMobPath(int layer)
    {
        return $"{outputPath}/Mobs/Layer{layer}";
    }
    
    private void EnsureFolderExists(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            string parentPath = Path.GetDirectoryName(path);
            string folderName = Path.GetFileName(path);
            
            if (!string.IsNullOrEmpty(parentPath))
            {
                EnsureFolderExists(parentPath);
            }
            
            AssetDatabase.CreateFolder(parentPath, folderName);
        }
    }
    
    private string GetRelativePath(string absolutePath)
    {
        if (absolutePath.StartsWith(Application.dataPath))
        {
            return "Assets" + absolutePath.Substring(Application.dataPath.Length);
        }
        return absolutePath;
    }
    
    private string GetString(DataRow row, string columnName)
    {
        if (row.Table.Columns.Contains(columnName) && row[columnName] != null && !string.IsNullOrEmpty(row[columnName].ToString()))
        {
            return row[columnName].ToString().Trim();
        }
        return string.Empty;
    }
    
    private int GetInt(DataRow row, string columnName, int defaultValue = 0)
    {
        string value = GetString(row, columnName);
        if (int.TryParse(value, out int result))
            return result;
        return defaultValue;
    }
    
    private float GetFloat(DataRow row, string columnName, float defaultValue = 0f)
    {
        string value = GetString(row, columnName);
        if (float.TryParse(value, out float result))
            return result;
        return defaultValue;
    }
    
    private bool GetBool(DataRow row, string columnName, bool defaultValue = false)
    {
        string value = GetString(row, columnName).ToUpper();
        if (value == "TRUE" || value == "1" || value == "YES")
            return true;
        if (value == "FALSE" || value == "0" || value == "NO")
            return false;
        return defaultValue;
    }
    
    private ItemCategory ParseCategory(string category)
    {
        return category.ToUpper() switch
        {
            "BASIC" => ItemCategory.Basic,
            "COMMON" => ItemCategory.Basic,
            "ADVANCED" => ItemCategory.Advanced,
            "CRAFTED" => ItemCategory.Advanced,
            "PREMIUM" => ItemCategory.Premium,
            "LUXURY" => ItemCategory.Premium,
            _ => ItemCategory.Basic
        };
    }
    
    private CombatBehaviorType ParseBehaviorType(string behavior)
    {
        return behavior switch
        {
            "Passive" => CombatBehaviorType.Passive,
            "Aggressive" => CombatBehaviorType.Aggressive,
            "Territorial" => CombatBehaviorType.Territorial,
            "Defensive" => CombatBehaviorType.Defensive,
            _ => CombatBehaviorType.Passive
        };
    }
}
#endif