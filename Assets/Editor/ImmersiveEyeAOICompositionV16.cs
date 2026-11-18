#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// ImmersiveEye v16: conservative, reversible AOI-preserving 2D plate composition.
/// Matched to VRETScene's original BlockA_Plate and PlateA_Group hierarchy.
/// Only positions of known AOI sprites, SpriteRenderer sortingOrder and
/// SortingGroup components on the five plate parents are modified.
/// Do not replace or resize collider/AOI objects or their assigned scripts.
/// </summary>
public static class ImmersiveEyeAOICompositionV16
{
    private const string Menu = "Tools/ImmersiveEye/v16/Compose natural food plates (A and B)";

    private struct Food
    {
        public readonly string name;
        public readonly float x, y, z;
        public readonly int order;
        public Food(string name, float x, float y, float z, int order)
        {
            this.name=name; this.x=x; this.y=y; this.z=z; this.order=order;
        }
        public Vector3 Position { get { return new Vector3(x,y,z); } }
    }

    // Coordinates are LOCAL to BlockA_Plate, based on the real original scene.
    // Back = rice and vegetables; front = chicken and avocado.
    private static readonly Food[] BlockA = {
        new Food("Plate_Base",    0.000f, 1.100f, 2.000f, 0),
        new Food("Carbohydrate",  0.085f, 1.235f, 1.995f, 10),
        new Food("Vegetables",   -0.190f, 1.222f, 1.968f, 20),
        new Food("HealthyFat",    0.200f, 0.993f, 1.946f, 30),
        new Food("Protein",      -0.105f, 0.947f, 1.925f, 40),
        new Food("Fruits",       -0.468f, 1.468f, 1.988f, 55),
        new Food("Beverage",      0.536f, 1.500f, 2.080f, 55)
    };

    // Coordinates are LOCAL to each PlateA...PlateD object.
    // Z is a consistent depth order so front-most visual foods also tend to be
    // nearest to the gaze ray. The user will still need to review AOI overlaps.
    private static readonly Food[] PlateA = {
        new Food("Rice",         0.60f, 1.86f, -1.40f, 10),
        new Food("Stirfry Vege", 3.45f, 0.18f, -1.65f, 20),
        new Food("Avocado",     -2.10f, 0.25f, -2.20f, 30),
        new Food("Fish",         0.10f,-1.30f, -2.80f, 40)
    };
    private static readonly Food[] PlateB = {
        new Food("Grilled Vege",-1.05f, 1.64f, -1.40f, 10),
        new Food("Olives",       2.07f, 0.96f, -1.65f, 20),
        new Food("Potato",      -3.10f,-0.30f, -2.15f, 30),
        new Food("Chicken",      1.08f,-1.35f, -2.80f, 40)
    };
    private static readonly Food[] PlateC = {
        new Food("Fresh Salad",  0.36f, 2.30f, -1.40f, 10),
        new Food("Cheese",       3.70f, 0.80f, -1.75f, 20),
        new Food("Bread",       -2.40f, 0.80f, -2.15f, 30),
        new Food("Beef",         0.60f,-0.70f, -2.80f, 40)
    };
    private static readonly Food[] PlateD = {
        new Food("Nuts",         0.83f, 2.28f, -1.40f, 10),
        new Food("Fruits",      -3.05f, 1.73f, -1.60f, 20),
        new Food("Pasta",        0.82f, 0.16f, -2.15f, 30),
        new Food("Tofu",        -2.26f,-0.04f, -2.80f, 40)
    };

