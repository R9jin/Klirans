using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class DeployStaircaseDummiesAndLoop
{
    [MenuItem("Tools/Deploy Staircase Dummies and Seamless Loop")]
    public static void Execute()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/SampleScene.unity")
        {
            scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== DEPLOY STAIRCASE DUMMIES AND SEAMLESS LOOP ===");

        // 1. Locate LoopingStaircaseManager
        GameObject lsmGO = GameObject.Find("LoopingStaircaseManager");
        if (lsmGO == null)
        {
            lsmGO = new GameObject("LoopingStaircaseManager");
            sb.AppendLine("Created new LoopingStaircaseManager GameObject.");
        }

        var lss = lsmGO.GetComponent<LoopingStaircaseSystem>();
        if (lss == null)
        {
            lss = lsmGO.AddComponent<LoopingStaircaseSystem>();
            sb.AppendLine("Added LoopingStaircaseSystem component.");
        }

        // 2. Configure LoopingStaircaseSystem settings
        lss.transitionCooldown = 10f;
        lss.safeFirstTraversal = true;
        lss.alwaysBlink = false;
        lss.blinkDuration = 0f;

        // Reset staircaseConnections with newly calibrated probabilities
        lss.staircaseConnections = new StaircaseConnection[]
        {
            // MainStairs
            new StaircaseConnection {
                stairwellID = "MainStairs", fromFloor = 1, goingUp = true, normalDestination = 2, normalProbability = 0.75f,
                alternateOutcomes = new AlternateOutcome[] { new AlternateOutcome { destinationFloor = 1, weight = 0.25f } }
            },
            new StaircaseConnection {
                stairwellID = "MainStairs", fromFloor = 2, goingUp = true, normalDestination = 3, normalProbability = 0.70f,
                alternateOutcomes = new AlternateOutcome[] { new AlternateOutcome { destinationFloor = 1, weight = 0.30f } }
            },
            new StaircaseConnection {
                stairwellID = "MainStairs", fromFloor = 3, goingUp = false, normalDestination = 2, normalProbability = 0.70f,
                alternateOutcomes = new AlternateOutcome[] { new AlternateOutcome { destinationFloor = 3, weight = 0.30f } }
            },
            new StaircaseConnection {
                stairwellID = "MainStairs", fromFloor = 2, goingUp = false, normalDestination = 1, normalProbability = 0.75f,
                alternateOutcomes = new AlternateOutcome[] { new AlternateOutcome { destinationFloor = 3, weight = 0.25f } }
            },

            // RightStairs
            new StaircaseConnection {
                stairwellID = "RightStairs", fromFloor = 1, goingUp = true, normalDestination = 2, normalProbability = 0.75f,
                alternateOutcomes = new AlternateOutcome[] { new AlternateOutcome { destinationFloor = 1, weight = 0.25f } }
            },
            new StaircaseConnection {
                stairwellID = "RightStairs", fromFloor = 2, goingUp = true, normalDestination = 3, normalProbability = 0.70f,
                alternateOutcomes = new AlternateOutcome[] { new AlternateOutcome { destinationFloor = 1, weight = 0.30f } }
            },
            new StaircaseConnection {
                stairwellID = "RightStairs", fromFloor = 3, goingUp = false, normalDestination = 2, normalProbability = 0.70f,
                alternateOutcomes = new AlternateOutcome[] { new AlternateOutcome { destinationFloor = 3, weight = 0.30f } }
            },
            new StaircaseConnection {
                stairwellID = "RightStairs", fromFloor = 2, goingUp = false, normalDestination = 1, normalProbability = 0.75f,
                alternateOutcomes = new AlternateOutcome[] { new AlternateOutcome { destinationFloor = 3, weight = 0.25f } }
            },

            // LeftStairs
            new StaircaseConnection {
                stairwellID = "LeftStairs", fromFloor = 1, goingUp = true, normalDestination = 2, normalProbability = 0.75f,
                alternateOutcomes = new AlternateOutcome[] { new AlternateOutcome { destinationFloor = 1, weight = 0.25f } }
            },
            new StaircaseConnection {
                stairwellID = "LeftStairs", fromFloor = 2, goingUp = true, normalDestination = 3, normalProbability = 0.70f,
                alternateOutcomes = new AlternateOutcome[] { new AlternateOutcome { destinationFloor = 1, weight = 0.30f } }
            },
            new StaircaseConnection {
                stairwellID = "LeftStairs", fromFloor = 3, goingUp = false, normalDestination = 2, normalProbability = 0.70f,
                alternateOutcomes = new AlternateOutcome[] { new AlternateOutcome { destinationFloor = 3, weight = 0.30f } }
            },
            new StaircaseConnection {
                stairwellID = "LeftStairs", fromFloor = 2, goingUp = false, normalDestination = 1, normalProbability = 0.75f,
                alternateOutcomes = new AlternateOutcome[] { new AlternateOutcome { destinationFloor = 3, weight = 0.25f } }
            },

            // Forced Upward Loop (3F -> 1F)
            new StaircaseConnection {
                stairwellID = "MainStairs", fromFloor = 3, goingUp = true, normalDestination = 1, normalProbability = 1.0f,
                alternateOutcomes = new AlternateOutcome[0], forceLoop = true
            },
            new StaircaseConnection {
                stairwellID = "RightStairs", fromFloor = 3, goingUp = true, normalDestination = 1, normalProbability = 1.0f,
                alternateOutcomes = new AlternateOutcome[0], forceLoop = true
            },
            new StaircaseConnection {
                stairwellID = "LeftStairs", fromFloor = 3, goingUp = true, normalDestination = 1, normalProbability = 1.0f,
                alternateOutcomes = new AlternateOutcome[0], forceLoop = true
            },
        };
        sb.AppendLine($"Configured {lss.staircaseConnections.Length} directional connection rules.");

        // 3. Attach and configure StaircaseDummyReplicator
        var replicator = lsmGO.GetComponent<StaircaseDummyReplicator>();
        if (replicator == null)
        {
            replicator = lsmGO.AddComponent<StaircaseDummyReplicator>();
            sb.AppendLine("Added StaircaseDummyReplicator component to LoopingStaircaseManager.");
        }

        var dummyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/StaircasePlayerDummy.prefab");
        replicator.dummyPrefab = dummyPrefab;

        var playerGO = GameObject.FindWithTag("Player");
        if (playerGO != null)
        {
            replicator.playerTransform = playerGO.transform;
            replicator.playerMovement = playerGO.GetComponent<PlayerMovement>();
            replicator.playerCamera = playerGO.GetComponentInChildren<Camera>();

            var lights = playerGO.GetComponentsInChildren<Light>(true);
            foreach (var l in lights)
            {
                if (l.type == LightType.Spot)
                {
                    replicator.playerFlashlight = l;
                    break;
                }
            }
        }

        replicator.EnsureDummiesExist();
        sb.AppendLine($"Replicator initialized with {replicator.dummies.Count} dummy instances.");

        EditorUtility.SetDirty(lsmGO);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        sb.AppendLine("SampleScene saved successfully.");

        Debug.Log(sb.ToString());
    }
}
