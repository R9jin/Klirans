using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

/// <summary>
/// DeployEnvironmentalStorytelling — Generates rich, atmospheric environmental storytelling
/// across the university building (notes, incident reports, diary scraps, locker warnings, and blackboard scrawls).
/// </summary>
public static class DeployEnvironmentalStorytelling
{
    [MenuItem("Klirans/Deploy Environmental Storytelling")]
    public static void Deploy()
    {
        Debug.Log("[DeployEnvironmentalStorytelling] Beginning environmental storytelling deployment...");

        // 1. Create Icons
        Sprite memoIcon = CreateOrLoadIcon("Assets/Items/Icons/Note_Memo_Icon.png", new Color(0.20f, 0.35f, 0.55f), new Color(0.95f, 0.95f, 0.95f), "MEMO");
        Sprite diaryIcon = CreateOrLoadIcon("Assets/Items/Icons/Note_Diary_Icon.png", new Color(0.70f, 0.55f, 0.30f), new Color(0.98f, 0.95f, 0.88f), "DIARY");
        Sprite warningIcon = CreateOrLoadIcon("Assets/Items/Icons/Note_Warning_Icon.png", new Color(0.75f, 0.15f, 0.15f), new Color(1.0f, 0.90f, 0.30f), "WARN");

        // 2. Create Materials
        Material paperMat = CreateOrLoadPaperMaterial();
        Material tapeMat = CreateOrLoadTapeMaterial();
        Material pinMat = CreateOrLoadPinMaterial();

        // 3. Deploy 12 World Notes
        DeployWorldNotes(paperMat, tapeMat, pinMat, memoIcon, diaryIcon, warningIcon);

        // 4. Deploy Blackboard Chalk Scrawls
        DeployBlackboardChalkScrawls();

        // 5. Save assets and active scene
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

        Debug.Log("[DeployEnvironmentalStorytelling] Deployment successfully completed!");
    }

    private static Sprite CreateOrLoadIcon(string iconPath, Color bgColor, Color fgColor, string type)
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

            // Document shape
            for (int y = 10; y < 86; y++)
            {
                for (int x = 16; x < 80; x++)
                {
                    // Top-right dog-ear fold
                    if (x > 62 && y > 68 && (x - 62) + (y - 68) > 16) continue;

                    bool isBorder = (x <= 18 || x >= 78 || y <= 12 || y >= 84);
                    Color col = isBorder ? fgColor : bgColor;

                    // Text lines preview
                    if (!isBorder && y % 10 == 0 && x > 26 && x < 70)
                        col = fgColor * 0.9f;

                    tex.SetPixel(x, y, col);
                }
            }

            tex.Apply();
            File.WriteAllBytes(iconPath, tex.EncodeToPNG());
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

