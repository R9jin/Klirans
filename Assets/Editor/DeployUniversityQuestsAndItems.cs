using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class DeployUniversityQuestsAndItems
{
    [MenuItem("Klirans/Deploy University Quests and Items")]
    public static void Deploy()
    {
        Debug.Log("[DeployUniversityQuestsAndItems] Starting deployment...");

        // 1. Create Icons
        Sprite handbookIcon = CreateOrLoadIcon("Assets/Items/Icons/Student_Handbook_Icon.png", new Color(0.20f, 0.40f, 0.65f), new Color(0.85f, 0.75f, 0.35f));
        Sprite comlabIcon = CreateOrLoadIcon("Assets/Items/Icons/ComLab_Form_Icon.png", new Color(0.25f, 0.60f, 0.45f), new Color(0.95f, 0.95f, 0.95f));
        Sprite sealIcon = CreateOrLoadIcon("Assets/Items/Icons/Executive_Seal_Icon.png", new Color(0.85f, 0.65f, 0.20f), new Color(0.95f, 0.85f, 0.45f));

        // 2. Create ScriptableObject Items
        InventoryItem handbookItem = CreateOrUpdateHandbookItem(handbookIcon);
        InventoryItem comlabItem = CreateOrUpdateComLabItem(comlabIcon);
        InventoryItem sealItem = CreateOrUpdateSealItem(sealIcon);
        UpdateExistingItemsInspection();

        // 3. Add Handbook to Starting Items on InventoryManager
        AddHandbookToStartingItems(handbookItem);

        // 4. Create and place pickups in the scene
        DeployPickupsInScene(comlabItem, sealItem);

        // 5. Wire ClearanceNPC references in scene
        WireClearanceNPCs(comlabItem, sealItem);

        // 6. Save assets and scene
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

        Debug.Log("[DeployUniversityQuestsAndItems] Deployment successfully completed!");
    }

    private static Sprite CreateOrLoadIcon(string iconPath, Color mainColor, Color accentColor)
    {
        if (!File.Exists(iconPath))
        {
            string dir = Path.GetDirectoryName(iconPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            int size = 96;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color transparent = new Color(0, 0, 0, 0);

            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    tex.SetPixel(x, y, transparent);

            // Draw rounded badge / document icon
            for (int y = 14; y < 82; y++)
            {
                for (int x = 18; x < 78; x++)
                {
                    bool isBorder = (x <= 20 || x >= 76 || y <= 16 || y >= 80);
                    Color col = isBorder ? accentColor : mainColor;

                    // Inner detail lines
                    if (!isBorder && y % 12 == 0 && x > 28 && x < 68)
                        col = accentColor;

                    tex.SetPixel(x, y, col);
                }
            }

            tex.Apply();
            byte[] bytes = tex.EncodeToPNG();
            File.WriteAllBytes(iconPath, bytes);
            AssetDatabase.ImportAsset(iconPath, ImportAssetOptions.ForceUpdate);

            TextureImporter importer = AssetImporter.GetAtPath(iconPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spritePixelsPerUnit = 100;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
    }

    private static InventoryItem CreateOrUpdateHandbookItem(Sprite icon)
    {
        string path = "Assets/Items/Student_Handbook.asset";
        InventoryItem item = AssetDatabase.LoadAssetAtPath<InventoryItem>(path);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<InventoryItem>();
            AssetDatabase.CreateAsset(item, path);
        }

        item.itemName = "Student Handbook & Notes";
        item.description = "Official university student handbook. Contains senior student survival notes, clearance instructions, and curfew warnings.";
        item.itemType = InventoryItem.ItemType.Document;
        item.canBeDropped = false;
        item.isStackable = false;
        item.maxStack = 1;
        item.icon = icon;
        item.isInspectable = true;
        item.inspectionTitle = "Pamantasan ng Cabuyao — Student Clearance Handbook & Diary";
        item.inspectionText =
@"========================================
PAMANTASAN NG CABUYAO (PNC)
OFFICIAL STUDENT CLEARANCE HANDBOOK & DIARY
========================================
Property of: Senior Year Student (ID #2021-08492)

[PAGE 1: CLEARANCE SIGNING PROTOCOL]
To officially exit the campus grounds, you must gather all 6 department endorsements IN EXACT SEQUENCE:
  1. Head Librarian (3rd Floor Library Room 308)
     - Resolve overdue borrow records with printed clearance voucher from Room 308 workstation.
  2. Guidance Counselor (2nd Floor Guidance Room 207)
     - Must demonstrate institutional alignment by unscrambling the Core Values sheet.
  3. CCS Dean / Dept Head (2nd Floor Faculty Room 205)
     - Requires ComLab Hardware Accountability Form from 1st Floor ComLab (Room 105).
  4. University Registrar (Ground Floor Window 2 Room 104)
     - Requires sorting disorganized student grade records into archival trays.
  5. University Cashier (Ground Floor Window 2 Room 102)
     - Requires calculating assessment balance sheet total.
  6. Executive Vice President (Ground Floor Executive Suite Room 101)
     - Requires retrieving the Official University Seal Stamp from 2nd Floor Records Locker.
  * Final Step: Submit completed clearance slip back to Registrar Window 2 to unlock the campus main gate!

[PAGE 2: CAMPUS NIGHT PROTOCOL & BLACKOUT ADVISORY]
- When the sirens wail and power drops, ALL classroom doors automatically lock down.
- Do NOT run aimlessly in open hallways. Find a Hallway Locker immediately!
- Certain lockers are secured with padlocks; spare Locker Keys are scattered across faculty and librarian desks.
- While hiding, peer through the ventilation slats and remain still.
- Ground items shimmer periodically and give off a subtle halo, even in low visibility.
========================================";

        EditorUtility.SetDirty(item);
        return item;
    }

    private static InventoryItem CreateOrUpdateComLabItem(Sprite icon)
    {
        string path = "Assets/Items/ComLab_Clearance_Form.asset";
        InventoryItem item = AssetDatabase.LoadAssetAtPath<InventoryItem>(path);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<InventoryItem>();
            AssetDatabase.CreateAsset(item, path);
        }

        item.itemName = "ComLab Accountability Form";
        item.description = "Official College of Computing Studies laboratory equipment return voucher and thesis terminal clearance.";
        item.itemType = InventoryItem.ItemType.Document;
        item.canBeDropped = false;
        item.isStackable = false;
        item.maxStack = 1;
        item.icon = icon;
        item.isInspectable = true;
        item.inspectionTitle = "College of Computing Studies — Laboratory Accountability Slip";
        item.inspectionText =
@"========================================
COLLEGE OF COMPUTING STUDIES (CCS)
COMPUTER LABORATORY ACCOUNTABILITY FORM
========================================
Student Number: 2021-08492
Program: BS Computer Science / BSIT
Terminal Station: ComLab 1 - Station 14

ACCOUNTABILITY STATUS:
[✓] Terminal PC & Peripheral Inventory: Intact & Returned
[✓] Capstone / Thesis Source Code Archive: Deposited
[✓] Laboratory Breakage Surcharge: None (Waived)

REMARKS: Approved for Department Clearance Endorsement.
Laboratory Custodian: [Signed - R. Santos, ComLab Custodian]
========================================";

        EditorUtility.SetDirty(item);
        return item;
    }

    private static InventoryItem CreateOrUpdateSealItem(Sprite icon)
    {
        string path = "Assets/Items/Executive_Seal_Stamp.asset";
        InventoryItem item = AssetDatabase.LoadAssetAtPath<InventoryItem>(path);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<InventoryItem>();
            AssetDatabase.CreateAsset(item, path);
        }

        item.itemName = "Official University Seal Stamp";
        item.description = "Heavy engraved brass university seal stamp. Required by the Executive Vice President to officially seal the final clearance slip.";
        item.itemType = InventoryItem.ItemType.KeyItem;
        item.canBeDropped = false;
        item.isStackable = false;
        item.maxStack = 1;
        item.icon = icon;
        item.isInspectable = true;
        item.inspectionTitle = "Office of the Executive Vice President — Embossing Seal";
        item.inspectionText =
@"========================================
OFFICE OF THE EXECUTIVE VICE PRESIDENT
OFFICIAL UNIVERSITY EMBOSSING SEAL
========================================
""VERITAS, VIRTUS, SCIENTIA""

This heavy brass embossing seal bears the official
crest of Pamantasan ng Cabuyao.

It is required to officially validate and imprint
the final executive signature on senior graduation clearance.

AUTHORIZATION CODE: EVP-EXEC-1994-08
Clearance Release Order: APPROVED
========================================";

        EditorUtility.SetDirty(item);
        return item;
    }

    private static void UpdateExistingItemsInspection()
    {
        // Blank Paper
        var blankPaper = AssetDatabase.LoadAssetAtPath<InventoryItem>("Assets/Items/Blank_Paper.asset");
        if (blankPaper != null)
        {
            blankPaper.isInspectable = true;
            blankPaper.inspectionTitle = "Official University Clearance Slip";
            blankPaper.inspectionText =
@"========================================
PAMANTASAN NG CABUYAO
OFFICIAL SENIOR GRADUATION CLEARANCE SLIP
========================================
Student Name: Dela Cruz, Juan M.
Student ID: 2021-08492
Academic Year: Senior Year (Graduating Status)

SIGNATURE ACCOUNTABILITIES (IN SEQUENCE):
[1] University Library (3rd Floor Room 308)
[2] Guidance & Counseling Office (2nd Floor Room 207)
[3] College of Computing Studies Dean (2nd Floor Room 205)
[4] University Registrar (Ground Floor Window 2 Room 104)
[5] University Cashier (Ground Floor Window 2 Room 102)
[6] Office of the Executive Vice President (Ground Floor Room 101)

* FINAL SUBMISSION: Surrender fully signed slip to Registrar Window 2
  to authorize release and unlock the campus main gate.
========================================";
            EditorUtility.SetDirty(blankPaper);
        }

        // Library Voucher
        var voucher = AssetDatabase.LoadAssetAtPath<InventoryItem>("Assets/Items/Library_Voucher.asset");
        if (voucher != null)
        {
            voucher.isInspectable = true;
            voucher.inspectionTitle = "Official Library Clearance Voucher";
            voucher.inspectionText =
@"========================================
UNIVERSITY LIBRARY SYSTEM
ACCOUNTABILITY WAIVER & CLEARANCE VOUCHER
========================================
Transaction ID: LIB-CLR-1994-409
Student ID: 2021-08492

RECORD VERIFICATION:
Overdue Holdings: ""Introduction to Operating Systems (1988)""
Status: Account Adjusted & Fine Settlement Cleared.

The bearer has resolved all outstanding library records
and is hereby recommended for Library Clearance Signing.
========================================";
            EditorUtility.SetDirty(voucher);
        }
    }

    private static void AddHandbookToStartingItems(InventoryItem handbook)
    {
        var invMgr = Object.FindAnyObjectByType<InventoryManager>();
        if (invMgr == null) return;

        var existing = invMgr.startingItems;
        bool alreadyHas = false;
        if (existing != null)
        {
            foreach (var item in existing)
            {
                if (item == handbook) { alreadyHas = true; break; }
            }
        }

        if (!alreadyHas)
        {
            int oldLen = existing != null ? existing.Length : 0;
            var newItems = new InventoryItem[oldLen + 1];
            if (existing != null) System.Array.Copy(existing, newItems, oldLen);
            newItems[oldLen] = handbook;
            invMgr.startingItems = newItems;
            EditorUtility.SetDirty(invMgr);
            Debug.Log("[DeployUniversityQuestsAndItems] Added Student Handbook to InventoryManager starting items!");
        }
    }

    private static void DeployPickupsInScene(InventoryItem comlabItem, InventoryItem sealItem)
    {
        GameObject container = GameObject.Find("QuestPickupsContainer");
        if (container == null) container = new GameObject("QuestPickupsContainer");

        // 1. ComLab Form Pickup on Floor 1 ComLab Teacher Desk (-91.54, 3.05, 41.50)
        Transform existingComlab = container.transform.Find("Pickup_ComLabClearanceForm");
        if (existingComlab == null)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Pickup_ComLabClearanceForm";
            go.transform.SetParent(container.transform, false);
            go.transform.position = new Vector3(-91.54f, 3.05f, 41.50f);
            go.transform.rotation = Quaternion.Euler(0f, 25f, 0f);
            go.transform.localScale = new Vector3(0.24f, 0.03f, 0.32f);

            var bc = go.GetComponent<BoxCollider>();
            if (bc != null) bc.isTrigger = false;

            var pickup = go.AddComponent<PickupItem>();
            pickup.itemData = comlabItem;
            pickup.amount = 1;

            var highlight = go.AddComponent<ItemVisualHighlight>();
            highlight.category = ItemVisualHighlight.ItemCategory.DocumentOrPaper;
            highlight.glowColor = new Color(0.85f, 0.94f, 1.0f, 1f);

            EditorUtility.SetDirty(go);
            Debug.Log("[DeployUniversityQuestsAndItems] Spawned Pickup_ComLabClearanceForm in Floor 1 ComLab!");
        }

        // 2. Executive Seal Stamp Pickup at Floor 2 Faculty Records Locker (-76.00, 9.25, -12.00)
        Transform existingSeal = container.transform.Find("Pickup_ExecutiveSealStamp");
        if (existingSeal == null)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Pickup_ExecutiveSealStamp";
            go.transform.SetParent(container.transform, false);
            go.transform.position = new Vector3(-76.00f, 9.25f, -12.00f);
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = new Vector3(0.08f, 0.06f, 0.08f);

            var col = go.GetComponent<Collider>();
            if (col != null) col.isTrigger = false;

            var pickup = go.AddComponent<PickupItem>();
            pickup.itemData = sealItem;
            pickup.amount = 1;

            var highlight = go.AddComponent<ItemVisualHighlight>();
            highlight.category = ItemVisualHighlight.ItemCategory.Key;
            highlight.glowColor = new Color(1.0f, 0.84f, 0.32f, 1f);

            EditorUtility.SetDirty(go);
            Debug.Log("[DeployUniversityQuestsAndItems] Spawned Pickup_ExecutiveSealStamp at 2F Faculty Records Locker!");
        }
    }

    private static void WireClearanceNPCs(InventoryItem comlabItem, InventoryItem sealItem)
    {
        var npcs = Object.FindObjectsByType<ClearanceNPC>(FindObjectsInactive.Include);
        foreach (var npc in npcs)
        {
            if (npc.signatureIndex == 2)
            {
                npc.comlabFormItem = comlabItem;
                EditorUtility.SetDirty(npc);
                Debug.Log($"[DeployUniversityQuestsAndItems] Wired comlabFormItem to {npc.name} (CCS Dean)!");
            }
            else if (npc.signatureIndex == 5)
            {
                npc.executiveSealItem = sealItem;
                EditorUtility.SetDirty(npc);
                Debug.Log($"[DeployUniversityQuestsAndItems] Wired executiveSealItem to {npc.name} (EVP)!");
            }
        }
    }
}
