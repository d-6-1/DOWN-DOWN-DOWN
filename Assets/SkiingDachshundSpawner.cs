// @role Same as the dog spawner, but spawns long dachshunds with a lying-down collider
// @calls SkiingDogSpawner DachshundBuilder SkierController
// @tune raiseUp:float:0-0.5:Lifts the dachshund model above the snow
// SkiingDachshundSpawner.cs —— 一群戴不同颜色围巾的腊肠狗，和小狗、小鸡一样从山顶源源不断地滑下来
// 需要：DachshundBuilder.cs、DogToon.shader、SkiingDogSpawner.cs 都已经在 Assets 里。
// 用法：Hierarchy 里新建一个空物体，挂上这个脚本，Play 就行（可以和小狗、小鸡的生成器同时用）。
//
// 和 SkiingChickenSpawner 一样，是 SkiingDogSpawner 的“加强版”：从山顶出生、按自己的速度滑、绕开树和北极熊、
// 撞到就判输、幽灵按 E 附身……全部直接用 SkiingDogSpawner 里的代码，只是把小狗换成腊肠狗。
// Inspector 里的参数和小狗生成器一模一样（写着“小狗”的地方，在这里指的是腊肠狗）。
// 三种动物一起用时别太多，免得卡：建议把最多数量调到 80 左右、身边数量调到 3 左右。
// 每只腊肠狗出场时轮流换一条围巾（六种颜色），脚下的木板滑雪板和围巾同色。
// 腊肠狗身体长，碰撞体是一根横着的胶囊（从屁股到鼻子），所以鼻子也不会穿进树里。

using UnityEngine;

public class SkiingDachshundSpawner : SkiingDogSpawner
{
    [Header("腊肠狗")]
    [Tooltip("腊肠狗往上抬多少（1 = 一只腊肠狗那么高的 0.8 倍左右）。还陷在雪里就调大一点，飘起来了就调小一点。Play 之前改才有用。")]
    public float raiseUp = 0.2f;

    // 在 SkiingDogSpawner 的 Start 之前，先做好一只腊肠狗，交给它当“小狗 Prefab”
    void Awake()
    {
        // 每半秒给新出场的腊肠狗挂上 DachshundBody，由它管碰撞体（不管你用的是哪一版 SkiingDogSpawner 都能用）
        InvokeRepeating("LayDachshundColliders", 0f, 0.5f);

        if (dogPrefab != null && dogPrefab.GetComponentInChildren<DachshundBuilder>(true) != null) return;

        float s = DachshundSize();
        var src = new GameObject("DachshundSource");
        src.SetActive(false);                 // 不让它自己 Awake（不会自己生成、也不占用围巾颜色）
        src.transform.SetParent(transform, false);
        var b = src.AddComponent<DachshundBuilder>();
        b.size = s;
        b.buildOnPlayIfMissing = false;
        b.uniqueScarfOnPlay = true;           // 每只复制出来的腊肠狗出场时换一条围巾
        b.addSkis = true;
        b.Build();
        var body = src.transform.Find(DachshundBuilder.RootName);
        if (body != null) body.localPosition += Vector3.up * raiseUp * s;   // 往上抬，免得陷进雪里
        dogPrefab = src;
    }

    // 每只腊肠狗身上挂一个 DachshundBody，由它决定碰撞体是横的（自己滑）还是竖的（被附身）
    void LayDachshundColliders()
    {
        foreach (Transform c in transform)
        {
            if (c.GetComponent<CapsuleCollider>() == null || c.GetComponent<DachshundBody>() != null) continue;
            c.gameObject.AddComponent<DachshundBody>().size = dogSize;
        }
    }

    // 腊肠狗要多大：和小鸡一样，勾了 Match Player Size 就按玩家小狗的缩放来（腊肠狗本身就比小狗矮一点）
    float DachshundSize()
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

// 管一只腊肠狗的碰撞体：
// 自己滑的时候：横着的胶囊（从屁股到鼻子），小狗小鸡和你都会绕开它整个长身体。
// 被附身的时候：竖着的胶囊，和小狗小鸡一样只有脚下一个点贴着雪。
//   （被附身时身体是站直的，雪坡却是斜的，横胶囊的鼻子那头会一直戳进下坡的雪里，所以会扭来扭去、像被拖住一样）
public class DachshundBody : MonoBehaviour
{
    public float size = 1f;
    CapsuleCollider col;
    int mode = -1;   // 0 = 自己滑，1 = 被附身

    void Awake() { col = GetComponent<CapsuleCollider>(); }

    void Update()
    {
        if (col == null) return;
        var sk = GetComponent<SkierController>();
        int want = (sk != null && sk.enabled) ? 1 : 0;
        if (want == mode) return;
        mode = want;
        if (mode == 0)
        {
            col.direction = 2;                // 横着，顺着脸的方向（Z）
            col.center = new Vector3(0f, 0.45f, 0.05f) * size;
            col.height = 2.1f * size;
            col.radius = 0.45f * size;
        }
        else
        {
            col.direction = 1;                // 竖着（Y），底部正好在脚底
            col.center = new Vector3(0f, 0.65f, 0f) * size;
            col.height = 1.3f * size;
            col.radius = 0.45f * size;
        }
    }
}
