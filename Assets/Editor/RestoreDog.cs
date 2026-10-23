// RestoreDog.cs —— 把 rider 上的小狗恢复成原来的样子（大小、位置都放回加腊肠狗之前）
// 团结编译完这个文件后会自动做一次并保存场景；以后也可以手动点菜单 Tools →「恢复原来的小狗」。
// 恢复好以后可以删掉这个文件。
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class RestoreDog
{
    const string DoneKey = "RestoreDog_AutoDone_20261018";

    static RestoreDog()
    {
        EditorApplication.delayCall += AutoRun;
    }

    static void AutoRun()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorPrefs.GetBool(DoneKey, false)) return;
        if (Restore(true)) EditorPrefs.SetBool(DoneKey, true);
    }

    [MenuItem("Tools/恢复原来的小狗")]
    static void RestoreMenu()
    {
        Restore(false);
    }

    static bool Restore(bool save)
    {
        var rider = GameObject.Find("rider");
        Transform visual = rider != null ? rider.transform.Find("visual") : null;
        var b = visual != null ? visual.GetComponent<DogScoutBuilder>() : null;
        if (b == null)
        {
            Debug.LogError("恢复小狗：没找到 rider/visual 上的 DogScoutBuilder（请先打开 雪峰之灵 场景）");
            return false;
        }

        Undo.RecordObject(b, "恢复原来的小狗");
        b.size = 18.93f;
        b.addSnowboard = false;
        b.assetFolder = "Assets/DogScout";
        b.Build();

        Transform model = visual.Find("DogModel");
        if (model != null)
        {
            // 站在 hitbox 胶囊的底部（原来手动摆的位置）
            model.localPosition = new Vector3(-14.02f, 265.27f, 589.74f);
            model.localRotation = Quaternion.identity;
        }
        EditorUtility.SetDirty(b);
        EditorSceneManager.MarkSceneDirty(b.gameObject.scene);
        if (save) EditorSceneManager.SaveScene(b.gameObject.scene);
        Debug.Log("恢复小狗：已经恢复成原来的小狗（Size 18.93）" + (save ? "，场景已保存。" : "。请按 Ctrl+S 保存场景。"));
        return true;
    }
}
