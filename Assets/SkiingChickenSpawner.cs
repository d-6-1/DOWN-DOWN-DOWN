// @role Same as the dog spawner, but spawns chickens with different scarf colors
// @calls SkiingDogSpawner ChickenBuilder
// SkiingChickenSpawner.cs —— 一群戴不同颜色围巾的小鸡，和小狗一样从山顶源源不断地滑下来
// 需要：ChickenBuilder.cs、DogToon.shader、SkiingDogSpawner.cs 都已经在 Assets 里。
// 用法：Hierarchy 里新建一个空物体，挂上这个脚本，Play 就行（和小狗的生成器可以同时用）。
//
// 它是 SkiingDogSpawner 的“加强版”：从山顶出生、按自己的速度滑、躲树、撞到就判输、幽灵按 E 附身……
// 全部直接用 SkiingDogSpawner 里的代码，只是把小狗换成小鸡。
// 所以 Inspector 里的参数和小狗生成器一模一样（写着“小狗”的地方，在这里指的是小鸡）。
// 每只小鸡出场时会自动换一条和别的小鸡都不一样颜色的围巾。

using UnityEngine;

public class SkiingChickenSpawner : SkiingDogSpawner
{
    // 刚把脚本挂上去时的默认值（和小狗分开，免得两个生成器的文字叠在一起）
    void Reset()
    {
        animalCap = 120;
        showDebug = false;
    }

    // 在 SkiingDogSpawner 的 Start 之前，先做好一只小鸡，交给它当“小狗 Prefab”
    void Awake()
    {
        if (dogPrefab != null && dogPrefab.GetComponentInChildren<ChickenBuilder>(true) != null) return;

        float s = ChickenSize();
        var src = new GameObject("ChickenSource");
        src.SetActive(false);                 // 不让它自己 Awake（不会自己生成、也不占用围巾颜色）
        src.transform.SetParent(transform, false);
        var b = src.AddComponent<ChickenBuilder>();
        b.size = s;
        b.buildOnPlayIfMissing = false;
        b.uniqueScarfOnPlay = true;           // 每只复制出来的小鸡出场时换一条新颜色的围巾
        b.Build();
        dogPrefab = src;
    }

    // 小鸡要多大：和 SkiingDogSpawner 算小狗大小的方法一样（勾了 Match Player Size 就和玩家一样大）
    float ChickenSize()
    {
        if (!matchPlayerSize) return dogSize;
        Transform p = player;
        if (p == null)
        {
            var go = GameObject.Find("rider");
            if (go != null) p = go.transform;
        }
        if (p == null) return dogSize;
        foreach (var t in p.GetComponentsInChildren<Transform>(true))
            if (t.name == "DogModel") return t.lossyScale.y;

        float ws = worldScale;
        if (autoWorldScale)
        {
            bool any = false;
            Bounds bb = new Bounds(p.position, Vector3.zero);
            foreach (var r in p.GetComponentsInChildren<Renderer>())
            {
                if (!any) { bb = r.bounds; any = true; }
                else bb.Encapsulate(r.bounds);
            }
            if (any && bb.size.y >= 0.01f) ws = Mathf.Clamp(bb.size.y / 1.6f, 0.1f, 1000f);
        }
        return Mathf.Max(0.1f, ws * 1.6f / 1.55f);
    }
}