    private static Material CreateOrLoadPaperMaterial()
    {
        string path = "Assets/Materials/WorldPaperNote_Mat.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            mat = new Material(shader);
            mat.SetColor("_BaseColor", new Color(0.96f, 0.93f, 0.86f, 1f)); // Aged cream paper
            mat.SetFloat("_Smoothness", 0.15f);

            var paperTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/PlayerAssets/Blank_paper.png");
            if (paperTex != null) mat.SetTexture("_BaseMap", paperTex);

            AssetDatabase.CreateAsset(mat, path);
        }
        return mat;
    }

    private static Material CreateOrLoadTapeMaterial()
    {
        string path = "Assets/Materials/TapeStrip_Mat.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            mat = new Material(shader);
            mat.SetColor("_BaseColor", new Color(0.90f, 0.86f, 0.68f, 0.88f)); // Translucent masking tape
            mat.SetFloat("_Smoothness", 0.35f);
            AssetDatabase.CreateAsset(mat, path);
        }
        return mat;
    }

    private static Material CreateOrLoadPinMaterial()
    {
        string path = "Assets/Materials/PushPin_Mat.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            mat = new Material(shader);
            mat.SetColor("_BaseColor", new Color(0.85f, 0.15f, 0.15f, 1f)); // Red push-pin
            mat.SetFloat("_Smoothness", 0.85f);
            AssetDatabase.CreateAsset(mat, path);
        }
        return mat;
    }

    private static void DeployWorldNotes(Material paperMat, Material tapeMat, Material pinMat, Sprite memoIcon, Sprite diaryIcon, Sprite warningIcon)
    {
        GameObject container = GameObject.Find("EnvironmentalNotesContainer");
        if (container == null) container = new GameObject("EnvironmentalNotesContainer");

        // ── 1F Notes ─────────────────────────────────────────────────────────

        // Note 01: Main Lobby Counter
        SpawnNote(
            container: container,
            name: "Note_01_LobbyCurfewOrder",
            pos: new Vector3(-82.45f, 3.10f, 16.65f),
            rot: Quaternion.Euler(0f, 20f, 0f),
            size: new Vector3(0.24f, 0.003f, 0.32f),
            paperMat: paperMat,
            detailMat: pinMat,
            isWallMounted: false,
            title: "ADMINISTRATIVE ORDER 88: MANDATORY CLEARANCE CURFEW",
            promptVerb: "Read Memorandum",
            icon: memoIcon,
            content:
@"============================================================
PAMANTASAN NG CABUYAO — OFFICE OF THE EXECUTIVE VICE PRESIDENT
MEMORANDUM CIRCULAR NO. 88-B: CLEARANCE PROTOCOL & CAMPUS CURFEW
============================================================
TO: ALL GRADUATING STUDENTS, FACULTY & ADMINISTRATIVE STAFF
DATE: OCTOBER 11, 1994
SUBJECT: STRICT IMPLEMENTATION OF 7:00 PM CURFEW & FACILITY LOCKDOWN

It has come to the attention of the Administration that unauthorized students
continue to linger within university premises past operating hours under the
pretense of securing delinquent clearance signatures.

EFFECTIVE IMMEDIATELY:
1. All clearance transactions strictly cease at 19:00 hrs.
2. The campus main grille gate will automatically lock upon curfew commencement.
3. Auxiliary emergency lighting (Red Circuit) indicates lockdown state.
4. Any student apprehended on campus grounds past curfew will face immediate
   sanction and disciplinary clearance cancellation by the Head Proctor.

NOTE ON ELECTRICAL SYSTEM:
The Facility Management office is aware of breaker tripping in Substation B-1.
Do NOT attempt to bypass fuse boxes. Report all irregularities to Security.

BY ORDER OF:
OFFICE OF THE EXECUTIVE VICE PRESIDENT
""Integrity, Discipline, Academic Excellence""
============================================================"
        );

        // Note 02: Security Station Desk
        SpawnNote(
            container: container,
            name: "Note_02_SecurityStationLog",
            pos: new Vector3(-82.52f, 3.08f, 4.90f),
            rot: Quaternion.Euler(0f, -15f, 0f),
            size: new Vector3(0.24f, 0.003f, 0.32f),
            paperMat: paperMat,
            detailMat: tapeMat,
            isWallMounted: false,
            title: "SECURITY LOGBOOK: SHIFT TURNOVER (23:14 HRS)",
            promptVerb: "Read Guard Logbook",
            icon: memoIcon,
            content:
@"============================================================
CAMPUS SECURITY DIVISION — DAILY LOG SHEET
OFFICER ON DUTY: SG-2 M. DELA CRUZ
============================================================
19:00 - Main gate locked per EVP Admin Order 88. Several students turned away.
20:15 - Power fluctuation reported in 2nd Floor Faculty wing. Sent guard to check.
21:40 - Guard B. Santos radioed in distress from Room 205 corridor. Reported hearing
        loud female weeping in the ceiling ventilation. Radio went dead.
22:30 - Breaker tripped. Complete blackout across Main Building. Emergency red lights active.
23:10 - Inspected 1st floor corridor. I saw the Head Proctor standing motionless at the
        end of the hallway. I called out to him, but he didn't answer. When I shone my
        heavy spotlight, he had no face—only blank, smooth skin beneath his visor.
23:14 - (Shaking handwriting):
        He turned his head toward me with an audible snap.
        Main gate is deadbolted from the central system. I took the spare keys and locked
        myself inside the booth. May Diyos na maawa sa amin.
============================================================"
        );

        // Note 03: 1F West Locker Door
        SpawnNote(
            container: container,
            name: "Note_03_LockerSurvivalScrawl",
            pos: new Vector3(-85.65f, 3.80f, 34.29f),
            rot: Quaternion.Euler(0f, 90f, 0f),
            size: new Vector3(0.22f, 0.30f, 0.003f),
            paperMat: paperMat,
            detailMat: tapeMat,
            isWallMounted: true,
            title: "DESPERATE SCRAWL: HOW TO SURVIVE THE LOCKDOWN",
            promptVerb: "Read Taped Note",
            icon: diaryIcon,
            content:
@"============================================================
[FRANTIC BALLPEN SCRAWL ON A TORN EXAM BOOKLET COVER]
============================================================
KUNG SINO KA MAN NA NAGBABASA NITO:

HUWAG KANG LALABAS SA GITNA NG HALLWAY KAPAG PATAY ANG ILAW!

1. ANG MGA LOCKER:
   Ligtas ka sa loob ng locker! Hindi ka nila nakikita hangga't
   tahimik ka. Pero pakinggan mo ang tibok ng puso mo—kapag mabilis,
   ibig sabihin nasa labas lang siya.

2. ANG PEEPHOLE:
   Huwag kang sisilip sa peep hole kung marinig mo ang mabigat na yabag!
   Pag sumilip ka habang nakatitig ang Proctor sa pinto, bubuksan niya ito!

3. ANG MGA SUSI:
   Kalahati ng mga locker sa hallway ay nakapadlock. Ang mga guro ang
   nagtatago ng susi sa mga desk nila sa classrooms (201, 203, 301, ComLab).
   Maghanap ka ng mga susi bago ka maabutan ng dilim.

Panginoon, tulungan Mo kami. Huwag mo siyang hahayaang lapitan ka.
============================================================"
        );

        // Note 04: 1F ComLab (Room 105) Podium
        SpawnNote(
            container: container,
            name: "Note_04_ComLabIncidentReport",
            pos: new Vector3(-89.90f, 3.06f, 38.93f),
            rot: Quaternion.Euler(0f, 10f, 0f),
            size: new Vector3(0.24f, 0.003f, 0.32f),
            paperMat: paperMat,
            detailMat: pinMat,
            isWallMounted: false,
            title: "COMLAB LOG: TERMINAL 14 ABNORMALITY",
            promptVerb: "Read ComLab Notice",
            icon: warningIcon,
            content:
@"============================================================
COLLEGE OF COMPUTING STUDIES — COMPUTER LABORATORY 105
LABORATORY TECHNICIAN MAINTENANCE NOTICE
============================================================
STATION ISSUE LOG: TERMINAL 14 (CORNER WORKSTATION)

Date Reported: Oct 09, 1994
Technician: Engr. R. Bautista

DETAILS:
Student ID 2021-09411 (Benedicta ""Bene"" Santos) was assigned to Terminal 14
during the thesis defense clearance week. A 16MB RAM module and optical mouse
were declared missing upon her logout. Because of this, her ComLab Accountability
Form was marked REJECTED by Department Head.

ANOMALY LOG:
- Despite Terminal 14 being unplugged from the wall socket, the CRT monitor
  spontaneously turns on at precisely 20:00 hrs every evening.
- It displays a blinking DOS terminal cursor with text repeating infinitely:
  ""HOLD STATUS: ACCOUNTABILITY UNCLEARED. PLEASE DO NOT LEAVE ME.""
- Keyboards in row 3 rattle without anyone typing.
- Crying sounds can be heard directly from the internal PC speaker horn.

DIRECTIVE:
Do NOT power on Terminal 14. Keep Room 105 locked until Dean inspects hardware.
============================================================"
        );

        // Note 05: 1F Registrar Corridor Wall
        SpawnNote(
            container: container,
            name: "Note_05_RegistrarClearanceNotice",
            pos: new Vector3(-80.50f, 3.50f, 4.50f),
            rot: Quaternion.Euler(0f, -90f, 0f),
            size: new Vector3(0.22f, 0.30f, 0.003f),
            paperMat: paperMat,
            detailMat: tapeMat,
            isWallMounted: true,
            title: "REGISTRAR NOTICE: STRICT ACCOUNTABILITY POLICY",
            promptVerb: "Read Pinned Notice",
            icon: memoIcon,
            content:
@"============================================================
OFFICE OF THE UNIVERSITY REGISTRAR
WARNING TO ALL CANDIDATES FOR GRADUATION
============================================================
The University strictly implements the 6-STAGE VERIFICATION HIERARCHY.
No diploma, transcript, or honorable dismissal shall be released without
the physical validation of all respective department heads:

1. Main Library Archive (Settlement of overdue literature)
2. Guidance & Counseling (Mental wellness & clearance interview)
3. College Dean (Academic & laboratory property accountability)
4. Office of the Registrar (Verification of academic credentials)
5. University Cashier (Official assessment & tuition settlement)
6. Executive Vice President (Final executive institutional seal)

WARNING AGAINST FORGERY:
Any forged signature or unauthorized stamp will result in permanent
academic disqualification and immediate referral to the Disciplinary Committee.

(Scrawled in red pencil underneath):
""Paulit-ulit na lang... ilang araw na kaming walang tulog.
Bakit kailangang dumaan sa lahat bago makauwi?""
============================================================"
        );

        // ── 2F Notes ─────────────────────────────────────────────────────────

        // Note 06: Classroom 201 Teacher Desk
        SpawnNote(
            container: container,
            name: "Note_06_ExamPaperDesperateNote",
            pos: new Vector3(-89.50f, 9.12f, -5.07f),
            rot: Quaternion.Euler(0f, -25f, 0f),
            size: new Vector3(0.24f, 0.003f, 0.32f),
            paperMat: paperMat,
            detailMat: pinMat,
            isWallMounted: false,
            title: "TORN EXAM PAPER: 'HINDI SIYA GURO'",
            promptVerb: "Read Exam Scrap",
            icon: diaryIcon,
            content:
@"============================================================
[STAINED MIDTERM EXAMINATION SHEET — ADVANCED ALGORITHMS]
STUDENT NAME: [Scribbled Out]
SCORE: INCOMPLETE
============================================================
(The test answers abruptly stop halfway down page 2.
The rest of the lined paper is filled with frantic, trembling handwriting):

""He is standing by the doorway again.
He doesn't breathe. His suit is covered in grey chalk dust.
Every time someone moves their chair, his eyes snap toward them.

Sir Reyes is not a teacher. He is not human.
When Carla tried to run for the stairs during the quiz, he was in front
of her in less than a second. We heard a sickening crunch in the dark hallway.

If you see him:
STOP MOVING.
Don't run in an open corridor. Duck behind the desks or get into a locker.
He only catches you when he sees you standing.""
============================================================"
        );

        // Note 07: 2F Hallway Corkboard near Dean's Office (Room 205)
        SpawnNote(
            container: container,
            name: "Note_07_FacultyBeneIncident",
            pos: new Vector3(-83.25f, 9.75f, 28.09f),
            rot: Quaternion.Euler(0f, -90f, 0f),
            size: new Vector3(0.22f, 0.30f, 0.003f),
            paperMat: paperMat,
            detailMat: pinMat,
            isWallMounted: true,
            title: "CONFIDENTIAL MEMO: INCIDENT OF STUDENT BENE",
            promptVerb: "Read Incident Report",
            icon: memoIcon,
            content:
@"============================================================
OFFICE OF ACADEMIC AFFAIRS — STRICTLY CONFIDENTIAL
INCIDENT REPORT NO. 1994-0412
============================================================
DATE: OCTOBER 08, 1994
SUBJECT: CASE OF STUDENT BENEDICTA ""BENE"" SANTOS (BSIT 4-A)

On Wednesday evening, student Santos was denied final clearance by the
Dean of Computing Studies due to an unresolved ComLab hardware claim.
Witnesses state she spent over four hours outside Room 205 in tears,
begging for a reconsideration as she was the first in her family to graduate.

At approximately 21:30 hrs, custodial staff noted her uniform necktie
and clearance slip lying drenched on the 2nd floor staircase landing.
The student has not returned home since that evening.

FACULTY DIRECTIVE:
Staff are advised NOT to entertain inquiries from the student's parents.
Security has been instructed to dispel persistent rumors of a weeping
female figure roaming the 2nd and 3rd floor corridors after lights-out.
============================================================"
        );

        // Note 08: 2F Guidance Counseling Waiting Area (Room 207)
        SpawnNote(
            container: container,
            name: "Note_08_GuidanceCounselorNotes",
            pos: new Vector3(-92.20f, 9.07f, 43.50f),
            rot: Quaternion.Euler(0f, 15f, 0f),
            size: new Vector3(0.24f, 0.003f, 0.32f),
            paperMat: paperMat,
            detailMat: pinMat,
            isWallMounted: false,
            title: "GUIDANCE COUNSELOR CLINICAL JOURNAL SCRAP",
            promptVerb: "Read Counselor Notes",
            icon: memoIcon,
            content:
@"============================================================
OFFICE OF GUIDANCE & COUNSELING
CLINICAL OBSERVATION LOG — DR. E. VALDERRAMA
============================================================
October 10, 1994:

Something is deeply wrong with this building.
This week alone, seven students came to my office suffering from acute
sleep deprivation and auditory panic. All of them described the exact
same sequence of events:

1. A sudden drop in corridor temperature followed by fluorescent buzzing.
2. The sound of high-heeled school shoes dragging along the linoleum.
3. A young woman sobbing uncontrollably, whispering:
   ""Asan po ang pirma ko? Bakit ayaw niyo akong paalisin?""
   (Where is my signature? Why won't you let me leave?)

I asked the Dean to postpone clearance deadlines, but he scoffed.
Tonight I heard the weeping outside my own door.
It wasn't a hallucination. It was right behind the frosted glass.
I am packing my things and leaving early. May God forgive us.
============================================================"
        );

        // Note 09: 2F East Locker Door
        SpawnNote(
            container: container,
            name: "Note_09_StudentWeepingWarning",
            pos: new Vector3(-83.15f, 9.60f, 28.10f),
            rot: Quaternion.Euler(0f, -90f, 0f),
            size: new Vector3(0.22f, 0.30f, 0.003f),
            paperMat: paperMat,
            detailMat: tapeMat,
            isWallMounted: true,
            title: "TORN DIARY: 'KAPAG UMIIYAK SI BENE'",
            promptVerb: "Read Warning Note",
            icon: warningIcon,
            content:
@"============================================================
[NOTEBOOK PAPER WEDGED BETWEEN LOCKER SLATS]
============================================================
KAPAG NARIRINIG MO SI BENE:

1. Huwag kang mag-flashlight sa mukha niya!
   Mabilis siyang magalit kapag nasilawan. Namumula ang mata niya
   at bigla siyang susugod nang mabilis.

2. Makinig ka sa iyak niya:
   - Malayo pa: Mahinang hikbi, parang humihingi ng tulong.
   - Malapit na: Malakas at parang sumisigaw na sa sakit.
   Kapag lumalakas, yumuko ka (Crouch) at dahan-dahang lumayo,
   o kaya ay pumasok agad sa pinakamalapit na bukas na locker!

3. Huwag kang tumakbo sa mahabang hallway!
   Mas mabilis siya sa deretsong takbuhan. Gamitin mo ang mga pinto
   ng classroom para putulin ang tingin niya.

Si Bene lang yan... gusto lang niyang makatapos... pero hindi na siya buhay.
============================================================"
        );

        // ── 3F Notes ─────────────────────────────────────────────────────────

        // Note 10: 3F Library Archive Desk (Room 308)
        SpawnNote(
            container: container,
            name: "Note_10_LibrarianDeskNotice",
            pos: new Vector3(-76.85f, 15.06f, 44.15f),
            rot: Quaternion.Euler(0f, 30f, 0f),
            size: new Vector3(0.24f, 0.003f, 0.32f),
            paperMat: paperMat,
            detailMat: pinMat,
            isWallMounted: false,
            title: "CHIEF LIBRARIAN MEMORANDUM: LOST ARCHIVE VOLUMES",
            promptVerb: "Read Archive Notice",
            icon: memoIcon,
            content:
@"============================================================
PAMANTASAN NG CABUYAO — UNIVERSITY LIBRARY ARCHIVES
MEMORANDUM FROM THE CHIEF LIBRARIAN
============================================================
POLICY ON STACK CLEARANCE & OVERDUE PENALTIES:

Under Section 4 of the University Code, failure to account for reference
holdings results in immediate suspension of student status.

INCIDENT NOTE (Handwritten in margin):
""Books in Aisle 6 were found strewn across the floor again this morning.
All titles were related to Computer Science Thesis Archives (1990-1994).
A single clearance slip was tucked inside Volume 3, soaked in tears.
The signature line for the Library was empty.

Whoever is tampering with the card catalog at night, stop this childish game.
The library doors will be locked with heavy padlocks starting at sundown.
No exceptions will be made for late submissions.""
============================================================"
        );

        // Note 11: Classroom 301 Teacher Podium
        SpawnNote(
            container: container,
            name: "Note_11_Classroom301LectureNote",
            pos: new Vector3(-89.51f, 15.14f, 6.96f),
            rot: Quaternion.Euler(0f, -10f, 0f),
            size: new Vector3(0.24f, 0.003f, 0.32f),
            paperMat: paperMat,
            detailMat: tapeMat,
            isWallMounted: false,
            title: "CHALKBOARD SCRAWL & STUDENT WARNING",
            promptVerb: "Read Classroom Note",
            icon: diaryIcon,
            content:
@"============================================================
[A CRUMBLED CHALK WRITING NOTE LEFT ON THE TEACHER'S PODIUM]
============================================================
NAKASULAT SA PISARA (BLACKBOARD) SA LIKOD:

""WALA NANG MAKAKAALIS DITO.""
(Nobody leaves here anymore.)

""THE PROCTOR WALKS THE 3RD FLOOR CORRIDOR AT MIDNIGHT.
HE DOES NOT BLINK. HE DOES NOT STOP.
HE WILL STAND RIGHT OUTSIDE YOUR LOCKER.
IF YOU HEAR THE HEAVY BOOTS:
HOLD YOUR BREATH.
DO NOT MOVE.
DO NOT TOUCH THE DOOR HANDLE.
HE LEAVES ONLY WHEN SILENCE RETURNS.""

- Batch 1994 Delinquents
============================================================"
        );

        // Note 12: 1F to 2F Stairwell Landing Wall
        SpawnNote(
            container: container,
            name: "Note_12_StairwellEmergencyNotice",
            pos: new Vector3(-85.86f, 6.00f, 16.65f),
            rot: Quaternion.Euler(0f, 90f, 0f),
            size: new Vector3(0.22f, 0.30f, 0.003f),
            paperMat: paperMat,
            detailMat: warningIcon != null ? pinMat : tapeMat,
            isWallMounted: true,
            title: "FACILITIES NOTICE: EMERGENCY STAIRWELL LOCKOUT",
            promptVerb: "Read Safety Notice",
            icon: warningIcon,
            content:
@"============================================================
CAMPUS ENGINEERING & PHYSICAL PLANT SERVICES
WARNING: EMERGENCY AUXILIARY GRID ACTIVE
============================================================
During campus blackout conditions:
- Automatic fire doors and magnetic deadbolts on all ground floor exits
  will lock down to prevent looting and unauthorized access.
- Auxiliary red lighting will illuminate staircases and primary egress paths.
- Clearance authorization from the Office of the Executive Vice President
  is REQUIRED to disengage magnetic locks and release the main security gate.

DO NOT ATTEMPT TO FORCE EXIT DOORS.
Wait for clearance processing or security personnel.

(Beneath the official notice, someone violently scratched into the paint):
""KANDADO LAHAT NG PINTO! HINDI KAMI PINAPALABAS!""
(All doors are locked! They won't let us out!)
============================================================"
        );
    }

    private static void SpawnNote(GameObject container, string name, Vector3 pos, Quaternion rot, Vector3 size,
        Material paperMat, Material detailMat, bool isWallMounted, string title, string promptVerb, Sprite icon, string content)
    {
        Transform existing = container.transform.Find(name);
        GameObject root = existing != null ? existing.gameObject : new GameObject(name);
        root.transform.SetParent(container.transform, false);
        root.transform.position = pos;
        root.transform.rotation = rot;

        // Paper Mesh
        Transform paperChild = root.transform.Find("PaperSheet");
        GameObject paperGO = paperChild != null ? paperChild.gameObject : GameObject.CreatePrimitive(PrimitiveType.Cube);
        paperGO.name = "PaperSheet";
        paperGO.transform.SetParent(root.transform, false);
        paperGO.transform.localPosition = Vector3.zero;
        paperGO.transform.localRotation = Quaternion.identity;
        paperGO.transform.localScale = size;

        var paperCol = paperGO.GetComponent<Collider>();
        if (paperCol != null) Object.DestroyImmediate(paperCol);

        var paperRenderer = paperGO.GetComponent<MeshRenderer>();
        if (paperRenderer != null && paperMat != null) paperRenderer.sharedMaterial = paperMat;

        // Mounting Pin or Tape
        Transform pinChild = root.transform.Find("MountingAnchor");
        GameObject pinGO = pinChild != null ? pinChild.gameObject : (isWallMounted ? GameObject.CreatePrimitive(PrimitiveType.Sphere) : GameObject.CreatePrimitive(PrimitiveType.Cube));
        pinGO.name = "MountingAnchor";
        pinGO.transform.SetParent(root.transform, false);

        if (isWallMounted)
        {
            pinGO.transform.localPosition = new Vector3(0f, size.y * 0.45f, size.z * 0.6f);
            pinGO.transform.localScale = new Vector3(0.016f, 0.016f, 0.016f);
        }
        else
        {
            pinGO.transform.localPosition = new Vector3(0f, size.y * 0.6f, size.z * 0.42f);
            pinGO.transform.localScale = new Vector3(0.08f, 0.004f, 0.024f);
        }

        var pinCol = pinGO.GetComponent<Collider>();
        if (pinCol != null) Object.DestroyImmediate(pinCol);

        var pinRenderer = pinGO.GetComponent<MeshRenderer>();
        if (pinRenderer != null && detailMat != null) pinRenderer.sharedMaterial = detailMat;

        // BoxCollider on Root for Interaction Raycasting
        var bc = root.GetComponent<BoxCollider>();
        if (bc == null) bc = root.AddComponent<BoxCollider>();
        bc.isTrigger = true;
        bc.size = new Vector3(0.40f, 0.40f, 0.25f);
        bc.center = Vector3.zero;

        // WorldNoteInteract Component
        var note = root.GetComponent<WorldNoteInteract>();
        if (note == null) note = root.AddComponent<WorldNoteInteract>();
        note.noteTitle = title;
        note.promptVerb = promptVerb;
        note.noteIcon = icon;
        note.noteContent = content;
        note.autoAddHighlight = true;
        note.glowColor = new Color(0.92f, 0.96f, 1.0f, 1.0f);

        // Visual Highlight
        var hl = root.GetComponent<ItemVisualHighlight>();
        if (hl == null) hl = root.AddComponent<ItemVisualHighlight>();
        hl.category = ItemVisualHighlight.ItemCategory.DocumentOrPaper;
        hl.glowColor = new Color(0.92f, 0.96f, 1.0f, 1.0f);
        hl.lightBaseIntensity = 0.65f;
        hl.sparkleScale = 0.22f;

        EditorUtility.SetDirty(root);
    }

    private static void DeployBlackboardChalkScrawls()
    {
        GameObject container = GameObject.Find("BlackboardScrawlsContainer");
        if (container == null) container = new GameObject("BlackboardScrawlsContainer");

        // Blackboard Scrawl 1: Room 201 (Second Floor)
        SpawnChalkText(
            container: container,
            name: "ChalkScrawl_Room201",
            pos: new Vector3(-89.60f, 9.40f, -3.56f),
            rot: Quaternion.Euler(0f, 180f, 0f),
            text: "BENE IS STILL HERE\nCLEARANCE: REJECTED\nWHY WON'T YOU LET US LEAVE?",
            fontSize: 2.2f
        );

        // Blackboard Scrawl 2: Room 301 (Third Floor)
        SpawnChalkText(
            container: container,
            name: "ChalkScrawl_Room301",
            pos: new Vector3(-89.60f, 15.40f, -3.56f),
            rot: Quaternion.Euler(0f, 180f, 0f),
            text: "DON'T LOOK BACK WHEN SHE CRIES\nTHE PROCTOR IS WATCHING\nSILENCE IN THE HALLWAYS",
            fontSize: 2.2f
        );

        // Blackboard Scrawl 3: Room 105 (ComLab 1)
        SpawnChalkText(
            container: container,
            name: "ChalkScrawl_Room105",
            pos: new Vector3(-89.60f, 3.40f, 33.42f),
            rot: Quaternion.Euler(0f, 180f, 0f),
            text: "TERMINAL 14: DO NOT TOUCH\nHARDWARE HOLD ACTIVE\nNO SIGNATURE = NO EXIT",
            fontSize: 2.2f
        );
    }

    private static void SpawnChalkText(GameObject container, string name, Vector3 pos, Quaternion rot, string text, float fontSize)
    {
        Transform existing = container.transform.Find(name);
        GameObject go = existing != null ? existing.gameObject : new GameObject(name);
        go.transform.SetParent(container.transform, false);
        go.transform.position = pos;
        go.transform.rotation = rot;

        var tmp = go.GetComponent<TextMeshPro>();
        if (tmp == null) tmp = go.AddComponent<TextMeshPro>();

        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.92f, 0.90f, 0.85f, 0.80f); // Chalky off-white with slight translucency
        tmp.characterSpacing = 1.2f;
        tmp.lineSpacing = 18f;

        var rt = go.GetComponent<RectTransform>();
        if (rt != null) rt.sizeDelta = new Vector2(3.0f, 1.2f);

        EditorUtility.SetDirty(go);
    }
}