    [MenuItem(Menu)]
    public static void Compose()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.name != "VRETScene" || String.IsNullOrEmpty(scene.path))
        {
            Alert("Open the saved VRETScene before applying the composition.");
            return;
        }

        Transform rootA=FindExact(scene, "BlockA_Plate");
        Transform rootB=FindExact(scene, "PlateA_Group");
        if(rootA==null || rootB==null)
        {
            Alert("Cannot find BlockA_Plate or PlateA_Group. Nothing changed.");
            return;
        }

        Transform[] roots = {
            rootA, rootB.Find("PlateA"), rootB.Find("PlateB"),
            rootB.Find("PlateC"), rootB.Find("PlateD")
        };
        Food[][] foods = {BlockA,PlateA,PlateB,PlateC,PlateD};
        for(int i=0;i<roots.Length;i++)
        {
            if(roots[i]==null)
            {
                Alert("Missing plate parent for group "+i+". Nothing changed.");
                return;
            }
            foreach(Food f in foods[i])
            {
                Transform child=roots[i].Find(f.name);
                if(child==null || child.GetComponent<SpriteRenderer>()==null)
                {
                    Alert("Missing SpriteRenderer for "+roots[i].name+"/"+f.name+". Nothing changed.");
                    return;
                }
            }
        }

        if(!EditorUtility.DisplayDialog("ImmersiveEye: Natural plate composition",
            "Apply a conservative, AOI-preserving layout?\n\n"+
            "Block A: rice and vegetables behind, chicken at the front, avocado at the front-right.\n"+
            "Block B: each protein in front, vegetables/rice/garnish behind, four plates remain in a 2×2 grid.\n\n"+
            "Existing AOI components, colliders, object names, sprite assets and recording scripts remain unchanged. " +
            "The scene will be backed up first.", "Apply and back up", "Cancel")) return;

        if(scene.isDirty && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        string backupDir = Path.GetFullPath(Path.Combine(Application.dataPath,"../ImmersiveEye_v16_SceneBackups"));
        string backup = Path.Combine(backupDir,"VRETScene_before_AOI_composition_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity");
        try
        {
            Directory.CreateDirectory(backupDir);
            File.Copy(Path.GetFullPath(scene.path),backup,false);
        }
        catch(Exception ex)
        {
            Alert("Scene backup failed, so no changes were made. "+ex.Message);
            return;
        }

        int group=Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("ImmersiveEye AOI-preserving food composition v16");
        int count=0;
        for(int i=0;i<roots.Length;i++)
        {
            Transform plate=roots[i];
            SortingGroup sorting=plate.GetComponent<SortingGroup>();
            if(sorting==null)sorting=Undo.AddComponent<SortingGroup>(plate.gameObject);
            Undo.RecordObject(sorting,"Configure plate sorting group");
            sorting.sortingLayerID=0;
            sorting.sortingOrder=0;
            foreach(Food f in foods[i])
            {
                Transform t=plate.Find(f.name);
                SpriteRenderer sprite=t.GetComponent<SpriteRenderer>();
                Undo.RecordObject(t,"Arrange "+plate.name+"/"+f.name);
                Undo.RecordObject(sprite,"Layer "+plate.name+"/"+f.name);
                t.localPosition=f.Position;
                sprite.sortingLayerID=0;
                sprite.sortingOrder=f.order;
                count++;
            }
            // Each Block B plate retains its own original SpriteRenderer and position.
            // All child SpriteRenderers sort together within the parent SortingGroup.
        }
        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("IE_AOI_COMPOSITION_V16|DONE|FOOD_ITEMS="+count+"|BACKUP="+backup);
        EditorUtility.DisplayDialog("ImmersiveEye: layout applied",
            "Adjusted "+count+" sprites across Blocks A/B.\n\n"+
            "BACKUP: "+backup+"\n\n"+
            "Now inspect the plates in Scene view, save (Cmd+S), then Build and Run to check them in VR. " +
            "Each food still has its original AOI and collider. Pixel-level AOI overlap is NOT resolved by sorting alone.","OK");
    }

    private static Transform FindExact(Scene scene,string name)
    {
        foreach(GameObject root in scene.GetRootGameObjects())
        {
            foreach(Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if(t.name==name)return t;
            }
        }
        return null;
    }

    private static void Alert(string message)
    {
        Debug.LogError("IE_AOI_COMPOSITION_V16|"+message);
        EditorUtility.DisplayDialog("ImmersiveEye AOI composition",message,"OK");
    }
}
#endif
